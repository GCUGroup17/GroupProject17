using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.ApiTests
{
    /// <summary>Smoke tests that public pages load and protected pages require login.</summary>
    [TestClass]
    public class PageApiTests : ApiTestBase
    {
        [DataTestMethod]
        [DataRow("/", "Climate Awareness Geo-Explorer")]
        [DataRow("/Home/Index", "Climate Awareness Geo-Explorer")]
        [DataRow("/Home/About", "<html")]
        [DataRow("/Home/Contact", "<html")]
        [DataRow("/Account/Login", "Welcome Back")]
        [DataRow("/Account/Register", "Create Account")]
        public async Task PublicPage_Returns200(string path, string expectedText)
        {
            var html = await AssertOkHtml(await Get(path));

            StringAssert.Contains(html, expectedText);
        }

        [DataTestMethod]
        [DataRow("/Account/Login")]
        [DataRow("/Account/Register")]
        public async Task AccountForms_IncludeAntiForgeryToken(string path)
        {
            var html = await AssertOkHtml(await Get(path));

            Assert.IsFalse(string.IsNullOrEmpty(ExtractAntiForgeryToken(html)));
        }

        [DataTestMethod]
        [DataRow("/Manage")]
        [DataRow("/Manage/ChangePassword")]
        [DataRow("/Manage/EditAccount")]
        public async Task ManagePages_WhenLoggedOut_RedirectToLogin(string path)
        {
            AssertRedirectsTo(await Get(path), "/Account/Login");
        }

        [TestMethod]
        public async Task Login_WithBadCredentials_ShowsLoginPageAgain()
        {
            var page = await AssertOkHtml(await Get("/Account/Login"));

            var response = await PostForm("/Account/Login", new System.Collections.Generic.Dictionary<string, string>
            {
                { "__RequestVerificationToken", ExtractAntiForgeryToken(page) },
                { "Email", "nobody-" + System.Guid.NewGuid().ToString("N") + "@example.com" },
                { "Password", "WrongPassword1!" },
                { "RememberMe", "false" }
            });

            // Failed login re-renders the form (200) instead of redirecting
            var html = await AssertOkHtml(response);
            StringAssert.Contains(html, "Welcome Back");
            StringAssert.Contains(html, "Invalid login attempt.");
        }

        [DataTestMethod]
        [DataRow("/DoesNotExist")]
        [DataRow("/Home/DoesNotExist")]
        public async Task UnknownUrl_Returns404(string path)
        {
            Assert.AreEqual(HttpStatusCode.NotFound, (await Get(path)).StatusCode);
        }
    }
}
