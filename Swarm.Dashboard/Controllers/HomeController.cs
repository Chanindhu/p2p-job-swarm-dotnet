using Microsoft.AspNetCore.Mvc;

namespace Swarm.Dashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;
        public HomeController(IConfiguration config) { _config = config; }

        public IActionResult Index()
        {
            ViewBag.ServerBaseUrl = _config["ServerBaseUrl"] ?? "http://localhost:5265";
            return View();
        }
    }
}
