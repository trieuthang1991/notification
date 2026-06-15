# Hướng dẫn cấu hình và khắc phục sự cố Logging

## Vấn đề hiện tại

Hiện tại, hệ thống không hiển thị logs như mong đợi. Có một số nguyên nhân có thể dẫn đến vấn đề này:

## 1. Cấu hình IIS Express

Trong file cấu hình IIS Express, logging đang bị tắt. Để bật logging:

1. Mở file `.vs\NotificationAPI\config\applicationhost.config`
2. Tìm đến phần cấu hình logging:
   ```xml
   <!-- To enable logging, please change the below attribute "enabled" to "true" -->
   <logFile logFormat="W3C" directory="%AppData%\Microsoft\IISExpressLogs" enabled="false" />
   <traceFailedRequestsLogging directory="%AppData%\Microsoft" enabled="false" maxLogFileSizeKB="1024" />
   ```
3. Thay đổi `enabled="false"` thành `enabled="true"` cho cả hai dòng
4. Lưu file và khởi động lại IIS Express

## 2. Cấu hình Logging trong ASP.NET Core

Cấu hình logging hiện tại trong `appsettings.json`:

```json
"Logging": {
    "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
    }
}
```

Cấu hình này chỉ hiển thị:
- Log cấp độ Information trở lên cho các category mặc định
- Log cấp độ Warning trở lên cho các category thuộc Microsoft.AspNetCore

### Điều chỉnh cấu hình logging

Để hiển thị nhiều log hơn, bạn có thể điều chỉnh cấu hình trong `appsettings.json`:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Debug",
            "Microsoft.AspNetCore": "Information",
            "NotificationAPI": "Debug"
        },
        "Console": {
            "LogLevel": {
                "Default": "Debug",
                "Microsoft.AspNetCore": "Information",
                "NotificationAPI": "Debug"
            }
        },
        "File": {
            "Path": "logs/app.log",
            "Append": true,
            "FileSizeLimitBytes": 1000000,
            "MaxRollingFiles": 5
        }
    }
}
```

## 3. Thêm File Logging

ASP.NET Core mặc định không hỗ trợ ghi log ra file. Để thêm tính năng này, bạn cần:

1. Thêm package NuGet `Serilog.AspNetCore` và `Serilog.Sinks.File`:

```
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
```

2. Cập nhật file `Program.cs` để cấu hình Serilog:

```csharp
using Serilog;
using Serilog.Events;

// Cấu hình Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/notification-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting web application");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Thêm Serilog vào ứng dụng
    builder.Host.UseSerilog();
    
    // Các cấu hình khác...
    
    var app = builder.Build();
    
    // Các middleware...
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

## 4. Kiểm tra các log trong JavaScript client

Trong file `notification-client.js`, có một số phương thức debug:

```javascript
NotificationClient.prototype._debug = function (message, data) {
    if (this.config.debug) {
        if (data) {
            console.log('DEBUG -', message, data);
        } else {
            console.log('DEBUG -', message);
        }
    }
};
```

Để bật debug logs trong JavaScript client, đảm bảo cấu hình client có `debug: true`:

```javascript
var notificationClient = new NotificationClient({
    // Các cấu hình khác
    debug: true
});
```

## 5. Kiểm tra các log trong NotificationCB.cs

Trong file `NotificationCB.cs`, có nhiều phương thức sử dụng logger:

```csharp
_logger.LogInformation("Lấy thông báo theo ID: {Id}", id);
```

Đảm bảo rằng:
1. Logger được inject đúng cách
2. Cấu hình logging cho category `NotificationAPI.Services.Couchbase.NotificationCB` đã được thiết lập đúng

## 6. Tạo thư mục logs

Đảm bảo thư mục `logs` đã được tạo trong thư mục gốc của ứng dụng và ứng dụng có quyền ghi vào thư mục này.

## 7. Kiểm tra Event Viewer

Đối với các ứng dụng chạy trên Windows, bạn cũng có thể kiểm tra Event Viewer để xem các log của ứng dụng:

1. Mở Event Viewer (eventvwr.msc)
2. Điều hướng đến Windows Logs > Application
3. Tìm các sự kiện liên quan đến ứng dụng của bạn

## Kết luận

Sau khi thực hiện các bước trên, hệ thống sẽ ghi log đầy đủ hơn, giúp dễ dàng theo dõi và gỡ lỗi. Nếu vẫn gặp vấn đề, hãy kiểm tra:

1. Quyền truy cập file và thư mục
2. Cấu hình logging trong các môi trường khác nhau (Development, Production)
3. Các lỗi trong quá trình khởi động ứng dụng
