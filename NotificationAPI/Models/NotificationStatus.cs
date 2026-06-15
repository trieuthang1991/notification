using System.Collections.Generic;
using Newtonsoft.Json;

namespace NotificationAPI.Models
{
    public class NotificationStatus
    {
        /// <summary>
        /// Mã ID của thông báo.
        /// </summary>
        [JsonProperty("notificationId")]
        public string NotificationId { get; set; } = string.Empty;
        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Mã ID của người dùng nhận thông báo.
        /// </summary>
        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Số lần hiển thị còn lại của thông báo cho người dùng.
        /// </summary>
        [JsonProperty("remainingShows")]
        public int RemainingShows { get; set; } = 0;

        /// <summary>
        /// Thời gian lần hiển thị thông báo cuối cùng.
        /// </summary>
        [JsonProperty("lastShown")]
        public long LastShown { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);
        [JsonProperty("lastClick")]
        public long LastClick { get; set; } = 0;
        public NotificationStatus()
        {

        }
        public string GetKey(string _preKey)
        {
            return $"{_preKey}notification_status_{this.NotificationId}_{this.UserId}_{this.Domain}";
        }
    }

    /// <summary>
    /// Thông tin sẽ lưu ở client => Tạo ra ở đây có thể sau sẽ tuyển token qua đỡ phải dùng js lọc lại
    /// </summary>
    public class ClientNotificationStatus
    {
        /// <summary>
        /// Mã ID của thông báo.
        /// </summary>
        public string NotificationId { get; set; } = string.Empty;




        /// <summary>
        /// Số lần hiển thị thông báo đã được lưu trên client.
        /// </summary>
        public int ShownCount { get; set; } = 0;

        /// <summary>
        /// Thời gian lần hiển thị thông báo cuối cùng.
        /// </summary>
        public long LastShown { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);

        /// <summary>
        /// Thời gian hết hạn của thông báo trên client.
        /// </summary>
        public long Expires { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);
    }
}
