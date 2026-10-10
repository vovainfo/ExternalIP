using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class ProxyDiagnosticsTests
{
    [Fact]
    public void Hides_the_proxy_login_and_password()
    {
        var text = ProxyDiagnostics.Redact("http://user:secret@127.0.0.1:10808");

        Assert.Equal("http://***@127.0.0.1:10808", text);
    }

    [Theory]
    [InlineData("http://127.0.0.1:10808")]
    [InlineData("127.0.0.1:10808")]
    [InlineData("не адрес")]
    public void Leaves_an_address_without_credentials_as_is(string value)
    {
        Assert.Equal(value, ProxyDiagnostics.Redact(value));
    }

    [Theory]
    [InlineData("http://api.ipify.org:443", "https://API.ipify.org/", true)]
    [InlineData("http://127.0.0.1:10808", "https://api.ipify.org/", false)]
    [InlineData("http://api.ipify.org:8080", "https://api.ipify.org/", false)]
    public void Recognises_a_proxy_that_is_the_service_itself(string proxy, string destination, bool same)
    {
        Assert.Equal(same, ProxyDiagnostics.SameEndpoint(new Uri(proxy), new Uri(destination)));
    }
}
