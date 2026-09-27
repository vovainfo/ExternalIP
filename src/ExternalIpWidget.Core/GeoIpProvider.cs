namespace ExternalIpWidget.Core;

/// <summary>
/// HTTPS-сервис GeoIP. В адресе должен быть плейсхолдер {ip}: местоположение запрашивается
/// для уже определённого внешнего адреса, а не для адреса сетевой карты.
/// </summary>
public sealed class GeoIpProvider
{
    public GeoIpProvider(string name, string urlTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("У сервиса должно быть имя.", nameof(name));
        if (string.IsNullOrWhiteSpace(urlTemplate) || !urlTemplate.Contains("{ip}", StringComparison.Ordinal))
            throw new ArgumentException("В адресе сервиса нужен плейсхолдер {ip}.", nameof(urlTemplate));

        var sample = urlTemplate.Replace("{ip}", "203.0.113.10", StringComparison.Ordinal);
        if (!Uri.TryCreate(sample, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Нужен абсолютный https-адрес сервиса GeoIP.", nameof(urlTemplate));

        Name = name.Trim();
        UrlTemplate = urlTemplate;
    }

    public string Name { get; }

    public string UrlTemplate { get; }

    public Uri CreateUrl(string canonicalAddress)
    {
        var url = UrlTemplate.Replace("{ip}", canonicalAddress, StringComparison.Ordinal);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new FormatException("Не удалось собрать адрес запроса GeoIP.");
        return uri;
    }
}
