using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using ClimateGSR.Controllers;
using ClimateGSR.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class HomeControllerTests
    {
        private static string AppDataPath
        {
            get
            {
                var testDir = Path.GetDirectoryName(typeof(HomeControllerTests).Assembly.Location);
                return Path.Combine(testDir, "App_Data");
            }
        }

        private HomeController _controller;
        private CultureInfo _originalCulture;

        [TestInitialize]
        public void SetUp()
        {
            // GetClimateData formats numbers with the current culture ("N2"), so pin it
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            _controller = new HomeController();
            ControllerTestContext.Attach(_controller, AppDataPath);
        }

        [TestCleanup]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
            _controller.Dispose();
        }

        private List<CountryStat> GetStats()
        {
            var result = _controller.GetClimateData();
            Assert.AreEqual(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
            return (List<CountryStat>)result.Data;
        }

        [TestMethod]
        public void Index_ReturnsView()
        {
            Assert.IsInstanceOfType(_controller.Index(), typeof(ViewResult));
        }

        [TestMethod]
        public void About_ReturnsViewWithMessage()
        {
            var result = (ViewResult)_controller.About();

            Assert.AreEqual("Your application description page.", result.ViewData["Message"]);
        }

        [TestMethod]
        public void Contact_ReturnsViewWithMessage()
        {
            var result = (ViewResult)_controller.Contact();

            Assert.AreEqual("Your contact page.", result.ViewData["Message"]);
        }

        [TestMethod]
        public void GetClimateData_ReturnsEveryCountryFromGeoNames()
        {
            var stats = GetStats();

            Assert.IsTrue(stats.Count > 200, "Expected 200+ countries, got " + stats.Count);
            Assert.AreEqual(stats.Count, stats.Select(s => s.IsoCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "ISO codes should be unique");
        }

        [TestMethod]
        public void GetClimateData_IsSortedByCountryName()
        {
            var names = GetStats().Select(s => s.Country).ToList();

            CollectionAssert.AreEqual(names.OrderBy(n => n).ToList(), names);
        }

        [TestMethod]
        public void GetClimateData_DoesNotIncludeWorldAggregate()
        {
            Assert.IsFalse(GetStats().Any(s => s.IsoCode == "WLD"));
        }

        [TestMethod]
        public void GetClimateData_UnitedKingdom_HasLatestFiguresFormatted()
        {
            var uk = GetStats().Single(s => s.IsoCode == "GBR");

            Assert.AreEqual("United Kingdom", uk.Country);

            // CO2.csv: latest (2024) column
            Assert.AreEqual(312906000d, uk.RawCO2);
            Assert.AreEqual("312.91 Million tonnes (Mt/yr)", uk.CO2);

            // population.csv: latest year (2023)
            Assert.AreEqual(68682965d, uk.RawPopulation);
            Assert.AreEqual("68.68 Million people", uk.Population);

            // temperature.csv: latest year (2025)
            Assert.AreEqual("10.37 °C", uk.Temperature);

            Assert.AreEqual("4.56 tonnes / person", uk.CO2PerCapita);
        }

        [TestMethod]
        public void GetClimateData_LargeEmitter_UsesBillionTonnes()
        {
            var china = GetStats().Single(s => s.IsoCode == "CHN");

            StringAssert.EndsWith(china.CO2, " Billion tonnes (Gt/yr)");
            StringAssert.EndsWith(china.Population, " Billion people");
        }

        [TestMethod]
        public void GetClimateData_FieldsAreEitherFormattedOrNA()
        {
            foreach (var s in GetStats())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.Country), s.IsoCode + " has no name");
                Assert.IsTrue(s.CO2 == "N/A" || s.CO2.Contains("tonnes"), s.IsoCode + " CO2: " + s.CO2);
                Assert.IsTrue(s.Population == "N/A" || s.Population.EndsWith("people"), s.IsoCode + " population: " + s.Population);
                Assert.IsTrue(s.Temperature == "N/A" || s.Temperature.EndsWith("°C"), s.IsoCode + " temperature: " + s.Temperature);

                // Per-capita is only computed when both inputs exist
                bool canCompute = s.RawCO2.HasValue && s.RawPopulation > 0;
                Assert.AreEqual(canCompute, s.CO2PerCapita != "N/A", s.IsoCode + " per capita: " + s.CO2PerCapita);
            }
        }
    }
}
