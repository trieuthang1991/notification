using NotificationAPI.Enums;
using Newtonsoft.Json;

namespace NotificationAPI.Models
{
    public class NotificationConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Các tên miền mà thông báo sẽ áp dụng. Ví dụ: ["example.com", "mydomain.com"]
        /// </summary>
        [JsonProperty("domains")]
        public List<string> Domains { get; set; } = new List<string>();

        /// <summary>
        /// Danh sách người dùng nhận thông báo, có thể là "all" (tất cả người dùng) hoặc các Id người dùng cụ thể.
        /// </summary>
        [JsonProperty("user_id")]
        public string UserId { get; set; } = Utils.Common.All;

        /// <summary>
        /// Các trang sẽ hiển thị thông báo, có thể sử dụng "all" cho tất cả các trang.
        /// Có thể sử dụng URL trực tiếp hoặc regex để xác định các trang.
        /// </summary>
        [JsonProperty("page_shows")]
        public List<ConfigLink> PageShows { get; set; } = new List<ConfigLink>();

        /// <summary>
        /// Các trang sẽ không nhận thông báo. Ví dụ: ["/khuyen-mai", "/dang-nhap"]
        /// </summary>
        [JsonProperty("page_excludes")]
        public List<ConfigLink> PageExcludes { get; set; } = new List<ConfigLink>();

        /// <summary>
        /// Kiểu hiển thị thông báo (Popup, HTML trên trang, hoặc Liên kết).
        /// Ví dụ: ["Popup", "HTML", "Link"]
        /// </summary>
        [JsonProperty("show_types")]
        public List<ShowTypeNotification> ShowTypes { get; set; } = new List<ShowTypeNotification>();

        /// <summary>
        /// Tiêu đề của thông báo. Ví dụ: "Khuyến mãi đặc biệt!"
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung của thông báo. Ví dụ: "Giảm giá 20% hôm nay."
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin bổ sung về cách hiển thị nội dung, ví dụ: cho phép tắt popup.
        /// </summary>
        [JsonProperty("info_more")]
        public InfoMoreShowNoiDung InfoMore { get; set; } = new InfoMoreShowNoiDung();

        /// <summary>
        /// Thời gian bắt đầu hiển thị thông báo (Unix time).
        /// </summary>
        [JsonProperty("start_date")]
        public long StartDate { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);

        /// <summary>
        /// Thời gian kết thúc hiển thị thông báo (Unix time).
        /// </summary>
        [JsonProperty("end_date")]
        public long EndDate { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);

        /// <summary>
        /// Số lần tối đa hiển thị cho mỗi người dùng. Ví dụ: 3 (lần hiển thị tối đa là 3).
        /// </summary>
        [JsonProperty("max_show")]
        public int MaxShow { get; set; } = 0;

        /// <summary>
        /// Tần suất hiển thị thông báo (Tính theo giờ).
        /// Nếu giá trị là 0, thông báo sẽ hiển thị mỗi lần người dùng truy cập trang.
        /// Nếu giá trị khác 0, thông báo chỉ hiển thị lại sau khoảng thời gian tương ứng (tính theo giờ) kể từ lần hiển thị trước.
        /// </summary>
        [JsonProperty("frequency")]
        public int Frequency { get; set; } = 0;

        /// <summary>
        /// Danh sách thời gian hiển thị theo phút cho mỗi lần hiển thị.
        /// Ví dụ: [5, 10, 15] - Lần đầu hiển thị sau 5 phút, lần 2 sau 10 phút, lần 3 sau 15 phút.
        /// Nếu số lần hiển thị vượt quá số phần tử trong danh sách, sẽ sử dụng phần tử cuối cùng.
        /// Nếu ListTime có giá trị, sẽ ưu tiên sử dụng ListTime thay vì Frequency.
        /// </summary>
        [JsonProperty("list_time")]
        public List<int> ListTime { get; set; } = new List<int>();


        /// <summary>
        /// Thứ tự ưu tiên khi có nhiều thông báo. Các thông báo với giá trị Order thấp sẽ được hiển thị trước (từ nhỏ đến lớn).
        /// </summary>
        [JsonProperty("ord")]
        public int Order { get; set; } = 100;

        /// <summary>
        /// Các thuộc tính mở rộng có thể được cấu hình cho thông báo.
        /// Ví dụ: Trừ số lần hiển thị trên server.
        /// </summary>
        [JsonProperty("attributes")]
        public List<int> Attributes { get; set; } = new List<int>();

        /// <summary>
        /// Trạng thái của thông báo (Active hoặc Inactive). Mặc định là "Active" (hoạt động).
        /// </summary>
        [JsonProperty("status")]
        public StatusNotification Status { get; set; } = StatusNotification.Active;

        /// <summary>
        /// Các hành động kích hoạt thông báo (ví dụ: OnRegister - khi người dùng đăng ký).
        /// Mặc định là "all" nghĩa là sẽ hiển thị cho tất cả các hành động.
        /// </summary>
        [JsonProperty("trigger_actions")]
        public List<string> TriggerActions { get; set; } = new List<string>();

        /// <summary>
        /// Các loại thiết bị mà thông báo sẽ hiển thị (desktop, mobile, all).
        /// </summary>
        [JsonProperty("device_types")]
        public List<DeviceType> DeviceTypes { get; set; } = new List<DeviceType>();

        /// <summary>
        /// Thời gian cập nhật gần nhất của thông báo (Unix time).
        /// </summary>
        [JsonProperty("last_updated")]
        public long LastUpdated { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);
        [JsonProperty("date_created")]
        public long DateCreated { get; set; } = XMUtility.XUtility.UnixTime(DateTime.Now);
        public NotificationConfig()
        {

        }
    }

    public class PagingNotificationConfig
    {
        public int Total { get; set; } = 0;
        public List<NotificationConfig> Data { get; set; } = new List<NotificationConfig>();
    }

    public class ConfigLink
    {
        /// <summary>
        /// Lấy theo path hay lấy theo path + query từ url
        /// </summary>
        [JsonProperty("clt")]
        public ConfigLinkType CLT { get; set; } = ConfigLinkType.Path;
        /// <summary>
        /// So sánh bằng hay so sánh theo regex
        /// </summary>
        [JsonProperty("cltg")]
        public ConfigLinkTypeGet CLTG { get; set; } = ConfigLinkTypeGet.Same;
        /// <summary>
        /// Đường dẫn cần so sánh ("/dang-nhạp"....)
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;
        public ConfigLink()
        {

        }
        public ConfigLink(string value)
        {
            Value = value;
        }
    }



    [JsonObject("info_more_details")]
    public class InfoMoreShowNoiDung
    {
        /// <summary>
        /// Cho phép người dùng tắt popup không? Nếu "true", người dùng có thể đóng popup.
        /// </summary>
        [JsonProperty("popup_dismissable")]
        public bool PopupDismissable { get; set; } = true;

        /// <summary>
        /// Vị trí hiển thị HTML nếu có, ví dụ: "div#notification-container". Được sử dụng khi thông báo là HTML thay vì Popup.
        /// </summary>
        [JsonProperty("html_display_location")]
        public string? HtmlDisplayLocation { get; set; } = string.Empty;

        /// <summary>
        /// Cho phép tạo liên kết trực tiếp không? Nếu "true", thông báo sẽ tự động chuyển hướng người dùng đến một trang khác mà không cần nhấp.
        /// </summary>
        [JsonProperty("is_direct_link")]
        public bool IsDirectLink { get; set; } = false;

        /// <summary>
        /// Sau bao lâu mới hiển thị
        /// </summary>
        [JsonProperty("delay")]
        public int Delay { get; set; } = 0;

        public InfoMoreShowNoiDung()
        {

        }
    }
}


