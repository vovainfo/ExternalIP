using System.Net;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class GeoIpLookupTests
{
    [Fact]
    public void Default_providers_query_the_external_address_over_https()
    {
        Assert.NotEmpty(GeoIpLookup.DefaultProviders);
        Assert.All(GeoIpLookup.DefaultProviders, provider =>
        {
            var url = provider.CreateUrl("203.0.113.10");
            Assert.Equal("https", url.Scheme);
            Assert.Contains("203.0.113.10", url.AbsoluteUri, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Parses_ipwho_response_for_the_requested_address()
    {
        const string json = """
            {
              "ip": "8.8.8.8",
              "success": true,
              "country": "США",
              "country_code": "US",
              "region": "Калифорния",
              "city": "Сан-Хосе",
              "connection": { "isp": "Google LLC", "org": "Google LLC" },
              "timezone": { "id": "America/Los_Angeles" }
            }
            """;

        var info = GeoIpLookup.Parse("ipwho.is", "https://ipwho.is/8.8.8.8?lang=ru", "8.8.8.8", json);

        Assert.Equal("8.8.8.8", info.Address);
        Assert.Equal("Сан-Хосе, Калифорния, США · Google LLC", info.FormatPlace());
        Assert.Equal("America/Los_Angeles", info.TimeZone);
        Assert.Contains("не координаты компьютера", info.FormatDetails(), StringComparison.Ordinal);
        Assert.False(info.BelongsToRussia());
    }

    [Fact]
    public void Treats_a_russian_geoip_response_as_russia()
    {
        const string json = """
            {
              "ip": "77.88.8.8",
              "success": true,
              "country": "Россия",
              "country_code": "RU",
              "city": "Москва"
            }
            """;

        var info = GeoIpLookup.Parse("ipwho.is", "https://ipwho.is/77.88.8.8?lang=ru", "77.88.8.8", json);

        Assert.Equal("RU", info.CountryCode);
        Assert.True(info.BelongsToRussia());
    }

    [Theory]
    [InlineData("RU", "Россия", true)]
    [InlineData("ru", null, true)]
    [InlineData("RUS", null, true)]
    [InlineData(null, "Россия", true)]
    [InlineData(null, "Russia", true)]
    [InlineData(null, "Russian Federation", true)]
    [InlineData(null, "Российская Федерация", true)]
    [InlineData("US", "США", false)]
    [InlineData(null, "Румыния", false)]
    [InlineData(null, null, false)]
    [InlineData("BY", "Беларусь", false)]
    public void Detects_russia_by_country_code_or_name(string? code, string? country, bool expected)
    {
        var info = new GeoIpInfo("77.88.8.8", null, null, country, code, null, null, "test", "https://example.test");

        Assert.Equal(expected, info.BelongsToRussia());
    }

    [Fact]
    public void Parses_ipinfo_country_code_and_strips_the_asn()
    {
        const string json = """
            {
              "ip": "8.8.8.8",
              "city": "Mountain View",
              "region": "California",
              "country": "US",
              "org": "AS15169 Google LLC",
              "timezone": "America/Los_Angeles"
            }
            """;

        var info = GeoIpLookup.Parse("ipinfo", "https://ipinfo.io/8.8.8.8/json", "8.8.8.8", json);

        Assert.Equal("US", info.CountryCode);
        Assert.Null(info.Country);
        Assert.Equal("Google LLC", info.Organization);
        Assert.Contains("Mountain View", info.FormatPlace(), StringComparison.Ordinal);
        Assert.Contains("Google LLC", info.FormatPlace(), StringComparison.Ordinal);
        Assert.DoesNotContain("AS15169", info.FormatPlace(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_response_about_a_different_address()
    {
        const string json = """
            { "ip": "1.1.1.1", "success": true, "country": "Австралия", "city": "Сидней" }
            """;

        Assert.Throws<FormatException>(() =>
            GeoIpLookup.Parse("ipwho.is", "https://ipwho.is/8.8.8.8?lang=ru", "8.8.8.8", json));
    }

    [Theory]
    [InlineData("""{"success":false,"message":"Invalid IP address"}""")]
    [InlineData("not json")]
    [InlineData("""{"city":"Москва"}""")]
    public void Rejects_unusable_payloads(string json)
    {
        Assert.Throws<FormatException>(() =>
            GeoIpLookup.Parse("ipwho.is", "https://ipwho.is/8.8.8.8?lang=ru", "8.8.8.8", json));
    }

    [Fact]
    public async Task Uses_the_first_service_that_describes_the_same_address()
    {
        var requested = new List<string>();
        var handler = new DelegateHandler((request, _) =>
        {
            requested.Add(request.RequestUri!.AbsoluteUri);
            var body = request.RequestUri.Host.Contains("down", StringComparison.Ordinal)
                ? """{"success":false}"""
                : """{"ip":"203.0.113.10","country":"Тестовия","country_code":"TV","city":"Тестград"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        });

        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var lookup = new GeoIpLookup(http,
        [
            new GeoIpProvider("down", "https://down.test/{ip}"),
            new GeoIpProvider("up", "https://up.test/{ip}"),
        ]);

        var info = await lookup.LookupAsync("203.0.113.10");

        Assert.Equal("up", info.ProviderName);
        Assert.Equal("Тестград, Тестовия", info.FormatPlace());
        Assert.Equal(
            ["https://down.test/203.0.113.10", "https://up.test/203.0.113.10"],
            requested);
    }

    [Fact]
    public async Task Refuses_to_query_a_private_address()
    {
        using var http = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var lookup = new GeoIpLookup(http, [new GeoIpProvider("up", "https://up.test/{ip}")]);

        await Assert.ThrowsAsync<ArgumentException>(() => lookup.LookupAsync("192.168.1.20"));
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _send(request, cancellationToken);
        }
    }
}
