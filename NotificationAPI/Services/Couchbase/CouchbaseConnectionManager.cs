using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;

using Couchbase.Core.Serialization;


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NotificationAPI.Config;
using System;
using System.Threading;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Lớp quản lý kết nối Couchbase, đảm bảo chỉ có một kết nối duy nhất
    /// </summary>
    public class CouchbaseConnectionManager
    {
        private static CouchbaseConnectionManager _instance;
        private static readonly object _lock = new object();


        private IBucket _bucket;
     
        private readonly CouchbaseConfig _config;
        private static ILogger _logger;

        // Retry config cho bootstrap. Tổng wait tối đa: 2+4+8+16 = 30s trước khi bỏ cuộc.
        // SDK 2.7.8 không retry mặc định → 1 network blip = app crash + restart loop.
        // Hôm 2026-06-15 prod restart 4 lần (01:21, 06:07, 10:57, 15:30) đều cùng pattern này.
        private const int BootstrapMaxAttempts = 5;

        private CouchbaseConnectionManager(CouchbaseConfig config)
        {
            _config = config;

            Exception lastError = null;
            for (int attempt = 1; attempt <= BootstrapMaxAttempts; attempt++)
            {
                Cluster cluster = null;
                try
                {
                    _logger?.LogInformation(
                        "Bootstrap Couchbase attempt {Attempt}/{Max} với servers: {Servers}",
                        attempt, BootstrapMaxAttempts, string.Join(",", _config.Servers));

                    ClientConfiguration configCouchbase = new ClientConfiguration
                    {
                        Servers = _config.Servers.Select(a => new Uri(a)).ToList(),
                        Serializer = () => new DefaultSerializer(new JsonSerializerSettings(), new JsonSerializerSettings())
                    };
                    cluster = new Cluster(configCouchbase);

                    PasswordAuthenticator authenticator = new PasswordAuthenticator(_config.Username, _config.Password);
                    cluster.Authenticate(authenticator);
                    _bucket = cluster.OpenBucket(_config.BucketName);

                    _logger?.LogInformation("Kết nối Couchbase thành công (attempt {Attempt})", attempt);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger?.LogWarning(
                        "Bootstrap attempt {Attempt}/{Max} thất bại: {Error}",
                        attempt, BootstrapMaxAttempts, ex.Message);

                    // Dispose cluster đã tạo trong lần thất bại để tránh leak socket/handle
                    try { cluster?.Dispose(); } catch { /* best effort */ }

                    if (attempt < BootstrapMaxAttempts)
                    {
                        int delaySec = (int)Math.Pow(2, attempt); // 2s, 4s, 8s, 16s
                        _logger?.LogInformation("Chờ {Delay}s trước khi thử lại bootstrap...", delaySec);
                        Thread.Sleep(TimeSpan.FromSeconds(delaySec));
                    }
                }
            }

            _logger?.LogError(lastError,
                "Tất cả {Max} lần bootstrap Couchbase đều thất bại — bỏ cuộc",
                BootstrapMaxAttempts);
            throw new Exception(
                $"Lỗi khi kết nối đến Couchbase sau {BootstrapMaxAttempts} lần thử: {lastError?.Message}",
                lastError);
        }
       


        /// <summary>
        /// Lấy instance của CouchbaseConnectionManager
        /// </summary>
        /// <param name="config">Cấu hình Couchbase</param>
        /// <param name="logger">Logger (tùy chọn)</param>
        /// <returns>Instance của CouchbaseConnectionManager</returns>
        public static CouchbaseConnectionManager GetInstance(CouchbaseConfig config, ILogger logger = null)
        {
            // Gán logger nếu có
            if (logger != null)
            {
                _logger = logger;
            }

            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _logger?.LogInformation("Tạo instance mới của CouchbaseConnectionManager");
                        _instance = new CouchbaseConnectionManager(config);
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Lấy cluster Couchbase
        /// </summary>
        /// <returns>Cluster Couchbase</returns>
        public Cluster GetCluster()
        {
            return null; // SDK 2.x không có trả về cluster
        }

        /// <summary>
        /// Lấy bucket Couchbase
        /// </summary>
        /// <returns>Bucket Couchbase</returns>
        public IBucket GetBucket()
        {
            return _bucket;
        }

        /// <summary>
        /// Đóng kết nối Couchbase
        /// </summary>
        public void CloseConnection()
        {
            try
            {
                _logger?.LogInformation("Bắt đầu đóng kết nối Couchbase...");

                // Đóng bucket
                if (_bucket != null)
                {
                    _logger?.LogInformation("Đóng Couchbase bucket...");
                    _bucket.Dispose();
                    _logger?.LogInformation("Đóng Couchbase bucket thành công");
                }

                // Reset instance
                _instance = null;
                _logger?.LogInformation("Đóng kết nối Couchbase thành công");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Lỗi khi đóng kết nối Couchbase: {Message}", ex.Message);
                throw new Exception($"Lỗi khi đóng kết nối Couchbase: {ex.Message}", ex);
            }
        }
    }
}
