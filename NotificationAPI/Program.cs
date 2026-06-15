using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using NotificationAPI.Areas.Admin;
using NotificationAPI.Config;
using NotificationAPI.Extensions;
using NotificationAPI.Services;
using NotificationAPI.Services.Couchbase;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

// Cấu hình Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // hoặc Debug nếu bạn cần nhiều hơn
    .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal) // ⚠️ Tắt log hệ thống Microsoft
    .MinimumLevel.Override("System", LogEventLevel.Fatal)    // ⚠️ Tắt log từ thư viện nền .NET
    .Enrich.FromLogContext()
    .WriteTo.File("logs/notification-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting Notification API"); // ✅ Log thủ công — OK

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Sử dụng Serilog cho logging
    builder.Host.UseSerilog();

// Add services to the container.

// Thêm hỗ trợ cho MVC với Areas
builder.Services.AddControllersWithViews(options => {
    options.Conventions.Add(new AdminAreaConvention());
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký cấu hình cache và Couchbase
builder.Services.AddCacheConfiguration(builder.Configuration);

// Đăng ký dịch vụ xác thực API key
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

// Đăng ký dịch vụ quản lý người dùng
builder.Services.AddScoped<IUserService, UserService>();

// Thêm xác thực với cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;
        options.Cookie.Name = "NotificationAPI.Auth";
        options.Cookie.HttpOnly = true;
    });

// Thêm CORS cho phép tất cả các nguồn gốc
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Thêm hỗ trợ cho static files (CSS, JS, images)
app.UseStaticFiles();

// Thêm hỗ trợ cho routing
app.UseRouting();

// Sử dụng CORS
app.UseCors("AllowAll");

// Thêm middleware xác thực và phân quyền
app.UseAuthentication();
app.UseAuthorization();

// Cấu hình endpoints
app.UseEndpoints(endpoints =>
{
    // Admin area routes - đặt trước để ưu tiên cao hơn
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    // API routes - sử dụng MapControllers() để đăng ký các API controller có attribute [ApiController]
    endpoints.MapControllers();

    // Default route
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

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
