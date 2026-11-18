using System.Collections.Generic;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;

namespace ClimateGSR.UnitTests.Helpers
{
    /// <summary>Simple dictionary-backed session so controllers can read/write Session[...].</summary>
    internal class FakeSession : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        public override object this[string name]
        {
            get { object value; return _items.TryGetValue(name, out value) ? value : null; }
            set { _items[name] = value; }
        }

        public override void Remove(string name) { _items.Remove(name); }
    }

    /// <summary>
    /// Wires a controller up to a fake HttpContext (session, user, Server.MapPath)
    /// so actions can be invoked directly from a unit test.
    /// </summary>
    internal static class ControllerTestContext
    {
        public static FakeSession Attach(Controller controller, string appDataPath = null, IPrincipal user = null)
        {
            var session = new FakeSession();

            var server = new Mock<HttpServerUtilityBase>();
            if (appDataPath != null)
                server.Setup(s => s.MapPath("~/App_Data")).Returns(appDataPath);

            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Session).Returns(session);
            httpContext.Setup(c => c.Server).Returns(server.Object);
            httpContext.Setup(c => c.User).Returns(user ?? Guest());
            httpContext.Setup(c => c.Items).Returns(new Dictionary<object, object>());

            controller.ControllerContext = new ControllerContext(httpContext.Object, new RouteData(), controller);
            return session;
        }

        /// <summary>A user who is not logged in.</summary>
        public static IPrincipal Guest()
        {
            return new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
        }
    }
}
