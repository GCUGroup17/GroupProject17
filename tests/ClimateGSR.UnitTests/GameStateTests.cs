using System.Collections.Generic;
using ClimateGSR.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class GameStateTests
    {
        private static GameState StateWith(int questionCount)
        {
            var game = new Game();
            for (int i = 0; i < questionCount; i++)
                game.Questions.Add(new Question { Answer = "Country " + i });
            return new GameState { Game = game };
        }

        [TestMethod]
        public void NewGame_HasEmptyQuestionListAndZeroScore()
        {
            var game = new Game();

            Assert.IsNotNull(game.Questions);
            Assert.AreEqual(0, game.Questions.Count);
            Assert.AreEqual(0, game.Score);
        }

        [TestMethod]
        public void Current_ReturnsQuestionAtCurrentIndex()
        {
            var state = StateWith(3);
            state.CurrentIndex = 2;

            Assert.AreEqual("Country 2", state.Current.Answer);
        }

        [TestMethod]
        public void Finished_IsFalseWhileQuestionsRemain()
        {
            var state = StateWith(5);
            state.CurrentIndex = 4;

            Assert.IsFalse(state.Finished);
        }

        [TestMethod]
        public void Finished_IsTrueOncePastLastQuestion()
        {
            var state = StateWith(5);
            state.CurrentIndex = 5;

            Assert.IsTrue(state.Finished);
        }

        [TestMethod]
        public void Finished_IsTrueForGameWithNoQuestions()
        {
            Assert.IsTrue(StateWith(0).Finished);
        }
    }
}
