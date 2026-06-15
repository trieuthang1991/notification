using Microsoft.AspNetCore.Mvc;

namespace NotificationAPI.Areas.Admin.Controllers
{
    public class HomeController : AdminBaseController
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
