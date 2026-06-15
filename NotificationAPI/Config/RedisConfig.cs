namespace NotificationAPI.Config
{
    /// <summary>
    /// Config Redis cho cache layer của NotificationCB.
    /// Đọc từ appsettings.json:Redis.
    /// </summary>
    public class RedisConfig
    {
        /// <summary>
        /// Chuỗi kết nối StackExchange.Redis.
        /// VD: "host:6379,password=pwd,abortConnect=false,connectTimeout=2000"
        /// Để rỗng để disable cache.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Bật/tắt Redis cache. Nếu false, mọi request đều xuống thẳng Couchbase (giống code trước Phase 1).
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// TTL cache (giây). Hash khoá maxLastUpdated sẽ tự expire sau khoảng này
        /// nếu không bị invalidate chủ động.
        /// </summary>
        public int CacheTtlSeconds { get; set; } = 60;

        /// <summary>
        /// Prefix cho mọi Redis key của app này, tránh đụng namespace với app khác share Redis.
        /// </summary>
        public string KeyPrefix { get; set; } = "nof:";
    }
}
