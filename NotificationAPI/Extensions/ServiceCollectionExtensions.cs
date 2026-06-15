using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationAPI.Config;
using NotificationAPI.Services.Couchbase;
using StackExchange.Redis;
using System.Linq;

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
            // Đọc Redis config, register IConnectionMultiplexer (lazy connect, singleton).
            // Nếu Enabled=false hoặc ConnectionString rỗng → register null, NotificationCB sẽ bypass cache.
            var redisOptions = config.GetSection("Redis").Get<RedisConfig>() ?? new RedisConfig();
            services.AddSingleton(redisOptions);

            services.AddSingleton<IConnectionMultiplexer?>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<NotificationCB>>();
                if (!redisOptions.Enabled || string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
                {
                    logger.LogInformation("Redis cache disabled (Enabled={Enabled}, ConnectionString empty={Empty})",
                        redisOptions.Enabled, string.IsNullOrWhiteSpace(redisOptions.ConnectionString));
                    return null;
                }

                try
                {
                    var configOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
                    configOptions.AbortOnConnectFail = false;       // không crash app nếu Redis down lúc startup
                    configOptions.ConnectTimeout = 2000;            // 2s timeout
                    configOptions.SyncTimeout = 1000;               // 1s cho từng operation
                    var mux = ConnectionMultiplexer.Connect(configOptions);
                    logger.LogInformation("Redis connected: {Endpoints}", string.Join(",", mux.GetEndPoints().Select(e => e.ToString())));
                    return mux;
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Không thể kết nối Redis ({Connection}). App sẽ bypass cache.",
                        redisOptions.ConnectionString);
                    return null;
                }
            });

            // Đăng ký NotificationCB
            services.AddSingleton<INotificationCB>(sp =>
            {
                var couchbaseOptions = config.GetSection("Couchbase").Get<CouchbaseConfig>();
                var logger = sp.GetRequiredService<ILogger<NotificationCB>>();
                var redis = sp.GetService<IConnectionMultiplexer?>();
                return new NotificationCB(
                    Microsoft.Extensions.Options.Options.Create(couchbaseOptions),
                    logger,
                    redis,
                    redisOptions);
            });

            // Đăng ký dịch vụ dọn dẹp Couchbase
            services.AddHostedService<CouchbaseCleanupService>();

            // Đăng ký các dịch vụ cache khác (giữ nguyên)
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddResponseCaching();

            return services;
        }
    }
}
