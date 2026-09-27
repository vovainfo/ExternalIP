using System.Net;
using System.Net.Sockets;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class LiveLookupTests
{
    [Fact]
    public async Task External_service_returns_a_public_address()
    {
        using var http = PublicIpLookup.CreateHttpClient();
        var lookup = new PublicIpLookup(http);

        var result = await lookup.GetAsync();

        Assert.True(IpAddressText.TryParse(result.Address, out var parsed));
        Assert.Equal(parsed, result.Address);
        Assert.Contains(PublicIpLookup.DefaultProviders, provider => provider.Name == result.ProviderName);
        Assert.False(IpPrivacy.IsNonPublic(result.Address));
        Assert.Equal(AddressFamily.InterNetwork, IPAddress.Parse(result.Address).AddressFamily);

        var nic = LocalNicAddress.TryGetOutbound();
        if (nic is not null && IpPrivacy.IsNonPublic(nic))
            Assert.NotEqual(nic, result.Address);
    }

    [Fact]
    public void Outbound_nic_lookup_returns_a_host_address_or_nothing()
    {
        var nic = LocalNicAddress.TryGetOutbound();
        if (nic is null)
            return;

        Assert.True(IPAddress.TryParse(nic, out var parsed));
        Assert.Equal(parsed.ToString(), nic);
    }
}
