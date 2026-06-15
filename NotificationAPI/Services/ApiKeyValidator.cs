using Microsoft.Extensions.Configuration;

namespace NotificationAPI.Services
{
    /// <summary>
    /// Dịch vụ xác thực API key
    /// </summary>
    public class ApiKeyValidator : IApiKeyValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyValidator> _logger;

        public ApiKeyValidator(IConfiguration configuration, ILogger<ApiKeyValidator> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Xác thực API key
        /// </summary>
        /// <param name="apiKey">API key cần xác thực</param>
        /// <returns>True nếu API key hợp lệ, ngược lại là False</returns>
        public bool IsValid(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("API key is null or empty");
                return false;
            }

            // Lấy danh sách API key từ cấu hình
            var validApiKeys = _configuration.GetSection("ApiKeys").Get<List<string>>();
            
            if (validApiKeys == null || !validApiKeys.Any())
            {
                _logger.LogWarning("No valid API keys configured");
                return false;
            }

            // Kiểm tra xem API key có trong danh sách không
            var isValid = validApiKeys.Contains(apiKey);
            
            if (!isValid)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", apiKey);
            }
            
            return isValid;
        }
    }

    /// <summary>
    /// Interface cho dịch vụ xác thực API key
    /// </summary>
    public interface IApiKeyValidator
    {
        bool IsValid(string apiKey);
    }
}
