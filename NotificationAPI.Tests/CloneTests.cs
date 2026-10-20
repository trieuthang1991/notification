using NotificationAPI.Models;
using Xunit;

namespace NotificationAPI.Tests;

public class CloneTests
{
    [Fact]
    public void Sua_ban_sao_khong_anh_huong_ban_goc()
    {
        var original = new NotificationConfig
        {
            Id = "a",
            Content = "noi dung",
            Attributes = new List<int> { 2 }
        };

        var clone = original.CloneForResponse();
        clone.Content = string.Empty;
        clone.Attributes.Add(1);

        Assert.Equal("noi dung", original.Content);
        Assert.Equal(new List<int> { 2 }, original.Attributes);
        Assert.Equal("a", clone.Id);
    }

    [Fact]
    public void Attributes_null_duoc_chuyen_thanh_list_rong()
    {
        var original = new NotificationConfig { Id = "a" };
        original.Attributes = null!;
        var clone = original.CloneForResponse();
        Assert.NotNull(clone.Attributes);
        Assert.Empty(clone.Attributes);
    }
}
