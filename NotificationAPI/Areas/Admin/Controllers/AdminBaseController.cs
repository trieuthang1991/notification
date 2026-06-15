using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public abstract class AdminBaseController : Controller
    {
        // Base controller cho các controller trong area Admin
        // Yêu cầu người dùng đã đăng nhập và có role Admin
    }
}
