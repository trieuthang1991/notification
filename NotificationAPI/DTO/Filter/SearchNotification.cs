using NotificationAPI.Enums;

namespace NotificationAPI.DTO.Filter
{
    public class SearchNotification
    {
        public string Domain { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DeviceType Device { get; set; } = DeviceType.All;
        //public string Page { get; set; } = string.Empty;

        public string? TriggerActionString { get; set; } = string.Empty;
        public List<string> TriggerAction
        {
            get
            {
                if (string.IsNullOrEmpty(TriggerActionString))
                {
                    return new List<string>();
                }
                return TriggerActionString.Split(',').Select(x => x.Trim()).ToList();
            }
        }

        
        public long LastUpdated { get; set; } = XMUtility.XUtility.TimeInEpoch(DateTime.Now);
    }
    public class SearchNotificationPaging:SearchNotification
    {
        public string Title { get; set; }= string.Empty;
        public int PageSize { get; set; } = 10;
        public int PageIndex { get; set; } = 1;
        public StatusNotification Status { get; set; } = StatusNotification.All;
        public ShowTypeNotification ShowType { get; set; } = ShowTypeNotification.All;

        public SearchNotificationPaging()
        {
            this.Device = DeviceType.Unknown;
        }

        //public string SortBy { get; set; } = "lastUpdated";
        //public string SortDirection { get; set; } = "desc";
    }

}
