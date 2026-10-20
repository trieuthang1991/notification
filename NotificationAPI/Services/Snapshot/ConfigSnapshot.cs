using NotificationAPI.Models;

namespace NotificationAPI.Services.Snapshot
{
    /// <summary>
    /// Kho giữ snapshot toàn bộ config trong RAM. Đọc không khóa (tham chiếu volatile tới
    /// dictionary bất biến); ghi tuần tự bằng lock theo kiểu copy-on-write.
    /// </summary>
    public sealed class ConfigSnapshot
    {
        private readonly Func<long> _clock;
        private readonly object _gate = new();
        private volatile Dictionary<string, NotificationConfig>? _byId;
        private long _lastRefreshUnix;
        private long _lastFullLoadUnix;
        private long _serverWatermark;

        /// <param name="clock">Trả về unix time (giây) hiện tại; truyền vào để test được.</param>
        public ConfigSnapshot(Func<long> clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Tạo bản lưu vào snapshot: bản sao (không sửa object gốc của caller) và KHÔNG giữ HTML lớn
        /// trong RAM. Content dài hơn ngưỡng thì bỏ trống và gắn attribute 1 (client lấy lại nội dung
        /// theo id từ Couchbase), tương đương đoạn xử lý ở GetActiveNotificationsAsync.
        /// </summary>
        public static NotificationConfig ToSnapshotItem(NotificationConfig c)
        {
            var clone = c.CloneForResponse();
            if (!string.IsNullOrEmpty(clone.Content) && clone.Content.Length > Utils.Common.MaxInlineContentChars)
            {
                clone.Content = string.Empty;
                if (!clone.Attributes.Contains(1)) clone.Attributes.Add(1);
            }
            return clone;
        }

        /// <summary>Đã nạp đầy đủ ít nhất một lần.</summary>
        public bool IsReady => _byId != null;

        /// <summary>Các config hiện có (rỗng nếu chưa sẵn sàng).</summary>
        public IReadOnlyCollection<NotificationConfig> Items
            => (IReadOnlyCollection<NotificationConfig>?)_byId?.Values ?? Array.Empty<NotificationConfig>();

        /// <summary>
        /// Mốc last_updated cho lần nạp delta. CHỈ theo dữ liệu từ server (ReplaceAll/Merge);
        /// Upsert/Remove cục bộ không làm thay đổi để không bỏ sót bản sửa của instance khác.
        /// </summary>
        public long WatermarkLastUpdated => Interlocked.Read(ref _serverWatermark);

        /// <summary>Số giây kể từ lần nạp đầy đủ gần nhất (0 nếu chưa nạp).</summary>
        public long FullLoadAgeSeconds
        {
            get
            {
                var last = Interlocked.Read(ref _lastFullLoadUnix);
                return last == 0 ? 0 : _clock() - last;
            }
        }

        /// <summary>Đã sẵn sàng và lần refresh thành công cuối cùng chưa quá maxStaleSeconds.</summary>
        public bool IsFresh(int maxStaleSeconds)
            => IsReady && (_clock() - Interlocked.Read(ref _lastRefreshUnix)) <= maxStaleSeconds;

        /// <summary>Thay toàn bộ snapshot bằng kết quả nạp đầy đủ.</summary>
        public void ReplaceAll(IEnumerable<NotificationConfig> all)
        {
            var next = new Dictionary<string, NotificationConfig>(StringComparer.Ordinal);
            long max = 0;
            foreach (var c in all)
            {
                if (string.IsNullOrEmpty(c.Id)) continue;
                next[c.Id] = c;
                if (c.LastUpdated > max) max = c.LastUpdated;
            }
            lock (_gate)
            {
                _byId = next;
                Interlocked.Exchange(ref _serverWatermark, max);
                var now = _clock();
                Interlocked.Exchange(ref _lastRefreshUnix, now);
                Interlocked.Exchange(ref _lastFullLoadUnix, now);
            }
        }

        /// <summary>
        /// Gộp kết quả nạp delta. Chỉ thay doc khi bản mới có last_updated >= bản đang giữ,
        /// để delta cũ không đè lên bản ghi xuyên cục bộ mới hơn.
        /// </summary>
        public void Merge(IEnumerable<NotificationConfig> delta)
        {
            lock (_gate)
            {
                var current = _byId;
                if (current == null) return; // chưa nạp đầy đủ thì chưa gộp delta
                var next = new Dictionary<string, NotificationConfig>(current, StringComparer.Ordinal);
                long wm = Interlocked.Read(ref _serverWatermark);
                foreach (var c in delta)
                {
                    if (string.IsNullOrEmpty(c.Id)) continue;
                    if (c.LastUpdated > wm) wm = c.LastUpdated;
                    if (next.TryGetValue(c.Id, out var existing) && existing.LastUpdated > c.LastUpdated) continue;
                    next[c.Id] = c;
                }
                _byId = next;
                Interlocked.Exchange(ref _serverWatermark, wm);
                Interlocked.Exchange(ref _lastRefreshUnix, _clock());
            }
        }

        /// <summary>Ghi xuyên một config vừa được lưu (đọc-sau-ghi trên cùng instance).</summary>
        public void Upsert(NotificationConfig config)
        {
            if (string.IsNullOrEmpty(config.Id)) return;
            lock (_gate)
            {
                var current = _byId;
                if (current == null) return; // chưa sẵn sàng thì lần nạp đầy đủ sẽ lấy được
                var next = new Dictionary<string, NotificationConfig>(current, StringComparer.Ordinal)
                {
                    [config.Id] = config
                };
                _byId = next;
            }
        }

        /// <summary>Gỡ một config vừa bị xóa.</summary>
        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_gate)
            {
                var current = _byId;
                if (current == null || !current.ContainsKey(id)) return;
                var next = new Dictionary<string, NotificationConfig>(current, StringComparer.Ordinal);
                next.Remove(id);
                _byId = next;
            }
        }
    }
}
