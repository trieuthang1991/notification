using NotificationAPI.Models;
using NotificationAPI.Services.Snapshot;
using Xunit;

namespace NotificationAPI.Tests;

public class ConfigSnapshotTests
{
    private static NotificationConfig C(string id, long lu) => new() { Id = id, LastUpdated = lu };

    [Fact]
    public void Chua_nap_thi_khong_san_sang()
    {
        var s = new ConfigSnapshot(() => 1000);
        Assert.False(s.IsReady);
        Assert.False(s.IsFresh(120));
        Assert.Empty(s.Items);
    }

    [Fact]
    public void ReplaceAll_san_sang_va_cap_nhat_watermark()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10), C("b", 30) });
        Assert.True(s.IsReady);
        Assert.Equal(2, s.Items.Count);
        Assert.Equal(30, s.WatermarkLastUpdated);
        Assert.True(s.IsFresh(120));
    }

    [Fact]
    public void ReplaceAll_bo_doc_khong_con_trong_lan_nap_moi()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10), C("b", 20) });
        s.ReplaceAll(new[] { C("a", 10) });
        Assert.Single(s.Items);
    }

    [Fact]
    public void Merge_ban_moi_hon_thang_ban_cu_hon_bi_bo_qua()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 50) });
        s.Merge(new[] { C("a", 40), C("b", 60) });
        var a = s.Items.Single(x => x.Id == "a");
        Assert.Equal(50, a.LastUpdated);
        Assert.Equal(2, s.Items.Count);
        Assert.Equal(60, s.WatermarkLastUpdated);

        s.Merge(new[] { C("a", 70) });
        Assert.Equal(70, s.Items.Single(x => x.Id == "a").LastUpdated);
    }

    [Fact]
    public void Upsert_va_Remove_ghi_xuyen_cuc_bo()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10) });
        s.Upsert(C("b", 20));
        Assert.Equal(2, s.Items.Count);
        s.Remove("a");
        Assert.Equal(new[] { "b" }, s.Items.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void Upsert_truoc_khi_nap_lan_dau_khong_lam_snapshot_san_sang()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.Upsert(C("a", 10));
        Assert.False(s.IsReady);
    }

    [Fact]
    public void IsFresh_het_han_theo_thoi_gian_nap_thanh_cong_cuoi()
    {
        long now = 1000;
        var s = new ConfigSnapshot(() => now);
        s.ReplaceAll(new[] { C("a", 10) });
        now = 1100;
        Assert.True(s.IsFresh(120));
        now = 1121;
        Assert.False(s.IsFresh(120));
        s.Merge(Array.Empty<NotificationConfig>()); // refresh delta thành công cũng làm "tươi" lại
        Assert.True(s.IsFresh(120));
    }

    [Fact]
    public void FullLoadAgeSeconds_chi_tinh_theo_lan_nap_day_du()
    {
        long now = 1000;
        var s = new ConfigSnapshot(() => now);
        s.ReplaceAll(new[] { C("a", 10) });
        now = 1050;
        s.Merge(Array.Empty<NotificationConfig>());
        Assert.Equal(50, s.FullLoadAgeSeconds);
    }

    [Fact]
    public void Bo_qua_doc_khong_co_id()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("", 10), C("a", 10) });
        Assert.Single(s.Items);
    }
}
