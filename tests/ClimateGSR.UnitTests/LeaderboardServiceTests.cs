using System.Collections.Generic;
using System.Linq;
using ClimateGSR.Models;
using ClimateGSR.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class LeaderboardServiceTests
    {
        private readonly List<ApplicationUser> _users = new List<ApplicationUser>();
        private readonly List<Member> _members = new List<Member>();

        private void AddPlayer(string id, string first, string last, int lifetimePoints,
            int memberHighscore, int gamesPlayed = 1, int legacyHighScore = 0)
        {
            _users.Add(new ApplicationUser { Id = id, FirstName = first, LastName = last, HighScore = legacyHighScore });
            _members.Add(new Member
            {
                ApplicationUserId = id,
                LifetimePoints = lifetimePoints,
                Highscore = memberHighscore,
                GamesPlayed = gamesPlayed
            });
        }

        private List<Models.ViewModels.LeaderboardEntryViewModel> Leaderboard(string currentUserId = null)
        {
            var context = new ClimateGSRDbContext
            {
                Users = MockDbSet.Of(_users),
                Members = MockDbSet.Of(_members)
            };
            return new LeaderboardService(context).GetTopPlayers(currentUserId);
        }

        [TestMethod]
        public void NoPlayers_ReturnsEmptyList()
        {
            Assert.AreEqual(0, Leaderboard().Count);
        }

        [TestMethod]
        public void RanksByAllTimeScoreDescending()
        {
            AddPlayer("a", "Alice", "Smith", 200, 100);
            AddPlayer("b", "Bob", "Jones", 500, 100);
            AddPlayer("c", "Cara", "Brown", 300, 100);

            var board = Leaderboard();

            CollectionAssert.AreEqual(new[] { "Bob J.", "Cara B.", "Alice S." }, board.Select(e => e.PlayerName).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, board.Select(e => e.Rank).ToArray());
            CollectionAssert.AreEqual(new[] { 500, 300, 200 }, board.Select(e => e.AllTimeScore).ToArray());
        }

        [TestMethod]
        public void EqualAllTimeScore_HigherHighScoreWins()
        {
            AddPlayer("a", "Alice", "Smith", 300, 200);
            AddPlayer("b", "Bob", "Jones", 300, 450);

            var board = Leaderboard();

            Assert.AreEqual("Bob J.", board[0].PlayerName);
            Assert.AreEqual(1, board[0].Rank);
            Assert.AreEqual(2, board[1].Rank);
        }

        [TestMethod]
        public void FullTie_SharesRank_AndNextPlayerSkipsAhead()
        {
            AddPlayer("a", "Zed", "Smith", 300, 200);
            AddPlayer("b", "Amy", "Jones", 300, 200);
            AddPlayer("c", "Cal", "Brown", 100, 100);

            var board = Leaderboard();

            // Tied players are ordered by first name
            CollectionAssert.AreEqual(new[] { "Amy J.", "Zed S.", "Cal B." }, board.Select(e => e.PlayerName).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, board.Select(e => e.Rank).ToArray());
        }

        [TestMethod]
        public void ReturnsAtMostTenPlayers()
        {
            for (int i = 0; i < 15; i++)
                AddPlayer("u" + i, "Player" + i, "Last", i * 10, i);

            var board = Leaderboard();

            Assert.AreEqual(10, board.Count);
            Assert.AreEqual(140, board.First().AllTimeScore);
            Assert.AreEqual(50, board.Last().AllTimeScore);
        }

        [TestMethod]
        public void UserWithoutMemberRecord_IsListedWithZeroPoints()
        {
            AddPlayer("a", "Alice", "Smith", 200, 100);
            _users.Add(new ApplicationUser { Id = "old", FirstName = "Olga", LastName = "Old", HighScore = 80 });

            var board = Leaderboard();

            var olga = board.Single(e => e.PlayerName == "Olga O.");
            Assert.AreEqual(0, olga.AllTimeScore);
            Assert.AreEqual(0, olga.GamesPlayed);
            Assert.AreEqual(80, olga.HighScore);
            Assert.AreEqual(2, olga.Rank);
        }

        [TestMethod]
        public void HighScore_UsesTheLargerOfLegacyAndMemberScore()
        {
            AddPlayer("a", "Alice", "Smith", 200, memberHighscore: 150, legacyHighScore: 300);
            AddPlayer("b", "Bob", "Jones", 100, memberHighscore: 400, legacyHighScore: 50);

            var board = Leaderboard();

            Assert.AreEqual(300, board.Single(e => e.PlayerName == "Alice S.").HighScore);
            Assert.AreEqual(400, board.Single(e => e.PlayerName == "Bob J.").HighScore);
        }

        [TestMethod]
        public void PlayerName_UsesFirstNameAndLastInitial_Trimmed()
        {
            AddPlayer("a", "  Alice  ", "  smith", 100, 100);

            Assert.AreEqual("Alice s.", Leaderboard()[0].PlayerName);
        }

        [TestMethod]
        public void PlayerName_MissingFirstName_FallsBackToPlayer()
        {
            AddPlayer("a", null, "Smith", 100, 100);
            AddPlayer("b", "   ", null, 50, 50);

            var board = Leaderboard();

            Assert.AreEqual("Player S.", board[0].PlayerName);
            Assert.AreEqual("Player", board[1].PlayerName);
        }

        [TestMethod]
        public void PlayerName_MissingLastName_ShowsFirstNameOnly()
        {
            AddPlayer("a", "Alice", "", 100, 100);

            Assert.AreEqual("Alice", Leaderboard()[0].PlayerName);
        }

        [TestMethod]
        public void MarksTheCurrentPlayer()
        {
            AddPlayer("a", "Alice", "Smith", 200, 100);
            AddPlayer("b", "Bob", "Jones", 100, 100);

            var board = Leaderboard(currentUserId: "b");

            Assert.IsFalse(board.Single(e => e.PlayerName == "Alice S.").IsCurrentPlayer);
            Assert.IsTrue(board.Single(e => e.PlayerName == "Bob J.").IsCurrentPlayer);
        }

        [TestMethod]
        public void NoCurrentUser_NobodyIsMarked()
        {
            AddPlayer("a", "Alice", "Smith", 200, 100);

            Assert.IsFalse(Leaderboard().Any(e => e.IsCurrentPlayer));
        }

        [TestMethod]
        public void CopiesGamesPlayedFromMemberRecord()
        {
            AddPlayer("a", "Alice", "Smith", 200, 100, gamesPlayed: 7);

            Assert.AreEqual(7, Leaderboard()[0].GamesPlayed);
        }
    }
}
