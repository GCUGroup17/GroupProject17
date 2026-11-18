using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace ClimateGSR.ApiTests
{
    /// <summary>HTTP tests for the JSON endpoint used by the home page map: GET /Home/GetClimateData</summary>
    [TestClass]
    public class ClimateDataApiTests : ApiTestBase
    {
        private const string Endpoint = "/Home/GetClimateData";

        private async Task<JArray> GetClimateData()
        {
            var response = await Get(Endpoint);
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
            Assert.AreEqual("application/json", response.Content.Headers.ContentType.MediaType);
            return JArray.Parse(body);
        }

        [TestMethod]
        public async Task GetClimateData_Returns200WithJsonArray()
        {
            var data = await GetClimateData();

            Assert.IsTrue(data.Count > 200, "Expected 200+ countries, got " + data.Count);
        }

        [TestMethod]
        public async Task GetClimateData_EachItemHasExpectedShape()
        {
            var data = await GetClimateData();
            var expected = new[] { "IsoCode", "Country", "CO2", "Population", "Temperature", "CO2PerCapita", "RawCO2", "RawPopulation" };

            foreach (JObject item in data)
            {
                foreach (var field in expected)
                    Assert.IsNotNull(item.Property(field), "Missing '" + field + "' on " + item);

                Assert.AreEqual(3, ((string)item["IsoCode"]).Length, "IsoCode should be ISO-3: " + item["IsoCode"]);
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)item["Country"]));
            }
        }

        [TestMethod]
        public async Task GetClimateData_ContainsUnitedKingdomWithRawValues()
        {
            var data = await GetClimateData();

            var uk = data.Cast<JObject>().SingleOrDefault(i => (string)i["IsoCode"] == "GBR");
            Assert.IsNotNull(uk, "GBR missing from response");
            Assert.AreEqual("United Kingdom", (string)uk["Country"]);
            Assert.AreEqual(312906000d, (double)uk["RawCO2"]);
            Assert.AreEqual(68682965d, (double)uk["RawPopulation"]);
            StringAssert.Contains((string)uk["CO2"], "Million tonnes");
            StringAssert.Contains((string)uk["Population"], "Million people");
            StringAssert.EndsWith((string)uk["Temperature"], "°C");
            StringAssert.EndsWith((string)uk["CO2PerCapita"], "tonnes / person");
        }

        [TestMethod]
        public async Task GetClimateData_IsSortedByCountryName()
        {
            var names = (await GetClimateData()).Select(i => (string)i["Country"]).ToList();

            // The server sorts with its own culture; ordinal-ignore-case is a close-enough check
            // for the first letter, which catches an unsorted response.
            for (int i = 1; i < names.Count; i++)
                Assert.IsTrue(
                    char.ToUpperInvariant(names[i - 1][0]) <= char.ToUpperInvariant(names[i][0]),
                    "'" + names[i - 1] + "' appears before '" + names[i] + "'");
        }

        [TestMethod]
        public async Task GetClimateData_IsoCodesAreUnique()
        {
            var codes = (await GetClimateData()).Select(i => (string)i["IsoCode"]).ToList();

            Assert.AreEqual(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [TestMethod]
        public async Task GetClimateData_RejectsPost()
        {
            var response = await PostForm(Endpoint, new System.Collections.Generic.Dictionary<string, string>());

            // [HttpGet] only - MVC finds no matching action for POST
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
