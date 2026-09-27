using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class IpAddressTextTests
{
    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10")]
    [InlineData("  203.0.113.10\n", "203.0.113.10")]
    [InlineData("\"198.51.100.8\"", "198.51.100.8")]
    [InlineData("2001:db8::10\r\n", "2001:db8::10")]
    [InlineData("8.8.8.8 extra", "8.8.8.8")]
    public void Parses_a_host_address(string body, string expected)
    {
        Assert.True(IpAddressText.TryParse(body, out var address));
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an ip")]
    [InlineData("<html>203.0.113.10</html>")]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.0.1")]
    [InlineData("100.64.1.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    public void Rejects_values_that_are_not_an_external_host(string? body)
    {
        Assert.False(IpAddressText.TryParse(body, out _));
    }
}
