using System.Text.RegularExpressions;

namespace NotificationAPI.Utils
{
    /// <summary>
    /// Kiểm tra định dạng đầu vào do client gửi trước khi đưa vào query hoặc key Couchbase.
    /// Kiểm tra theo ĐỊNH DẠNG (không dùng whitelist domain) để domain mới vẫn dùng được.
    /// </summary>
    public static class InputGuard
    {
        // \z (không dùng $) để từ chối chuỗi có ký tự xuống dòng ở cuối.
        private static readonly Regex DomainRegex =
            new(@"^[a-z0-9]([a-z0-9.\-]{0,251}[a-z0-9])?(:[0-9]{1,5})?\z", RegexOptions.Compiled);

        private static readonly Regex TokenRegex =
            new(@"^[A-Za-z0-9_\-.:]{1,100}\z", RegexOptions.Compiled);

        /// <summary>
        /// Chuẩn hóa (trim + chữ thường) và kiểm tra domain. Rỗng được chấp nhận (nghĩa là chỉ match 'all').
        /// </summary>
        public static bool TryNormalizeDomain(string? input, out string normalized)
        {
            normalized = (input ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0) return true;
            return normalized.Length <= 260 && DomainRegex.IsMatch(normalized);
        }

        /// <summary>
        /// Kiểm tra token đơn (userId, notificationId, triggerAction). Không chấp nhận rỗng.
        /// </summary>
        public static bool IsValidToken(string? input)
            => !string.IsNullOrEmpty(input) && TokenRegex.IsMatch(input);

        /// <summary>
        /// Kiểm tra danh sách token; phần tử rỗng (vd "a,,b") được bỏ qua, phần tử xấu thì từ chối.
        /// </summary>
        public static bool AreValidTokens(IEnumerable<string> items)
            => items.All(i => string.IsNullOrEmpty(i) || TokenRegex.IsMatch(i));
    }
}
