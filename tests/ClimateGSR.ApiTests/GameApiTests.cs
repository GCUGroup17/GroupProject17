using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.ApiTests
{
    /// <summary>
    /// HTTP tests for the game flow (/Game/*). These need a database with countries,
    /// which CI provides by running with CLIMATEGSR_SEED_DB=true.
    /// </summary>
    [TestClass]
    public class GameApiTests : ApiTestBase
    {
        private static readonly Regex RoundRegex = new Regex(@"Round (\d+) of (\d+)");

        /// <summary>Hits /Game/Start and follows the redirect to the first round.</summary>
        private async Task<string> StartGame()
        {
            var start = await Get("/Game/Start");
            AssertRedirectsTo(start, "/Game/Play");

            return await AssertOkHtml(await Get("/Game/Play"));
        }

        private Task<HttpResponseMessage> PostWithToken(string path, string pageHtml, string guess = null)
        {
            var fields = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", ExtractAntiForgeryToken(pageHtml) }
            };
            if (guess != null) fields["guess"] = guess;
            return PostForm(path, fields);
        }

        // ---------- no game in progress ----------

        [TestMethod]
        public async Task Play_WithoutGame_RedirectsToStart()
        {
            AssertRedirectsTo(await Get("/Game/Play"), "/Game/Start");
        }

        [TestMethod]
        public async Task Result_WithoutGame_RedirectsToStart()
        {
            AssertRedirectsTo(await Get("/Game/Result"), "/Game/Start");
        }

        [TestMethod]
        public async Task Guess_WithoutAntiForgeryToken_IsRejected()
        {
            var response = await PostForm("/Game/Guess", new Dictionary<string, string> { { "guess", "France" } });

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode,
                "POST without __RequestVerificationToken should fail anti-forgery validation");
        }

        [TestMethod]
        public async Task GameActions_RejectGet()
        {
            // Guess/RevealHint/GiveUp are [HttpPost] only
            foreach (var path in new[] { "/Game/Guess", "/Game/RevealHint", "/Game/GiveUp" })
                Assert.AreEqual(HttpStatusCode.NotFound, (await Get(path)).StatusCode, path);
        }

        // ---------- playing a game ----------

        [TestMethod]
        public async Task Start_ShowsFirstRoundOfFive()
        {
            var html = await StartGame();

            var round = RoundRegex.Match(html);
            Assert.IsTrue(round.Success, "Round counter not found on Play page");
            Assert.AreEqual("1", round.Groups[1].Value);
            Assert.AreEqual("5", round.Groups[2].Value);
            StringAssert.Contains(html, "Which country is this?");
            StringAssert.Contains(html, "This country emitted about");
            StringAssert.Contains(html, "No hints revealed yet.");
            StringAssert.Contains(html, "Points available for this round: <strong>100</strong>");
        }

        [TestMethod]
        public async Task RevealHint_ShowsAHint()
        {
            var html = await StartGame();

            AssertRedirectsTo(await PostWithToken("/Game/RevealHint", html), "/Game/Play");
            html = await AssertOkHtml(await Get("/Game/Play"));

            Assert.IsFalse(html.Contains("No hints revealed yet."));
            StringAssert.Contains(html, "hint-box");
        }

        [TestMethod]
        public async Task WrongGuess_RevealsHintAndShowsFeedback()
        {
            var html = await StartGame();

            AssertRedirectsTo(await PostWithToken("/Game/Guess", html, "Not A Real Country"), "/Game/Play");
            html = await AssertOkHtml(await Get("/Game/Play"));

            StringAssert.Contains(html, "Not quite. Here&#39;s another hint.");
            StringAssert.Contains(html, "Round 1 of 5");
        }

        [TestMethod]
        public async Task GiveUp_MovesToNextRoundAndShowsAnswer()
        {
            var html = await StartGame();

            AssertRedirectsTo(await PostWithToken("/Game/GiveUp", html), "/Game/Play");
            html = await AssertOkHtml(await Get("/Game/Play"));

            StringAssert.Contains(html, "Round 2 of 5");
            StringAssert.Contains(html, "The answer was ");
        }

        [TestMethod]
        public async Task Result_BeforeFinishing_RedirectsToPlay()
        {
            await StartGame();

            AssertRedirectsTo(await Get("/Game/Result"), "/Game/Play");
        }

        [TestMethod]
        public async Task FullGame_GivingUpEveryRound_EndsWithZeroPoints()
        {
            var html = await StartGame();

            for (int round = 1; round <= 5; round++)
            {
                StringAssert.Contains(html, "Round " + round + " of 5");
                var response = await PostWithToken("/Game/GiveUp", html);

                if (round < 5)
                {
                    AssertRedirectsTo(response, "/Game/Play");
                    html = await AssertOkHtml(await Get("/Game/Play"));
                }
                else
                {
                    AssertRedirectsTo(response, "/Game/Result");
                }
            }

            var result = await AssertOkHtml(await Get("/Game/Result"));
            StringAssert.Contains(result, "Game over!");
            StringAssert.Contains(result, "<div class=\"score-big\">0</div>");
            StringAssert.Contains(result, "points out of a possible 500");
            StringAssert.Contains(result, "to save your scores", "Guests should be told their score was not saved");

            // Once finished, Play sends you to the result page
            AssertRedirectsTo(await Get("/Game/Play"), "/Game/Result");
        }

        [TestMethod]
        public async Task Leaderboard_Returns200()
        {
            var html = await AssertOkHtml(await Get("/Game/Leaderboard"));

            StringAssert.Contains(html, "Game Leaderboard");
        }
    }
}
