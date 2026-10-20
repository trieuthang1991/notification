namespace NotificationAPI.Config
{
    /// <summary>
    /// Cấu hình snapshot config trong RAM. Đọc từ appsettings.json:ConfigSnapshot.
    /// </summary>
    public class ConfigSnapshotConfig
    {
        /// <summary>Bật/tắt snapshot. Tắt = mọi request đi N1QL như cũ.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Chu kỳ chạy refresh (giây). Mỗi chu kỳ là một lần nạp delta (nhẹ).</summary>
        public int RefreshSeconds { get; set; } = 5;

        /// <summary>Chu kỳ nạp lại toàn bộ (giây) để bắt các doc bị xóa/hết hạn.</summary>
        public int FullReloadSeconds { get; set; } = 60;

        /// <summary>Lùi watermark last_updated bấy nhiêu giây khi nạp delta, tránh sót doc cùng giây.</summary>
        public int DeltaOverlapSeconds { get; set; } = 5;

        /// <summary>Snapshot cũ hơn ngưỡng này (giây) thì bị coi là không dùng được, rơi về N1QL.</summary>
        public int MaxStaleSeconds { get; set; } = 120;
    }
}
