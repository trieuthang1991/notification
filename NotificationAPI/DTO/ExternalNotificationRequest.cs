using NotificationAPI.Enums;
using NotificationAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace NotificationAPI.DTO
{
    /// <summary>
    /// DTO cho yêu cầu tạo thông báo từ hệ thống bên ngoài
    /// </summary>
    public class ExternalNotificationRequest : NotificationConfig
    {
        /// <summary>
        /// API Key để xác thực hệ thống gọi API
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;

        public string TempTieuDe { get; set; } = string.Empty;
        public string TempNoiDung { get; set; } = string.Empty;
        public long TempStartDate { get; set; } = 0;
        public long TempEndDate { get; set; } = 0;
       

        public NotificationConfig ToNotificationConfig()
        {
            var config = new NotificationConfig
            {
                Id = Id,
                Domains = Domains,
                UserId = UserId,
                PageShows = PageShows,
                PageExcludes = PageExcludes,
                ShowTypes = ShowTypes,
                Title = Title,
                Content = Content,
                InfoMore = this.InfoMore,
                StartDate = StartDate,
                EndDate = EndDate,
                MaxShow = MaxShow,
                Frequency = Frequency,
                ListTime = ListTime,
                Order = Order,
                Attributes = Attributes,
                DeviceTypes = DeviceTypes,
                TriggerActions = TriggerActions,
                Status = StatusNotification.Active,
                LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now)
            };

            return config;
        }
    }
}
