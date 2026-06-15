using Couchbase;
using Couchbase.Core;
using Microsoft.Extensions.Options;
using NotificationAPI.Config;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Lớp cung cấp bucket thông báo
    /// </summary>
    public class NotificationBucketProvider : INotificationBucketProvider
    {
        private readonly IBucket _bucket;

        public NotificationBucketProvider(IOptions<CouchbaseConfig> config)
        {
            // Sử dụng CouchbaseConnectionManager để lấy bucket
            var connectionManager = CouchbaseConnectionManager.GetInstance(config.Value);
            _bucket = connectionManager.GetBucket();
        }

        /// <summary>
        /// Lấy bucket thông báo
        /// </summary>
        /// <returns>Bucket thông báo</returns>
        public IBucket GetBucket()
        {
            return _bucket;
        }
    }
}
