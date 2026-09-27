using System.Net;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class OptionalEnvironmentProxyTests
{
    private static readonly Uri Https = new("https://api.ipify.org/ip");
    private static readonly Uri Http = new("http://example.test/ip");

    [Fact]
    public void Direct_mode_ignores_environment_and_system_proxies()
    {
        var proxy = Create(
            ProxyMode.None,
            name => name == "HTTPS_PROXY" ? "http://127.0.0.1:10808" : null,
            new FixedProxy(new Uri("http://10.1.1.1:8888")));

        Assert.True(proxy.IsBypassed(Https));
        var text = string.Join('\n', proxy.Describe(Https));
        Assert.Contains("без прокси", text, StringComparison.Ordinal);
        Assert.Contains("напрямую", text, StringComparison.Ordinal);
        Assert.DoesNotContain("сокет откроется к прокси", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Environment_mode_prefers_https_proxy_and_shows_the_variables()
    {
        var proxy = Create(ProxyMode.Environment, name => name switch
        {
            "HTTPS_PROXY" => "http://user:secret@127.0.0.1:10808",
            "HTTP_PROXY" => "http://10.0.0.1:8080",
            _ => null,
        });

        Assert.False(proxy.IsBypassed(Https));
        var httpsProxy = proxy.GetProxy(Https);
        Assert.Equal("127.0.0.1", httpsProxy!.Host);
        Assert.Equal("user", ((NetworkCredential)proxy.Credentials!).UserName);
        Assert.Equal("10.0.0.1", proxy.GetProxy(Http)!.Host);

        var text = proxy.ReferenceText(Https);
        Assert.Contains("HTTP_PROXY: http://10.0.0.1:8080", text, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:10808", text, StringComparison.Ordinal);
        Assert.Contains("ALL_PROXY: не задана", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Environment_mode_uses_http_proxy_for_https_when_https_proxy_is_absent()
    {
        var proxy = Create(ProxyMode.Environment, name => name == "HTTP_PROXY" ? "http://10.0.0.1:8080" : null);

        Assert.Equal("10.0.0.1", proxy.GetProxy(Https)!.Host);
        Assert.Equal(8080, proxy.GetProxy(Https)!.Port);
    }

    [Fact]
    public void Environment_mode_does_not_fall_back_to_the_system_proxy()
    {
        var proxy = Create(ProxyMode.Environment, _ => null, new FixedProxy(new Uri("http://10.1.1.1:8888")));

        Assert.True(proxy.IsBypassed(Https));
        Assert.Contains("не заданы", string.Join('\n', proxy.Describe(Https)), StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_all_proxy_and_honors_no_proxy()
    {
        var proxy = Create(ProxyMode.Environment, name => name switch
        {
            "ALL_PROXY" => "127.0.0.1:10808",
            "NO_PROXY" => "localhost, api.ipify.org",
            _ => null,
        });

        Assert.True(proxy.IsBypassed(Https));
        Assert.False(proxy.IsBypassed(new Uri("https://ifconfig.me/ip")));
        Assert.Equal(10808, proxy.GetProxy(new Uri("https://ifconfig.me/ip"))!.Port);
        Assert.Contains("NO_PROXY: localhost, api.ipify.org", proxy.ReferenceText(Https), StringComparison.Ordinal);
    }

    [Fact]
    public void System_mode_uses_the_system_proxy_and_ignores_variables()
    {
        var proxy = new OptionalEnvironmentProxy
        {
            Mode = ProxyMode.System,
            EnvironmentReader = name => name == "HTTPS_PROXY" ? "http://127.0.0.1:10808" : null,
            SystemProxy = new FixedProxy(new Uri("http://10.1.1.1:8888")),
        };

        Assert.Equal("10.1.1.1", proxy.GetProxy(Https)!.Host);
        Assert.Equal("http://10.1.1.1:8888/", proxy.ReferenceText(Https));
        Assert.Contains("системный прокси Windows", string.Join('\n', proxy.Describe(Https)), StringComparison.Ordinal);
    }

    [Fact]
    public void System_mode_reports_when_windows_has_no_proxy()
    {
        var proxy = new OptionalEnvironmentProxy
        {
            Mode = ProxyMode.System,
            EnvironmentReader = _ => null,
            SystemProxy = DirectProxy.Instance,
        };

        Assert.True(proxy.IsBypassed(Https));
        Assert.Equal("системный прокси не задан", proxy.ReferenceText(Https));
    }

    [Fact]
    public void Custom_mode_uses_the_saved_address()
    {
        var proxy = new OptionalEnvironmentProxy
        {
            Mode = ProxyMode.Custom,
            CustomProxy = "http://user:secret@10.2.2.2:9090",
            EnvironmentReader = name => name == "HTTPS_PROXY" ? "http://127.0.0.1:10808" : null,
            SystemProxy = new FixedProxy(new Uri("http://10.1.1.1:8888")),
        };

        Assert.True(proxy.TryValidate(out _));
        Assert.Equal("10.2.2.2", proxy.GetProxy(Https)!.Host);
        Assert.Equal(9090, proxy.GetProxy(Https)!.Port);
        var text = string.Join('\n', proxy.Describe(Https));
        Assert.Contains("10.2.2.2:9090", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1:10808", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "не введён")]
    [InlineData("   ", "не введён")]
    [InlineData("://", "не распознан")]
    public void Custom_mode_rejects_an_unusable_address(string value, string expected)
    {
        var proxy = new OptionalEnvironmentProxy
        {
            Mode = ProxyMode.Custom,
            CustomProxy = value,
        };

        Assert.False(proxy.TryValidate(out var error));
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.True(proxy.IsBypassed(Https));
    }

    [Theory]
    [InlineData("127.0.0.1:10808", "https", "127.0.0.1", 10808)]
    [InlineData("http=10.0.0.2:80;https=10.0.0.3:443", "https", "10.0.0.3", 443)]
    [InlineData("http=10.0.0.2:80;https=10.0.0.3:443", "http", "10.0.0.2", 80)]
    public void Parses_a_windows_proxy_server_value(string server, string scheme, string host, int port)
    {
        var uri = ProxyServerText.ForScheme(server, scheme);

        Assert.NotNull(uri);
        Assert.Equal(host, uri!.Host);
        Assert.Equal(port, uri.Port);
    }

    [Theory]
    [InlineData(false, ProxyMode.None, true, false, ProxyMode.Environment)]
    [InlineData(false, ProxyMode.None, false, true, ProxyMode.System)]
    [InlineData(false, ProxyMode.None, false, false, ProxyMode.None)]
    [InlineData(true, ProxyMode.Custom, true, true, ProxyMode.Custom)]
    [InlineData(true, ProxyMode.None, true, false, ProxyMode.None)]
    public void Migrates_the_old_checkboxes_until_a_choice_is_saved(
        bool saved,
        ProxyMode current,
        bool environment,
        bool system,
        ProxyMode expected)
    {
        Assert.Equal(expected, ProxyModeMigration.Resolve(saved, current, environment, system));
    }

    private static OptionalEnvironmentProxy Create(ProxyMode mode, Func<string, string?> environment, IWebProxy? system = null)
    {
        return new OptionalEnvironmentProxy
        {
            Mode = mode,
            EnvironmentReader = environment,
            SystemProxy = system ?? DirectProxy.Instance,
        };
    }

    private sealed class FixedProxy(Uri proxy) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => proxy;

        public bool IsBypassed(Uri host) => false;
    }
}
