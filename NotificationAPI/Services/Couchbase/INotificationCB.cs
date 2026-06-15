using NotificationAPI.DTO.Filter;
using NotificationAPI.Enums;
using NotificationAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NotificationAPI.Services.Couchbase
{
    /// <summary>
    /// Interface cung cấp các phương thức thao tác với thông báo trong Couchbase
    /// </summary>
    public interface INotificationCB
    {
        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <returns>Thông báo tìm thấy hoặc null</returns>
        Task<NotificationConfig> GetByIdAsync(string id);

        Task<List<NotificationConfig>> GetByUserIdAsync(string userId);
        Task<List<NotificationConfig>> GetContentByIdsAsync(List<string> ids);
        /// <summary>
        /// Lưu thông báo mới hoặc cập nhật thông báo đã tồn tại
        /// </summary>
        /// <param name="notification">Thông báo cần lưu</param>
        /// <param name="id">ID của thông báo (nếu null, sẽ tạo ID mới)</param>
        /// <returns>ID của thông báo đã lưu</returns>
        Task<string> UpsertAsync(NotificationConfig notification, string id = "");

        /// <summary>
        /// Xóa thông báo theo ID
        /// </summary>
        /// <param name="id">ID của thông báo cần xóa</param>
        /// <returns>True nếu xóa thành công, False nếu không tìm thấy</returns>
        Task<bool> DeleteAsync(string id);

        /// <summary>
        /// Lấy tất cả thông báo đang hoạt động
        /// </summary>
        /// <returns>Danh sách các thông báo đang hoạt động</returns>
        Task<NotificationConfigVM> GetActiveNotificationsAsync(SearchNotification filter);

        /// <summary>
        /// Search notification
        /// </summary>
        /// <returns>Danh sách các thông báo đang hoạt động</returns>
        Task<PagingNotificationConfig> SearchAsync(SearchNotificationPaging filter);

        /// <summary>
        /// Lấy thông báo theo domain và trạng thái
        /// </summary>
        /// <param name="domain">Domain cần lọc</param>
        /// <param name="status">Trạng thái cần lọc</param>
        /// <returns>Danh sách các thông báo phù hợp</returns>
        Task<List<NotificationConfig>> GetByDomainActiveAsync(string domain, DeviceType device, string userId, int status, List<string> triggerAction);

        /// <summary>
        /// Lấy thông báo theo thiết bị và hành động kích hoạt
        /// </summary>
        /// <param name="deviceType">Loại thiết bị</param>
        /// <param name="triggerAction">Hành động kích hoạt</param>
        /// <returns>Danh sách các thông báo phù hợp</returns>
        Task<List<NotificationConfig>> GetByDeviceAndTriggerAsync(string deviceType, string triggerAction);

        /// <summary>
        /// Cập nhật trạng thái của thông báo
        /// </summary>
        /// <param name="id">ID của thông báo</param>
        /// <param name="status">Trạng thái mới</param>
        /// <returns>True nếu cập nhật thành công</returns>
        Task<bool> UpdateStatusAsync(string id, string status);


        /// <summary>
        /// Kiểm tra xem có bản ghi nào cùng domain và có lastUpdated lớn hơn tham số truyền vào không
        /// </summary>
        /// <param name="domain">Domain cần kiểm tra</param>
        /// <param name="lastUpdatedThreshold">Ngưỡng thời gian cập nhật (Unix time)</param>
        /// <returns>True nếu có bản ghi thỏa mãn điều kiện, False nếu không</returns>
        Task<bool> HasRecentUpdatesInDomainAsync(string domain, DeviceType device, string userId, List<string> triggerAction, long lastUpdatedThreshold);

        /// <summary>
        /// Lấy các bản ghi cùng domain và có lastUpdated lớn hơn tham số truyền vào
        /// </summary>
        /// <param name="domain">Domain cần kiểm tra</param>
        /// <param name="lastUpdatedThreshold">Ngưỡng thời gian cập nhật (Unix time)</param>
        /// <returns>Danh sách các thông báo thỏa mãn điều kiện</returns>
        Task<List<NotificationConfig>> GetRecentUpdatesInDomainAsync(string domain, long lastUpdatedThreshold);

        /// <summary>
        /// Xóa tất cả dữ liệu trong bucket
        /// </summary>
        /// <returns>Số lượng bản ghi đã xóa</returns>
        Task<int> ClearAllDataAsync();




        /// <summary>
        /// Cập nhật trạng thái của thông báo (hiển thị, tương tác)
        /// </summary>
        /// <param name="statusUpdate">Thông tin cập nhật trạng thái</param>
        /// <returns>True nếu cập nhật thành công</returns>
        Task<bool> UpdateNotificationStatusAsync(NotificationStatus statusUpdate);

        /// <summary>
        /// Lấy trạng thái thông báo theo danh sách ID
        /// </summary>
        /// <param name="notificationIds">Danh sách ID thông báo</param>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="domain">Tên miền</param>
        /// <returns>Dictionary chứa trạng thái của các thông báo</returns>
        Task<Dictionary<string, NotificationStatus>> GetNotificationStatusByIdsAsync(List<string> notificationIds, string userId, string domain);

        /// <summary>
        /// Lấy trạng thái thông báo theo danh sách ID
        /// </summary>
        /// <param name="notificationIds">Danh sách ID thông báo</param>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="domain">Tên miền</param>
        /// <returns>Dictionary chứa trạng thái của các thông báo</returns>
        Task<Dictionary<string, NotificationStatus>> GetNotificationStatusByIdsAsync(List<string> notificationIds);

        /// <summary>
        /// Tạo các thông báo mẫu cho trang em-vn.joboko.com
        /// </summary>
        /// <returns>Danh sách ID của các thông báo đã tạo</returns>
        Task<List<string>> AddJobokoSampleNotificationsAsync();
    }
}
