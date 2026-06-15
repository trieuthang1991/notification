using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationAPI.Config;
using System.Threading;
using System.Threading.Tasks;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Dịch vụ xử lý đóng kết nối Couchbase khi ứng dụng kết thúc
    /// </summary>
    public class CouchbaseCleanupService : IHostedService
    {
        private readonly ILogger<CouchbaseCleanupService> _logger;
        private readonly IOptions<CouchbaseConfig> _config;

        public CouchbaseCleanupService(ILogger<CouchbaseCleanupService> logger, IOptions<CouchbaseConfig> config)
        {
            _logger = logger;
            _config = config;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dịch vụ CouchbaseCleanupService đã khởi động");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Đóng kết nối Couchbase...");

            try
            {
                // Lấy instance của CouchbaseConnectionManager và đóng kết nối
                var connectionManager = CouchbaseConnectionManager.GetInstance(_config.Value, _logger);
                connectionManager.CloseConnection();

                _logger.LogInformation("Đã đóng kết nối Couchbase thành công");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đóng kết nối Couchbase: {Message}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
