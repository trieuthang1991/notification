using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationAPI.Config;
using NotificationAPI.Services.Couchbase;

namespace NotificationAPI.Services.Snapshot
{
    /// <summary>
    /// Chạy nền: nạp snapshot ngay khi khởi động rồi làm mới theo chu kỳ.
    /// Lỗi một lần nạp chỉ được ghi log; snapshot cũ vẫn được dùng đến hết MaxStaleSeconds.
    /// </summary>
    public class ConfigSnapshotRefresher : BackgroundService
    {
        private readonly INotificationCB _cb;
        private readonly ConfigSnapshotConfig _cfg;
        private readonly ILogger<ConfigSnapshotRefresher> _logger;

        public ConfigSnapshotRefresher(
            INotificationCB cb,
            ConfigSnapshotConfig cfg,
            ILogger<ConfigSnapshotRefresher> logger)
        {
            _cb = cb;
            _cfg = cfg;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_cfg.Enabled)
            {
                _logger.LogInformation("ConfigSnapshot tắt (ConfigSnapshot:Enabled=false) → mọi request đi N1QL");
                return;
            }

            _logger.LogInformation(
                "ConfigSnapshot bật | refresh {Refresh}s, full {Full}s, stale tối đa {Stale}s",
                _cfg.RefreshSeconds, _cfg.FullReloadSeconds, _cfg.MaxStaleSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _cb.RefreshConfigSnapshotAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Làm mới snapshot config lỗi (giữ snapshot cũ): {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _cfg.RefreshSeconds)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
