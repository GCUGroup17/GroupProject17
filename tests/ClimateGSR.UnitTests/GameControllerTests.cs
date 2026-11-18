using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ClimateGSR.Controllers;
using ClimateGSR.Models;
using ClimateGSR.Models.ViewModels;
using ClimateGSR.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class GameControllerTests
    {
        private GameController _controller;
        private FakeSession _session;

        // Runs before every test: a fresh controller wired to a fake HttpContext with an empty session and a logged-out user.
        [TestInitialize]
        public void SetUp()
        {
            _controller = new GameController();
            _session = ControllerTestContext.Attach(_controller);
        }

        // Runs after every test: disposes the controller.
        [TestCleanup]
        public void TearDown()
        {
            _controller.Dispose();
        }

        // ---------- helpers ----------

        // Builds a fake question whose hints are named "<answer> hint 1", "<answer> hint 2", ... so tests can tell which hints were shown.
        private static Question MakeQuestion(string answer, int hintCount = 6)
        {
            return new Question
            {
                Answer = answer,
                EmissionsPrompt = "Prompt for " + answer,
                Hints = Enumerable.Range(1, hintCount)
                    .Select(i => new Hint { Type = "T" + i, Value = answer + " hint " + i })
                    .ToList()
            };
        }

        // Puts a game with one question per answer into the session, as if /Game/Start had been called. Returns the state so tests can tweak and inspect it.
        private GameState StartGame(params string[] answers)
        {
            var game = new Game { Questions = answers.Select(a => MakeQuestion(a)).ToList() };
            var state = new GameState { Game = game };
            _session["GameState"] = state;
            return state;
        }

        // Checks that an action redirected to the given action name (e.g. "Play", "Start", "Result").
        private static void AssertRedirect(ActionResult result, string action)
        {
            Assert.IsInstanceOfType(result, typeof(RedirectToRouteResult));
            Assert.AreEqual(action, ((RedirectToRouteResult)result).RouteValues["action"]);
        }

        // ---------- Play ----------

        // Opening /Game/Play with no game in the session should send the player to Start to begin a new game.
        [TestMethod]
        public void Play_WithNoGameInSession_RedirectsToStart()
        {
            AssertRedirect(_controller.Play(), "Start");
        }

        // Once every round has been played, Play should send the player to the Result page instead of showing a round.
        [TestMethod]
        public void Play_WhenGameFinished_RedirectsToResult()
        {
            var state = StartGame("France");
            state.CurrentIndex = 1;

            AssertRedirect(_controller.Play(), "Result");
        }

        // Play should fill the view model from the current round: round number, total rounds, score, emissions prompt,
        // only the hints revealed so far (in order), whether more hints exist, points available, and the feedback message from TempData.
        [TestMethod]
        public void Play_BuildsViewModelForCurrentRound()
        {
            var state = StartGame("France", "Japan", "Brazil");
            state.CurrentIndex = 1;
            state.HintsRevealed = 2;
            state.Game.Score = 175;
            _controller.TempData["Feedback"] = "Nice one";

            var result = _controller.Play() as ViewResult;

            Assert.IsNotNull(result);
            var model = (PlayViewModel)result.Model;
            Assert.AreEqual(2, model.RoundNumber);
            Assert.AreEqual(3, model.TotalRounds);
            Assert.AreEqual(175, model.Score);
            Assert.AreEqual("Prompt for Japan", model.EmissionsPrompt);
            CollectionAssert.AreEqual(new[] { "Japan hint 1", "Japan hint 2" },
                model.RevealedHints.Select(h => h.Value).ToArray());
            Assert.IsTrue(model.CanRevealMore);
            Assert.AreEqual(100, model.PointsAvailable);
            Assert.AreEqual("Nice one", model.Feedback);
        }

        // When every hint is already shown, the "Reveal hint" button should be hidden (CanRevealMore = false) and all 6 hints listed.
        [TestMethod]
        public void Play_WhenAllHintsRevealed_CannotRevealMore()
        {
            var state = StartGame("France");
            state.HintsRevealed = 6;

            var model = (PlayViewModel)((ViewResult)_controller.Play()).Model;

            Assert.IsFalse(model.CanRevealMore);
            Assert.AreEqual(6, model.RevealedHints.Count);
        }

        // Checks the scoring rule for each number of revealed hints: the first 2 hints are free,
        // then each extra hint costs 25 points (100 -> 75 -> 50 -> 25 -> 0), never going below 0.
        [DataTestMethod]
        [DataRow(0, 100)]
        [DataRow(1, 100)]
        [DataRow(2, 100)] // first two hints are free
        [DataRow(3, 75)]
        [DataRow(4, 50)]
        [DataRow(5, 25)]
        [DataRow(6, 0)]
        public void Play_PointsAvailable_DropBy25PerPaidHint(int hintsRevealed, int expectedPoints)
        {
            var state = StartGame("France");
            state.HintsRevealed = hintsRevealed;

            var model = (PlayViewModel)((ViewResult)_controller.Play()).Model;

            Assert.AreEqual(expectedPoints, model.PointsAvailable);
        }

        // ---------- Guess ----------

        // Guessing with no game in progress should redirect to Start rather than crash.
        [TestMethod]
        public void Guess_WithNoGame_RedirectsToStart()
        {
            AssertRedirect(_controller.Guess("France"), "Start");
        }

        // A correct guess with no hints should add 100 points, move to the next round, reset the hint count,
        // and show the "Correct!" feedback message.
        [TestMethod]
        public void Guess_Correct_AwardsPointsAndMovesToNextRound()
        {
            var state = StartGame("France", "Japan");

            var result = _controller.Guess("France");

            AssertRedirect(result, "Play");
            Assert.AreEqual(100, state.Game.Score);
            Assert.AreEqual(1, state.CurrentIndex);
            Assert.AreEqual(0, state.HintsRevealed);
            Assert.AreEqual("Correct! It was France. +100 points.", _controller.TempData["Feedback"]);
        }

        // A guess should count as correct regardless of upper/lower case or spaces before/after the name.
        [DataTestMethod]
        [DataRow("france")]
        [DataRow("FRANCE")]
        [DataRow("  France  ")]
        public void Guess_IgnoresCaseAndSurroundingSpaces(string guess)
        {
            var state = StartGame("France", "Japan");

            _controller.Guess(guess);

            Assert.AreEqual(100, state.Game.Score);
        }

        // Extra spaces between words ("united    kingdom") should still match a multi-word answer.
        [TestMethod]
        public void Guess_CollapsesSpacesBetweenWords()
        {
            var state = StartGame("United Kingdom", "Japan");

            _controller.Guess("united    kingdom");

            Assert.AreEqual(100, state.Game.Score);
        }

        // A correct guess after 4 hints (2 free + 2 paid) should only award 50 points.
        [TestMethod]
        public void Guess_CorrectAfterPaidHints_AwardsReducedPoints()
        {
            var state = StartGame("France", "Japan");
            state.HintsRevealed = 4;

            _controller.Guess("France");

            Assert.AreEqual(50, state.Game.Score);
        }

        // A wrong guess should reveal one more hint, stay on the same round, award no points,
        // and show the "Not quite" feedback message.
        [TestMethod]
        public void Guess_Wrong_RevealsAnotherHintAndStaysOnRound()
        {
            var state = StartGame("France", "Japan");

            var result = _controller.Guess("Spain");

            AssertRedirect(result, "Play");
            Assert.AreEqual(0, state.CurrentIndex);
            Assert.AreEqual(1, state.HintsRevealed);
            Assert.AreEqual(0, state.Game.Score);
            Assert.AreEqual("Not quite. Here's another hint.", _controller.TempData["Feedback"]);
        }

        // Submitting an empty form (null guess) should be handled like a wrong guess, not throw an error.
        [TestMethod]
        public void Guess_Null_IsTreatedAsWrong()
        {
            var state = StartGame("France", "Japan");

            _controller.Guess(null);

            Assert.AreEqual(1, state.HintsRevealed);
            Assert.AreEqual(0, state.Game.Score);
        }

        // A wrong guess when all hints are already shown should end the round with 0 points,
        // move to the next round, and tell the player the answer.
        [TestMethod]
        public void Guess_WrongWithNoHintsLeft_EndsRoundWithNoPoints()
        {
            var state = StartGame("France", "Japan");
            state.HintsRevealed = 6;

            var result = _controller.Guess("Spain");

            AssertRedirect(result, "Play");
            Assert.AreEqual(1, state.CurrentIndex);
            Assert.AreEqual(0, state.HintsRevealed);
            Assert.AreEqual(0, state.Game.Score);
            Assert.AreEqual("Out of hints. It was France.", _controller.TempData["Feedback"]);
        }

        // Answering the final round correctly should finish the game and redirect to the Result page.
        [TestMethod]
        public void Guess_CorrectOnLastRound_RedirectsToResult()
        {
            var state = StartGame("France");

            var result = _controller.Guess("France");

            AssertRedirect(result, "Result");
            Assert.IsTrue(state.Finished);
        }

        // Guessing after the game is over (e.g. resubmitting an old form) should redirect to Start and not change the score.
        [TestMethod]
        public void Guess_WhenGameAlreadyFinished_RedirectsToStart()
        {
            var state = StartGame("France");
            state.CurrentIndex = 1;

            AssertRedirect(_controller.Guess("France"), "Start");
            Assert.AreEqual(0, state.Game.Score);
        }

        // ---------- RevealHint ----------

        // Clicking "Reveal hint" should show one more hint and go back to the Play page.
        [TestMethod]
        public void RevealHint_IncrementsHintCount()
        {
            var state = StartGame("France");

            var result = _controller.RevealHint();

            AssertRedirect(result, "Play");
            Assert.AreEqual(1, state.HintsRevealed);
        }

        // Revealing a hint when all hints are already shown should do nothing (the count must not go past the number of hints).
        [TestMethod]
        public void RevealHint_DoesNotGoPastNumberOfHints()
        {
            var state = StartGame("France");
            state.HintsRevealed = 6;

            _controller.RevealHint();

            Assert.AreEqual(6, state.HintsRevealed);
        }

        // Revealing a hint with no game in progress should redirect to Start rather than crash.
        [TestMethod]
        public void RevealHint_WithNoGame_RedirectsToStart()
        {
            AssertRedirect(_controller.RevealHint(), "Start");
        }

        // ---------- GiveUp ----------

        // Giving up should move to the next round with 0 points, reset the hint count, and tell the player the answer.
        [TestMethod]
        public void GiveUp_SkipsRoundWithNoPointsAndShowsAnswer()
        {
            var state = StartGame("France", "Japan");
            state.HintsRevealed = 3;

            var result = _controller.GiveUp();

            AssertRedirect(result, "Play");
            Assert.AreEqual(1, state.CurrentIndex);
            Assert.AreEqual(0, state.HintsRevealed);
            Assert.AreEqual(0, state.Game.Score);
            Assert.AreEqual("The answer was France.", _controller.TempData["Feedback"]);
        }

        // Giving up on the final round should finish the game and redirect to the Result page.
        [TestMethod]
        public void GiveUp_OnLastRound_RedirectsToResult()
        {
            StartGame("France");

            AssertRedirect(_controller.GiveUp(), "Result");
        }

        // Giving up with no game in progress should redirect to Start rather than crash.
        [TestMethod]
        public void GiveUp_WithNoGame_RedirectsToStart()
        {
            AssertRedirect(_controller.GiveUp(), "Start");
        }

        // ---------- Result ----------

        // Opening the Result page with no game in the session should redirect to Start.
        [TestMethod]
        public void Result_WithNoGame_RedirectsToStart()
        {
            AssertRedirect(_controller.Result(), "Start");
        }

        // Players must not be able to skip ahead to the Result page while rounds are still left; they go back to Play.
        [TestMethod]
        public void Result_BeforeGameFinished_RedirectsToPlay()
        {
            StartGame("France", "Japan");

            AssertRedirect(_controller.Result(), "Play");
        }

        // A logged-out player who finishes a game should see the Result page with their game and the last feedback message,
        // but ScoreSaved should be false and nothing should be recorded (guests' scores are never saved).
        [TestMethod]
        public void Result_ForGuest_ShowsGameWithoutSavingScore()
        {
            var state = StartGame("France");
            state.Game.Score = 75;
            state.CurrentIndex = 1;
            _controller.TempData["Feedback"] = "The answer was France.";

            var result = _controller.Result() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(state.Game, result.Model);
            Assert.AreEqual(false, result.ViewData["ScoreSaved"]);
            Assert.AreEqual("The answer was France.", result.ViewData["Feedback"]);
            Assert.IsFalse(state.GameCounted, "Guest games must not be recorded");
            Assert.AreEqual(0, state.PointsSaved);
        }

        // ---------- full game ----------

        // Plays a whole 3-round game through the controller: a correct guess with no hints (+100),
        // a correct guess after 3 hints (+75), then giving up (+0). The total should be 175 and the game should end on Result.
        [TestMethod]
        public void FullGame_ScoreAccumulatesAcrossRounds()
        {
            var state = StartGame("France", "Japan", "Brazil");

            _controller.Guess("France");              // +100
            _controller.RevealHint();
            _controller.RevealHint();
            _controller.RevealHint();
            _controller.Guess("Japan");               // 3 hints -> +75
            var last = _controller.GiveUp();          // +0

            AssertRedirect(last, "Result");
            Assert.AreEqual(175, state.Game.Score);
            Assert.IsTrue(state.Finished);
        }
    }
}
