using Newtonsoft.Json;

namespace NotificationAPI.DTO
{
    /// <summary>
    /// DTO cho việc đánh dấu thông báo là "đã xem" (giữ lại để tương thích ngược)
    /// </summary>
    public class NotificationSeenUpdate
    {
        /// <summary>
        /// ID của thông báo
        /// </summary>
        [JsonProperty("notificationId")]
        public string NotificationId { get; set; } = string.Empty;

        /// <summary>
        /// ID của người dùng
        /// </summary>
        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Tên miền
        /// </summary>
        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Đã xem hay chưa
        /// </summary>
        [JsonProperty("seen")]
        public bool Seen { get; set; } = true;
    }
}
