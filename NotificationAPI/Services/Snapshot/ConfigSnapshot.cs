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

        /// <param name="clock">Trả về unix time (giây) hiện tại; truyền vào để test được.</param>
        public ConfigSnapshot(Func<long> clock)
        {
            _clock = clock;
        }

        /// <summary>Đã nạp đầy đủ ít nhất một lần.</summary>
        public bool IsReady => _byId != null;

        /// <summary>Các config hiện có (rỗng nếu chưa sẵn sàng).</summary>
        public IReadOnlyCollection<NotificationConfig> Items
            => (IReadOnlyCollection<NotificationConfig>?)_byId?.Values ?? Array.Empty<NotificationConfig>();

        /// <summary>Max last_updated trong snapshot, dùng làm mốc cho lần nạp delta.</summary>
        public long WatermarkLastUpdated
        {
            get
            {
                long max = 0;
                foreach (var c in Items) if (c.LastUpdated > max) max = c.LastUpdated;
                return max;
            }
        }

        /// <summary>Số giây kể từ lần nạp đầy đủ gần nhất (0 nếu chưa nạp).</summary>
        public long FullLoadAgeSeconds => _lastFullLoadUnix == 0 ? 0 : _clock() - _lastFullLoadUnix;

        /// <summary>Đã sẵn sàng và lần refresh thành công cuối cùng chưa quá maxStaleSeconds.</summary>
        public bool IsFresh(int maxStaleSeconds)
            => IsReady && (_clock() - Interlocked.Read(ref _lastRefreshUnix)) <= maxStaleSeconds;

        /// <summary>Thay toàn bộ snapshot bằng kết quả nạp đầy đủ.</summary>
        public void ReplaceAll(IEnumerable<NotificationConfig> all)
        {
            var next = new Dictionary<string, NotificationConfig>(StringComparer.Ordinal);
            foreach (var c in all)
            {
                if (string.IsNullOrEmpty(c.Id)) continue;
                next[c.Id] = c;
            }
            lock (_gate)
            {
                _byId = next;
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
                foreach (var c in delta)
                {
                    if (string.IsNullOrEmpty(c.Id)) continue;
                    if (next.TryGetValue(c.Id, out var existing) && existing.LastUpdated > c.LastUpdated) continue;
                    next[c.Id] = c;
                }
                _byId = next;
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
