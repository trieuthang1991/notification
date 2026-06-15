using System.ComponentModel.DataAnnotations;
using NotificationAPI.Models;

namespace NotificationAPI.DTO
{
    /// <summary>
    /// DTO cho yêu cầu tạo nhiều thông báo cùng lúc từ hệ thống bên ngoài
    /// </summary>
    public class BatchExternalNotificationRequest
    {
        /// <summary>
        /// API Key để xác thực hệ thống gọi API
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public long TempStartDate { get; set; } = 0;
        public long TempEndDate { get; set; } = 0;
        /// <summary>
        /// Danh sách các thông báo cần tạo
        /// </summary>
        [Required]
        public List<NotificationConfig> Notifications { get; set; } = new List<NotificationConfig>();
    }

  
}
