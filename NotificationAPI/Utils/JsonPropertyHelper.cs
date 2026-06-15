using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NotificationAPI.Utils
{
    /// <summary>
    /// Lớp tiện ích để làm việc với JsonProperty
    /// </summary>
    public static class JsonPropertyHelper
    {
        /// <summary>
        /// Lấy tên thuộc tính JSON từ tên thuộc tính C#
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="propertyName">Tên thuộc tính C#</param>
        /// <returns>Tên thuộc tính JSON hoặc null nếu không tìm thấy</returns>
        public static string GetJsonPropertyName<T>(string propertyName)
        {
            var property = typeof(T).GetProperty(propertyName);
            if (property == null)
                return null;

            var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
            return attribute?.PropertyName ?? propertyName;
        }

        /// <summary>
        /// Lấy tên thuộc tính JSON từ biểu thức lambda
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <typeparam name="TProperty">Kiểu thuộc tính</typeparam>
        /// <param name="propertyExpression">Biểu thức lambda trỏ đến thuộc tính</param>
        /// <returns>Tên thuộc tính JSON hoặc null nếu không tìm thấy</returns>
        public static string GetJsonPropertyName<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            var memberExpression = propertyExpression.Body as MemberExpression;
            if (memberExpression == null)
                return null;

            var propertyName = memberExpression.Member.Name;
            return GetJsonPropertyName<T>(propertyName);
        }

        /// <summary>
        /// Lấy tên thuộc tính JSON từ biểu thức lambda (phiên bản rút gọn)
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="propertyExpression">Biểu thức lambda trỏ đến thuộc tính</param>
        /// <returns>Tên thuộc tính JSON</returns>
        public static string JsonName<T>(Expression<Func<T, object>> propertyExpression)
        {
            MemberExpression memberExpression;

            // Xử lý trường hợp biểu thức lambda có chuyển đổi kiểu (boxing)
            if (propertyExpression.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else
            {
                memberExpression = propertyExpression.Body as MemberExpression;
            }

            if (memberExpression == null)
                throw new ArgumentException("Biểu thức không phải là thuộc tính", nameof(propertyExpression));

            var propertyName = memberExpression.Member.Name;
            var property = typeof(T).GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException($"Không tìm thấy thuộc tính {propertyName}", nameof(propertyExpression));

            var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
            return attribute?.PropertyName ?? propertyName;
        }

        /// <summary>
        /// Lấy tất cả các tên thuộc tính JSON của một lớp
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <returns>Dictionary chứa tên thuộc tính C# và tên thuộc tính JSON tương ứng</returns>
        public static Dictionary<string, string> GetAllJsonPropertyNames<T>()
        {
            var result = new Dictionary<string, string>();
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (attribute != null)
                {
                    result.Add(property.Name, attribute.PropertyName);
                }
                else
                {
                    result.Add(property.Name, property.Name);
                }
            }

            return result;
        }

        /// <summary>
        /// Lấy tên thuộc tính C# từ tên thuộc tính JSON
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="jsonPropertyName">Tên thuộc tính JSON</param>
        /// <returns>Tên thuộc tính C# hoặc null nếu không tìm thấy</returns>
        public static string GetCSharpPropertyName<T>(string jsonPropertyName)
        {
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (attribute != null && attribute.PropertyName == jsonPropertyName)
                {
                    return property.Name;
                }
            }

            // Nếu không tìm thấy, kiểm tra xem có thuộc tính C# nào trùng với tên JSON không
            var matchingProperty = properties.FirstOrDefault(p => p.Name == jsonPropertyName);
            return matchingProperty?.Name;
        }

        /// <summary>
        /// Tạo truy vấn N1QL với các tên trường JSON
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="fieldNames">Danh sách tên trường C#</param>
        /// <returns>Chuỗi truy vấn N1QL với các tên trường JSON</returns>
        public static string CreateN1qlSelectFields<T>(params string[] fieldNames)
        {
            if (fieldNames == null || fieldNames.Length == 0)
                return "*";

            var jsonNames = new List<string>();
            foreach (var fieldName in fieldNames)
            {
                var jsonName = GetJsonPropertyName<T>(fieldName);
                if (!string.IsNullOrEmpty(jsonName))
                {
                    jsonNames.Add($"{jsonName}");
                }
                else
                {
                    jsonNames.Add(fieldName);
                }
            }

            return string.Join(", ", jsonNames);
        }

        /// <summary>
        /// Tạo truy vấn N1QL với các tên trường JSON sử dụng biểu thức lambda
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="propertyExpressions">Danh sách biểu thức lambda trỏ đến các thuộc tính</param>
        /// <returns>Chuỗi truy vấn N1QL với các tên trường JSON</returns>
        public static string CreateN1qlSelectFields<T>(params Expression<Func<T, object>>[] propertyExpressions)
        {
            if (propertyExpressions == null || propertyExpressions.Length == 0)
                return "*";

            var jsonNames = new List<string>();
            foreach (var expression in propertyExpressions)
            {
                var jsonName = JsonName<T>(expression);
                if (!string.IsNullOrEmpty(jsonName))
                {
                    jsonNames.Add($"{jsonName}");
                }
            }

            return string.Join(", ", jsonNames);
        }

        /// <summary>
        /// Tạo điều kiện WHERE cho truy vấn N1QL
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="fieldName">Tên trường C#</param>
        /// <param name="paramName">Tên tham số</param>
        /// <returns>Chuỗi điều kiện WHERE với tên trường JSON</returns>
        public static string CreateN1qlWhereCondition<T>(string fieldName, string paramName)
        {
            var jsonName = GetJsonPropertyName<T>(fieldName);
            if (!string.IsNullOrEmpty(jsonName))
            {
                return $"{jsonName} = ${paramName}";
            }
            else
            {
                return $"{fieldName} = ${paramName}";
            }
        }

        /// <summary>
        /// Tạo điều kiện WHERE cho truy vấn N1QL sử dụng biểu thức lambda
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="propertyExpression">Biểu thức lambda trỏ đến thuộc tính</param>
        /// <param name="paramName">Tên tham số</param>
        /// <returns>Chuỗi điều kiện WHERE với tên trường JSON</returns>
        public static string CreateN1qlWhereCondition<T>(Expression<Func<T, object>> propertyExpression, string paramName)
        {
            var jsonName = JsonName<T>(propertyExpression);
            if (!string.IsNullOrEmpty(jsonName))
            {
                return $"{jsonName} = ${paramName}";
            }
            else
            {
                throw new ArgumentException("Không thể lấy tên trường JSON", nameof(propertyExpression));
            }
        }

        /// <summary>
        /// Tạo điều kiện WHERE cho truy vấn N1QL với toán tử
        /// </summary>
        /// <typeparam name="T">Kiểu đối tượng</typeparam>
        /// <param name="propertyExpression">Biểu thức lambda trỏ đến thuộc tính</param>
        /// <param name="paramName">Tên tham số</param>
        /// <param name="operator">Toán tử (=, >, <, >=, <=, LIKE, ...)</param>
        /// <returns>Chuỗi điều kiện WHERE với tên trường JSON</returns>
        public static string CreateN1qlWhereCondition<T>(Expression<Func<T, object>> propertyExpression, string paramName, string @operator)
        {
            var jsonName = JsonName<T>(propertyExpression);
            if (!string.IsNullOrEmpty(jsonName))
            {
                return $"{jsonName} {@operator} ${paramName}";
            }
            else
            {
                throw new ArgumentException("Không thể lấy tên trường JSON", nameof(propertyExpression));
            }
        }
    }
}
