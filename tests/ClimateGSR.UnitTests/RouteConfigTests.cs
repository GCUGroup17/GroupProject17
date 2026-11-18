using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClimateGSR.UnitTests
{
    [TestClass]
    public class RouteConfigTests
    {
        private static RouteData Resolve(string url)
        {
            var routes = new RouteCollection();
            RouteConfig.RegisterRoutes(routes);

            var request = new Mock<HttpRequestBase>();
            request.Setup(r => r.AppRelativeCurrentExecutionFilePath).Returns(url);
            request.Setup(r => r.PathInfo).Returns(string.Empty);

            var context = new Mock<HttpContextBase>();
            context.Setup(c => c.Request).Returns(request.Object);

            return routes.GetRouteData(context.Object);
        }

        [DataTestMethod]
        [DataRow("~/", "Home", "Index")]
        [DataRow("~/Home", "Home", "Index")]
        [DataRow("~/Home/GetClimateData", "Home", "GetClimateData")]
        [DataRow("~/Game/Start", "Game", "Start")]
        [DataRow("~/Game/Play", "Game", "Play")]
        [DataRow("~/Game/Leaderboard", "Game", "Leaderboard")]
        [DataRow("~/Account/Login", "Account", "Login")]
        public void DefaultRoute_MapsToControllerAndAction(string url, string controller, string action)
        {
            var data = Resolve(url);

            Assert.IsNotNull(data, "No route matched " + url);
            Assert.AreEqual(controller, data.Values["controller"]);
            Assert.AreEqual(action, data.Values["action"]);
        }

        [TestMethod]
        public void DefaultRoute_IdIsOptional()
        {
            var data = Resolve("~/Game/Play/42");

            Assert.AreEqual("42", data.Values["id"]);
            Assert.AreEqual(UrlParameter.Optional, Resolve("~/Game/Play").Values["id"]);
        }

        [TestMethod]
        public void AxdResources_AreIgnored()
        {
            var data = Resolve("~/WebResource.axd/abc");

            Assert.IsNotNull(data);
            Assert.IsInstanceOfType(data.RouteHandler, typeof(StopRoutingHandler));
        }
    }
}
