using System;
using System.Collections.Generic;
using System.Linq;
using ClimateGSR.Models;
using ClimateGSR.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class QuestionGeneratorTests
    {
        private static readonly Continent Europe = new Continent { Id = 1, Name = "Europe" };

        private static Country UnitedKingdom()
        {
            return new Country
            {
                Id = 1,
                IsoCode = "GBR",
                Name = "United Kingdom",
                Continent = Europe,
                Population = 68682965,
                Area = 244820,
                Emissions = 312906000,
                EmissionsPerCapita = 4.5558,
                AverageTemperature = 10.36597
            };
        }

        private static Country MakeCountry(int id, string name, int population, double emissions = 1e7)
        {
            return new Country
            {
                Id = id,
                IsoCode = name.Substring(0, 3).ToUpperInvariant(),
                Name = name,
                Continent = Europe,
                Population = population,
                Area = 1000,
                Emissions = emissions,
                EmissionsPerCapita = 1.0
            };
        }

        private static QuestionGenerator GeneratorWith(params Country[] countries)
        {
            var context = new ClimateGSRDbContext { Countries = MockDbSet.Of(countries) };
            return new QuestionGenerator(context);
        }

        private static string HintOfType(IEnumerable<Hint> hints, string type)
        {
            var hint = hints.SingleOrDefault(h => h.Type == type);
            Assert.IsNotNull(hint, "Expected a '" + type + "' hint");
            return hint.Value;
        }

        // ---------- GenerateHints ----------

        [TestMethod]
        // Verifies that countries with temperature data receive six distinct, correctly named hint types.
        public void GenerateHints_WithTemperature_ReturnsSixDistinctHintTypes()
        {
            var hints = new QuestionGenerator(null).GenerateHints(UnitedKingdom());

            Assert.AreEqual(6, hints.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Temperature", "EmissionsPerCapita", "Population", "Area", "Continent", "FirstLetter" },
                hints.Select(h => h.Type).ToArray());
        }

        [TestMethod]
        // Checks that missing temperature data removes only the temperature hint.
        public void GenerateHints_WithoutTemperature_SkipsTemperatureHint()
        {
            var country = UnitedKingdom();
            country.AverageTemperature = null;

            var hints = new QuestionGenerator(null).GenerateHints(country);

            Assert.AreEqual(5, hints.Count);
            Assert.IsFalse(hints.Any(h => h.Type == "Temperature"));
        }

        [TestMethod]
        // Confirms that each supported hint type is formatted into a readable user-facing sentence.
        public void GenerateHints_FormatsEachHintReadably()
        {
            var hints = new QuestionGenerator(null).GenerateHints(UnitedKingdom());

            Assert.AreEqual("Its average annual temperature is about 10°C.", HintOfType(hints, "Temperature"));
            Assert.AreEqual("Each person emits roughly 4.6 tonnes of CO2 per year.", HintOfType(hints, "EmissionsPerCapita"));
            Assert.AreEqual("Its population is about 68.7 million people.", HintOfType(hints, "Population"));
            Assert.AreEqual("Its area is about 244,820 km².", HintOfType(hints, "Area"));
            Assert.AreEqual("It is located in Europe.", HintOfType(hints, "Continent"));
            Assert.AreEqual("Its name starts with the letter U.", HintOfType(hints, "FirstLetter"));
        }

        [TestMethod]
        // Ensures generated hints never expose the country's name directly.
        public void GenerateHints_NeverRevealsTheCountryName()
        {
            var hints = new QuestionGenerator(null).GenerateHints(UnitedKingdom());

            Assert.IsFalse(hints.Any(h => h.Value.Contains("United Kingdom")));
        }

        [TestMethod]
        // Checks that repeated hint generation produces varied hint ordering rather than a fixed sequence.
        public void GenerateHints_OrderIsShuffled()
        {
            var generator = new QuestionGenerator(null);
            var orders = new HashSet<string>();

            // 6 hints have 720 orderings; 50 attempts all giving the same one is effectively impossible
            for (int i = 0; i < 50; i++)
                orders.Add(string.Join(",", generator.GenerateHints(UnitedKingdom()).Select(h => h.Type)));

            Assert.IsTrue(orders.Count > 1, "Hints should not always come out in the same order");
        }

        // ---------- GetRandomCountry ----------

        [TestMethod]
        // Verifies that requesting a random country from an empty dataset throws an exception.
        public void GetRandomCountry_NoCountries_Throws()
        {
            var generator = GeneratorWith();

            Assert.ThrowsException<InvalidOperationException>(() => generator.GetRandomCountry());
        }

        [TestMethod]
        // Checks that a dataset containing only countries below the population threshold has no selectable result.
        public void GetRandomCountry_OnlySmallCountries_Throws()
        {
            var generator = GeneratorWith(MakeCountry(1, "Tuvalu", 11000), MakeCountry(2, "Nauru", 12000));

            Assert.ThrowsException<InvalidOperationException>(() => generator.GetRandomCountry());
        }

        [TestMethod]
        // Ensures countries with fewer than one million people are excluded from random selection.
        public void GetRandomCountry_ExcludesCountriesUnderOneMillionPeople()
        {
            var generator = GeneratorWith(
                MakeCountry(1, "Tuvalu", 11000),
                MakeCountry(2, "France", 68000000),
                MakeCountry(3, "Nauru", 999999));

            for (int i = 0; i < 25; i++)
                Assert.AreEqual("France", generator.GetRandomCountry().Name);
        }

        [TestMethod]
        // Confirms that a country exactly at one million people is included in random selection.
        public void GetRandomCountry_IncludesCountryExactlyAtThreshold()
        {
            var generator = GeneratorWith(MakeCountry(1, "Bordertown", 1000000));

            Assert.AreEqual("Bordertown", generator.GetRandomCountry().Name);
        }

        [TestMethod]
        // Checks that repeated selection can eventually reach every country that meets the population threshold.
        public void GetRandomCountry_EventuallyPicksEveryQualifyingCountry()
        {
            var generator = GeneratorWith(
                MakeCountry(1, "France", 68000000),
                MakeCountry(2, "Japan", 124000000),
                MakeCountry(3, "Brazil", 216000000));

            var seen = new HashSet<string>();
            for (int i = 0; i < 200 && seen.Count < 3; i++)
                seen.Add(generator.GetRandomCountry().Name);

            Assert.AreEqual(3, seen.Count);
        }

        // ---------- GenerateQuestion ----------

        [TestMethod]
        // Verifies that a generated question uses the country name as its answer and includes its hints.
        public void GenerateQuestion_AnswerIsTheCountryName()
        {
            var question = GeneratorWith(UnitedKingdom()).GenerateQuestion();

            Assert.AreEqual("United Kingdom", question.Answer);
            Assert.AreEqual("GBR", question.Country.IsoCode);
            Assert.AreEqual(6, question.Hints.Count);
        }

        [DataTestMethod]
        [DataRow(1.5e9, "This country emitted about 1.5 billion tonnes of CO2 in 2024.")]
        [DataRow(11906000000.0, "This country emitted about 11.91 billion tonnes of CO2 in 2024.")]
        [DataRow(312906000.0, "This country emitted about 312.9 million tonnes of CO2 in 2024.")]
        [DataRow(1e6, "This country emitted about 1 million tonnes of CO2 in 2024.")]
        [DataRow(450000.0, "This country emitted about 450 thousand tonnes of CO2 in 2024.")]
        // Checks that emissions prompts use appropriate billion, million, or thousand units.
        public void GenerateQuestion_FormatsEmissionsPrompt(double tonnes, string expected)
        {
            var country = MakeCountry(1, "Testland", 5000000, tonnes);

            var question = GeneratorWith(country).GenerateQuestion();

            Assert.AreEqual(expected, question.EmissionsPrompt);
        }
    }
}
