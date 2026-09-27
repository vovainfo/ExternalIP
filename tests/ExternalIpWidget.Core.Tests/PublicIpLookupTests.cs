using System.Net;
using System.Net.Sockets;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class PublicIpLookupTests
{
    [Fact]
    public void Default_providers_are_external_https_services()
    {
        Assert.NotEmpty(PublicIpLookup.DefaultProviders);
        Assert.All(PublicIpLookup.DefaultProviders, provider =>
        {
            Assert.False(string.IsNullOrWhiteSpace(provider.Name));
            Assert.StartsWith("https://", provider.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("api64", provider.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ipv6", provider.Url, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(PublicIpLookup.DefaultProviders, provider => provider.Url.Contains("ipv4.icanhazip.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Skips_an_ipv6_answer_and_keeps_looking_for_ipv4()
    {
        var handler = new DelegateHandler((request, _) =>
        {
            var body = request.RequestUri!.AbsolutePath.EndsWith("/v6", StringComparison.Ordinal)
                ? "2001:db8::44"
                : "203.0.113.44";
            return Task.FromResult(Text(body));
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("v6", "https://example.test/v6"),
            new IpProvider("v4", "https://example.test/v4"),
        ]);

        var result = await lookup.GetAsync();

        Assert.Equal("203.0.113.44", result.Address);
        Assert.Equal("v4", result.ProviderName);
        Assert.Equal(AddressFamily.InterNetwork, IPAddress.Parse(result.Address).AddressFamily);
    }

    [Fact]
    public async Task Trace_records_the_body_and_why_an_answer_was_skipped()
    {
        var handler = new DelegateHandler((request, _) =>
        {
            var body = request.RequestUri!.AbsolutePath.EndsWith("/v6", StringComparison.Ordinal)
                ? "2001:db8::9"
                : "203.0.113.9";
            return Task.FromResult(Text(body));
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("v6", "https://example.test/v6"),
            new IpProvider("v4", "https://example.test/v4"),
        ]);
        var lines = new List<string>();

        var result = await lookup.GetAsync(trace: new SyncProgress(lines.Add));

        Assert.Equal("203.0.113.9", result.Address);
        Assert.Contains(lines, line => line.Contains("IPv6", StringComparison.Ordinal) && line.Contains("2001:db8::9", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("принят IPv4 203.0.113.9", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("HTTP 200", StringComparison.Ordinal) && line.Contains("203.0.113.9", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Uses_the_first_response_that_is_an_ip_address()
    {
        var calls = new List<string>();
        var handler = new DelegateHandler((request, _) =>
        {
            calls.Add(request.RequestUri!.AbsolutePath);
            var body = request.RequestUri.AbsolutePath.EndsWith("/bad", StringComparison.Ordinal)
                ? "<html>not an address</html>"
                : "203.0.113.10\n";
            return Task.FromResult(Text(body));
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("bad", "https://example.test/bad"),
            new IpProvider("good", "https://example.test/good"),
        ]);

        var names = new List<string>();
        var result = await lookup.GetAsync(progress: new SyncProgress(names.Add));

        Assert.Equal("203.0.113.10", result.Address);
        Assert.Equal("good", result.ProviderName);
        Assert.Equal(["/bad", "/good"], calls);
        Assert.Equal(["bad", "good"], names);
    }

    [Fact]
    public async Task Skips_a_private_address_because_it_is_not_external()
    {
        var handler = new DelegateHandler((request, _) =>
        {
            var body = request.RequestUri!.AbsolutePath.EndsWith("/lan", StringComparison.Ordinal)
                ? "192.168.1.20"
                : "203.0.113.15";
            return Task.FromResult(Text(body));
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("lan", "https://example.test/lan"),
            new IpProvider("wan", "https://example.test/wan"),
        ]);

        var result = await lookup.GetAsync();

        Assert.Equal("203.0.113.15", result.Address);
        Assert.Equal("wan", result.ProviderName);
    }

    [Fact]
    public async Task Skips_a_provider_that_cannot_be_reached()
    {
        var calls = 0;
        var handler = new DelegateHandler((_, _) =>
        {
            calls++;
            if (calls == 1)
                throw new IOException("сеть недоступна");
            return Task.FromResult(Text("203.0.113.44"));
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("down", "https://example.test/down"),
            new IpProvider("up", "https://example.test/up"),
        ]);

        var result = await lookup.GetAsync();

        Assert.Equal("203.0.113.44", result.Address);
        Assert.Equal("up", result.ProviderName);
    }

    [Fact]
    public async Task Skips_a_provider_that_times_out()
    {
        var calls = 0;
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            calls++;
            if (calls == 1)
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Text("198.51.100.20");
        });

        using var http = Client(handler);
        var lookup = new PublicIpLookup(
            http,
            [new IpProvider("slow", "https://example.test/slow"), new IpProvider("fast", "https://example.test/fast")],
            TimeSpan.FromMilliseconds(80));

        var result = await lookup.GetAsync();

        Assert.Equal("198.51.100.20", result.Address);
        Assert.Equal("fast", result.ProviderName);
    }

    [Fact]
    public async Task Throws_when_every_provider_fails()
    {
        var handler = new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var http = Client(handler);
        var lookup = new PublicIpLookup(http,
        [
            new IpProvider("one", "https://example.test/one"),
            new IpProvider("two", "https://example.test/two"),
        ]);

        var ex = await Assert.ThrowsAsync<PublicIpLookupException>(() => lookup.GetAsync());

        Assert.Equal(2, ex.Attempts.Count);
        Assert.Contains("one:", ex.Attempts[0], StringComparison.Ordinal);
        Assert.Contains("two:", ex.Attempts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_swallow_caller_cancellation()
    {
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Text("203.0.113.10");
        });
        using var http = Client(handler);
        var lookup = new PublicIpLookup(http, [new IpProvider("hang", "https://example.test/hang")], TimeSpan.FromSeconds(5));
        using var cts = new CancellationTokenSource(80);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.GetAsync(cts.Token));
    }

    [Fact]
    public void Rejects_an_empty_provider_list()
    {
        using var http = Client(new DelegateHandler((_, _) => Task.FromResult(Text("203.0.113.10"))));
        Assert.Throws<ArgumentException>(() => new PublicIpLookup(http, []));
    }

    private static HttpClient Client(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static HttpResponseMessage Text(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        };
    }

    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
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
