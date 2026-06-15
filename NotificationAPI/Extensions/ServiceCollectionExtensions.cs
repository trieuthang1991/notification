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
            // Đăng ký NotificationCB
            services.AddSingleton<INotificationCB>(sp =>
            {
                var couchbaseOptions = config.GetSection("Couchbase").Get<CouchbaseConfig>();
                var logger = sp.GetRequiredService<ILogger<NotificationCB>>();
                return new NotificationCB(Microsoft.Extensions.Options.Options.Create(couchbaseOptions), logger);
            });

            // Đăng ký dịch vụ dọn dẹp Couchbase
            services.AddHostedService<CouchbaseCleanupService>();

            // Đăng ký các dịch vụ cache
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddResponseCaching();

            return services;
        }
    }
}
