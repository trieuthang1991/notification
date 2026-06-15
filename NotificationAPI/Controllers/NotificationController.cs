using Microsoft.AspNetCore.Mvc;
using NotificationAPI.DTO;
using NotificationAPI.DTO.Filter;
using NotificationAPI.Models;
using NotificationAPI.Services;
using NotificationAPI.Services.Couchbase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NotificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationCB _notificationCB;
        private readonly ILogger<NotificationController> _logger;
        private readonly IApiKeyValidator _apiKeyValidator;

        public NotificationController(
            INotificationCB notificationCB,
            ILogger<NotificationController> logger,
            IApiKeyValidator apiKeyValidator)
        {
            _notificationCB = notificationCB;
            _logger = logger;
            _apiKeyValidator = apiKeyValidator;
        }

        /// <summary>
        /// Lấy tất cả thông báo đang hoạt động
        /// </summary>
        /// <returns>Danh sách các thông báo đang hoạt động</returns>
        [HttpGet]
        public async Task<ActionResult<NotificationConfigVM>> GetActiveNotifications([FromQuery] SearchNotification filter)
        {
            try
            {
                var notifications = await _notificationCB.GetActiveNotificationsAsync(filter);


                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách thông báo hoạt động: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy danh sách thông báo");
            }
        }

        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <returns>Thông báo tương ứng với ID</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationConfig>> GetNotification(string id)
        {
            try
            {
                var notification = await _notificationCB.GetByIdAsync(id);
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy thông báo");
            }
        }

        [HttpGet("by-userId{userId}")]
        public async Task<ActionResult<List<NotificationConfig>>> GetNotificationByUserId(string userId)
        {
            try
            {
                var notification = await _notificationCB.GetByUserIdAsync(userId);
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {userId}");
                }
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy thông báo");
            }
        }
        [HttpGet("by-all-template")]
        public async Task<ActionResult<List<NotificationConfig>>> GetNotificationTemplate()
        {
            try
            {
                var notification = await _notificationCB.GetByUserIdAsync("template");
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với template");
                }
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy thông báo");
            }
        }


        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <returns>Thông báo tương ứng với ID</returns>
        [HttpGet("by-ids")]
        public async Task<ActionResult<List<NotificationConfig>>> GetContentByIds([FromQuery] string strIds)
        {
            try
            {
                var ids = strIds.Split(',').ToList();
                var notification = await _notificationCB.GetContentByIdsAsync(ids);
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {String.Join(",", ids)}");
                }
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy thông báo");
            }
        }

        /// <summary>
        /// Lấy thông báo theo thiết bị và hành động kích hoạt
        /// </summary>
        /// <param name="deviceType">Loại thiết bị</param>
        /// <param name="triggerAction">Hành động kích hoạt</param>
        /// <returns>Danh sách các thông báo phù hợp</returns>
        [HttpGet("by-device-trigger")]
        public async Task<ActionResult<List<NotificationConfig>>> GetByDeviceAndTrigger([FromQuery] string deviceType, [FromQuery] string triggerAction)
        {
            try
            {
                var notifications = await _notificationCB.GetByDeviceAndTriggerAsync(deviceType, triggerAction);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo theo thiết bị và hành động: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi lấy thông báo");
            }
        }

        /// <summary>
        /// Tạo một thông báo mới
        /// </summary>
        /// <param name="notification">Thông tin thông báo mới</param>
        /// <returns>ID của thông báo đã tạo</returns>
        [HttpPost]
        public async Task<ActionResult<string>> CreateNotification([FromBody] NotificationConfig notification)
        {
            try
            {
                if (notification == null)
                {
                    return BadRequest("Thông tin thông báo không hợp lệ");
                }
                if (notification.TriggerActions == null || !notification.TriggerActions.Any())
                {
                    notification.TriggerActions = new List<string> { Utils.Common.All };
                }

                var id = await _notificationCB.UpsertAsync(notification, notification.Id);
                return Ok(new { Id = id, Message = "Đã tạo thông báo thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi tạo thông báo");
            }
        }

        /// <summary>
        /// Cập nhật thông báo
        /// </summary>
        /// <param name="id">ID của thông báo cần cập nhật</param>
        /// <param name="notification">Thông tin cập nhật</param>
        /// <returns>Kết quả cập nhật</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateNotification(string id, [FromBody] NotificationConfig notification)
        {
            try
            {


                await _notificationCB.UpsertAsync(notification, id);
                return Ok(new { Message = "Đã cập nhật thông báo thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi cập nhật thông báo");
            }
        }

        /// <summary>
        /// Cập nhật trạng thái của thông báo
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <param name="status">Trạng thái mới</param>
        /// <returns>Kết quả cập nhật</returns>
        [HttpPatch("{id}/status")]
        public async Task<ActionResult> UpdateStatus(string id, [FromQuery] string status)
        {
            try
            {

                var result = await _notificationCB.UpdateStatusAsync(id, status);
                if (!result)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }
                return Ok(new { Message = "Đã cập nhật trạng thái thông báo thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi cập nhật trạng thái thông báo");
            }
        }

        /// <summary>
        /// Cập nhật trạng thái của thông báo (hiển thị, tương tác)
        /// </summary>
        /// <param name="statusUpdate">Thông tin cập nhật trạng thái</param>
        /// <returns>Kết quả cập nhật</returns>
        [HttpPost("update-status")]
        public async Task<ActionResult> UpdateNotificationStatus([FromBody] NotificationStatus statusUpdate)
        {
            try
            {
                if (statusUpdate == null)
                {
                    return BadRequest("Thông tin cập nhật không hợp lệ");
                }
                if (string.IsNullOrEmpty(statusUpdate.NotificationId) || string.IsNullOrEmpty(statusUpdate.UserId))
                {
                    return BadRequest("NotificationId hoặc UserId không được để trống");
                }
                if (statusUpdate.RemainingShows > 1)
                {
                    statusUpdate.RemainingShows = 1;
                }
                var result = await _notificationCB.UpdateNotificationStatusAsync(statusUpdate);
                if (!result)
                {
                    return BadRequest("Không thể cập nhật trạng thái thông báo");
                }

                // Trả về thông báo phù hợp dựa trên loại cập nhật
                string message = statusUpdate.LastClick > 0
                    ? "Đã đánh dấu thông báo là \"đã xem\" thành công"
                    : "Đã cập nhật trạng thái hiển thị thông báo thành công";

                return Ok(new { Message = message });
            }
            catch (Exception ex)
            {
                string errorMessage = statusUpdate.LastClick > 0
                    ? "Lỗi khi đánh dấu thông báo là \"đã xem\""
                    : "Lỗi khi cập nhật trạng thái hiển thị thông báo";

                _logger.LogError(ex, "{ErrorMessage}: {Message}", errorMessage, ex.Message);
                return StatusCode(500, errorMessage);
            }
        }

        /// <summary>
        /// Đánh dấu thông báo là "đã xem" (API được giữ lại để tương thích ngược)
        /// </summary>
        /// <param name="seenUpdate">Thông tin cập nhật trạng thái đã xem</param>
        /// <returns>Kết quả cập nhật</returns>
        [HttpPost("mark-as-seen")]
        public async Task<ActionResult> MarkNotificationAsSeen([FromBody] NotificationSeenUpdate seenUpdate)
        {
            try
            {
                if (seenUpdate == null)
                {
                    return BadRequest("Thông tin cập nhật không hợp lệ");
                }

                // Chuyển đổi từ NotificationSeenUpdate sang NotificationStatusUpdate
                var statusUpdate = new NotificationStatus
                {
                    NotificationId = seenUpdate.NotificationId,
                    UserId = seenUpdate.UserId,
                    Domain = seenUpdate.Domain,
                    LastClick = XMUtility.XUtility.UnixTime(DateTime.Now) // Thời gian hiện tại
                };

                var result = await _notificationCB.UpdateNotificationStatusAsync(statusUpdate);
                if (!result)
                {
                    return BadRequest("Không thể đánh dấu thông báo là \"đã xem\"");
                }

                return Ok(new { Message = "Đã đánh dấu thông báo là \"đã xem\" thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh dấu thông báo là \"đã xem\": {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi đánh dấu thông báo là \"đã xem\"");
            }
        }

        /// <summary>
        /// Xóa thông báo
        /// </summary>
        /// <param name="id">ID của thông báo cần xóa</param>
        /// <returns>Kết quả xóa</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNotification(string id)
        {
            try
            {
                var result = await _notificationCB.DeleteAsync(id);
                if (!result)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }
                return Ok(new { Message = "Đã xóa thông báo thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa thông báo: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi xóa thông báo");
            }
        }

        /// <summary>
        /// API cho phép hệ thống bên ngoài tạo một thông báo
        /// </summary>
        /// <param name="request">Yêu cầu tạo thông báo từ hệ thống bên ngoài</param>
        /// <returns>Kết quả tạo thông báo</returns>
        [HttpPost("external")]
        public async Task<ActionResult> CreateExternalNotification([FromBody] ExternalNotificationRequest request)
        {
            try
            {
                // Xác thực API key
                if (!_apiKeyValidator.IsValid(request.ApiKey))
                {
                    return Unauthorized("API key không hợp lệ");
                }
                // Chuyển đổi từ ExternalNotificationRequest sang NotificationConfig
                var notification = request.ToNotificationConfig();

                NotificationConfig objTemplate = new NotificationConfig();
                if (!String.IsNullOrEmpty(request.TemplateId))
                {
                    objTemplate = await _notificationCB.GetByIdAsync(request.TemplateId);
                    if (objTemplate != null && !String.IsNullOrEmpty(objTemplate.Id))
                    {
                        if (request.TempStartDate > 0)
                        {
                            objTemplate.StartDate = request.TempStartDate;
                        }
                        if (request.TempEndDate > 0)
                        {
                            objTemplate.EndDate = request.TempEndDate;
                        }
                        if(!String.IsNullOrEmpty(request.TempTieuDe))
                        {
                            objTemplate.Title = request.TempTieuDe;
                        }
                        if(!String.IsNullOrEmpty(request.TempNoiDung))
                        {
                            objTemplate.Content = request.TempNoiDung;
                        }
                        if (request.TriggerActions != null && request.TriggerActions.Any())
                        {
                            objTemplate.TriggerActions = request.TriggerActions;
                        }
                        if (objTemplate.TriggerActions == null || !objTemplate.TriggerActions.Any())
                        {
                            objTemplate.TriggerActions = new List<string> { Utils.Common.All };
                        }
                    }
                    else
                    {
                        return StatusCode(404, "Mẫu không tồn tại");
                    }
                }

                if (objTemplate != null && !String.IsNullOrEmpty(objTemplate.Id))
                {
                    string strId = notification.Id;
                    string strUserId = notification.UserId;
                    notification = objTemplate;
                    notification.Id = strId;
                    notification.UserId = strUserId;
                    notification.DateCreated = XMUtility.XUtility.UnixTime(DateTime.Now);
                    notification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                }
                // Tạo thông báo
                var id = await _notificationCB.UpsertAsync(notification, notification.Id);

                return Ok(new { Id = id, Message = "Đã tạo thông báo từ hệ thống ngoài thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo từ hệ thống ngoài: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi tạo thông báo từ hệ thống ngoài");
            }
        }

        /// <summary>
        /// API cho phép hệ thống bên ngoài tạo nhiều thông báo cùng lúc
        /// </summary>
        /// <param name="request">Yêu cầu tạo nhiều thông báo từ hệ thống bên ngoài</param>
        /// <returns>Kết quả tạo các thông báo</returns>
        [HttpPost("external/batch")]
        public async Task<ActionResult> CreateBatchExternalNotifications([FromBody] BatchExternalNotificationRequest request)
        {
            try
            {
                // Xác thực API key
                if (!_apiKeyValidator.IsValid(request.ApiKey))
                {
                    return Unauthorized("API key không hợp lệ");
                }

                // Kiểm tra xem có thông báo nào không
                if (request.Notifications == null || !request.Notifications.Any())
                {
                    return BadRequest("Danh sách thông báo không được để trống");
                }
                // Danh sách kết quả
                var results = new List<object>();

                NotificationConfig objTemplate = new NotificationConfig();

                if (!String.IsNullOrEmpty(request.TemplateId))
                {
                    objTemplate = await _notificationCB.GetByIdAsync(request.TemplateId);
                    if (objTemplate != null && !String.IsNullOrEmpty(objTemplate.Id))
                    {
                        if (request.TempStartDate > 0)
                        {
                            objTemplate.StartDate = request.TempStartDate;
                        }
                        if (request.TempEndDate > 0)
                        {
                            objTemplate.EndDate = request.TempEndDate;
                        }



                        if (objTemplate.TriggerActions == null || !objTemplate.TriggerActions.Any())
                        {
                            objTemplate.TriggerActions = new List<string> { Utils.Common.All };
                        }
                    }
                    else
                    {
                        return StatusCode(404, "Mẫu không tồn tại");
                    }
                }


                // Xử lý từng thông báo
                foreach (var item in request.Notifications)
                {
                    try
                    {
                        // Chuyển đổi từ ExternalNotificationItem sang NotificationConfig
                        var notification = item;

                        if (objTemplate != null && !String.IsNullOrEmpty(objTemplate.Id))
                        {
                            string strId = notification.Id;
                            string strUserId = notification.UserId;
                            notification = objTemplate;
                            notification.Id = strId;
                            notification.UserId = strUserId;
                        }
                        // Tạo thông báo
                        var id = await _notificationCB.UpsertAsync(notification, notification.Id);

                        // Thêm kết quả vào danh sách
                        results.Add(new { Id = id, Title = item.Title, Success = true });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi tạo thông báo '{Title}': {Message}", item.Title, ex.Message);

                        // Thêm kết quả lỗi vào danh sách
                        results.Add(new { Title = item.Title, Success = false, Error = ex.Message });
                    }
                }

                // Tổng hợp kết quả
                var successCount = results.Count(r => ((dynamic)r).Success);
                var totalCount = request.Notifications.Count;

                return Ok(new
                {
                    Message = $"Đã tạo thành công {successCount}/{totalCount} thông báo",
                    Results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo nhiều thông báo từ hệ thống ngoài: {Message}", ex.Message);
                return StatusCode(500, "Lỗi khi tạo nhiều thông báo từ hệ thống ngoài");
            }
        }

        //[HttpGet("external/batch-trigger")]
        //public async Task<IActionResult> CreateBatchTriggerExternalNotifications()
        //{
        //    try
        //    {
        //        //var notifications = await _notificationCB.SearchAsync(new SearchNotificationPaging()
        //        //{
        //        //    PageIndex = 0,
        //        //    PageSize = 10000
        //        //});
        //        //foreach (var items in notifications.Data)
        //        //{
        //        //    if (items.TriggerActions == null || !items.TriggerActions.Any())
        //        //    {
        //        //        items.TriggerActions = new List<string> { Utils.Common.All };
        //        //        await _notificationCB.UpsertAsync(items, items.Id);
        //        //    }
                       
        //        //}
        //        return StatusCode(200, "Thành công");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi khi lấy trạng thái thông báo: {Message}", ex.Message);
        //        return StatusCode(404, "Thất bại");
        //    }
        //}

    }
}
