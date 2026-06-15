using NotificationAPI.Enums;
using System;
using System.Collections.Generic;

namespace NotificationAPI.Models
{
    /// <summary>
    /// ViewModel kết hợp NotificationConfig và NotificationStatus để trả về cho client
    /// </summary>
    public class NotificationConfigVM
    {
        public bool IsSetData { get; set; } = false; //Sử dụng để biết cần update lại lastActive ở client
        public List<NotificationConfig> Notifications { get; set; } = new List<NotificationConfig>();
        public Dictionary<string, NotificationStatus> NotificationStatus { get; set; } = new Dictionary<string, NotificationStatus>();

    }
}
