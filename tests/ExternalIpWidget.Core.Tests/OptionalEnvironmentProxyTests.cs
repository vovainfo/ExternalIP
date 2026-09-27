using System.Net;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class OptionalEnvironmentProxyTests
{
    private static readonly Uri Https = new("https://api.ipify.org/ip");
    private static readonly Uri Http = new("http://example.test/ip");

    [Fact]
    public void Ignores_environment_proxies_when_the_switch_is_off()
    {
        var proxy = Create(useEnvironment: false, name => name == "HTTPS_PROXY" ? "http://127.0.0.1:10808" : null);

        Assert.True(proxy.IsBypassed(Https));
        var text = string.Join('\n', proxy.Describe(Https));
        Assert.Contains("выключен", text, StringComparison.Ordinal);
        Assert.Contains("HTTPS_PROXY=http://127.0.0.1:10808", text, StringComparison.Ordinal);
        Assert.Contains("напрямую", text, StringComparison.Ordinal);
        Assert.DoesNotContain("сокет откроется к прокси", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_https_proxy_for_https_when_the_switch_is_on()
    {
        var proxy = Create(useEnvironment: true, name => name switch
        {
            "HTTPS_PROXY" => "http://user:secret@127.0.0.1:10808",
            "HTTP_PROXY" => "http://10.0.0.1:8080",
            _ => null,
        });

        Assert.False(proxy.IsBypassed(Https));
        Assert.Equal("127.0.0.1", proxy.GetProxy(Https)!.Host);
        Assert.Equal("10.0.0.1", proxy.GetProxy(Http)!.Host);

        var text = string.Join('\n', proxy.Describe(Https));
        Assert.Contains("включён", text, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:10808", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_all_proxy_and_honors_no_proxy()
    {
        var proxy = Create(useEnvironment: true, name => name switch
        {
            "ALL_PROXY" => "127.0.0.1:10808",
            "NO_PROXY" => "localhost, api.ipify.org",
            _ => null,
        });

        Assert.True(proxy.IsBypassed(Https));
        Assert.False(proxy.IsBypassed(new Uri("https://ifconfig.me/ip")));
        Assert.Equal(10808, proxy.GetProxy(new Uri("https://ifconfig.me/ip"))!.Port);
    }

    [Fact]
    public void Uses_the_system_proxy_when_variables_are_off()
    {
        var system = new FixedProxy(new Uri("http://10.1.1.1:8888"));
        var proxy = new OptionalEnvironmentProxy
        {
            UseEnvironmentVariables = false,
            EnvironmentReader = name => name == "HTTPS_PROXY" ? "http://127.0.0.1:10808" : null,
            SystemProxy = system,
        };

        Assert.Equal("10.1.1.1", proxy.GetProxy(Https)!.Host);
        Assert.Contains("системный прокси Windows", string.Join('\n', proxy.Describe(Https)), StringComparison.Ordinal);
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

    private static OptionalEnvironmentProxy Create(bool useEnvironment, Func<string, string?> environment)
    {
        return new OptionalEnvironmentProxy
        {
            UseEnvironmentVariables = useEnvironment,
            EnvironmentReader = environment,
            SystemProxy = DirectProxy.Instance,
        };
    }

    private sealed class FixedProxy(Uri proxy) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => proxy;

        public bool IsBypassed(Uri host) => false;
    }
}
