using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace NotificationAPI.Models
{
    /// <summary>
    /// Model cho thông tin đăng nhập người dùng
    /// </summary>
    public class UserLogin
    {
        [JsonProperty("username")]
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("password")]
        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [JsonProperty("remember_me")]
        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; } = false;
    }

    /// <summary>
    /// Model cho thông tin người dùng được lưu trữ
    /// </summary>
    public class UserInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonProperty("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string Role { get; set; } = "User";

        [JsonProperty("last_login")]
        public long LastLogin { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);

        [JsonProperty("created_at")]
        public long CreatedAt { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);

        [JsonProperty("is_active")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Model cho danh sách người dùng được lưu trữ trong file JSON
    /// </summary>
    public class UserStore
    {
        [JsonProperty("users")]
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();
    }
}
