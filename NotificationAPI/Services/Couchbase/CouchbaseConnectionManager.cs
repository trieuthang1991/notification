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

        private CouchbaseConnectionManager(CouchbaseConfig config)
        {
            _config = config;

            try
            {
                _logger?.LogInformation("Bắt đầu kết nối đến Couchbase với các server: {Servers}", string.Join(",", _config.Servers));
              
                ClientConfiguration configCouchbase = new ClientConfiguration
                {
                    Servers = _config.Servers.Select(a => new Uri(a)).ToList(),
                    Serializer = () => new DefaultSerializer(new JsonSerializerSettings(), new JsonSerializerSettings())
                };
                Cluster cluster = new Cluster(configCouchbase);

                PasswordAuthenticator authenticator = new PasswordAuthenticator(_config.Username, _config.Password);
                cluster.Authenticate(authenticator);
                string _bucketName = _config.BucketName;
                _bucket = cluster.OpenBucket(_bucketName);

                _logger?.LogInformation("Kết nối Couchbase thành công");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Lỗi khi kết nối đến Couchbase: {Message}", ex.Message);
                throw new Exception($"Lỗi khi kết nối đến Couchbase: {ex.Message}", ex);
            }
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
