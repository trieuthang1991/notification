using NotificationAPI.Models;
using Xunit;

namespace NotificationAPI.Tests;

public class SmokeTests
{
    [Fact]
    public void NotificationConfig_CanBeConstructed()
    {
        var cfg = new NotificationConfig { Id = "x" };
        Assert.Equal("x", cfg.Id);
        Assert.True(cfg.StartDate > 0); // default dùng XMUtility → chứng minh DLL load được
    }
}
