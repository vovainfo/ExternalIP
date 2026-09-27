using System.Text.Json;

namespace ExternalIpWidget.Core;

/// <summary>
/// Узнаёт страну, город и провайдера внешнего IP.
/// Запрос идёт по уже полученному внешнему адресу, а не по адресу сетевой карты.
/// </summary>
public sealed class GeoIpLookup
{
    public static IReadOnlyList<GeoIpProvider> DefaultProviders { get; } =
    [
        new("ipwho.is", "https://ipwho.is/{ip}?lang=ru"),
        new("geojs", "https://get.geojs.io/v1/ip/geo/{ip}.json"),
        new("ipinfo", "https://ipinfo.io/{ip}/json"),
    ];

    private readonly HttpClient _http;
    private readonly IReadOnlyList<GeoIpProvider> _providers;
    private readonly TimeSpan _perProviderTimeout;

    public GeoIpLookup(
        HttpClient httpClient,
        IReadOnlyList<GeoIpProvider>? providers = null,
        TimeSpan? perProviderTimeout = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _providers = providers ?? DefaultProviders;
        if (_providers.Count == 0)
            throw new ArgumentException("Нужен хотя бы один сервис GeoIP.", nameof(providers));

        _perProviderTimeout = perProviderTimeout ?? TimeSpan.FromSeconds(6);
        if (_perProviderTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(perProviderTimeout));
    }

    public async Task<GeoIpInfo> LookupAsync(
        string externalAddress,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        if (!IpAddressText.TryParse(externalAddress, out var canonical))
            throw new ArgumentException("GeoIP запрашивается только для внешнего IP-адреса.", nameof(externalAddress));

        var attempts = new List<string>();
        Exception? last = null;

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("GeoIP " + provider.Name);

            try
            {
                var info = await RequestAsync(provider, canonical, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(info.FormatPlace()))
                    throw new FormatException("В ответе нет данных о местоположении.");
                return info;
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                last = ex;
                attempts.Add($"{provider.Name}: {Short(ex)}");
            }
        }

        throw new GeoIpLookupException(
            "Не удалось определить местоположение внешнего IP.",
            attempts,
            last);
    }

    public static GeoIpInfo Parse(string providerName, string providerUrl, string requestedAddress, string json)
    {
        if (!IpAddressText.TryParse(requestedAddress, out var canonical))
            throw new FormatException("Некорректный внешний IP для разбора GeoIP.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Ответ GeoIP не является JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Ответ GeoIP не является объектом JSON.");
            if (IsExplicitFailure(root))
                throw new FormatException("Сервис GeoIP не нашёл адрес.");

            var reported = FirstString(root, "ip");
            if (string.IsNullOrWhiteSpace(reported)
                || !IpAddressText.TryParse(reported, out var reportedAddress)
                || !string.Equals(reportedAddress, canonical, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Сервис GeoIP вернул другой IP-адрес.");
            }

            var countryCode = FirstString(root, "country_code", "countryCode");
            var country = FirstString(root, "country", "country_name");
            if (countryCode is null && country is { Length: 2 } && country.All(char.IsLetter))
            {
                countryCode = country.ToUpperInvariant();
                country = null;
            }

            var organization = Organization(root);
            var timeZone = TimeZone(root);
            return new GeoIpInfo(
                canonical,
                FirstString(root, "city"),
                FirstString(root, "region", "region_name"),
                country,
                countryCode,
                organization,
                timeZone,
                providerName,
                providerUrl);
        }
    }

    private async Task<GeoIpInfo> RequestAsync(
        GeoIpProvider provider,
        string canonicalAddress,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_perProviderTimeout);

        var url = provider.CreateUrl(canonicalAddress);
        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (body.Length > 32_768)
            throw new FormatException("Слишком длинный ответ GeoIP.");

        return Parse(provider.Name, url.ToString(), canonicalAddress, body);
    }

    private static bool IsExplicitFailure(JsonElement root)
    {
        if (!root.TryGetProperty("success", out var success))
            return false;
        return success.ValueKind == JsonValueKind.False
            || (success.ValueKind == JsonValueKind.String
                && string.Equals(success.GetString(), "false", StringComparison.OrdinalIgnoreCase));
    }

    private static string? Organization(JsonElement root)
    {
        if (root.TryGetProperty("connection", out var connection) && connection.ValueKind == JsonValueKind.Object)
        {
            var fromConnection = StripAsn(FirstString(connection, "isp", "org"));
            if (!string.IsNullOrWhiteSpace(fromConnection))
                return fromConnection;
        }

        return StripAsn(FirstString(root, "organization_name", "org", "isp", "organization"));
    }

    private static string? TimeZone(JsonElement root)
    {
        if (root.TryGetProperty("timezone", out var timezone))
        {
            if (timezone.ValueKind == JsonValueKind.String)
                return timezone.GetString()?.Trim();
            if (timezone.ValueKind == JsonValueKind.Object)
                return FirstString(timezone, "id", "utc");
        }

        return FirstString(root, "time_zone");
    }

    private static string? StripAsn(string? organization)
    {
        if (string.IsNullOrWhiteSpace(organization))
            return null;

        var text = organization.Trim();
        var space = text.IndexOf(' ');
        if (space > 2
            && text.StartsWith("AS", StringComparison.Ordinal)
            && text[2..space].All(char.IsDigit))
        {
            text = text[(space + 1)..].Trim();
        }

        return text.Length == 0 ? null : text;
    }

    private static string? FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
                continue;

            var text = value.GetString()?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }

        return null;
    }

    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or IOException or FormatException or JsonException or OperationCanceledException;
    }

    private static string Short(Exception ex)
    {
        if (ex is OperationCanceledException)
            return "таймаут";

        var message = ex.Message.ReplaceLineEndings(" ");
        return message.Length > 140 ? message[..140] : message;
    }
}
