using System.Web.Mvc;

namespace ClimateGSR.Controllers
{
    public class StatsController : Controller
    {
        // The statistics are supplied by the existing HomeController.GetClimateData endpoint.
        public ActionResult Index()
        {
            return View();
        }
    }
}
