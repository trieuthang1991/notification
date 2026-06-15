using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NotificationAPI.Areas.Admin.Models;
using NotificationAPI.DTO.Filter;
using NotificationAPI.Enums;
using NotificationAPI.Models;
using NotificationAPI.Services.Couchbase;
using NotificationAPI.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace NotificationAPI.Areas.Admin.Controllers
{
    public class NotificationController : AdminBaseController
    {
        private readonly INotificationCB _notificationCB;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(
            INotificationCB notificationCB,
            ILogger<NotificationController> logger)
        {
            _notificationCB = notificationCB;
            _logger = logger;
        }

        // GET: Admin/Notification
        public async Task<IActionResult> Index(SearchNotificationPaging filter)
        {
            try
            {
                int total = 0;

                Dictionary<string, NotificationStatus> dicStatus = new Dictionary<string, NotificationStatus>();
                var pagingNotification = await _notificationCB.SearchAsync(filter);
                total = pagingNotification.Total;
                var notifications = pagingNotification.Data;
                if (notifications != null && notifications.Any())
                {
                    var ids = notifications.Where(a => a.UserId != Utils.Common.All).Select(n => n.Id).ToList();
                    dicStatus = await _notificationCB.GetNotificationStatusByIdsAsync(ids);
                }
                else
                {
                    notifications = new List<NotificationConfig>();
                }
                var viewModel = new NotificationListViewModel
                {
                    Notifications = notifications,
                    Filter = filter,
                    Total = total,
                    NotificationStatus = dicStatus
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách thông báo: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi lấy danh sách thông báo: " + ex.Message;
                return View(new NotificationListViewModel());
            }
        }

        // GET: Admin/Notification/Create
        public IActionResult Create()
        {
            var model = new NotificationViewModel
            {
                Notification = new NotificationConfig
                {
                    StartDate = XMUtility.XUtility.UnixTime(DateTime.Now),
                    EndDate = XMUtility.XUtility.UnixTime(DateTime.Now.AddDays(30)),
                    Status = StatusNotification.Active,
                    Order = 100,
                    MaxShow = 0,
                    Frequency = 0,
                    Domains = new List<string>(),
                    PageShows = new List<ConfigLink>() { new ConfigLink() { Value = Utils.Common.All } },
                    PageExcludes = new List<ConfigLink>(),
                    ShowTypes = new List<ShowTypeNotification> { ShowTypeNotification.Popup },
                    TriggerActions = new List<string> { Utils.Common.All },
                    DeviceTypes = new List<DeviceType> { DeviceType.All },
                    InfoMore = new InfoMoreShowNoiDung
                    {
                        PopupDismissable = true,
                        HtmlDisplayLocation = string.Empty,
                        IsDirectLink = false,
                    }
                },
                ShowTypeOptions = GetShowTypeOptions(),
                StatusOptions = GetStatusOptions()
            };

            return View(model);
        }

        // POST: Admin/Notification/Create
        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NotificationViewModel model)
        {
            try
            {

                ModelState.Remove("Notification.Id");
                if (ModelState.IsValid)
                {
                    if (!String.IsNullOrEmpty(model.IdChienDich))
                        model.Notification.Id = $"{model.IdChienDich}_{model.Notification.UserId}";

                    // Đảm bảo LastUpdated được cập nhật
                    model.Notification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);

                    // Thêm notification mới
                    var id = await _notificationCB.UpsertAsync(model.Notification, model.Notification.Id);

                    TempData["Success"] = "Thông báo đã được tạo thành công!";
                    return RedirectToAction(nameof(Index));
                }

                // Nếu ModelState không hợp lệ, cần cung cấp lại các options
                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();

                // Đảm bảo các danh sách không bị null
                if (model.Notification.Domains == null)
                    model.Notification.Domains = new List<string>();

                if (model.Notification.PageShows == null)
                    model.Notification.PageShows = new List<ConfigLink> { new ConfigLink(Utils.Common.All) };

                if (model.Notification.PageExcludes == null)
                    model.Notification.PageExcludes = new List<ConfigLink>();

                if (model.Notification.ShowTypes == null || !model.Notification.ShowTypes.Any())
                    model.Notification.ShowTypes = new List<ShowTypeNotification> { ShowTypeNotification.Popup };

                if (model.Notification.TriggerActions == null || !model.Notification.TriggerActions.Any())
                    model.Notification.TriggerActions = new List<string> { Utils.Common.All };

                if (model.Notification.DeviceTypes == null)
                    model.Notification.DeviceTypes = new List<DeviceType> { DeviceType.All };

                if (model.Notification.InfoMore == null)
                    model.Notification.InfoMore = new InfoMoreShowNoiDung
                    {
                        PopupDismissable = true,
                        HtmlDisplayLocation = string.Empty,
                        IsDirectLink = false
                    };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi tạo thông báo: " + ex.Message;

                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();

                return View(model);
            }
        }

        // GET: Admin/Notification/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("ID không hợp lệ");
                }

                var notification = await _notificationCB.GetByIdAsync(id);
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }

                var model = new NotificationViewModel
                {
                    Notification = notification,
                    ShowTypeOptions = GetShowTypeOptions(),
                    StatusOptions = GetStatusOptions()
                };

                // Đảm bảo các danh sách không bị null
                if (model.Notification.Domains == null)
                    model.Notification.Domains = new List<string>();

                if (model.Notification.PageShows == null)
                    model.Notification.PageShows = new List<ConfigLink>() { new ConfigLink(Utils.Common.All) };

                if (model.Notification.PageExcludes == null)
                    model.Notification.PageExcludes = new List<ConfigLink>();

                if (model.Notification.ShowTypes == null)
                    model.Notification.ShowTypes = new List<ShowTypeNotification> { ShowTypeNotification.Popup };

                if (model.Notification.TriggerActions == null || !model.Notification.TriggerActions.Any())
                    model.Notification.TriggerActions = new List<string> { Utils.Common.All };

                if (model.Notification.DeviceTypes == null)
                    model.Notification.DeviceTypes = new List<DeviceType> { DeviceType.All };

                if (model.Notification.InfoMore == null)
                    model.Notification.InfoMore = new InfoMoreShowNoiDung
                    {
                        PopupDismissable = true,
                        HtmlDisplayLocation = string.Empty,
                        IsDirectLink = false,
                    };
                if (String.IsNullOrEmpty(model.Notification.InfoMore.HtmlDisplayLocation))
                {
                    model.Notification.InfoMore.HtmlDisplayLocation = string.Empty;
                }
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo để chỉnh sửa: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi lấy thông báo để chỉnh sửa: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Notification/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, NotificationViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("ID không hợp lệ");
                }

                if (id != model.Notification.Id)
                {
                    return BadRequest("ID không khớp");
                }

                if (ModelState.IsValid)
                {
                    // Đảm bảo LastUpdated được cập nhật
                    model.Notification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);

                    // Cập nhật notification
                    await _notificationCB.UpsertAsync(model.Notification, id);

                    TempData["Success"] = "Thông báo đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Index));
                }

                // Nếu ModelState không hợp lệ, cần cung cấp lại các options
                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();

                // Đảm bảo các danh sách không bị null
                if (model.Notification.Domains == null)
                    model.Notification.Domains = new List<string>();

                if (model.Notification.PageShows == null)
                    model.Notification.PageShows = new List<ConfigLink>();

                if (model.Notification.PageExcludes == null)
                    model.Notification.PageExcludes = new List<ConfigLink>();

                if (model.Notification.ShowTypes == null)
                    model.Notification.ShowTypes = new List<ShowTypeNotification> { ShowTypeNotification.Popup };

                if (model.Notification.TriggerActions == null || !model.Notification.TriggerActions.Any())
                    model.Notification.TriggerActions = new List<string> { Utils.Common.All };

                if (model.Notification.DeviceTypes == null)
                    model.Notification.DeviceTypes = new List<DeviceType>();

                if (model.Notification.InfoMore == null)
                    model.Notification.InfoMore = new InfoMoreShowNoiDung
                    {
                        PopupDismissable = true,
                        HtmlDisplayLocation = string.Empty,
                        IsDirectLink = false,
                    };
                if (String.IsNullOrEmpty(model.Notification.InfoMore.HtmlDisplayLocation))
                {
                    model.Notification.InfoMore.HtmlDisplayLocation = string.Empty;
                }
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật thông báo: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi cập nhật thông báo: " + ex.Message;

                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();

                return View(model);
            }
        }

        // GET: Admin/Notification/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("ID không hợp lệ");
                }

                var notification = await _notificationCB.GetByIdAsync(id);
                if (notification == null)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }

                return View(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo để xóa: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi lấy thông báo để xóa: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Notification/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("ID không hợp lệ");
                }

                await _notificationCB.DeleteAsync(id);

                TempData["Success"] = "Thông báo đã được xóa thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa thông báo: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi xóa thông báo: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string status)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(status))
                {
                    return BadRequest("ID hoặc trạng thái không hợp lệ");
                }

                var result = await _notificationCB.UpdateStatusAsync(id, status);
                if (!result)
                {
                    return NotFound($"Không tìm thấy thông báo với ID: {id}");
                }

                return Json(new { success = true, message = "Trạng thái đã được cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thông báo: {Message}", ex.Message);
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex.Message });
            }
        }

        // Các phương thức hỗ trợ
        private List<SelectListItem> GetShowTypeOptions()
        {
            var options = new List<SelectListItem>();
            var dic = EnumsHelper.EnumToDictionary<ShowTypeNotification>();
            foreach (var type in dic)
            {
                options.Add(new SelectListItem
                {
                    Value = type.Key.ToString(),
                    Text = type.Value.ToString()
                });
            }

            return options;
        }

        private List<SelectListItem> GetStatusOptions()
        {
            var options = new List<SelectListItem>();

            foreach (StatusNotification status in Enum.GetValues(typeof(StatusNotification)))
            {
                options.Add(new SelectListItem
                {
                    Value = ((int)status).ToString(),
                    Text = status.ToString()
                });
            }

            return options;
        }

        // GET: Admin/Notification/BatchCreate
        public IActionResult BatchCreate(string sourceId = null)
        {
            var model = new BatchNotificationViewModel();
            if (!string.IsNullOrEmpty(sourceId))
            {
                var notification = _notificationCB.GetByIdAsync(sourceId).Result;
                if (notification != null)
                {
                    model.BaseNotification = notification;
                    model.BaseNotification.Id = string.Empty;
                    model.BaseNotification.UserId = string.Empty;
                    model.BaseNotification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                    model.ShowTypeOptions = GetShowTypeOptions();
                    model.StatusOptions = GetStatusOptions();
                }
            }
            else
            {
                model.BaseNotification = new NotificationConfig
                {
                    StartDate = XMUtility.XUtility.UnixTime(DateTime.Now),
                    EndDate = XMUtility.XUtility.UnixTime(DateTime.Now.AddDays(30)),
                    Status = StatusNotification.Active,
                    Order = 100,
                    MaxShow = 0,
                    Frequency = 0,
                    Domains = new List<string>(),
                    PageShows = new List<ConfigLink>() { new ConfigLink(Utils.Common.All) },
                    PageExcludes = new List<ConfigLink>(),
                    ShowTypes = new List<ShowTypeNotification> { ShowTypeNotification.Popup },
                    TriggerActions = new List<string> { Utils.Common.All },
                    DeviceTypes = new List<DeviceType> { DeviceType.All },
                    InfoMore = new InfoMoreShowNoiDung
                    {
                        PopupDismissable = true,
                        HtmlDisplayLocation = string.Empty,
                        IsDirectLink = false
                    }
                };
                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();
            }
            return View(model);
        }

        // POST: Admin/Notification/CreateMultiUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMultiUser(MultiUserNotificationViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.UserIds))
                {
                    ModelState.AddModelError("UserIds", "Vui lòng nhập danh sách ID người dùng");
                    model.ShowTypeOptions = GetShowTypeOptions();
                    model.StatusOptions = GetStatusOptions();
                    return View("BatchCreate", new BatchNotificationViewModel
                    {
                        BaseNotification = model.BaseNotification,
                        UserIds = model.UserIds,
                        ShowTypeOptions = model.ShowTypeOptions,
                        StatusOptions = model.StatusOptions
                    });
                }

                // Tách danh sách ID người dùng
                var userIds = model.UserIds
                    .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                if (userIds.Count == 0)
                {
                    ModelState.AddModelError("UserIds", "Không tìm thấy ID người dùng hợp lệ");
                    model.ShowTypeOptions = GetShowTypeOptions();
                    model.StatusOptions = GetStatusOptions();
                    return View("BatchCreate", new BatchNotificationViewModel
                    {
                        BaseNotification = model.BaseNotification,
                        UserIds = model.UserIds,
                        ShowTypeOptions = model.ShowTypeOptions,
                        StatusOptions = model.StatusOptions
                    });
                }

                // Tạo thông báo cho từng người dùng
                var createdIds = new List<string>();

                foreach (var userId in userIds.Distinct().ToList())
                {
                    var notification = new NotificationConfig
                    {
                        Title = model.BaseNotification.Title,
                        Content = model.BaseNotification.Content,
                        Domains = model.BaseNotification.Domains,
                        UserId = userId,
                        PageShows = model.BaseNotification.PageShows,
                        PageExcludes = model.BaseNotification.PageExcludes,
                        ShowTypes = model.BaseNotification.ShowTypes,
                        StartDate = model.BaseNotification.StartDate,
                        EndDate = model.BaseNotification.EndDate,
                        MaxShow = model.BaseNotification.MaxShow,
                        Frequency = model.BaseNotification.Frequency,
                        Order = model.BaseNotification.Order,
                        Attributes = model.BaseNotification.Attributes,
                        DeviceTypes = model.BaseNotification.DeviceTypes,
                        TriggerActions = model.BaseNotification.TriggerActions,
                        InfoMore = model.BaseNotification.InfoMore,
                        Status = model.BaseNotification.Status,
                        ListTime = model.BaseNotification.ListTime,
                        LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now)
                    };
                    if (!String.IsNullOrEmpty(model.IdChienDich))
                    {
                        notification.Id = $"{model.IdChienDich}_{notification.UserId}";
                    }
                    var id = await _notificationCB.UpsertAsync(notification, notification.Id);
                    createdIds.Add(id);
                }
                Thread.Sleep(1000);
                TempData["Success"] = $"Đã tạo {createdIds.Count} thông báo thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo nhiều thông báo: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi tạo nhiều thông báo: " + ex.Message;

                model.ShowTypeOptions = GetShowTypeOptions();
                model.StatusOptions = GetStatusOptions();
                return View("BatchCreate", new BatchNotificationViewModel
                {
                    BaseNotification = model.BaseNotification,
                    UserIds = model.UserIds,
                    ShowTypeOptions = model.ShowTypeOptions,
                    StatusOptions = model.StatusOptions
                });
            }
        }

        // POST: Admin/Notification/UploadExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel(ExcelUploadViewModel model)
        {
            try
            {
                if (model.ExcelFile == null || model.ExcelFile.Length == 0)
                {
                    ModelState.AddModelError("ExcelFile", "Vui lòng chọn file Excel");
                    return View("BatchCreate", new BatchNotificationViewModel
                    {
                        ShowTypeOptions = GetShowTypeOptions(),
                        StatusOptions = GetStatusOptions()
                    });
                }

                // Kiểm tra định dạng file
                var extension = Path.GetExtension(model.ExcelFile.FileName).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    ModelState.AddModelError("ExcelFile", "File không đúng định dạng Excel (.xlsx, .xls)");
                    return View("BatchCreate", new BatchNotificationViewModel
                    {
                        ShowTypeOptions = GetShowTypeOptions(),
                        StatusOptions = GetStatusOptions()
                    });
                }

                // Đọc file Excel
                using (var memoryStream = new MemoryStream())
                {
                    await model.ExcelFile.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    // Đọc danh sách thông báo từ Excel
                    var notifications = ExcelHelper.ReadNotificationsFromExcel(memoryStream.ToArray());

                    if (notifications.Count == 0)
                    {
                        ModelState.AddModelError("ExcelFile", "Không tìm thấy dữ liệu thông báo trong file Excel");
                        return View("BatchCreate", new BatchNotificationViewModel
                        {
                            ShowTypeOptions = GetShowTypeOptions(),
                            StatusOptions = GetStatusOptions()
                        });
                    }

                    // Tạo thông báo cho từng dòng trong Excel
                    var createdIds = new List<string>();
                    foreach (var notification in notifications)
                    {
                        notification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                        var id = await _notificationCB.UpsertAsync(notification);
                        createdIds.Add(id);
                    }

                    TempData["Success"] = $"Đã tạo {createdIds.Count} thông báo từ file Excel thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo từ Excel: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi tạo thông báo từ Excel: " + ex.Message;

                return View("BatchCreate", new BatchNotificationViewModel
                {
                    ShowTypeOptions = GetShowTypeOptions(),
                    StatusOptions = GetStatusOptions()
                });
            }
        }

        // GET: Admin/Notification/DownloadExcelTemplate
        public IActionResult DownloadExcelTemplate()
        {
            try
            {
                var fileContent = ExcelHelper.CreateExcelTemplate();
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "NotificationTemplate.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo mẫu Excel: {Message}", ex.Message);
                TempData["Error"] = "Lỗi khi tạo mẫu Excel: " + ex.Message;
                return RedirectToAction(nameof(BatchCreate));
            }
        }

        // POST: Admin/Notification/BatchUpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchUpdateStatus(List<string> ids, string status)
        {
            try
            {
                if (ids == null || !ids.Any() || string.IsNullOrEmpty(status))
                {
                    return Json(new { success = false, message = "Danh sách ID hoặc trạng thái không hợp lệ" });
                }

                var successCount = 0;
                var failedIds = new List<string>();

                foreach (var id in ids)
                {
                    try
                    {
                        var result = await _notificationCB.UpdateStatusAsync(id, status);
                        if (result)
                        {
                            successCount++;
                        }
                        else
                        {
                            failedIds.Add(id);
                        }
                    }
                    catch
                    {
                        failedIds.Add(id);
                    }
                }

                var statusText = status == "1" ? "kích hoạt" : "vô hiệu hóa";
                var message = $"Đã {statusText} {successCount}/{ids.Count} thông báo thành công";
                if (failedIds.Any())
                {
                    message += $". {failedIds.Count} thông báo không thể cập nhật";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái hàng loạt: {Message}", ex.Message);
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái hàng loạt: " + ex.Message });
            }
        }

        // POST: Admin/Notification/BatchDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(List<string> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                {
                    return Json(new { success = false, message = "Danh sách ID không hợp lệ" });
                }

                var successCount = 0;
                var failedIds = new List<string>();

                foreach (var id in ids)
                {
                    try
                    {
                        var result = await _notificationCB.DeleteAsync(id);
                        if (result)
                        {
                            successCount++;
                        }
                        else
                        {
                            failedIds.Add(id);
                        }
                    }
                    catch
                    {
                        failedIds.Add(id);
                    }
                }

                var message = $"Đã xóa {successCount}/{ids.Count} thông báo thành công";
                if (failedIds.Any())
                {
                    message += $". {failedIds.Count} thông báo không thể xóa";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa thông báo hàng loạt: {Message}", ex.Message);
                return Json(new { success = false, message = "Lỗi khi xóa thông báo hàng loạt: " + ex.Message });
            }
        }

        // POST: GETAllNotificationStatus
       

    }
}
