using Microsoft.AspNetCore.Mvc.Rendering;
using NotificationAPI.Enums;
using NotificationAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace NotificationAPI.Areas.Admin.Models
{
    public class BatchNotificationViewModel
    {
        // Thông tin cơ bản cho tất cả các thông báo
        public NotificationConfig BaseNotification { get; set; } = new NotificationConfig();

        // Danh sách ID người dùng
        [Display(Name = "Danh sách ID người dùng")]
        public string UserIds { get; set; } = string.Empty;
        /// <summary>
        /// Dùng để gen Id
        /// </summary>
        public string IdChienDich { get; set; } = string.Empty;
        // File Excel
        [Display(Name = "File Excel")]
        public IFormFile? ExcelFile { get; set; }

        // Options cho các dropdown
        public List<SelectListItem> ShowTypeOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
    }

    public class MultiUserNotificationViewModel
    {
        // Thông tin cơ bản cho tất cả các thông báo
        public NotificationConfig BaseNotification { get; set; } = new NotificationConfig();

        // Danh sách ID người dùng
        [Display(Name = "Danh sách ID người dùng")]
        [Required(ErrorMessage = "Vui lòng nhập danh sách ID người dùng")]
        public string UserIds { get; set; } = string.Empty;
        /// <summary>
        /// Dùng để gen Id
        /// </summary>
        public string IdChienDich { get; set; } = string.Empty;
        // Options cho các dropdown
        public List<SelectListItem> ShowTypeOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
    }

    public class ExcelUploadViewModel
    {
        // File Excel
        [Display(Name = "File Excel")]
        [Required(ErrorMessage = "Vui lòng chọn file Excel")]
        public IFormFile? ExcelFile { get; set; }
    }
}
