using NotificationAPI.Models;
using NotificationAPI.Services.Snapshot;
using NotificationAPI.Utils;
using Xunit;

namespace NotificationAPI.Tests;

public class SnapshotItemTests
{
    private static NotificationConfig Make(int contentLength, params int[] attrs) => new()
    {
        Id = "a",
        Content = new string('x', contentLength),
        Attributes = new List<int>(attrs)
    };

    // Mô phỏng đoạn xử lý ở GetActiveNotificationsAsync (giữ nguyên hành vi đường N1QL).
    private static (string Content, List<int> Attributes) XuLySau(NotificationConfig n)
    {
        var content = n.Content;
        var attributes = new List<int>(n.Attributes);
        if (attributes.Contains(1)) content = string.Empty;
        if (!string.IsNullOrEmpty(content) && content.Length > Common.MaxInlineContentChars)
        {
            content = string.Empty;
            attributes.Add(1);
            attributes = attributes.Distinct().ToList();
        }
        return (content, attributes);
    }

    [Fact]
    public void Content_ngan_giu_nguyen()
    {
        var item = ConfigSnapshot.ToSnapshotItem(Make(10, 2));
        Assert.Equal(10, item.Content.Length);
        Assert.Equal(new List<int> { 2 }, item.Attributes);
    }

    [Fact]
    public void Content_bang_nguong_giu_nguyen()
    {
        var item = ConfigSnapshot.ToSnapshotItem(Make(Common.MaxInlineContentChars));
        Assert.Equal(Common.MaxInlineContentChars, item.Content.Length);
        Assert.DoesNotContain(1, item.Attributes);
    }

    [Fact]
    public void Content_vuot_nguong_bi_cat_va_them_attribute_1()
    {
        var item = ConfigSnapshot.ToSnapshotItem(Make(Common.MaxInlineContentChars + 1, 2));
        Assert.Equal(string.Empty, item.Content);
        Assert.Equal(new List<int> { 2, 1 }, item.Attributes);
    }

    [Fact]
    public void Attribute_1_da_co_khong_bi_them_trung()
    {
        var item = ConfigSnapshot.ToSnapshotItem(Make(Common.MaxInlineContentChars + 1, 1));
        Assert.Equal(string.Empty, item.Content);
        Assert.Equal(new List<int> { 1 }, item.Attributes);
    }

    [Fact]
    public void Khong_sua_object_goc()
    {
        var original = Make(Common.MaxInlineContentChars + 1, 2);
        ConfigSnapshot.ToSnapshotItem(original);
        Assert.Equal(Common.MaxInlineContentChars + 1, original.Content.Length);
        Assert.Equal(new List<int> { 2 }, original.Attributes);
    }

    [Fact]
    public void Qua_doan_xu_ly_sau_tuong_duong_duong_N1QL()
    {
        foreach (var len in new[] { 0, 10, Common.MaxInlineContentChars, Common.MaxInlineContentChars + 1 })
        {
            var n1ql = XuLySau(Make(len, 2));
            var snap = XuLySau(ConfigSnapshot.ToSnapshotItem(Make(len, 2)));
            Assert.Equal(n1ql.Content, snap.Content);
            Assert.Equal(n1ql.Attributes.OrderBy(x => x), snap.Attributes.OrderBy(x => x));
        }
    }
}
