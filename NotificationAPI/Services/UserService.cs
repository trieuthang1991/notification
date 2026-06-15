using NotificationAPI.Models;
using Newtonsoft.Json;

namespace NotificationAPI.Services
{
    public interface IUserService
    {
        Task<bool> ValidateUserAsync(string username, string password);
        Task<UserInfo?> GetUserByUsernameAsync(string username);
        Task<bool> CreateUserAsync(UserInfo user, string password);
        Task<bool> UpdateUserAsync(UserInfo user);
        Task<List<UserInfo>> GetAllUsersAsync();
    }

    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly string _userStorePath;
        private readonly IConfiguration _configuration;

        public UserService(ILogger<UserService> logger, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _logger = logger;
            _configuration = configuration;

            // Đường dẫn đến file JSON lưu thông tin người dùng
            _userStorePath = Path.Combine(environment.ContentRootPath, "Data", "users.json");

            // Đảm bảo thư mục Data tồn tại
            var dataDirectory = Path.Combine(environment.ContentRootPath, "Data");
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // Đảm bảo file users.json tồn tại
            if (!File.Exists(_userStorePath))
            {
                // Tạo file với một admin mặc định
                var adminUser = new UserInfo
                {
                    Username = "admin",
                    PasswordHash = "admin123", // Lưu trực tiếp mật khẩu không mã hóa
                    FullName = "Administrator",
                    Email = "admin@example.com",
                    Role = "Admin",
                    IsActive = true
                };

                var userStore = new UserStore
                {
                    Users = new List<UserInfo> { adminUser }
                };

                File.WriteAllText(_userStorePath, JsonConvert.SerializeObject(userStore, Formatting.Indented));
            }
        }

        /// <summary>
        /// Xác thực người dùng dựa trên tên đăng nhập và mật khẩu
        /// </summary>
        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            try
            {
                var user = await GetUserByUsernameAsync(username);
                if (user == null || !user.IsActive)
                {
                    return false;
                }

                // Kiểm tra mật khẩu trực tiếp
                if (password == user.PasswordHash)
                {
                    // Cập nhật thời gian đăng nhập cuối
                    user.LastLogin = XMUtility.XUtility.UnixTime(DateTime.Now);
                    await UpdateUserAsync(user);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực người dùng: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Lấy thông tin người dùng theo tên đăng nhập
        /// </summary>
        public async Task<UserInfo?> GetUserByUsernameAsync(string username)
        {
            try
            {
                var userStore = await LoadUserStoreAsync();
                return userStore.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin người dùng: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Tạo người dùng mới
        /// </summary>
        public async Task<bool> CreateUserAsync(UserInfo user, string password)
        {
            try
            {
                var userStore = await LoadUserStoreAsync();

                // Kiểm tra xem tên đăng nhập đã tồn tại chưa
                if (userStore.Users.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                // Lưu mật khẩu trực tiếp
                user.PasswordHash = password;

                // Thêm người dùng mới
                userStore.Users.Add(user);

                // Lưu vào file
                await SaveUserStoreAsync(userStore);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo người dùng mới: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Cập nhật thông tin người dùng
        /// </summary>
        public async Task<bool> UpdateUserAsync(UserInfo user)
        {
            try
            {
                var userStore = await LoadUserStoreAsync();

                // Tìm người dùng cần cập nhật
                var existingUser = userStore.Users.FirstOrDefault(u => u.Id == user.Id);
                if (existingUser == null)
                {
                    return false;
                }

                // Cập nhật thông tin
                var index = userStore.Users.IndexOf(existingUser);
                userStore.Users[index] = user;

                // Lưu vào file
                await SaveUserStoreAsync(userStore);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật thông tin người dùng: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả người dùng
        /// </summary>
        public async Task<List<UserInfo>> GetAllUsersAsync()
        {
            try
            {
                var userStore = await LoadUserStoreAsync();
                return userStore.Users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách người dùng: {Message}", ex.Message);
                return new List<UserInfo>();
            }
        }

        /// <summary>
        /// Đọc dữ liệu từ file JSON
        /// </summary>
        private async Task<UserStore> LoadUserStoreAsync()
        {
            try
            {
                if (!File.Exists(_userStorePath))
                {
                    return new UserStore();
                }

                var json = await File.ReadAllTextAsync(_userStorePath);
                return JsonConvert.DeserializeObject<UserStore>(json) ?? new UserStore();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc file users.json: {Message}", ex.Message);
                return new UserStore();
            }
        }

        /// <summary>
        /// Lưu dữ liệu vào file JSON
        /// </summary>
        private async Task SaveUserStoreAsync(UserStore userStore)
        {
            try
            {
                var json = JsonConvert.SerializeObject(userStore, Formatting.Indented);
                await File.WriteAllTextAsync(_userStorePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu file users.json: {Message}", ex.Message);
                throw;
            }
        }

        // Các phương thức mã hóa mật khẩu đã được loại bỏ để đơn giản hóa
    }
}
