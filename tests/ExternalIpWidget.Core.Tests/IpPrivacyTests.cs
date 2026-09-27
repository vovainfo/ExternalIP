using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class IpPrivacyTests
{
    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.10")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("127.0.0.1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]
    public void Local_link_local_and_cgnat_are_not_public(string address)
    {
        Assert.True(IpPrivacy.IsNonPublic(address));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void Ordinary_internet_addresses_are_public(string address)
    {
        Assert.False(IpPrivacy.IsNonPublic(address));
    }
}
