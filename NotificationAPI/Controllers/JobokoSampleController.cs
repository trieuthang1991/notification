using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NotificationAPI.Services.Couchbase;
using System;
using System.Threading.Tasks;

namespace NotificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobokoSampleController : ControllerBase
    {
        private readonly INotificationCB _notificationCB;
        private readonly ILogger<JobokoSampleController> _logger;

        public JobokoSampleController(INotificationCB notificationCB, ILogger<JobokoSampleController> logger)
        {
            _notificationCB = notificationCB;
            _logger = logger;
        }

        /// <summary>
        /// Tạo các thông báo mẫu cho trang em-vn.joboko.com
        /// </summary>
        /// <returns>Danh sách ID của các thông báo đã tạo</returns>
        [HttpPost("create")]
        public async Task<ActionResult> CreateJobokoSampleNotifications()
        {
            try
            {
                var ids = await _notificationCB.AddJobokoSampleNotificationsAsync();
                return Ok(new { 
                    Message = $"Đã tạo {ids.Count} thông báo mẫu cho em-vn.joboko.com", 
                    Ids = ids 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo mẫu: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi tạo thông báo mẫu");
            }
        }
    }
}
