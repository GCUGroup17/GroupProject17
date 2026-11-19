using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClimateGSR.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.ApiTests
{
    /// <summary>
    /// Runs once before any API test.
    ///
    /// Environment variables:
    ///   CLIMATEGSR_BASE_URL  - address of the running site, e.g. http://localhost:8080
    ///                          (if missing, every API test is reported as Inconclusive)
    ///   CLIMATEGSR_SEED_DB   - "true" to create + seed the countries table first
    ///                          (needed on a fresh database such as the CI LocalDB)
    /// </summary>
    [TestClass]
    public static class ApiTestSetup
    {
        public static Uri BaseUri { get; private set; }

        [AssemblyInitialize]
        public static void Initialise(TestContext context)
        {
            var url = Environment.GetEnvironmentVariable("CLIMATEGSR_BASE_URL");
            if (!string.IsNullOrWhiteSpace(url))
                BaseUri = new Uri(url.TrimEnd('/') + "/");

            if (string.Equals(Environment.GetEnvironmentVariable("CLIMATEGSR_SEED_DB"), "true", StringComparison.OrdinalIgnoreCase))
            {
                using (var db = new ClimateGSRDbContext())
                {
                    db.Database.CreateIfNotExists();
                    DatabaseInitialiser.Seed(db); // no-op if countries already exist
                }
            }
        }
    }

    /// <summary>
    /// Base class: gives each test its own HttpClient with its own cookie jar
    /// (so each test gets its own ASP.NET session) and redirects turned off
    /// (so tests can assert on 302s).
    /// </summary>
    public abstract class ApiTestBase
    {
        private static readonly Regex AntiForgeryTokenRegex = new Regex(
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

        protected HttpClient Client { get; private set; }

        [TestInitialize]
        public void CreateClient()
        {
            if (ApiTestSetup.BaseUri == null)
                Assert.Inconclusive("CLIMATEGSR_BASE_URL is not set - start the site and set it to run API tests.");

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = new CookieContainer(),
                UseCookies = true
            };
            Client = new HttpClient(handler) { BaseAddress = ApiTestSetup.BaseUri, Timeout = TimeSpan.FromSeconds(60) };
        }

        [TestCleanup]
        public void DisposeClient()
        {
            if (Client != null) Client.Dispose();
        }

        protected Task<HttpResponseMessage> Get(string path)
        {
            return Client.GetAsync(path.TrimStart('/'));
        }

        protected Task<HttpResponseMessage> PostForm(string path, IDictionary<string, string> fields)
        {
            return Client.PostAsync(path.TrimStart('/'), new FormUrlEncodedContent(fields));
        }

        protected static string ExtractAntiForgeryToken(string html)
        {
            var match = AntiForgeryTokenRegex.Match(html);
            Assert.IsTrue(match.Success, "No __RequestVerificationToken found in page");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        protected static void AssertRedirectsTo(HttpResponseMessage response, string expectedPathStart)
        {
            Assert.AreEqual(HttpStatusCode.Found, response.StatusCode, "Expected a 302 redirect");
            var location = response.Headers.Location;
            Assert.IsNotNull(location, "Redirect had no Location header");

            var path = location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
            StringAssert.StartsWith(path, expectedPathStart, StringComparison.OrdinalIgnoreCase);
        }

        protected static async Task<string> AssertOkHtml(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body.Length > 500 ? body.Substring(0, 500) : body);
            StringAssert.StartsWith(response.Content.Headers.ContentType.MediaType, "text/html");
            return body;
        }
    }
}
