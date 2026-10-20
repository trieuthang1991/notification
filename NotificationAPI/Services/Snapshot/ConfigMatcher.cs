using NotificationAPI.Enums;
using NotificationAPI.Models;

namespace NotificationAPI.Services.Snapshot
{
    /// <summary>
    /// Lọc config trong bộ nhớ, giữ đúng ngữ nghĩa của hai câu N1QL trong NotificationCB
    /// (GetByDomainActiveAsync và QueryMaxLastUpdatedAsync). Hàm thuần, không I/O.
    /// </summary>
    public static class ConfigMatcher
    {
        private const string All = Utils.Common.All;

        /// <summary>
        /// Các config đang hoạt động khớp phạm vi, sắp xếp theo ord rồi id. Trả về BẢN SAO.
        /// </summary>
        public static List<NotificationConfig> MatchActive(
            IEnumerable<NotificationConfig> configs,
            string? domain,
            DeviceType device,
            string? userId,
            IReadOnlyCollection<string>? triggers,
            long nowUnix)
        {
            return configs
                .Where(n => n.Status == StatusNotification.Active
                            && n.StartDate <= nowUnix
                            && n.EndDate >= nowUnix
                            && MatchesScope(n, domain, device, userId, triggers))
                .OrderBy(n => n.Order)
                .ThenBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => n.CloneForResponse())
                .ToList();
        }

        /// <summary>
        /// MAX(last_updated) của mọi config khớp phạm vi (không lọc status/ngày, giống query gốc).
        /// </summary>
        public static long MaxLastUpdated(
            IEnumerable<NotificationConfig> configs,
            string? domain,
            DeviceType device,
            string? userId,
            IReadOnlyCollection<string>? triggers)
        {
            long max = 0;
            foreach (var n in configs)
            {
                if (n.LastUpdated > max && MatchesScope(n, domain, device, userId, triggers))
                    max = n.LastUpdated;
            }
            return max;
        }

        private static bool MatchesScope(
            NotificationConfig n,
            string? domain,
            DeviceType device,
            string? userId,
            IReadOnlyCollection<string>? triggers)
        {
            // Domain
            var domains = n.Domains;
            if (domains == null) return false;
            var domainOk = !string.IsNullOrEmpty(domain)
                ? domains.Any(d => d == domain || d == All)
                : domains.Any(d => d == All);
            if (!domainOk) return false;

            // Device (device == All thì không lọc)
            if (device != DeviceType.All)
            {
                var devices = n.DeviceTypes;
                if (devices == null || !devices.Any(dt => dt == device || dt == DeviceType.All)) return false;
            }

            // User
            var userOk = !string.IsNullOrEmpty(userId)
                ? (n.UserId == userId || n.UserId == All)
                : n.UserId == All;
            if (!userOk) return false;

            // Trigger
            var tas = n.TriggerActions;
            if (tas == null) return false;
            if (triggers != null && triggers.Count > 0)
                return tas.Any(ta => ta == All || triggers.Contains(ta));
            return tas.Any(ta => ta == All);
        }
    }
}
