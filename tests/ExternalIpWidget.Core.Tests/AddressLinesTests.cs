using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class AddressLinesTests
{
    [Theory]
    [InlineData("203.0.113.10", "203.0", "113.10")]
    [InlineData("8.8.8.8", "8.8", "8.8")]
    [InlineData("1.2.3.4", "1.2", "3.4")]
    [InlineData("255.255.255.255", "255.255", "255.255")]
    public void Splits_an_ipv4_address_into_two_octet_pairs(string address, string top, string bottom)
    {
        var lines = AddressLines.From(address);

        Assert.Equal(top, lines.Top);
        Assert.Equal(bottom, lines.Bottom);
    }

    [Fact]
    public void Keeps_a_placeholder_on_one_line()
    {
        var lines = AddressLines.From("…");

        Assert.Equal("…", lines.Top);
        Assert.False(lines.HasSecondLine);
    }

    [Fact]
    public void Splits_an_ipv6_address_so_the_label_stays_short()
    {
        var lines = AddressLines.From("2001:db8::10");

        Assert.Equal("2001", lines.Top);
        Assert.Equal("db8::10", lines.Bottom);
    }
}
