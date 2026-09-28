using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class GeoRefreshPolicyTests
{
    [Fact]
    public void Queries_when_the_place_is_not_known_yet()
    {
        Assert.True(GeoRefreshPolicy.ShouldQuery(null, "203.0.113.10"));
    }

    [Fact]
    public void Skips_a_repeat_lookup_of_the_same_address()
    {
        var known = Place("203.0.113.10");

        Assert.False(GeoRefreshPolicy.ShouldQuery(known, "203.0.113.10"));
        Assert.False(GeoRefreshPolicy.ShouldQuery(known, " 203.0.113.10 "));
    }

    [Fact]
    public void Queries_again_when_the_address_changes()
    {
        var known = Place("203.0.113.10");

        Assert.True(GeoRefreshPolicy.ShouldQuery(known, "198.51.100.20"));
    }

    [Fact]
    public void Does_not_query_an_empty_address()
    {
        Assert.False(GeoRefreshPolicy.ShouldQuery(Place("203.0.113.10"), " "));
        Assert.False(GeoRefreshPolicy.ShouldQuery(null, ""));
    }

    private static GeoIpInfo Place(string address)
    {
        return new GeoIpInfo(address, "City", null, "Country", "US", null, null, "ipwho.is", "https://ipwho.is/");
    }
}
