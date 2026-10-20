using NotificationAPI.Enums;
using NotificationAPI.Models;
using NotificationAPI.Services.Snapshot;
using Xunit;

namespace NotificationAPI.Tests;

public class ConfigMatcherTests
{
    private const long Now = 1_800_000_000;

    private static NotificationConfig Cfg(
        string id,
        string[]? domains = null,
        DeviceType[]? devices = null,
        string userId = "all",
        string[]? triggers = null,
        StatusNotification status = StatusNotification.Active,
        long start = Now - 100,
        long end = Now + 100,
        int ord = 100,
        long lastUpdated = 10)
        => new()
        {
            Id = id,
            Domains = (domains ?? new[] { "all" }).ToList(),
            DeviceTypes = (devices ?? new[] { DeviceType.All }).ToList(),
            UserId = userId,
            TriggerActions = (triggers ?? new[] { "all" }).ToList(),
            Status = status,
            StartDate = start,
            EndDate = end,
            Order = ord,
            LastUpdated = lastUpdated,
            Attributes = new List<int>()
        };

    private static List<string> Ids(IEnumerable<NotificationConfig> l) => l.Select(x => x.Id).ToList();

    [Fact]
    public void Chi_lay_status_active_va_trong_khoang_ngay()
    {
        var list = new[]
        {
            Cfg("ok"),
            Cfg("inactive", status: StatusNotification.Inactive),
            Cfg("success", status: StatusNotification.Success),
            Cfg("chua-bat-dau", start: Now + 10),
            Cfg("het-han", end: Now - 10),
        };
        var r = ConfigMatcher.MatchActive(list, "vn.joboko.com", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "ok" }, Ids(r));
    }

    [Fact]
    public void Domain_khop_hoac_all()
    {
        var list = new[]
        {
            Cfg("all"),
            Cfg("vn", domains: new[] { "vn.joboko.com" }),
            Cfg("em", domains: new[] { "em-vn.joboko.com" }),
        };
        var r = ConfigMatcher.MatchActive(list, "vn.joboko.com", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "all", "vn" }, Ids(r).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Domain_rong_chi_khop_all()
    {
        var list = new[] { Cfg("all"), Cfg("vn", domains: new[] { "vn.joboko.com" }) };
        var r = ConfigMatcher.MatchActive(list, "", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "all" }, Ids(r));
    }

    [Fact]
    public void Device_khac_all_khop_dung_hoac_all_con_device_all_khong_loc()
    {
        var list = new[]
        {
            Cfg("all-dev"),
            Cfg("web", devices: new[] { DeviceType.Website }),
            Cfg("mobile", devices: new[] { DeviceType.Mobile }),
        };
        var web = ConfigMatcher.MatchActive(list, "d", DeviceType.Website, "all", null, Now);
        Assert.Equal(new[] { "all-dev", "web" }, Ids(web).OrderBy(x => x).ToArray());

        var any = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(3, any.Count);
    }

    [Fact]
    public void User_khop_dung_hoac_all()
    {
        var list = new[] { Cfg("all"), Cfg("u1", userId: "u1"), Cfg("u2", userId: "u2") };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "u1", null, Now);
        Assert.Equal(new[] { "all", "u1" }, Ids(r).OrderBy(x => x).ToArray());

        var anon = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "", null, Now);
        Assert.Equal(new[] { "all" }, Ids(anon));
    }

    [Fact]
    public void Trigger_chon_dung_recruiter_va_all()
    {
        var list = new[]
        {
            Cfg("chung"),
            Cfg("rec1", triggers: new[] { "com_1" }),
            Cfg("rec2", triggers: new[] { "com_2" }),
        };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", new[] { "com_1" }, Now);
        Assert.Equal(new[] { "chung", "rec1" }, Ids(r).OrderBy(x => x).ToArray());

        var none = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", Array.Empty<string>(), Now);
        Assert.Equal(new[] { "chung" }, Ids(none));
    }

    [Fact]
    public void So_sanh_phan_biet_hoa_thuong()
    {
        var list = new[] { Cfg("c", triggers: new[] { "COM_1" }) };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", new[] { "com_1" }, Now);
        Assert.Empty(r);
    }

    [Fact]
    public void Sap_xep_theo_ord_roi_theo_id()
    {
        var list = new[] { Cfg("b", ord: 5), Cfg("a", ord: 5), Cfg("z", ord: 1) };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "z", "a", "b" }, Ids(r).ToArray());
    }

    [Fact]
    public void Ket_qua_la_ban_sao_khong_dung_chung_attributes()
    {
        var src = Cfg("a");
        src.Attributes = new List<int> { 2 };
        var r = ConfigMatcher.MatchActive(new[] { src }, "d", DeviceType.All, "all", null, Now);
        r[0].Attributes.Add(1);
        r[0].Content = "da-sua";
        Assert.Equal(new List<int> { 2 }, src.Attributes);
        Assert.NotEqual("da-sua", src.Content);
    }

    [Fact]
    public void MaxLastUpdated_tinh_ca_doc_khong_active_nhung_khong_tinh_ngoai_pham_vi()
    {
        var list = new[]
        {
            Cfg("a", lastUpdated: 100),
            Cfg("tat", status: StatusNotification.Inactive, lastUpdated: 500),
            Cfg("khac-domain", domains: new[] { "khac.com" }, lastUpdated: 900),
        };
        var max = ConfigMatcher.MaxLastUpdated(list, "vn.joboko.com", DeviceType.All, "all", null);
        Assert.Equal(500, max);
    }

    [Fact]
    public void Bien_ngay_bang_now_van_duoc_chon()
    {
        var list = new[] { Cfg("bd", start: Now), Cfg("kt", end: Now) };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void Danh_sach_rong_khong_khop()
    {
        var list = new List<NotificationConfig>
        {
            Cfg("dom-rong", domains: Array.Empty<string>()),
            Cfg("trg-rong", triggers: Array.Empty<string>()),
            Cfg("dev-rong", devices: Array.Empty<DeviceType>()),
        };
        // device=All: chỉ domain/trigger rỗng bị loại, dev-rong vẫn khớp (không lọc device)
        var all = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "dev-rong" }, Ids(all));
        // device=Website: dev-rong cũng bị loại
        var web = ConfigMatcher.MatchActive(list, "d", DeviceType.Website, "all", null, Now);
        Assert.Empty(web);
    }

    [Fact]
    public void Danh_sach_null_khong_khop_va_khong_nem_loi()
    {
        var a = Cfg("a"); a.Domains = null!;
        var b = Cfg("b"); b.TriggerActions = null!;
        var c = Cfg("c"); c.DeviceTypes = null!;
        var list = new[] { a, b, c };
        var web = ConfigMatcher.MatchActive(list, "d", DeviceType.Website, "all", null, Now);
        Assert.Empty(web);
        var any = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "c" }, Ids(any));
    }

    [Fact]
    public void Client_gui_userId_all_chi_khop_config_user_all()
    {
        var list = new[] { Cfg("all"), Cfg("u1", userId: "u1") };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "all" }, Ids(r));
    }

    [Fact]
    public void Trigger_client_chua_san_all_van_khop_config_all()
    {
        var list = new[] { Cfg("chung"), Cfg("rec1", triggers: new[] { "com_1" }) };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", new[] { "all" }, Now);
        Assert.Equal(new[] { "chung" }, Ids(r));
    }

    [Fact]
    public void Domain_va_userId_phan_biet_hoa_thuong()
    {
        var list = new[]
        {
            Cfg("d", domains: new[] { "VN.joboko.com" }),
            Cfg("u", userId: "UserA"),
        };
        Assert.Empty(ConfigMatcher.MatchActive(list, "vn.joboko.com", DeviceType.All, "usera", null, Now));
        var r = ConfigMatcher.MatchActive(list, "VN.joboko.com", DeviceType.All, "UserA", null, Now);
        Assert.Equal(new[] { "d", "u" }, Ids(r).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Sap_xep_Ordinal_chu_hoa_truoc_chu_thuong()
    {
        var list = new[] { Cfg("a", ord: 5), Cfg("B", ord: 5) };
        var r = ConfigMatcher.MatchActive(list, "d", DeviceType.All, "all", null, Now);
        Assert.Equal(new[] { "B", "a" }, Ids(r).ToArray());
    }

    [Fact]
    public void MaxLastUpdated_tinh_ca_doc_het_han()
    {
        var list = new[] { Cfg("a", lastUpdated: 100), Cfg("het-han", end: Now - 10, lastUpdated: 700) };
        Assert.Equal(700, ConfigMatcher.MaxLastUpdated(list, "d", DeviceType.All, "all", null));
    }

    [Fact]
    public void MaxLastUpdated_loc_theo_device_userId_trigger()
    {
        var list = new[]
        {
            Cfg("base", lastUpdated: 100),
            Cfg("mobile", devices: new[] { DeviceType.Mobile }, lastUpdated: 200),
            Cfg("u2", userId: "u2", lastUpdated: 300),
            Cfg("rec2", triggers: new[] { "com_2" }, lastUpdated: 400),
        };
        Assert.Equal(100, ConfigMatcher.MaxLastUpdated(list, "d", DeviceType.Website, "u1", new[] { "com_1" }));
        Assert.Equal(200, ConfigMatcher.MaxLastUpdated(list, "d", DeviceType.Mobile, "u1", new[] { "com_1" }));
        Assert.Equal(300, ConfigMatcher.MaxLastUpdated(list, "d", DeviceType.Website, "u2", new[] { "com_1" }));
        Assert.Equal(400, ConfigMatcher.MaxLastUpdated(list, "d", DeviceType.Website, "u1", new[] { "com_2" }));
    }

    [Fact]
    public void MaxLastUpdated_khong_co_doc_nao_tra_ve_0()
    {
        Assert.Equal(0, ConfigMatcher.MaxLastUpdated(Array.Empty<NotificationConfig>(), "d", DeviceType.All, "all", null));
    }
}
