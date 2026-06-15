using OfficeOpenXml;
using OfficeOpenXml.Style;
using NotificationAPI.Models;
using System.Globalization;
using NotificationAPI.Enums;

namespace NotificationAPI.Utils
{
    public static class ExcelHelper
    {
        /// <summary>
        /// Tạo file Excel mẫu cho việc nhập nhiều thông báo
        /// </summary>
        /// <returns>Mảng byte của file Excel</returns>
        public static byte[] CreateExcelTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Notifications");

                // Tạo header
                var headers = new[] { "Title", "Content", "Domains", "UserId", "ShowTypes", "StartDate", "EndDate", "MaxShow", "Frequency", "Order", "Attributes", "DeviceTypes", "TriggerActions", "HtmlDisplayLocation", "IsDirectLink", "PopupDismissable" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Thêm dữ liệu mẫu
                worksheet.Cells[2, 1].Value = "Khuyến mãi đặc biệt";
                worksheet.Cells[2, 2].Value = "Giảm giá 20% cho tất cả sản phẩm";
                worksheet.Cells[2, 3].Value = "example.com,mydomain.com";
                worksheet.Cells[2, 4].Value = "all";
                worksheet.Cells[2, 5].Value = "1,3"; // Popup, Link
                worksheet.Cells[2, 6].Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[2, 7].Value = DateTime.Now.AddDays(30).ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[2, 8].Value = "5";
                worksheet.Cells[2, 9].Value = "0";
                worksheet.Cells[2, 10].Value = "1";
                worksheet.Cells[2, 11].Value = "2,3"; // Attributes
                worksheet.Cells[2, 12].Value = "1"; // All devices
                worksheet.Cells[2, 13].Value = "all";
                worksheet.Cells[2, 14].Value = "#notification-container";
                worksheet.Cells[2, 15].Value = "FALSE";
                worksheet.Cells[2, 16].Value = "TRUE";
                worksheet.Cells[2, 17].Value = "bootstrap.Modal.getOrCreateInstance";

                // Thêm dòng mẫu thứ hai
                worksheet.Cells[3, 1].Value = "Thông báo HTML";
                worksheet.Cells[3, 2].Value = "<div class='alert alert-info'>Thông báo quan trọng</div>";
                worksheet.Cells[3, 3].Value = "example.com";
                worksheet.Cells[3, 4].Value = "user123";
                worksheet.Cells[3, 5].Value = "2"; // HTML
                worksheet.Cells[3, 6].Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[3, 7].Value = DateTime.Now.AddDays(15).ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[3, 8].Value = "3";
                worksheet.Cells[3, 9].Value = "24";
                worksheet.Cells[3, 10].Value = "2";
                worksheet.Cells[3, 11].Value = "3"; // Attributes
                worksheet.Cells[3, 12].Value = "2"; // Website
                worksheet.Cells[3, 13].Value = "OnLogin";
                worksheet.Cells[3, 14].Value = "#sidebar-notification";
                worksheet.Cells[3, 15].Value = "FALSE";
                worksheet.Cells[3, 16].Value = "TRUE";

                // Tự động điều chỉnh độ rộng cột
                worksheet.Cells.AutoFitColumns();

                // Thêm validation và ghi chú
                var titleComment = worksheet.Cells[1, 1].AddComment("Tiêu đề thông báo", "NotificationAPI");
                titleComment.AutoFit = true;

                var contentComment = worksheet.Cells[1, 2].AddComment("Nội dung thông báo. Đối với loại Link, đây là URL", "NotificationAPI");
                contentComment.AutoFit = true;

                var domainsComment = worksheet.Cells[1, 3].AddComment("Danh sách tên miền, phân cách bằng dấu phẩy", "NotificationAPI");
                domainsComment.AutoFit = true;

                var userIdComment = worksheet.Cells[1, 4].AddComment("ID người dùng hoặc 'all' cho tất cả người dùng", "NotificationAPI");
                userIdComment.AutoFit = true;

                var showTypesComment = worksheet.Cells[1, 5].AddComment("Loại hiển thị: 1=Popup, 2=HTML, 3=Link. Phân cách bằng dấu phẩy", "NotificationAPI");
                showTypesComment.AutoFit = true;

                return package.GetAsByteArray();
            }
        }

        /// <summary>
        /// Đọc danh sách thông báo từ file Excel
        /// </summary>
        /// <param name="fileContent">Nội dung file Excel</param>
        /// <returns>Danh sách thông báo</returns>
        public static List<NotificationConfig> ReadNotificationsFromExcel(byte[] fileContent)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var notifications = new List<NotificationConfig>();

            using (var package = new ExcelPackage(new MemoryStream(fileContent)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // Lấy sheet đầu tiên

                int rowCount = worksheet.Dimension.Rows;

                // Bỏ qua dòng header
                for (int row = 2; row <= rowCount; row++)
                {
                    var notification = new NotificationConfig();

                    // Đọc các giá trị từ Excel
                    notification.Title = worksheet.Cells[row, 1].Value?.ToString() ?? string.Empty;
                    notification.Content = worksheet.Cells[row, 2].Value?.ToString() ?? string.Empty;

                    // Domains
                    var domainsStr = worksheet.Cells[row, 3].Value?.ToString();
                    if (!string.IsNullOrEmpty(domainsStr))
                    {
                        notification.Domains = domainsStr.Split(',').Select(d => d.Trim()).ToList();
                    }

                    // UserId
                    notification.UserId = worksheet.Cells[row, 4].Value?.ToString() ?? Common.All;

                    // ShowTypes
                    var showTypesStr = worksheet.Cells[row, 5].Value?.ToString();
                    if (!string.IsNullOrEmpty(showTypesStr))
                    {
                        notification.ShowTypes = showTypesStr.Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int showType) ? (ShowTypeNotification)showType : ShowTypeNotification.Popup)
                            .ToList();
                    }

                    // StartDate
                    var startDateStr = worksheet.Cells[row, 6].Value?.ToString();
                    if (!string.IsNullOrEmpty(startDateStr) && DateTime.TryParseExact(startDateStr, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate))
                    {
                        notification.StartDate = XMUtility.XUtility.UnixTime(startDate);
                    }
                    else
                    {
                        notification.StartDate = XMUtility.XUtility.UnixTime(DateTime.Now);
                    }

                    // EndDate
                    var endDateStr = worksheet.Cells[row, 7].Value?.ToString();
                    if (!string.IsNullOrEmpty(endDateStr) && DateTime.TryParseExact(endDateStr, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
                    {
                        notification.EndDate = XMUtility.XUtility.UnixTime(endDate);
                    }
                    else
                    {
                        notification.EndDate = XMUtility.XUtility.UnixTime(DateTime.Now.AddDays(30));
                    }

                    // MaxShow
                    var maxShowStr = worksheet.Cells[row, 8].Value?.ToString();
                    if (!string.IsNullOrEmpty(maxShowStr) && int.TryParse(maxShowStr, out int maxShow))
                    {
                        notification.MaxShow = maxShow;
                    }

                    // Frequency
                    var frequencyStr = worksheet.Cells[row, 9].Value?.ToString();
                    if (!string.IsNullOrEmpty(frequencyStr) && int.TryParse(frequencyStr, out int frequency))
                    {
                        notification.Frequency = frequency;
                    }

                    // Order
                    var orderStr = worksheet.Cells[row, 10].Value?.ToString();
                    if (!string.IsNullOrEmpty(orderStr) && int.TryParse(orderStr, out int order))
                    {
                        notification.Order = order;
                    }
                    else
                    {
                        notification.Order = 1;
                    }

                    // Attributes
                    var attributesStr = worksheet.Cells[row, 11].Value?.ToString();
                    if (!string.IsNullOrEmpty(attributesStr))
                    {
                        notification.Attributes = attributesStr.Split(',')
                            .Select(a => int.TryParse(a.Trim(), out int attr) ? attr : 0)
                            .Where(a => a > 0)
                            .ToList();
                    }

                    // DeviceTypes
                    var deviceTypesStr = worksheet.Cells[row, 12].Value?.ToString();
                    if (!string.IsNullOrEmpty(deviceTypesStr))
                    {
                        notification.DeviceTypes = deviceTypesStr.Split(',')
                            .Select(d => int.TryParse(d.Trim(), out int deviceType) ? (DeviceType)deviceType : DeviceType.All)
                            .ToList();
                    }
                    else
                    {
                        notification.DeviceTypes = new List<DeviceType> { DeviceType.All };
                    }

                    // TriggerActions
                    var triggerActionsStr = worksheet.Cells[row, 13].Value?.ToString();
                    if (!string.IsNullOrEmpty(triggerActionsStr))
                    {
                        notification.TriggerActions = triggerActionsStr.Split(',')
                            .Select(t => t.Trim())
                            .ToList();
                    }
                    else
                    {
                        notification.TriggerActions = new List<string> { Common.All };
                    }

                    // InfoMore
                    notification.InfoMore = new InfoMoreShowNoiDung();

                    // HtmlDisplayLocation
                    notification.InfoMore.HtmlDisplayLocation = worksheet.Cells[row, 14].Value?.ToString() ?? string.Empty;

                    // IsDirectLink
                    var isDirectLinkStr = worksheet.Cells[row, 15].Value?.ToString();
                    if (!string.IsNullOrEmpty(isDirectLinkStr) && bool.TryParse(isDirectLinkStr, out bool isDirectLink))
                    {
                        notification.InfoMore.IsDirectLink = isDirectLink;
                    }

                    // PopupDismissable
                    var popupDismissableStr = worksheet.Cells[row, 16].Value?.ToString();
                    if (!string.IsNullOrEmpty(popupDismissableStr) && bool.TryParse(popupDismissableStr, out bool popupDismissable))
                    {
                        notification.InfoMore.PopupDismissable = popupDismissable;
                    }
                    else
                    {
                        notification.InfoMore.PopupDismissable = true;
                    }

                    // Đặt LastUpdated
                    notification.LastUpdated = XMUtility.XUtility.UnixTime(DateTime.Now);

                    // Đặt Status
                    notification.Status = StatusNotification.Active;

                    // Thêm vào danh sách
                    notifications.Add(notification);
                }
            }

            return notifications;
        }
    }
}
