using NotificationAPI.Utils;
using Xunit;

namespace NotificationAPI.Tests;

public class InputGuardTests
{
    [Theory]
    [InlineData("vn.joboko.com", "vn.joboko.com")]
    [InlineData("  VN.Joboko.COM ", "vn.joboko.com")]
    [InlineData("localhost:5201", "localhost:5201")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Domain_hop_le_duoc_chuan_hoa(string? input, string expected)
    {
        Assert.True(InputGuard.TryNormalizeDomain(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("x'] END OR 1=1 --")]
    [InlineData("a.com/../b")]
    [InlineData("a b.com")]
    [InlineData("a.com;ls")]
    [InlineData("$(whoami).com")]
    [InlineData("a.com\nb")]
    [InlineData("-a.com")]
    [InlineData("a.com:99999999")]
    public void Domain_khong_hop_le_bi_tu_choi(string input)
    {
        Assert.False(InputGuard.TryNormalizeDomain(input, out _));
    }

    [Fact]
    public void Domain_port_chu_so_unicode_bi_tu_choi()
    {
        Assert.False(InputGuard.TryNormalizeDomain("a.com:١٢٣", out _));
    }

    [Fact]
    public void Domain_co_xuong_dong_cuoi_duoc_trim_thanh_gia_tri_sach()
    {
        // Trim loại ký tự xuống dòng cuối. Caller PHẢI dùng giá trị normalized, không dùng input gốc.
        Assert.True(InputGuard.TryNormalizeDomain("a.com\n", out var normalized));
        Assert.Equal("a.com", normalized);
    }

    [Fact]
    public void Domain_qua_dai_bi_tu_choi()
    {
        Assert.False(InputGuard.TryNormalizeDomain(new string('a', 300) + ".com", out _));
    }

    [Theory]
    [InlineData("747114")]
    [InlineData("all")]
    [InlineData("com_599076")]
    [InlineData("abc-1.2:3")]
    public void Token_hop_le(string input) => Assert.True(InputGuard.IsValidToken(input));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("a'b")]
    [InlineData("a b")]
    [InlineData("abc\n")]
    [InlineData("a;b")]
    public void Token_khong_hop_le(string? input) => Assert.False(InputGuard.IsValidToken(input));

    [Fact]
    public void AreValidTokens_bo_qua_phan_tu_rong_nhung_bat_phan_tu_xau()
    {
        Assert.True(InputGuard.AreValidTokens(new[] { "a", "", "b" }));
        Assert.False(InputGuard.AreValidTokens(new[] { "a", "b'c" }));
        Assert.True(InputGuard.AreValidTokens(Array.Empty<string>()));
    }
}
