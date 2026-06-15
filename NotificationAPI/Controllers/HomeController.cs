using Microsoft.AspNetCore.Mvc;

namespace NotificationAPI.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Debug()
        {
            return View();
        }
    }
}
