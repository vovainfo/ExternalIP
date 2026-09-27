using System.Net;

namespace ExternalIpWidget.Core;

/// <summary>
/// Узнаёт внешний IP у стороннего сервиса.
/// Адрес сетевой карты не используется: из-за NAT он не совпадает с тем, что видит интернет.
/// </summary>
public sealed class PublicIpLookup
{
    public static IReadOnlyList<IpProvider> DefaultProviders { get; } =
    [
        new("ipify", "https://api.ipify.org"),
        new("icanhazip", "https://ipv4.icanhazip.com"),
        new("AWS checkip", "https://checkip.amazonaws.com"),
        new("ifconfig.me", "https://ifconfig.me/ip"),
        new("ipinfo", "https://ipinfo.io/ip"),
        new("ipify (IPv6 или IPv4)", "https://api64.ipify.org"),
    ];

    private readonly HttpClient _http;
    private readonly IReadOnlyList<IpProvider> _providers;
    private readonly TimeSpan _perProviderTimeout;

    public PublicIpLookup(
        HttpClient httpClient,
        IReadOnlyList<IpProvider>? providers = null,
        TimeSpan? perProviderTimeout = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _providers = providers ?? DefaultProviders;
        if (_providers.Count == 0)
            throw new ArgumentException("Нужен хотя бы один сервис.", nameof(providers));

        _perProviderTimeout = perProviderTimeout ?? TimeSpan.FromSeconds(6);
        if (_perProviderTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(perProviderTimeout));
    }

    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ExternalIpWidget/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        return client;
    }

    public async Task<PublicIpResult> GetAsync(
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        var attempts = new List<string>();
        Exception? last = null;

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(provider.Name);

            try
            {
                var address = await RequestAddressAsync(provider, cancellationToken).ConfigureAwait(false);
                return new PublicIpResult(address, provider.Name, provider.Url, DateTimeOffset.Now);
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                last = ex;
                attempts.Add($"{provider.Name}: {Short(ex)}");
            }
        }

        throw new PublicIpLookupException(
            "Не удалось определить внешний IP ни через один сервис.",
            attempts,
            last);
    }

    private async Task<string> RequestAddressAsync(IpProvider provider, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_perProviderTimeout);

        using var response = await _http
            .GetAsync(provider.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (body.Length > 512)
            throw new FormatException("Слишком длинный ответ.");

        return IpAddressText.Parse(body);
    }

    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or IOException or FormatException or OperationCanceledException;
    }

    private static string Short(Exception ex)
    {
        if (ex is OperationCanceledException)
            return "таймаут";

        var message = ex.Message.ReplaceLineEndings(" ");
        return message.Length > 140 ? message[..140] : message;
    }
}
