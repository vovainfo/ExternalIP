using System.Net;
using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class ProxyDiagnosticsTests
{
    private static readonly Uri Service = new("https://api.ipify.org/");

    [Fact]
    public void Names_the_environment_variable_when_it_points_at_loopback()
    {
        var lines = ProxyDiagnostics.Explain(
            Service,
            new FixedProxy(new Uri("http://user:secret@127.0.0.1:10808")),
            name => name == "HTTPS_PROXY" ? "http://user:secret@127.0.0.1:10808" : null,
            () => ["Windows: ProxyEnable=0"]);

        var text = string.Join('\n', lines);
        Assert.Contains("127.0.0.1:10808", text, StringComparison.Ordinal);
        Assert.Contains("переменные прокси заданы", text, StringComparison.Ordinal);
        Assert.Contains("не к ipify", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.Contains("порт 10808", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Points_at_windows_settings_when_no_proxy_variable_is_set()
    {
        var lines = ProxyDiagnostics.Explain(
            Service,
            new FixedProxy(new Uri("http://127.0.0.1:10808")),
            _ => null,
            () => ["HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings: ProxyEnable=1, ProxyServer=127.0.0.1:10808"]);

        var text = string.Join('\n', lines);
        Assert.Contains("переменные HTTP_PROXY, HTTPS_PROXY, ALL_PROXY, NO_PROXY не заданы", text, StringComparison.Ordinal);
        Assert.Contains("системные настройки Windows", text, StringComparison.Ordinal);
        Assert.Contains("ProxyEnable=1", text, StringComparison.Ordinal);
        Assert.Contains("это не адрес сервиса", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Says_when_the_request_goes_straight_to_the_service()
    {
        var lines = ProxyDiagnostics.Explain(
            Service,
            new FixedProxy(null, bypass: true),
            _ => null,
            () => []);

        var text = string.Join('\n', lines);
        Assert.Contains("напрямую к имени сервиса", text, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", text, StringComparison.Ordinal);
    }

    private sealed class FixedProxy(Uri? proxy, bool bypass = false) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => bypass ? destination : proxy;

        public bool IsBypassed(Uri host) => bypass;
    }
}
