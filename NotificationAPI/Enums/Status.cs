namespace NotificationAPI.Enums
{
    public enum StatusNotification
    {
        All = 0,
        Active = 1,
        Inactive = 2,
        Success = 3,//Đã xử lý rồi
    }

    public enum DeviceType
    {
        Unknown = 0,
        All = 1,
        Website = 2,
        MobileWeb = 3,
        Mobile = 4
    }


    public enum ConfigLinkType
    {
        Path = 0,
        Link = 1,

    }
    public enum ConfigLinkTypeGet
    {
        Same = 0,
        Regex = 1
    }
}
