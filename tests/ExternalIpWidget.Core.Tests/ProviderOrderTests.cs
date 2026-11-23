using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class ProviderOrderTests
{
    private static readonly IReadOnlyList<GeoIpProvider> Available =
    [
        new("a", "https://a.test/{ip}"),
        new("b", "https://b.test/{ip}"),
        new("c", "https://c.test/{ip}"),
    ];

    [Fact]
    public void Keeps_the_default_order_when_nothing_is_saved()
    {
        Assert.Equal(["a", "b", "c"], ProviderOrder.Names(null, Available));
        Assert.Equal(["a", "b", "c"], ProviderOrder.Names([], Available));
    }

    [Fact]
    public void Follows_the_saved_order()
    {
        var ordered = ProviderOrder.Arrange(["c", "a", "b"], Available);

        Assert.Equal(["c", "a", "b"], ordered.Select(provider => provider.Name));
        Assert.Same(Available[2], ordered[0]);
    }

    [Fact]
    public void Appends_services_missing_from_the_saved_order()
    {
        Assert.Equal(["b", "a", "c"], ProviderOrder.Names(["b"], Available));
    }

    [Fact]
    public void Skips_unknown_blank_and_repeated_names()
    {
        Assert.Equal(
            ["c", "a", "b"],
            ProviderOrder.Names(["removed", " C ", "", "c", "A"], Available));
    }

    [Fact]
    public void Default_services_are_all_reachable_by_name()
    {
        var publicIp = PublicIpLookup.DefaultProviders.Select(provider => provider.Name).Reverse().ToArray();
        var geoIp = GeoIpLookup.DefaultProviders.Select(provider => provider.Name).Reverse().ToArray();

        Assert.Equal(publicIp, ProviderOrder.Names(publicIp, PublicIpLookup.DefaultProviders));
        Assert.Equal(geoIp, ProviderOrder.Names(geoIp, GeoIpLookup.DefaultProviders));
    }
}
