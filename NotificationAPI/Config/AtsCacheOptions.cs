using System.Collections.Generic;

namespace NotificationAPI.Config
{
    /// <summary>
    /// Cấu hình cho Couchbase Cache
    /// </summary>
    public class AtsCacheOptions
    {
        /// <summary>
        /// Tên section trong appsettings.json
        /// </summary>
        public const string Name = "Couchbase";

        /// <summary>
        /// Danh sách các server Couchbase
        /// </summary>
        public List<string> Servers { get; set; } = new List<string>();

        /// <summary>
        /// Username để xác thực với Couchbase Server
        /// </summary>
        public string Username { get; set; } = "xlog";

        /// <summary>
        /// Password để xác thực với Couchbase Server
        /// </summary>
        public string Password { get; set; } = "xlogxlog";

        /// <summary>
        /// Sử dụng SSL để kết nối đến Couchbase Server
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Tiền tố cho các key trong Couchbase
        /// </summary>
        public string PreKey { get; set; } = "v1_";

        /// <summary>
        /// Tên bucket chứa dữ liệu thông báo
        /// </summary>
        public string BucketName { get; set; } = "xlog";
    }
}
