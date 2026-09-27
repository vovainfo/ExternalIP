using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class LiveGeoIpTests
{
    [Fact]
    public async Task Geo_of_the_public_address_describes_that_same_address()
    {
        using var http = PublicIpLookup.CreateHttpClient();
        var address = (await new PublicIpLookup(http).GetAsync()).Address;
        var geo = await new GeoIpLookup(http).LookupAsync(address);

        Assert.Equal(address, geo.Address);
        Assert.False(string.IsNullOrWhiteSpace(geo.FormatPlace()));
        Assert.False(string.IsNullOrWhiteSpace(geo.CountryCode));
    }
}
