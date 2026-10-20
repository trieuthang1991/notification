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
    public void Watermark_khong_bi_Upsert_cuc_bo_day_len()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10) });
        s.Upsert(C("b", 500));
        Assert.Equal(10, s.WatermarkLastUpdated);
    }

    [Fact]
    public void Watermark_theo_Merge_va_khong_giam()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10) });
        s.Merge(new[] { C("c", 60) });
        Assert.Equal(60, s.WatermarkLastUpdated);
        s.Merge(new[] { C("d", 20) });
        Assert.Equal(60, s.WatermarkLastUpdated);
    }

    [Fact]
    public void Watermark_khong_bi_Remove_thay_doi()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("a", 10), C("b", 30) });
        s.Remove("b");
        Assert.Equal(30, s.WatermarkLastUpdated);
    }

    [Fact]
    public void Merge_last_updated_bang_nhau_thi_ban_moi_thay_ban_cu()
    {
        var s = new ConfigSnapshot(() => 1000);
        var cu = C("a", 50); cu.Content = "cu";
        var moi = C("a", 50); moi.Content = "moi";
        s.ReplaceAll(new[] { cu });
        s.Merge(new[] { moi });
        Assert.Equal("moi", s.Items.Single().Content);
    }

    [Fact]
    public void Merge_va_Remove_truoc_khi_nap_lan_dau_khong_ne_loi_va_khong_san_sang()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.Merge(new[] { C("a", 10) });
        s.Remove("a");
        Assert.False(s.IsReady);
        Assert.Empty(s.Items);
    }

    [Fact]
    public void FullLoadAgeSeconds_bang_0_khi_chua_nap()
    {
        var s = new ConfigSnapshot(() => 1000);
        Assert.Equal(0, s.FullLoadAgeSeconds);
    }

    [Fact]
    public void Bo_qua_doc_khong_co_id()
    {
        var s = new ConfigSnapshot(() => 1000);
        s.ReplaceAll(new[] { C("", 10), C("a", 10) });
        Assert.Single(s.Items);
    }
}
