namespace ExternalIpWidget.Core;

/// <summary>
/// Внешний сервис, который видит адрес, с которого пришёл запрос.
/// </summary>
public sealed class IpProvider
{
    public IpProvider(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("У сервиса должно быть имя.", nameof(name));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("Нужен абсолютный http(s)-адрес сервиса.", nameof(url));

        Name = name;
        Url = uri.ToString();
    }

    public string Name { get; }

    public string Url { get; }
}
