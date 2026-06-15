using Microsoft.AspNetCore.Mvc.Rendering;
using NotificationAPI.DTO.Filter;
using NotificationAPI.Models;
using System.Collections.Generic;

namespace NotificationAPI.Areas.Admin.Models
{


    public class NotificationListViewModel
    {
        public List<NotificationConfig> Notifications { get; set; } = new List<NotificationConfig>();
        public SearchNotificationPaging Filter { get; set; } = new SearchNotificationPaging();
        public Dictionary<string, NotificationStatus> NotificationStatus { get; set; } = new Dictionary<string, NotificationStatus>();
        public int Total { get; set; } = 0;
    }

    public class NotificationViewModel
    {
        public string IdChienDich { get; set; } = string.Empty;
        public NotificationConfig Notification { get; set; } = new NotificationConfig();
        public List<SelectListItem> ShowTypeOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
    }
}
