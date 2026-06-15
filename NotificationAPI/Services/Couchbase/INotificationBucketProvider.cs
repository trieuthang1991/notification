using Couchbase;
using Couchbase.Core;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Interface cung cấp bucket thông báo
    /// </summary>
    public interface INotificationBucketProvider
    {
        /// <summary>
        /// Lấy bucket thông báo
        /// </summary>
        /// <returns>Bucket thông báo</returns>
        IBucket GetBucket();
    }
}
