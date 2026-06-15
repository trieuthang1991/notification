using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationAPI.Config;
using NotificationAPI.Services.Couchbase;

namespace NotificationAPI.Extensions
{
    /// <summary>
    /// Extension methods cho IServiceCollection
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký cấu hình cache và Couchbase
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="config">IConfiguration</param>
        /// <returns>IServiceCollection</returns>
        public static IServiceCollection AddCacheConfiguration(this IServiceCollection services, IConfiguration config)
        {
            // Đăng ký memory cache TRƯỚC NotificationCB để inject vào constructor.
            // SizeLimit chặn cache phình to nếu cardinality key bùng nổ (mỗi entry SetSize(1) → tối đa 10k entry).
            services.AddMemoryCache(opts => opts.SizeLimit = 10_000);
            services.AddDistributedMemoryCache();
            services.AddResponseCaching();

            // Đăng ký NotificationCB
            services.AddSingleton<INotificationCB>(sp =>
            {
                var couchbaseOptions = config.GetSection("Couchbase").Get<CouchbaseConfig>();
                var logger = sp.GetRequiredService<ILogger<NotificationCB>>();
                var memoryCache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                return new NotificationCB(Microsoft.Extensions.Options.Options.Create(couchbaseOptions), logger, memoryCache);
            });

            // Đăng ký dịch vụ dọn dẹp Couchbase
            services.AddHostedService<CouchbaseCleanupService>();

            return services;
        }
    }
}
