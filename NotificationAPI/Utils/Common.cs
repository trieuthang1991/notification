namespace NotificationAPI.Utils
{
    public class Common
    {
        public const string All = "all";
        // get atttibute name notificationConfig


        public static List<KeyValuePair<int, string>> Attributes { get; set; } = new List<KeyValuePair<int, string>>(){
            new KeyValuePair<int, string>(1,"Lấy lại nội dung khi load"),
            new KeyValuePair<int, string>(2,"Cập nhật trạng thái về server"),
            new KeyValuePair<int, string>(3,"Không làm phiền người dùng"),
        };
        public static List<KeyValuePair<string, string>> Domains { get; set; } = new List<KeyValuePair<string, string>>(){
            new KeyValuePair<string, string>("vn.joboko.com","vn.joboko.com"),
            new KeyValuePair<string, string>("em-vn.joboko.com","em-vn.joboko.com")
        };
        public static List<KeyValuePair<string, string>> ShowListDomain(List<string> selecteds)
        {
            var result = new List<KeyValuePair<string, string>>();
            result.AddRange(Domains);
            foreach (var item in selecteds)
            {
                if (!result.Any(a=> a.Key == item))
                {
                    result.Add(new KeyValuePair<string, string>(item,item));
                }
            }
            return result;
        }

        public static List<KeyValuePair<string, string>> ShowListTriggerAction(List<string> selecteds)
        {
            var result = new List<KeyValuePair<string, string>>();
            result.Add(new KeyValuePair<string, string>(Utils.Common.All,"Tất cả"));
            foreach (var item in selecteds)
            {
                if (!result.Any(a => a.Key == item))
                {
                    result.Add(new KeyValuePair<string, string>(item, item));
                }
            }
            return result;
        }

        public static List<KeyValuePair<string, string>> ShowPages(List<string> selecteds)
        {
            var result = new List<KeyValuePair<string, string>>();
            result.Add(new KeyValuePair<string, string>("/","Trang chủ (Home Page)"));
            result.Add(new KeyValuePair<string, string>(All, "Tất cả các trang"));
            foreach (var item in selecteds)
            {
                if (!result.Any(a => a.Key == item))
                {
                    result.Add(new KeyValuePair<string, string>(item, item));
                }
            }
            return result;
        }
        public static TimeSpan TimeUntil(DateTime? date)
        {
            if (!date.HasValue) return TimeSpan.Zero;
            return date.Value.ToUniversalTime() - DateTime.UtcNow;
        }
        public static List<int> ListPageSize() => new List<int>() { 10, 20, 50, 100 };
        public static List<KeyValuePair<string,string>> ListSortBy()
        {
            var result = new List<KeyValuePair<string, string>>();
            result.Add(new KeyValuePair<string, string>("LastUpdated","Thời gian cập nhật"));
            result.Add(new KeyValuePair<string, string>("Title","Tiêu đề"));
            result.Add(new KeyValuePair<string, string>("Order","Thứ tự ưu tiên"));
            result.Add(new KeyValuePair<string, string>("StartDate","Ngày bắt đầu"));
            result.Add(new KeyValuePair<string, string>("EndDate","Ngày kết thúc"));
            result.Add(new KeyValuePair<string, string>("MaxShow","Số lần hiển thị tối đa"));
           return result;
        }
        public static List<KeyValuePair<string, string>> ListSortDirection()
        {
            var result = new List<KeyValuePair<string, string>>();
            result.Add(new KeyValuePair<string, string>("desc", "Giảm dần"));
            result.Add(new KeyValuePair<string, string>("asc", "Tăng dần"));
            return result;
        }
        
    }
}
