using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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
    ];

    private static readonly AsyncLocal<Action<string>?> ConnectTrace = new();

    private readonly HttpClient _http;
    private readonly IReadOnlyList<IpProvider> _providers;
    private readonly TimeSpan _perProviderTimeout;
    private readonly OptionalEnvironmentProxy? _environmentProxy;

    public PublicIpLookup(
        HttpClient httpClient,
        IReadOnlyList<IpProvider>? providers = null,
        TimeSpan? perProviderTimeout = null,
        OptionalEnvironmentProxy? environmentProxy = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _providers = providers ?? DefaultProviders;
        if (_providers.Count == 0)
            throw new ArgumentException("Нужен хотя бы один сервис.", nameof(providers));

        _perProviderTimeout = perProviderTimeout ?? TimeSpan.FromSeconds(6);
        if (_perProviderTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(perProviderTimeout));
        _environmentProxy = environmentProxy;
    }

    public static HttpClient CreateHttpClient(OptionalEnvironmentProxy? proxy = null)
    {
        proxy ??= new OptionalEnvironmentProxy { Mode = ProxyMode.None };
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseProxy = true,
            Proxy = proxy,
            // Сервис возвращает адрес того подключения, которое к нему пришло.
            // Сокет только IPv4, поэтому в ответе внешний IPv4, а не IPv6.
            ConnectCallback = ConnectOverIPv4Async,
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
        IProgress<string>? progress = null,
        IProgress<string>? trace = null)
    {
        var attempts = new List<string>();
        Exception? last = null;
        Note(trace, $"старт, сервисов {_providers.Count}, таймаут {_perProviderTimeout.TotalSeconds:0} с, соединение только IPv4");
        if (_environmentProxy is not null && Uri.TryCreate(_providers[0].Url, UriKind.Absolute, out var sample))
        {
            foreach (var line in _environmentProxy.Describe(sample))
                Note(trace, line);
            if (!_environmentProxy.TryValidate(out var proxyError))
            {
                Note(trace, proxyError);
                throw new PublicIpLookupException(proxyError, attempts, null);
            }
        }

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(provider.Name);
            Note(trace, $"{provider.Name} {provider.Url}");

            try
            {
                var address = await RequestAddressAsync(provider, cancellationToken, trace).ConfigureAwait(false);
                Note(trace, $"{provider.Name} принят IPv4 {address}");
                return new PublicIpResult(address, provider.Name, provider.Url, DateTimeOffset.Now);
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                last = ex;
                var reason = Describe(ex);
                attempts.Add($"{provider.Name}: {Short(ex)}");
                Note(trace, $"{provider.Name} пропущен: {reason}");
            }
        }

        Note(trace, "ни один сервис не вернул IPv4");
        throw new PublicIpLookupException(
            "Не удалось определить внешний IP ни через один сервис.",
            attempts,
            last);
    }

    private async Task<string> RequestAddressAsync(
        IpProvider provider,
        CancellationToken cancellationToken,
        IProgress<string>? trace)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_perProviderTimeout);
        var started = Stopwatch.StartNew();
        ConnectTrace.Value = line => Note(trace, $"{provider.Name} {line}");

        try
        {
            using var response = await _http
                .GetAsync(provider.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            var body = response.Content is null
                ? ""
                : await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            Note(trace, $"{provider.Name} HTTP {(int)response.StatusCode} за {started.ElapsedMilliseconds} мс, тело {body.Length}: {Preview(body)}");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            if (body.Length > 512)
                throw new FormatException("Слишком длинный ответ.");

            var address = IpAddressText.Parse(body);
            if (!IPAddress.TryParse(address, out var ip))
                throw new FormatException("Ответ сервиса не содержит IP-адрес.");
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new FormatException($"Сервис вернул IPv6 {ip}. Нужен IPv4.");

            return ip.ToString();
        }
        finally
        {
            ConnectTrace.Value = null;
        }
    }

    private static async ValueTask<Stream> ConnectOverIPv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        var request = context.InitialRequestMessage?.RequestUri;
        if (request is not null
            && !host.Equals(request.Host, StringComparison.OrdinalIgnoreCase))
        {
            ConnectTrace.Value?.Invoke(
                $"подключение к {host}:{port}, а не к {request.Host}:{request.Port}. Это адрес прокси. DNS сервиса {request.Host} здесь не выполнялся.");
        }

        if (IPAddress.TryParse(host, out _))
        {
            ConnectTrace.Value?.Invoke($"адрес подключения уже задан как {host}:{port}, отдельный DNS-запрос к имени сервиса не нужен.");
        }
        else
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellationToken)
                    .ConfigureAwait(false);
                ConnectTrace.Value?.Invoke(
                    $"DNS {host} A: {(addresses.Length == 0 ? "нет записей" : string.Join(", ", addresses.Select(item => item.ToString())))}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                ConnectTrace.Value?.Invoke($"DNS {host}: {Describe(ex)}");
            }
        }

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
            ConnectTrace.Value?.Invoke($"TCP {socket.LocalEndPoint} -> {socket.RemoteEndPoint}");
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception ex)
        {
            socket.Dispose();
            ConnectTrace.Value?.Invoke($"TCP {host}:{port}: {Describe(ex)}");
            if (IsLoopback(host) && ex is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
            {
                ConnectTrace.Value?.Invoke(
                    $"{host}:{port} отверг соединение: прокси прописан, но на этом порту никто не слушает. К самому сервису подключение не дошло.");
            }

            throw;
        }
    }

    private static bool IsLoopback(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static void Note(IProgress<string>? trace, string line)
    {
        trace?.Report($"{DateTimeOffset.Now:HH:mm:ss.fff} {line}");
    }

    private static string Preview(string body)
    {
        var text = body.ReplaceLineEndings(" ").Trim();
        if (text.Length == 0)
            return "(пусто)";
        return text.Length <= 160 ? text : text[..160] + "…";
    }

    private static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null && parts.Count < 4; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                parts.Add("таймаут");
                break;
            }

            var message = current.Message.ReplaceLineEndings(" ").Trim();
            if (message.Length > 180)
                message = message[..180];
            parts.Add($"{current.GetType().Name}: {message}");
        }

        return parts.Count == 0 ? ex.GetType().Name : string.Join(" ← ", parts);
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
