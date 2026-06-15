using Couchbase;
using Couchbase.Core;
using Couchbase.N1QL;

using Couchbase.Search;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NotificationAPI.Config;
using NotificationAPI.DTO.Filter;
using NotificationAPI.Enums;
using NotificationAPI.Models;
using NotificationAPI.Utils;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using static OfficeOpenXml.ExcelErrorValue;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Lớp cung cấp các phương thức thao tác với thông báo trong Couchbase
    /// </summary>
    public class NotificationCB : INotificationCB
    {
        private readonly IBucket _bucket;
        private readonly string _preKey;
        private readonly CouchbaseConfig _config;
        private readonly ILogger<NotificationCB> _logger;

        // Redis (nullable — nếu null thì bypass cache layer)
        private readonly IConnectionMultiplexer? _redis;
        private readonly RedisConfig _redisConfig;

        /// <summary>
        /// Cấu hình JsonSerializerSettings để tránh việc trùng lặp giá trị mặc định
        /// </summary>
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Populate
        };

        public NotificationCB(
            IOptions<CouchbaseConfig> config,
            ILogger<NotificationCB> logger,
            IConnectionMultiplexer? redis,
            RedisConfig redisConfig)
        {
            _logger = logger;
            _redis = redis;
            _redisConfig = redisConfig ?? new RedisConfig();

            _logger.LogInformation("Khởi tạo NotificationCB...");

            // Sử dụng CouchbaseConnectionManager để lấy bucket
            var connectionManager = CouchbaseConnectionManager.GetInstance(config.Value, logger);
            _bucket = connectionManager.GetBucket();
            _preKey = config.Value.PreKey;
            _config = config.Value;
            _logger.LogInformation(
                "Khởi tạo NotificationCB thành công (Redis cache: {RedisState})",
                _redis != null && _redis.IsConnected ? "ENABLED" : "DISABLED");
        }

        // ─────────────────────────────────────────────────────────────────
        // Redis cache layer: per (domain, device, userId, triggerActions)
        // ─────────────────────────────────────────────────────────────────

        private bool CacheEnabled => _redis != null && _redisConfig.Enabled && _redis.IsConnected;

        /// <summary>
        /// Hash key cho 1 domain. Mọi (device, userId, triggers) entry cho domain này
        /// đều nằm trong cùng 1 hash → DEL hash = invalidate hết.
        /// </summary>
        private string BuildDomainHashKey(string? domain)
            => $"{_redisConfig.KeyPrefix}dom:{domain ?? "*"}";

        /// <summary>
        /// Field trong hash, encode (device, userId, triggers) ổn định.
        /// triggers được sort + distinct để cùng tập trigger nhưng khác thứ tự cho chung 1 key.
        /// </summary>
        private static string BuildHashField(DeviceType device, string? userId, List<string>? triggerActions)
        {
            var ta = (triggerActions == null || triggerActions.Count == 0)
                ? Utils.Common.All
                : string.Join(",", triggerActions.Distinct().OrderBy(x => x, StringComparer.Ordinal));
            return $"{(int)device}:{userId ?? "*"}:{ta}";
        }

        /// <summary>
        /// Đọc maxLastUpdated từ Redis hash. Return null nếu cache miss hoặc Redis down.
        /// </summary>
        private async Task<long?> TryGetCachedMaxLuAsync(string? domain, DeviceType device, string? userId, List<string>? triggerActions)
        {
            if (!CacheEnabled) return null;
            try
            {
                var db = _redis!.GetDatabase();
                var val = await db.HashGetAsync(BuildDomainHashKey(domain), BuildHashField(device, userId, triggerActions));
                if (val.HasValue && long.TryParse(val.ToString(), out var lu)) return lu;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis HashGet lỗi (graceful degrade)");
                return null;
            }
        }

        /// <summary>
        /// Ghi maxLastUpdated vào Redis hash + set TTL trên cả hash.
        /// Lỗi Redis được swallow để không ảnh hưởng request.
        /// </summary>
        private async Task SetCachedMaxLuAsync(string? domain, DeviceType device, string? userId, List<string>? triggerActions, long maxLu)
        {
            if (!CacheEnabled) return;
            try
            {
                var db = _redis!.GetDatabase();
                var hashKey = BuildDomainHashKey(domain);
                var field = BuildHashField(device, userId, triggerActions);
                var batch = db.CreateBatch();
                _ = batch.HashSetAsync(hashKey, field, maxLu);
                _ = batch.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(_redisConfig.CacheTtlSeconds));
                batch.Execute();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis HashSet lỗi (graceful degrade)");
            }
        }

        /// <summary>
        /// Xoá toàn bộ cache cho mọi domain notification match. Gọi sau khi
        /// UpsertAsync/UpdateStatus/Delete thành công.
        /// </summary>
        private void InvalidateMaxLuFor(NotificationConfig notification)
        {
            if (!CacheEnabled || notification == null) return;
            try
            {
                var db = _redis!.GetDatabase();
                var domains = notification.Domains;
                if (domains == null || domains.Count == 0 || domains.Contains(Utils.Common.All))
                {
                    // Notification có domain="all" hoặc không có domain rõ → ảnh hưởng mọi domain.
                    // Pattern unlink: dùng SCAN + UNLINK để không block server.
                    var endpoints = _redis!.GetEndPoints();
                    foreach (var ep in endpoints)
                    {
                        var server = _redis.GetServer(ep);
                        if (server.IsConnected && !server.IsReplica)
                        {
                            foreach (var key in server.Keys(pattern: $"{_redisConfig.KeyPrefix}dom:*", pageSize: 200))
                                _ = db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
                        }
                    }
                    return;
                }

                foreach (var d in domains.Distinct())
                    _ = db.KeyDeleteAsync(BuildDomainHashKey(d), CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis invalidate lỗi (graceful degrade)");
            }
        }

        /// <summary>
        /// Tạo key đầy đủ với tiền tố
        /// </summary>
        /// <param name="id">ID gốc</param>
        /// <returns>Key đầy đủ</returns>
        private string CreateKey(string id)
        {
            return $"{_preKey}{id}";
        }

        /// <summary>
        /// Lấy ID gốc từ key đầy đủ
        /// </summary>
        /// <param name="key">Key đầy đủ</param>
        /// <returns>ID gốc</returns>
        private string GetOriginalId(string key)
        {
            if (key.StartsWith(_preKey))
            {
                return key.Substring(_preKey.Length);
            }
            return key;
        }

        public string Fields(Expression<Func<NotificationConfig, object>> field)
        {
            return JsonPropertyHelper.JsonName(field);
        }
        public string FieldsSts(Expression<Func<NotificationStatus, object>> field)
        {
            return JsonPropertyHelper.JsonName(field);
        }


        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <returns>Thông báo tìm thấy hoặc null</returns>
        public async Task<NotificationConfig> GetByIdAsync(string id)
        {
            try
            {
                var result = _bucket.Get<NotificationConfig>(CreateKey(id));
                if (result.Success)
                {
                    return result.Value;
                }
                else if (result == null)
                {
                    _logger.LogWarning("Không tìm thấy thông báo với ID: {Id}", id);
                    return null;
                }
                else
                {
                    throw new Exception($"Lỗi khi lấy thông báo: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <returns>Thông báo tìm thấy hoặc null</returns>
        public async Task<List<NotificationConfig>> GetByUserIdAsync(string userId)
        {
            try
            {
                string idUserField = Fields(n => n.UserId);
                string lastUpdatedField = Fields(n => n.LastUpdated);
                string status = Fields(n => n.Status);
                string query = $@"
                    SELECT n.*
                        FROM `{_bucket.Name}` n
                        WHERE n.{idUserField} == $userId AND n.{status} == $sts AND  n.{lastUpdatedField} >= 0 LIMIT 100 ";

                var parameters = new[]
                {
                    new KeyValuePair<string, object>("$userId", userId),
                    new KeyValuePair<string, object>("$sts", (int)Enums.StatusNotification.Active),
                };

                var results = await ExecuteQueryAsync<NotificationConfig>(query, parameters);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo: {Message}", ex.Message);
                throw;
            }
        }


        /// <summary>
        /// Lấy thông báo theo danh sách ID sử dụng N1QL
        /// </summary>
        /// <param name="ids">Danh sách ID cần lấy</param>
        /// <returns>Danh sách các thông báo tương ứng</returns>
        public async Task<List<NotificationConfig>> GetContentByIdsAsync(List<string> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return new List<NotificationConfig>();
                }

                // Lấy trường Id của NotificationConfig
                string idField = Fields(n => n.Id);

                // Chọn các trường cần lấy
                string fieldsGet = JsonPropertyHelper.CreateN1qlSelectFields<NotificationConfig>(n => n.Id, n => n.Title, n => n.Content, n => n.Status);

                // Tạo truy vấn N1QL sử dụng IN để lấy nhiều document cùng lúc
                string query = $@"
                    SELECT {fieldsGet}
                    FROM `{_bucket.Name}` n
                    WHERE n.{idField} IN $ids AND n.{Fields(n => n.LastUpdated)} >= 0";

                var parameters = new[]
                {
                    new KeyValuePair<string, object>("$ids", ids),

                };

                var results = await ExecuteQueryAsync<dynamic>(query, parameters);

                var notifications = new List<NotificationConfig>();
                foreach (var result in results)
                {
                    try
                    {
                        var notification = Newtonsoft.Json.JsonConvert.DeserializeObject<NotificationConfig>(result.ToString(), _jsonSettings);
                        if (notification != null)
                        {
                            notifications.Add(notification);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi chuyển đổi thông báo: {Message}", ex.Message);
                    }
                }

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông báo theo danh sách ID: {Message}", ex.Message);
                throw;
            }
        }


        /// <summary>
        /// Lưu thông báo mới hoặc cập nhật thông báo đã tồn tại
        /// </summary>
        /// <param name="notification">Thông báo cần lưu</param>
        /// <param name="id">ID của thông báo (nếu null, sẽ tạo ID mới)</param>
        /// <returns>ID của thông báo đã lưu</returns>
        public async Task<string> UpsertAsync(NotificationConfig notification, string id = null)
        {
            try
            {
                // Tạo ID mới nếu không có
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                }
                notification.Content = XMUtility.XUtility.ChuanHoaHtml(notification.Content);
                if (notification.ShowTypes.Contains(ShowTypeNotification.Link))
                    notification.Content = XMUtility.XUtility.StripHTML(notification.Content);
                // Gán ID cho notification
                notification.Id = id;

                TimeSpan time = Utils.Common.TimeUntil(XMUtility.XUtility.UnixTime(notification.EndDate));
                _logger.LogInformation("Thời gian hết hạn của thông báo: {Time}", time.TotalDays);
                // Thêm document vào bucket
                var result = await _bucket.UpsertAsync(CreateKey(id), notification, time);

                if (!result.Success)
                {
                    throw new Exception($"Lỗi khi lưu thông báo: {result.Message}");
                }

                _logger.LogInformation("Đã lưu thông báo với ID: {Id}", id);

                // Invalidate Redis cache để user kế tiếp thấy notification mới ngay.
                InvalidateMaxLuFor(notification);

                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu thông báo: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Xóa thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo cần xóa</param>
        /// <returns>True nếu xóa thành công, False nếu không tìm thấy</returns>
        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                // Load doc TRƯỚC khi xoá để biết Domains cho cache invalidate.
                var existing = _bucket.Get<NotificationConfig>(CreateKey(id));
                var result = await _bucket.RemoveAsync(CreateKey(id));
                if (result.Success)
                {
                    _logger.LogInformation("Đã xóa thông báo với ID: {Id}", id);
                    if (existing.Success) InvalidateMaxLuFor(existing.Value);
                    return true;
                }
                else if (result == null)
                {
                    _logger.LogWarning("Không tìm thấy thông báo để xóa với ID: {Id}", id);
                    return false;
                }
                else
                {
                    throw new Exception($"Lỗi khi xóa thông báo: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa thông báo: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Thực thi truy vấn N1QL và trả về kết quả
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu kết quả</typeparam>
        /// <param name="query">Câu truy vấn N1QL</param>
        /// <param name="parameters">Tham số truy vấn</param>
        /// <returns>Danh sách kết quả</returns>
        private async Task<List<T>> ExecuteQueryAsync<T>(string query, params KeyValuePair<string, object>[] parameters)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<T>();

            // Khởi tạo logger (giả sử bạn đã inject ILogger vào class)
            // Ví dụ: private readonly ILogger<YourClass> _logger;
            // Nếu chưa có, bạn cần inject ILogger vào constructor của class chứa phương thức này

            IQueryRequest req = new QueryRequest(query);
            if (parameters != null && parameters.Any())
            {
                req.AddNamedParameter(parameters);
            }


            // Đo thời gian thực thi query
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _bucket.QueryAsync<T>(req);
                stopwatch.Stop();

                // Ngưỡng thời gian (ms) để coi query là chậm, ví dụ 500ms
                const int slowQueryThresholdMs = 500;
                if (stopwatch.ElapsedMilliseconds > slowQueryThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow query detected: Query={Query}, Parameters={Parameters}, ExecutionTimeMs={ExecutionTimeMs}",
                        query,
                        Newtonsoft.Json.JsonConvert.SerializeObject(parameters),
                        stopwatch.ElapsedMilliseconds
                    );
                }

                return result.Success ? result.Rows : new List<T>();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Query execution failed: Query={Query}, Parameters={Parameters}, ExecutionTimeMs={ExecutionTimeMs}",
                    query,
                    Newtonsoft.Json.JsonConvert.SerializeObject(parameters),
                    stopwatch.ElapsedMilliseconds
                );

                return new List<T>();
            }
        }


        private async Task<List<T>> ExecuteQueryViaApiAsync<T>(string query, params KeyValuePair<string, object>[] parameters)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<T>();

            // Giả sử bạn đã inject ILogger<YourClass> _logger;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Thay thế các tham số vào câu query


                string parameterizedQuery = query;
                foreach (var parameter in parameters)
                {
                    if (parameter.Value != null)
                    {
                        if (parameter.Value is IEnumerable<object> enumerable && parameter.Value.GetType() != typeof(string))
                        {
                            var values = enumerable.Select(v => v?.ToString()).Where(v => v != null);
                            if (values != null && values.Any())
                            {
                                var formattedValue = $"[{string.Join(",", values.Select(v => $"\"{v}\""))}]";
                                parameterizedQuery = parameterizedQuery.Replace(parameter.Key, formattedValue); // Thêm dấu nháy kép cho chuỗi
                            }

                        }
                        else if (parameter.Value.GetType() == typeof(string))
                        {
                            parameterizedQuery = parameterizedQuery.Replace(parameter.Key, $"\"{parameter.Value}\""); // Thêm dấu nháy kép cho chuỗi
                        }
                        else
                        {
                            parameterizedQuery = parameterizedQuery.Replace(parameter.Key, parameter.Value.ToString());
                        }
                    }
                }

                using var httpClient = new HttpClient();
                string url = $"{_config.Servers.FirstOrDefault()?.Replace("8091", "8093")}/query/service";
                string credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) }
                };

                // Tạo form data với câu query đã thay thế tham số
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("statement", parameterizedQuery)
                };

                request.Content = new FormUrlEncodedContent(formData);

                // Gửi request và đo thời gian
                var response = await httpClient.SendAsync(request);
                stopwatch.Stop();

                // Log query chậm (ngưỡng ví dụ: 500ms)
                const int slowQueryThresholdMs = 500;
                if (stopwatch.ElapsedMilliseconds > slowQueryThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow query detected: Query={Query}, Parameters={Parameters}, ExecutionTimeMs={ExecutionTimeMs}, Url={Url}",
                        parameterizedQuery,
                        Newtonsoft.Json.JsonConvert.SerializeObject(parameters),
                        stopwatch.ElapsedMilliseconds,
                        url
                    );
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                var resultList = new List<T>();

                if (json.RootElement.TryGetProperty("results", out var resultsElement))
                {
                    foreach (var item in resultsElement.EnumerateArray())
                    {
                        var model = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(item.GetRawText());
                        if (model != null)
                        {
                            resultList.Add(model);
                        }
                    }
                }

                return resultList;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Query execution failed: Query={Query}, Parameters={Parameters}, ExecutionTimeMs={ExecutionTimeMs}, Url={Url}",
                    query,
                     Newtonsoft.Json.JsonConvert.SerializeObject(parameters),
                    stopwatch.ElapsedMilliseconds,
                    $"{_config.Servers.FirstOrDefault()?.Replace("8091", "8093")}/query/service"
                );
            }
            return new List<T>();
        }

        /// <summary>
        /// Lấy tất cả thông báo đang hoạt động
        /// </summary>
        /// <returns>Danh sách các thông báo đang hoạt động</returns>
        public async Task<NotificationConfigVM> GetActiveNotificationsAsync(SearchNotification filter)
        {

            var result = new NotificationConfigVM();

            if (filter == null)
                filter = new SearchNotification();
            if (String.IsNullOrEmpty(filter.UserId))
                filter.UserId = Utils.Common.All;

            var isGetAll = await HasRecentUpdatesInDomainAsync(filter.Domain, filter.Device, filter.UserId, filter.TriggerAction, filter.LastUpdated);

            if (isGetAll)
            {
                result.IsSetData = true;
                var notifications = await GetByDomainActiveAsync(filter.Domain, filter.Device, filter.UserId, (int)Enums.StatusNotification.Active, filter.TriggerAction);
                if (notifications != null && notifications.Any())
                {
                    // Lấy danh sách ID có thuộc tinh 2 và 3 của các thông báo để check thuộc tính trên server
                    var notificationIds = notifications.Where(a => a.Attributes.Contains(2) || a.Attributes.Contains(3)).Select(n => n.Id).ToList();

                    Dictionary<string, NotificationStatus> statuses = new Dictionary<string, NotificationStatus>();

                    // Lấy trạng thái của các thông báo
                    if (notificationIds != null && notificationIds.Any())
                        statuses = await GetNotificationStatusByIdsAsync(notificationIds, filter.UserId, filter.Domain);

                    // Lọc các thông báo dựa trên trạng thái
                    var filteredNotifications = new List<NotificationConfig>();
                    var dicStatus = new Dictionary<string, NotificationStatus>();
                    foreach (var notification in notifications)
                    {
                        var notificationId = notification.Id;
                        var attributes = notification.Attributes;

                        if (attributes == null)
                            attributes = new List<int>();

                        var status = new NotificationStatus();
                        statuses.TryGetValue(notificationId, out status);
                        if (status != null && status.RemainingShows > 0)
                        {
                            // Xử lý thuộc tính 2: Cập nhật số lượng hiển thị
                            if (attributes.Contains(2))
                            {
                                var numberShow = notification.MaxShow - statuses[notificationId].RemainingShows;
                                if (numberShow <= 0)
                                {
                                    continue;
                                }
                            }
                            // Kiểm tra xem thông báo có thuộc tính "Không làm phiền người dùng" không
                            var hasDoNotDisturbAttribute = notification.Attributes != null && notification.Attributes.Contains(3);
                            // Nếu thông báo có thuộc tính "Không làm phiền người dùng", kiểm tra xem đã được đánh dấu là "đã xem" chưa
                            if (hasDoNotDisturbAttribute)
                            {
                                // Kiểm tra trường lastClick trong Dictionary
                                try
                                {
                                    if (status != null && status.LastClick > 0)
                                    {
                                        var lastClickValue = status.LastClick;
                                        if (lastClickValue > 0)
                                        {
                                            // Thông báo đã được đánh dấu là "đã xem", không hiển thị
                                            //_logger.LogInformation("Thông báo {NotificationId} đã được đánh dấu là \"đã xem\" và sẽ không hiển thị", notificationId);
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Lỗi khi kiểm tra trường lastClick: {Message}", ex.Message);
                                }
                            }
                        }
                        // Thêm thông báo vào danh sách kết quả
                        if (status != null && !String.IsNullOrEmpty(status.NotificationId))
                        {
                            dicStatus.Add(status.NotificationId, status);
                        }


                        // Xử lý thuộc tính 1: Lấy lại nội dung khi load
                        if (attributes.Contains(1))
                            notification.Content = string.Empty;

                        if (!String.IsNullOrEmpty(notification.Content) && notification.Content.Length > 3000)
                        {
                            notification.Content = string.Empty;
                            attributes.Add(1);
                            attributes = attributes.Distinct().ToList();
                            _logger.LogInformation($"Thông báo {notification.Id} có lượng text quá lớn");
                        }


                        filteredNotifications.Add(notification);
                    }



                    result.Notifications = filteredNotifications;
                    result.NotificationStatus = dicStatus;

                    return result;

                }
            }




            return result;
        }


        /// <summary>
        /// Lấy thông báo theo domain và trạng thái
        /// </summary>
        /// <param name="domain">Domain cần lọc</param>
        /// <param name="status">Trạng thái cần lọc</param>
        /// <returns>Danh sách các thông báo phù hợp</returns>
        public async Task<List<NotificationConfig>> GetByDomainActiveAsync(string domain, DeviceType device, string userId, int status, List<string> triggerActions)
        {
            string domainField = Fields(n => n.Domains);
            string statusField = Fields(n => n.Status);
            string orderField = Fields(n => n.Order);
            string startDateField = Fields(n => n.StartDate);
            string endDateField = Fields(n => n.EndDate);
            string userIdField = Fields(n => n.UserId);

            long currentDate = XMUtility.XUtility.UnixTime(DateTime.Now);
            var lstParam = new List<KeyValuePair<string, object>>();
            lstParam.Add(new KeyValuePair<string, object>("$status", status));
            lstParam.Add(new KeyValuePair<string, object>("$currentDate", currentDate));
            StringBuilder stringBuilderquery = new StringBuilder();
            stringBuilderquery.Append($"SELECT n.* FROM {_bucket.Name} n WHERE n.{statusField} = $status");

            // Cú pháp ANY ... SATISFIES ... IN [...] END thay vì ARRAY_CONTAINS OR ARRAY_CONTAINS.
            // Lý do: optimizer Couchbase 6.0 không union được 2 ARRAY_CONTAINS với index DISTINCT ARRAY,
            // dẫn tới Fetch toàn bộ doc (~12k cho em-vn) rồi filter. ANY-SATISFIES dùng được 1 lần scan
            // với IN list → fetch chỉ doc thực sự match (vài cái).
            if (!String.IsNullOrEmpty(domain))
            {
                stringBuilderquery.Append($" AND ANY d IN n.{domainField} SATISFIES d IN [$domain, '{Utils.Common.All}'] END");
                lstParam.Add(new KeyValuePair<string, object>("$domain", domain));
            }
            else
            {
                stringBuilderquery.Append($" AND ANY d IN n.{domainField} SATISFIES d = '{Utils.Common.All}' END");
            }

            if (device != DeviceType.All)
            {
                stringBuilderquery.Append($" AND ANY dt IN n.{Fields(n => n.DeviceTypes)} SATISFIES dt IN [$device, {(int)DeviceType.All}] END");
                lstParam.Add(new KeyValuePair<string, object>("$device", (int)device));
            }

            if (!String.IsNullOrEmpty(userId))
            {
                stringBuilderquery.Append($" AND (n.{userIdField} = $userId OR n.{userIdField} = '{Utils.Common.All}')");
                lstParam.Add(new KeyValuePair<string, object>("$userId", userId));
            }
            else
            {
                stringBuilderquery.Append($" AND  n.{userIdField} = '{Utils.Common.All}'");
            }
            if (triggerActions != null && triggerActions.Any())
            {
                // Thêm 'all' rồi distinct → list các trigger client gửi + fallback 'all'
                var triggerList = triggerActions.Concat(new[] { Utils.Common.All }).Distinct();
                var inList = string.Join(",", triggerList.Select(t => $"'{t}'"));
                stringBuilderquery.Append($" AND ANY ta IN n.{Fields(n => n.TriggerActions)} SATISFIES ta IN [{inList}] END");
            }
            else
            {
                stringBuilderquery.Append($" AND ANY ta IN n.{Fields(n => n.TriggerActions)} SATISFIES ta = '{Utils.Common.All}' END");
            }

            stringBuilderquery.Append($" AND n.{startDateField} <= $currentDate AND n.{endDateField} >= $currentDate ORDER BY n.{orderField} ASC");

            var parameters = lstParam.ToArray();
            var results = await ExecuteQueryAsync<dynamic>(stringBuilderquery.ToString(), parameters);

            var notifications = new List<NotificationConfig>();
            foreach (var result in results)
            {
                var utems = JsonConvert.SerializeObject(result);
                var notification = JsonConvert.DeserializeObject<NotificationConfig>(result.ToString());
                notifications.Add(notification);
            }

            return notifications;
        }


        /// <summary>
        /// Lấy thông báo theo thiết bị và hành động kích hoạt
        /// </summary>
        /// <param name="deviceType">Loại thiết bị</param>
        /// <param name="triggerAction">Hành động kích hoạt</param>
        /// <returns>Danh sách các thông báo phù hợp</returns>
        public async Task<List<NotificationConfig>> GetByDeviceAndTriggerAsync(string deviceType, string triggerAction)
        {
            string deviceTypeField = Fields(n => n.DeviceTypes);
            string triggerActionField = Fields(n => n.TriggerActions);
            string statusField = Fields(n => n.Status);
            string startDateField = Fields(n => n.StartDate);
            string endDateField = Fields(n => n.EndDate);
            string orderField = Fields(n => n.Order);

            string query = $@"
                SELECT n.*
                FROM `{_bucket.Name}` n
                WHERE ANY dt IN n.{deviceTypeField} SATISFIES dt IN [$deviceType, 'all'] END
                AND ANY ta IN n.{triggerActionField} SATISFIES ta IN [$triggerAction, 'all'] END
                AND n.{statusField} = 'Active'
                AND n.{startDateField} <= $currentTime
                AND n.{endDateField} >= $currentTime
                ORDER BY n.{orderField} ASC";

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var parameters = new[]
            {
                new KeyValuePair<string, object>("$deviceType", deviceType),
                new KeyValuePair<string, object>("$triggerAction", triggerAction),
                new KeyValuePair<string, object>("$currentTime", currentTime),
                new KeyValuePair<string, object>("$preKey", $"{_preKey}%")
            };


            var results = await ExecuteQueryAsync<dynamic>(query, parameters);

            var notifications = new List<NotificationConfig>();
            foreach (var result in results)
            {
                var notification = Newtonsoft.Json.JsonConvert.DeserializeObject<NotificationConfig>(result.ToString(), _jsonSettings);
                notifications.Add(notification);
            }

            return notifications;
        }

        /// <summary>
        /// Cập nhật trạng thái của thông báo
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <param name="status">Trạng thái mới</param>
        /// <returns>True nếu cập nhật thành công</returns>
        public async Task<bool> UpdateStatusAsync(string id, string status)
        {
            try
            {
                // Chuyển đổi status từ string sang int nếu cần
                StatusNotification statusValue;
                if (!StatusNotification.TryParse(status, out statusValue))
                {
                    // Nếu status không phải là số, kiểm tra xem có phải là tên enum không
                    if (Enum.TryParse<StatusNotification>(status, out var statusEnum))
                    {
                        statusValue = statusEnum;
                    }
                    else
                    {
                        return false;
                    }
                }
                string key = CreateKey(id);
                var objCache = _bucket.Get<NotificationConfig>(key);
                if (objCache.Success)
                {
                    objCache.Value.Status = statusValue;
                    objCache.Value.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                    var ok = _bucket.Upsert(key, objCache.Value).Success;
                    if (ok) InvalidateMaxLuFor(objCache.Value);
                    return ok;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thông báo: {Message}", ex.Message);

            }
            return false;
        }




        /// <summary>
        /// Kiểm tra xem có bản ghi nào cùng domain và có lastUpdated lớn hơn tham số truyền vào không
        /// </summary>
        /// <param name="domain">Domain cần kiểm tra</param>
        /// <param name="lastUpdatedThreshold">Ngưỡng thời gian cập nhật (Unix time)</param>
        /// <returns>True nếu có bản ghi thỏa mãn điều kiện, False nếu không</returns>
        //public async Task<bool> HasRecentUpdatesInDomainAsync(string domain, DeviceType device, string userId, long lastUpdatedThreshold)
        //{
        //    string domainField = Fields(n => n.Domains);
        //    string lastUpdatedField = Fields(n => n.LastUpdated);
        //    string userIdField = Fields(n => n.UserId);

        //    var lstParam = new List<KeyValuePair<string, object>>();
        //    lstParam.Add(new KeyValuePair<string, object>("$lastUpdatedThreshold", lastUpdatedThreshold));
        //    StringBuilder stringBuilderquery = new StringBuilder();
        //    stringBuilderquery.Append($"SELECT COUNT(*) as count FROM {_bucket.Name} n WHERE n.{lastUpdatedField} > $lastUpdatedThreshold");

        //    if (!String.IsNullOrEmpty(domain))
        //    {
        //        //Nếu có domain sẽ lọc theo domain và các thông báo all
        //        stringBuilderquery.Append($" AND (ARRAY_CONTAINS(n.{domainField}, $domain) OR ARRAY_CONTAINS(n.{domainField}, '{Utils.Common.All}'))");
        //        lstParam.Add(new KeyValuePair<string, object>("$domain", domain));
        //    }
        //    else
        //    {
        //        stringBuilderquery.Append($" AND ARRAY_CONTAINS(n.{domainField}, '{Utils.Common.All}'))");
        //    }

        //    if (device != DeviceType.All)
        //    {
        //        stringBuilderquery.Append($" AND (ARRAY_CONTAINS(n.{Fields(n => n.DeviceTypes)}, $device) OR ARRAY_CONTAINS(n.{Fields(n => n.DeviceTypes)}, {(int)DeviceType.All}))");
        //        lstParam.Add(new KeyValuePair<string, object>("$device", (int)device));
        //    }

        //    if (!String.IsNullOrEmpty(userId))
        //    {
        //        stringBuilderquery.Append($" AND (n.{userIdField} = $userId OR n.{userIdField} = '{Utils.Common.All}')");
        //        lstParam.Add(new KeyValuePair<string, object>("$userId", userId));
        //    }
        //    else
        //    {
        //        stringBuilderquery.Append($" AND  n.{userIdField} = '{Utils.Common.All}'");
        //    }
        //    var parameters = lstParam.ToArray();
        //    var result = await ExecuteQueryAsync<dynamic>(stringBuilderquery.ToString(), parameters);

        //    if (result.Count > 0)
        //    {
        //        // Đọc giá trị count từ kết quả
        //        int count = Convert.ToInt32(((dynamic)result[0]).count);
        //        return count > 0;
        //    }

        //    return false;
        //}


        public async Task<bool> HasRecentUpdatesInDomainAsync(string domain, DeviceType device, string userId, List<string> triggerActions, long lastUpdatedThreshold)
        {
            // 1. Cache layer (Redis Hash) — key per (domain, device, userId, triggers)
            //    Đo prod: HasRecent chiếm ~68% slow query (29,364/ngày), p50 4.6s do Couchbase queue.
            //    Cache hit → bỏ qua DB, so sánh in-memory với T client gửi.
            var cached = await TryGetCachedMaxLuAsync(domain, device, userId, triggerActions);
            if (cached.HasValue)
            {
                return cached.Value > lastUpdatedThreshold;
            }

            // 2. Cache miss → query Couchbase MAX(last_updated) cho tuple đầy đủ.
            //    Logic filter giữ nguyên Fix #C — chỉ đổi SELECT để lấy MAX thay vì tồn tại.
            var maxLu = await QueryMaxLastUpdatedAsync(domain, device, userId, triggerActions);

            // 3. Cache result. Lỗi Redis được swallow trong helper, không ảnh hưởng request.
            await SetCachedMaxLuAsync(domain, device, userId, triggerActions, maxLu);

            return maxLu > lastUpdatedThreshold;
        }

        /// <summary>
        /// Query Couchbase: MAX(last_updated) cho mọi notification match
        /// (domain, device, userId, triggerActions). Logic filter y hệt
        /// HasRecentUpdatesInDomainAsync cũ — chỉ đổi SELECT.
        /// </summary>
        private async Task<long> QueryMaxLastUpdatedAsync(string domain, DeviceType device, string userId, List<string> triggerActions)
        {
            string domainField = Fields(n => n.Domains);
            string lastUpdatedField = Fields(n => n.LastUpdated);
            string userIdField = Fields(n => n.UserId);

            StringBuilder sb = new StringBuilder();
            sb.Append($"SELECT MAX(n.{lastUpdatedField}) AS max_lu FROM {_bucket.Name} AS n WHERE n.{lastUpdatedField} IS NOT MISSING");

            if (!string.IsNullOrEmpty(domain))
                sb.Append($" AND ANY d IN n.{domainField} SATISFIES d IN ['{domain}', '{Utils.Common.All}'] END");
            else
                sb.Append($" AND ANY d IN n.{domainField} SATISFIES d = '{Utils.Common.All}' END");

            if (device != DeviceType.All)
                sb.Append($" AND ANY dt IN n.{Fields(n => n.DeviceTypes)} SATISFIES dt IN [{(int)device}, {(int)DeviceType.All}] END");

            if (!string.IsNullOrEmpty(userId))
                sb.Append($" AND (n.{userIdField} = '{userId}' OR n.{userIdField} = '{Utils.Common.All}')");
            else
                sb.Append($" AND n.{userIdField} = '{Utils.Common.All}'");

            if (triggerActions != null && triggerActions.Any())
            {
                var triggerList = triggerActions.Concat(new[] { Utils.Common.All }).Distinct();
                var inList = string.Join(",", triggerList.Select(t => $"'{t}'"));
                sb.Append($" AND ANY ta IN n.{Fields(n => n.TriggerActions)} SATISFIES ta IN [{inList}] END");
            }
            else
            {
                sb.Append($" AND ANY ta IN n.{Fields(n => n.TriggerActions)} SATISFIES ta = '{Utils.Common.All}' END");
            }

            var rows = await ExecuteQueryAsync<dynamic>(sb.ToString(), null);
            if (rows == null || rows.Count == 0 || rows[0]?.max_lu == null) return 0L;
            return Convert.ToInt64(rows[0].max_lu);
        }

        /// <summary>
        /// Lấy các bản ghi cùng domain và có lastUpdated lớn hơn tham số truyền vào
        /// </summary>
        /// <param name="domain">Domain cần kiểm tra</param>
        /// <param name="lastUpdatedThreshold">Ngưỡng thời gian cập nhật (Unix time)</param>
        /// <returns>Danh sách các thông báo thỏa mãn điều kiện</returns>
        public async Task<List<NotificationConfig>> GetRecentUpdatesInDomainAsync(string domain, long lastUpdatedThreshold)
        {
            string domainField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.Domains);
            string lastUpdatedField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.LastUpdated);
            string orderField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.Order);

            string query = $@"
                SELECT n.*
                FROM `{_bucket.Name}` n
                WHERE ARRAY_CONTAINS(n.{domainField}, $domain)
                AND n.{lastUpdatedField} > $lastUpdatedThreshold
                ORDER BY n.{orderField} ASC";

            var parameters = new[]
            {
                new KeyValuePair<string, object>("$domain", domain),
                new KeyValuePair<string, object>("$lastUpdatedThreshold", lastUpdatedThreshold),
                new KeyValuePair<string, object>("$preKey", $"{_preKey}%")
            };

            return await ExecuteQueryAsync<NotificationConfig>(query, parameters);
        }



        /// <summary>
        /// Xóa tất cả dữ liệu trong bucket
        /// </summary>
        /// <returns>Số lượng bản ghi đã xóa</returns>
        public async Task<int> ClearAllDataAsync()
        {
            try
            {
                // string query = $@"
                //     DELETE FROM `{_bucket.Name}` n
                //     WHERE META().id LIKE $preKey
                //     RETURNING META().id";

                // var parameters = new[]
                // {
                //     new KeyValuePair<string, object>("$preKey", $"{_preKey}%")
                // };

                // var result = await ExecuteQueryAsync<dynamic>(query, parameters);
                // int count = result.Count;

                // _logger.LogInformation("Xóa {Count} bản ghi từ bucket {BucketName}", count, _bucket.Name);
                // return count;
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa dữ liệu: {Message}", ex.Message);

            }
            return 0;
        }




        public async Task<PagingNotificationConfig> SearchAsync(SearchNotificationPaging filter)
        {
            int total = 0;
            try
            {
                string lastUpdatedField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.LastUpdated);
                string domainField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.Domains);
                string statusField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.Status);
                string titleField = JsonPropertyHelper.JsonName<NotificationConfig>(n => n.Title);
                string userIdField = Fields(n => n.UserId);
                string deviceField = Fields(n => n.DeviceTypes);
                string showTypeField = Fields(n => n.ShowTypes);

                var parameters = new List<KeyValuePair<string, object>>();
                var useKeys = false;
                List<string> docIds = new List<string>();

                // Nếu có filter.Title thì dùng FTS
                if (!string.IsNullOrEmpty(filter.Title))
                {
                    var httpClient = new HttpClient();
                    var firstServer = _config.Servers.FirstOrDefault().Replace("8091", "8094");
                    var requestUri = $"{firstServer}/api/index/fts_xlog_title/query";
                    var requestBody = new
                    {
                        explain = false,
                        fields = new[] { "*" },
                        query = new
                        {
                            field = "title",
                            match_phrase = filter.Title.ToLower()
                        },
                        size = 1000
                    };

                    var json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"))
                    );

                    var response = await httpClient.PostAsync(requestUri, content);
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"FTS query failed: {response.ReasonPhrase}");

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var ftsResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    if (ftsResponse?.hits != null)
                    {
                        docIds = ((IEnumerable<dynamic>)ftsResponse.hits)
                            .Where(hit => hit.id != null)
                            .Select(hit => (string)hit.id)
                            .ToList();
                    }

                    if (!docIds.Any())
                        return new PagingNotificationConfig(); // Không có kết quả

                    useKeys = true;
                }

                StringBuilder query = new();
                StringBuilder countQuery = new();

                if (useKeys)
                {
                    query.Append($"SELECT n.* FROM `{_bucket.Name}` AS n USE KEYS $docIds WHERE n.{lastUpdatedField} >= 0");
                    countQuery.Append($"SELECT  COUNT(1) AS total FROM `{_bucket.Name}` AS n USE KEYS $docIds WHERE n.{lastUpdatedField} >= 0");
                    parameters.Add(new KeyValuePair<string, object>("$docIds", docIds));
                }
                else
                {
                    query.Append($"SELECT n.* FROM `{_bucket.Name}` AS n WHERE n.{lastUpdatedField} >= 0");
                    countQuery.Append($"SELECT  COUNT(1) AS total FROM `{_bucket.Name}` AS n WHERE n.{lastUpdatedField} >= 0");
                }

                if (!string.IsNullOrEmpty(filter.Domain))
                {
                    query.Append($" AND ANY d IN n.{domainField} SATISFIES d = $domain END");
                    countQuery.Append($" AND ANY d IN n.{domainField} SATISFIES d = $domain END");
                    parameters.Add(new KeyValuePair<string, object>("$domain", filter.Domain));
                }

                if (filter.Status != StatusNotification.All)
                {
                    query.Append($" AND n.{statusField} = $status");
                    countQuery.Append($" AND n.{statusField} = $status");
                    parameters.Add(new KeyValuePair<string, object>("$status", (int)filter.Status));
                }

                if (!string.IsNullOrEmpty(filter.UserId))
                {
                    query.Append($" AND n.{userIdField} = $userId");
                    countQuery.Append($" AND n.{userIdField} = $userId");
                    parameters.Add(new KeyValuePair<string, object>("$userId", filter.UserId));
                }

                if (filter.Device != DeviceType.Unknown)
                {
                    query.Append($" AND ANY d IN n.{deviceField} SATISFIES d = $device END");
                    countQuery.Append($" AND ANY d IN n.{deviceField} SATISFIES d = $device END");
                    parameters.Add(new KeyValuePair<string, object>("$device", (int)filter.Device));
                }

                if (filter.ShowType != ShowTypeNotification.All)
                {
                    query.Append($" AND ANY s IN n.{showTypeField} SATISFIES s = {(int)filter.ShowType} END");
                    countQuery.Append($" AND ANY s IN n.{showTypeField} SATISFIES s = {(int)filter.ShowType} END");
                }

                query.Append($" ORDER BY n.{lastUpdatedField} DESC LIMIT $limit OFFSET $offset");
                //countQuery.Append($" ORDER BY n.{lastUpdatedField} DESC ");
                parameters.Add(new KeyValuePair<string, object>("$limit", filter.PageSize));
                parameters.Add(new KeyValuePair<string, object>("$offset", (filter.PageIndex - 1) * filter.PageSize));

                if (!useKeys)
                {
                    // Dùng SDK QueryAsync (pooled connection) thay vì raw HttpClient new mỗi request.
                    // Trước: ~17s/req do socket setup; sau: ~600ms (cùng query, cùng plan).
                    var countResult = await ExecuteQueryAsync<dynamic>(countQuery.ToString(), parameters.ToArray());
                    if (countResult is not null && countResult.Count > 0)
                    {
                        var firstItem = countResult[0];
                        var totalProperty = firstItem?.total;

                        int value = 0;
                        if (totalProperty != null && int.TryParse(totalProperty.ToString(), out value))
                        {
                            total = value;
                        }
                    }
                }
                else
                {
                    var result = await ExecuteQueryAsync<dynamic>(countQuery.ToString(), parameters.ToArray());
                    if (result.Count > 0)
                    {
                        // Đọc giá trị count từ kết quả
                        var firstItem = result[0];
                        var totalProperty = firstItem?.total;

                        int value = 0;
                        if (totalProperty != null && int.TryParse(totalProperty.ToString(), out value))
                        {
                            total = value;
                        }

                    }
                }


                var results = await ExecuteQueryAsync<NotificationConfig>(query.ToString(), parameters.ToArray());

                return new PagingNotificationConfig()
                {
                    Data = results,
                    Total = total
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tìm kiếm thông báo: {Message}", ex.Message);
                return new PagingNotificationConfig();
            }
        }



        public async Task<bool> UpdateNotificationStatusAsync(NotificationStatus statusUpdate)
        {
            if (string.IsNullOrEmpty(statusUpdate.NotificationId) || string.IsNullOrEmpty(statusUpdate.UserId))
            {
                _logger.LogWarning("NotificationId hoặc UserId không được để trống");
                return false;
            }

            var statusKey = statusUpdate.GetKey(_preKey);// $"{_preKey}notification_status_{statusUpdate.NotificationId}_{statusUpdate.UserId}_{statusUpdate.Domain}";
            var configKey = CreateKey(statusUpdate.NotificationId);

            try
            {
                // Chạy 2 lệnh GET song song
                var getStatusTask = _bucket.GetAsync<NotificationStatus>(statusKey);
                var getConfigTask = _bucket.GetAsync<NotificationConfig>(configKey);

                await Task.WhenAll(getStatusTask, getConfigTask);

                var currentNotificationStatus = getStatusTask.Result.Success
                    ? getStatusTask.Result.Value
                    : new NotificationStatus();

                var objCacheConfig = getConfigTask.Result;

                var statusDict = new Dictionary<string, object>
                {
                    { "notificationId", statusUpdate.NotificationId },
                    { "userId", statusUpdate.UserId },
                    { "domain", statusUpdate.Domain },
                    { "remainingShows", currentNotificationStatus.RemainingShows },
                    { "lastShown", currentNotificationStatus.LastShown },

                };

                if (statusUpdate.RemainingShows > 0)
                {
                    statusDict["remainingShows"] = currentNotificationStatus.RemainingShows + statusUpdate.RemainingShows;
                    statusDict["lastShown"] = XMUtility.XUtility.TimeInEpoch(DateTime.Now);
                }

                if (statusUpdate.LastClick > 0)
                {
                    if (!statusDict.ContainsKey("lastClick"))
                    {
                        statusDict.Add("lastClick", XMUtility.XUtility.TimeInEpoch(DateTime.Now));
                    }
                    else
                        statusDict["lastClick"] = XMUtility.XUtility.TimeInEpoch(DateTime.Now);
                }

                // Set thời gian tồn tại mặc định 30 ngày
                TimeSpan time = TimeSpan.FromDays(30);
                if (objCacheConfig.Success && !string.IsNullOrEmpty(objCacheConfig.Value?.Id))
                {
                    time = Utils.Common.TimeUntil(XMUtility.XUtility.UnixTime(objCacheConfig.Value.EndDate));
                }
                var result = await _bucket.UpsertAsync(statusKey, statusDict, time);
                if (result.Success)
                {
                    try
                    {
                        if (objCacheConfig.Success && objCacheConfig.Value != null && objCacheConfig.Value.UserId != Utils.Common.All)
                        {

                            if (statusDict.ContainsKey("lastClick") && int.Parse(statusDict["lastClick"].ToString()) > 0)
                            {
                                objCacheConfig.Value.Status = StatusNotification.Success;
                                objCacheConfig.Value.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                                _bucket.Upsert(configKey, objCacheConfig.Value, time);
                                InvalidateMaxLuFor(objCacheConfig.Value);
                            }
                            else
                            {
                                if (statusDict.ContainsKey("remainingShows") && int.Parse(statusDict["remainingShows"].ToString()) >= objCacheConfig.Value.MaxShow)
                                {
                                    objCacheConfig.Value.Status = StatusNotification.Success;
                                    objCacheConfig.Value.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);
                                    _bucket.Upsert(configKey, objCacheConfig.Value, time);
                                    InvalidateMaxLuFor(objCacheConfig.Value);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thông báo: {Message}", ex.Message);
                    }
                    _logger.LogInformation(
                        statusUpdate.LastClick > 0
                            ? "Đánh dấu thông báo đã xem thành công: {NotificationId}, {UserId}"
                            : "Cập nhật trạng thái thông báo thành công: {NotificationId}, {UserId}",
                        statusUpdate.NotificationId, statusUpdate.UserId);
                    return true;
                }

                _logger.LogWarning("Lỗi khi cập nhật trạng thái thông báo: {Message}", result.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thông báo: {Message}", ex.Message);
                return false;
            }
        }




        /// <summary>
        /// Lấy trạng thái thông báo theo danh sách ID sử dụng N1QL
        /// </summary>
        /// <param name="notificationIds">Danh sách ID thông báo</param>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="domain">Tên miền</param>
        /// <returns>Dictionary chứa trạng thái của các thông báo</returns>
        /// <summary>
        /// Lấy trạng thái thông báo theo danh sách ID sử dụng N1QL
        /// </summary>
        /// <param name="notificationIds">Danh sách ID thông báo</param>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="domain">Tên miền</param>
        /// <returns>Dictionary chứa trạng thái của các thông báo</returns>
        //public async Task<Dictionary<string, NotificationStatus>> GetNotificationStatusByIdsAsync(
        //    List<string> notificationIds, string userId, string domain)
        //{
        //    if (notificationIds == null || notificationIds.Count == 0)
        //        return new Dictionary<string, NotificationStatus>();

        //    try
        //    {
        //        var result = new Dictionary<string, NotificationStatus>();

        //        // Ghép chuỗi ID dạng 'id1','id2',...
        //        var idList = notificationIds.Select(a => a).Distinct().ToList();

        //        string notificationIdField = FieldsSts(a => a.NotificationId);
        //        string userIdField = FieldsSts(a => a.UserId);
        //        string domainField = FieldsSts(a => a.Domain);

        //        string query = $@"
        //        SELECT n.*
        //        FROM `{_bucket.Name}` AS n
        //        WHERE n.{notificationIdField} IN $ids
        //          AND n.{userIdField} = $userId
        //          AND n.{domainField} = $domain
        //          AND META().id LIKE '{_preKey}notification_status_%'";

        //        var parameters = new[]
        //        {
        //            new KeyValuePair<string, object>("$userId", userId),
        //            new KeyValuePair<string, object>("$domain", domain),
        //            new KeyValuePair<string, object>("$ids", idList)
        //        };

        //        var queryResult = await ExecuteQueryAsync<NotificationStatus>(query, parameters);

        //        // Chuyển kết quả về dictionary
        //        return queryResult
        //            .Where(status => !string.IsNullOrEmpty(status.NotificationId))
        //            .ToDictionary(status => status.NotificationId, status => status);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi khi lấy trạng thái thông báo theo danh sách ID: {Message}", ex.Message);
        //        return new Dictionary<string, NotificationStatus>();
        //    }
        //}


        public async Task<Dictionary<string, NotificationStatus>> GetNotificationStatusByIdsAsync(
         List<string> notificationIds, string userId, string domain)
        {
            if (notificationIds == null || notificationIds.Count == 0)
                return new Dictionary<string, NotificationStatus>();

            var sw = Stopwatch.StartNew();

            var result = new ConcurrentDictionary<string, NotificationStatus>();

            var keys = notificationIds
                .Select(id => $"{_preKey}notification_status_{id}_{userId}_{domain}")
                .Distinct()
                .ToList();

            int totalKeys = keys.Count;
            int successCount = 0;

            // 🔎 Log danh sách key (giới hạn 20 cái đầu cho an toàn)
            var previewKeys = string.Join(", ", keys.Take(20));
            _logger.LogInformation("KV MultiGet Keys Preview ({Count}): {Keys}", totalKeys, previewKeys);

            var throttler = new SemaphoreSlim(50);

            var tasks = keys.Select(async key =>
            {
                await throttler.WaitAsync().ConfigureAwait(false);
                try
                {
                    var doc = await _bucket.GetAsync<NotificationStatus>(key).ConfigureAwait(false);

                    if (doc.Success && doc.Value != null && !string.IsNullOrEmpty(doc.Value.NotificationId))
                    {
                        result[doc.Value.NotificationId] = doc.Value;
                        Interlocked.Increment(ref successCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "KV get lỗi với key {Key}", key);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            sw.Stop();

            _logger.LogInformation(
                "KV MultiGet NotificationStatus DONE | Keys: {TotalKeys} | Found: {SuccessCount} | Time: {ElapsedMs} ms",
                totalKeys,
                successCount,
                sw.ElapsedMilliseconds
            );

            return result.ToDictionary(k => k.Key, v => v.Value);
        }



        public async Task<Dictionary<string, NotificationStatus>> GetNotificationStatusByIdsAsync(List<string> notificationIds)
        {
            if (notificationIds == null || notificationIds.Count == 0)
                return new Dictionary<string, NotificationStatus>();

            try
            {
                var idList = notificationIds.Distinct().ToList();

                string notificationIdField = FieldsSts(a => a.NotificationId);

                string query = $@"
                  SELECT n.*
                    FROM `{_bucket.Name}` AS n
                    WHERE n.{notificationIdField} IN $ids
                      AND META().id LIKE '{_preKey}notification_status_%'";

                var parameters = new[]
                {
                    new KeyValuePair<string, object>("$ids", idList)
                };

                var queryResult = await ExecuteQueryAsync<NotificationStatus>(query, parameters);

                if (queryResult != null && queryResult.Any())
                {
                    return queryResult?
                       .Where(x => !string.IsNullOrEmpty(x.NotificationId))
                       .GroupBy(x => x.NotificationId)
                       .ToDictionary(g => g.Key, g => g.FirstOrDefault())
                       ?? new Dictionary<string, NotificationStatus>();
                }
                else
                    return new Dictionary<string, NotificationStatus>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy trạng thái thông báo theo danh sách ID: {Message}", ex.Message);
                return new Dictionary<string, NotificationStatus>();
            }
        }


        /// <summary>
        /// Tạo các thông báo mẫu cho trang em-vn.joboko.com
        /// </summary>
        /// <returns>Danh sách ID của các thông báo đã tạo</returns>
        public async Task<List<string>> AddJobokoSampleNotificationsAsync()
        {
            //try
            //{
            //    var ids = new List<string>();
            //    long now = XMUtility.XUtility.UnixTime(DateTime.Now);
            //    long oneMonth = XMUtility.XUtility.UnixTime(DateTime.Now.AddMonths(1));
            //    long oneYear = XMUtility.XUtility.UnixTime(DateTime.Now.AddYears(1));

            //    List<NotificationConfig> notifications = new()
            //    {
            //    new NotificationConfig
            //{
            //    Id = "emvnjobokopopup1",
            //    Domains = new List<string> { "em-vn.joboko.com" },
            //    UserId = Utils.Common.All,
            //    PageShows = new List<ConfigLink>(){ new ConfigLink(Utils.Common.All) },
            //    ShowTypes = new() { ShowTypeNotification.Popup },
            //    Title = "Chào mừng đến với Joboko!",
            //    Content = "<p>Chúng tôi rất vui mừng được chào đón bạn đến với nền tảng tìm việc hàng đầu Việt Nam.</p><p>Hãy khám phá hàng ngàn cơ hội việc làm phù hợp với bạn ngay hôm nay!</p>",
            //    InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true },
            //    StartDate = now, EndDate = oneMonth,
            //    MaxShow = 1000, Frequency = 0, Order = 1,
            //    Attributes = new() { 2, 3 },
            //    Status = StatusNotification.Active,
            //    TriggerActions = new() { Utils.Common.All },
            //    DeviceTypes = new() { DeviceType.Website },
            //    LastUpdated = now
            //},

            //    new NotificationConfig
            //{
            //    Id = "emvnjobokopopup3",
            //    Domains = new() { "em-vn.joboko.com" },
            //    UserId = Utils.Common.All,
            //    PageShows = new List<ConfigLink>(){ new ConfigLink(Utils.Common.All) },
            //    ShowTypes = new() { ShowTypeNotification.Popup },
            //    Title = "Khởi đầu mới cùng Joboko X!",
            //    Content = "<p>🎉 Chào mừng bạn đến với <strong>Joboko X</strong> – nơi sự nghiệp của bạn được nâng tầm!</p>" +
            //              "<p>🌟 Tìm kiếm công việc mơ ước, kết nối với nhà tuyển dụng hàng đầu và xây dựng hành trình nghề nghiệp ngay hôm nay.</p>" +
            //              "<p>🚀 Cùng khám phá những tính năng thông minh và trải nghiệm tuyển dụng hiện đại nhất tại Joboko X!</p>",
            //    InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true },
            //    StartDate = now, EndDate = oneMonth,
            //    MaxShow = 1000, Frequency = 0, Order = 5,
            //    Attributes = new() { 2, 3 },
            //    Status = StatusNotification.Active,
            //    TriggerActions = new() { Utils.Common.All },
            //    DeviceTypes = new() { DeviceType.Website },
            //    LastUpdated = now
            //},

            //    new NotificationConfig
            //{
            //    Id = "emvnjobokohtml1",
            //    Domains = new() { "em-vn.joboko.com" },
            //    UserId = Utils.Common.All,
            //    PageShows = new List<ConfigLink>(){ new ConfigLink("/"), new ConfigLink("/quan-ly"), new ConfigLink("/chien-dich") },
            //    PageExcludes = new List < ConfigLink >() { new ConfigLink("/dang-nhap"), new ConfigLink("/dang-ky") },
            //    ShowTypes = new() { ShowTypeNotification.Html },
            //    Title = "Ưu đãi đặc biệt tháng 5",
            //    Content = "<div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px;'><h4 style='color: #007bff;'>Ưu đãi đặc biệt tháng 4</h4><p>Đăng ký tài khoản Premium trong tháng 4 và nhận ngay <strong>50% giảm giá</strong> cho 3 tháng đầu tiên!</p><a href='/premium' class='btn btn-primary'>Đăng ký ngay</a></div>",
            //    InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true, HtmlDisplayLocation = "#notification-container" },
            //    StartDate = now, EndDate = oneMonth,
            //    MaxShow = 0, Frequency = 0, Order = 2,
            //    Attributes = new() { 1 },
            //    Status = StatusNotification.Active,
            //    TriggerActions = new() { Utils.Common.All },
            //    DeviceTypes = new() { DeviceType.All },
            //    LastUpdated = now
            //},

            //    new NotificationConfig
            //{
            //    Id = "emvnjobokolink1",
            //    Domains = new() { "em-vn.joboko.com" },
            //    UserId = Utils.Common.All,
            //    PageShows = new List < ConfigLink >() { new ConfigLink(Utils.Common.All) , new ConfigLink("/quan-ly"), new ConfigLink("/chien-dich") },
            //    ShowTypes = new() { ShowTypeNotification.Link },
            //    Title = "Cập nhật hồ sơ để sở hữu những việc làm hữu ích nhất",
            //    Content = "/cong-ty ",
            //    InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true },
            //    StartDate = now, EndDate = oneMonth,
            //    MaxShow = 1000, Frequency = 0, Order = 3,
            //    Attributes = new() { 3 },
            //    Status = StatusNotification.Active,
            //    TriggerActions = new() { Utils.Common.All },
            //    DeviceTypes = new() { DeviceType.Website },
            //    LastUpdated = now
            //},

            //    new NotificationConfig
            //{
            //    Id = "emvnjobokopopup2",
            //    Domains = new() { "em-vn.joboko.com" },
            //    UserId = "new_candidates",
            //    PageShows = new List < ConfigLink >() { new ConfigLink("/quan-ly") },
            //    ShowTypes = new() { ShowTypeNotification.Popup },
            //    Title = "Chào mừng ứng viên mới!",
            //    Content = "<p>Chúc mừng bạn đã tạo tài khoản thành công trên Joboko!</p><p>Hãy hoàn thiện hồ sơ của bạn để tăng cơ hội được tuyển dụng nhé.</p>",
            //    InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true },
            //    StartDate = now, EndDate = oneYear,
            //    MaxShow = 10000, Frequency = 0, Order = 1,
            //    Attributes = new() { 2, 3 },
            //    Status = StatusNotification.Active,
            //    TriggerActions = new() { "new_user_registration" },
            //    DeviceTypes = new() { DeviceType.All },
            //    LastUpdated = now
            //},

            //    new NotificationConfig
            //    {
            //        Id = "emvnjobokopopupmobile",
            //        Domains = new() { "em-vn.joboko.com" },
            //        UserId = "new_candidates",
            //        PageShows = new List <ConfigLink>() { new ConfigLink("/quan-ly") },
            //        ShowTypes = new() { ShowTypeNotification.Popup },
            //        Title = "Chào mừng ứng viên mới Mobile!",
            //        Content = "<p>Chúc mừng bạn đã tạo tài khoản thành công trên Joboko Mobile!</p><p>Hãy hoàn thiện hồ sơ của bạn để tăng cơ hội được tuyển dụng nhé.</p>",
            //        InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true },
            //        StartDate = now, EndDate = oneYear,
            //        MaxShow = 10000, Frequency = 0, Order = 1,
            //        Attributes = new() { 2, 3 },
            //        Status = StatusNotification.Active,
            //        TriggerActions = new() { "new_user_registration" },
            //        DeviceTypes = new() { DeviceType.MobileWeb },
            //        LastUpdated = now
            //    },

            //    // Ví dụ 1: Modal thông báo chính sách mới
            //    new NotificationConfig
            //    {
            //        Id = "emvnjobokomodal1",
            //        Domains = new() { "em-vn.joboko.com" },
            //        UserId = Utils.Common.All,
            //        PageShows = new List<ConfigLink>(){ new ConfigLink("/"), new ConfigLink("/quan-ly") },
            //        ShowTypes = new() { (ShowTypeNotification)4 }, // 4 = Modal
            //        Title = "Chính sách bảo mật mới",
            //        Content = "<div class='p-3'><h4 class='text-primary mb-3'>Chính sách bảo mật đã được cập nhật</h4><p>Chúng tôi đã cập nhật chính sách bảo mật để tuân thủ các quy định mới về GDPR và bảo vệ dữ liệu cá nhân.</p><p>Những thay đổi chính bao gồm:</p><ul><li>Cập nhật cách chúng tôi xử lý dữ liệu cá nhân</li><li>Làm rõ quyền riêng tư của bạn</li><li>Cải thiện bảo mật dữ liệu</li></ul><p class='mt-3'>Bạn có thể xem chính sách đầy đủ <a href='/chinh-sach-bao-mat' class='notification-track-click'>tại đây</a>.</p><div class='text-end mt-4'><button class='btn btn-primary notification-track-click'>Tôi đã hiểu</button></div></div>",
            //        InfoMore = new InfoMoreShowNoiDung {
            //            PopupDismissable = true,
            //            HtmlDisplayLocation = "#x-msg-notification"
            //        },
            //        StartDate = now, EndDate = oneMonth,
            //        MaxShow = 1, Frequency = 0, Order = 1,
            //        Attributes = new() { 3 }, // 3 = Không làm phiền người dùng
            //        Status = StatusNotification.Active,
            //        DeviceTypes = new() { DeviceType.All },
            //        LastUpdated = now
            //    },

            //    // Ví dụ 2: Modal thông báo khuyến mãi
            //    new NotificationConfig
            //    {
            //        Id = "emvnjobokomodal2",
            //        Domains = new() { "em-vn.joboko.com" },
            //        UserId = Utils.Common.All,
            //        PageShows = new List<ConfigLink>(){ new ConfigLink("/chien-dich"), new ConfigLink("/cong-ty") },
            //        ShowTypes = new() { (ShowTypeNotification)4 }, // 4 = Modal
            //        Title = "Khuyến mãi đặc biệt",
            //        Content = "<div class='text-center p-3'><div class='mb-3'></div><h4 class='text-danger mb-3'>Giảm 30% gói Premium</h4><p>Chỉ trong tháng này, đăng ký gói Premium và nhận ngay ưu đãi giảm 30% cho năm đầu tiên!</p><p class='text-muted small'>Mã khuyến mãi: <strong>PREMIUM30</strong></p><div class='mt-4'><a href='/premium' class='btn btn-danger notification-track-click'>Nâng cấp ngay</a></div><div class='mt-3'><button class='btn btn-link text-muted notification-track-click'>Để sau</button></div></div>",
            //        InfoMore = new InfoMoreShowNoiDung {
            //            PopupDismissable = true,
            //            HtmlDisplayLocation = "#x-msg-notification"
            //        },
            //        StartDate = now, EndDate = oneMonth,
            //        MaxShow = 3, Frequency = 24, Order = 2,
            //        Attributes = new() { 3 }, // 3 = Không làm phiền người dùng
            //        Status = StatusNotification.Active,
            //        DeviceTypes = new() { DeviceType.All },
            //        LastUpdated = now
            //    },

            //    new NotificationConfig
            //    {
            //            Id = "emvnjobokohtml2",
            //            Domains = new() { "em-vn.joboko.com" },
            //            UserId = Utils.Common.All,
            //            PageShows = new List<ConfigLink>() { new ConfigLink("/"), new ConfigLink("/quan-ly"), new ConfigLink("/chien-dich") } ,
            //            ShowTypes = new() { ShowTypeNotification.Html },
            //            Title = "Việc làm mới trong tuần",
            //            Content = "<div style='background-color: #e9ecef; padding: 10px; border-radius: 5px;'><h5 style='color: #28a745;'>Có hơn 100 việc làm mới trong tuần này!</h5><p>Khám phá ngay để tìm cơ hội phù hợp với bạn.</p><a href='/jobs?sort=newest' class='btn btn-success btn-sm'>Xem việc làm mới</a></div>",
            //            InfoMore = new InfoMoreShowNoiDung { PopupDismissable = true, HtmlDisplayLocation = "#html-main-top" },
            //            StartDate = now, EndDate = oneYear,
            //            MaxShow = 10000, Frequency = 0, Order = 2,
            //            Attributes = new() { 1,3 },
            //            Status = StatusNotification.Active,
            //            TriggerActions = new() { "weekly_job_update" },
            //            DeviceTypes = new() { DeviceType.All },
            //            LastUpdated = now
            //        }
            //    };
            //    var lstkey = new List<string>();
            //    foreach (var notification in notifications)
            //    {
            //        var id = notification.Id;
            //        var result = await UpsertAsync(notification, id);
            //        if (!String.IsNullOrEmpty(result))
            //        {
            //            var statusKey = $"{_preKey}notification_status_{notification.Id}_26398_em-vn.joboko.com";
            //            await _bucket.RemoveAsync(statusKey);

            //            ids.Add(id);
            //            _logger.LogInformation("Thêm thông báo mẫu với ID: {Id}", id);
            //        }
            //        else
            //        {
            //            _logger.LogError("Lỗi khi thêm thông báo mẫu với ID: {Id}, Lỗi: {Message}", id, "Có lỗi trong quá trình xử lý");
            //        }
            //    }





            //    return ids;
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Lỗi khi thêm dữ liệu mẫu: {Message}", ex.Message);

            //}
            return new List<string>();
        }

    }
}
