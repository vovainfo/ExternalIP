using System.Net;
using System.Runtime.Versioning;

namespace ExternalIpWidget.Core;

/// <summary>
/// Прокси для HttpClient. Переменные окружения и системный прокси Windows используются только если это включено.
/// </summary>
public sealed class OptionalEnvironmentProxy : IWebProxy
{
    public bool UseEnvironmentVariables { get; set; }

    public bool UseSystemProxy { get; set; }

    public Func<string, string?> EnvironmentReader { get; init; } = Environment.GetEnvironmentVariable;

    public IWebProxy SystemProxy { get; init; } = SystemWebProxy.Create();

    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination)
    {
        var choice = Choose(destination);
        return choice.Direct ? destination : choice.Proxy;
    }

    public bool IsBypassed(Uri host)
    {
        return Choose(host).Direct;
    }

    public IReadOnlyList<string> Describe(Uri destination)
    {
        var lines = new List<string>
        {
            UseEnvironmentVariables
                ? "флажок «Прокси HTTP_PROXY»: включён. Используются HTTP_PROXY, HTTPS_PROXY и ALL_PROXY."
                : "флажок «Прокси HTTP_PROXY»: выключен. HTTP_PROXY, HTTPS_PROXY и ALL_PROXY не используются.",
            UseSystemProxy
                ? "флажок «Системный прокси»: включён. Используются настройки прокси Windows."
                : "флажок «Системный прокси»: выключен. Настройки прокси Windows не используются.",
        };

        foreach (var name in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "NO_PROXY" })
        {
            var value = ReadEither(EnvironmentReader, name.ToLowerInvariant(), name);
            lines.Add(string.IsNullOrWhiteSpace(value)
                ? $"переменная {name} не задана"
                : $"переменная {name}={ProxyDiagnostics.Redact(value)}");
        }

        var choice = Choose(destination);
        if (choice.Direct)
            lines.Add($"сокет откроется напрямую к {destination.Host}. {choice.Reason}");
        else
            lines.Add($"сокет откроется к прокси {ProxyDiagnostics.Redact(choice.Proxy!.ToString())}, а не к {destination.Host}. Это не адрес сервиса. {choice.Reason}");

        foreach (var line in ProxyDiagnostics.ReadWindowsInternetSettings())
            lines.Add(line);

        return lines;
    }

    public ProxyChoice Choose(Uri destination)
    {
        if (UseEnvironmentVariables
            && EnvironmentProxyRules.TryGet(EnvironmentReader, destination, out var proxy, out var bypassed))
        {
            if (bypassed)
                return ProxyChoice.AsDirect("NO_PROXY исключает этот адрес");
            return ProxyChoice.AsVia(proxy!, "адрес взят из HTTP_PROXY, HTTPS_PROXY или ALL_PROXY");
        }

        if (!UseSystemProxy)
        {
            var skipped = UseEnvironmentVariables
                ? "переменные прокси не заданы, флажок системного прокси выключен"
                : "флажки прокси выключены, запрос идёт напрямую";
            return ProxyChoice.AsDirect(skipped);
        }

        if (SystemProxy.IsBypassed(destination))
        {
            return ProxyChoice.AsDirect("системный прокси включён, но в Windows он не задан");
        }

        var system = SystemProxy.GetProxy(destination);
        if (system is null || SameEndpoint(system, destination))
        {
            return ProxyChoice.AsDirect("системный прокси включён, но в Windows он не задан");
        }

        var via = UseEnvironmentVariables
            ? "переменные прокси не заданы, взят системный прокси Windows"
            : "переменные прокси выключены, взят системный прокси Windows";
        return ProxyChoice.AsVia(system, via);
    }

    private static string? ReadEither(Func<string, string?> read, string lower, string upper)
    {
        var value = read(lower);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        value = read(upper);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool SameEndpoint(Uri proxy, Uri destination)
    {
        return proxy.Host.Equals(destination.Host, StringComparison.OrdinalIgnoreCase)
            && proxy.Port == destination.Port;
    }
}

public readonly record struct ProxyChoice(bool Direct, Uri? Proxy, string Reason)
{
    public static ProxyChoice AsDirect(string reason) => new(true, null, reason);

    public static ProxyChoice AsVia(Uri proxy, string reason) => new(false, proxy, reason);
}

internal static class EnvironmentProxyRules
{
    public static bool TryGet(Func<string, string?> environment, Uri destination, out Uri? proxy, out bool bypassed)
    {
        var http = Parse(Read(environment, "http_proxy", "HTTP_PROXY"));
        var https = Parse(Read(environment, "https_proxy", "HTTPS_PROXY"));
        var all = Parse(Read(environment, "all_proxy", "ALL_PROXY"));
        proxy = destination.Scheme switch
        {
            "http" => http ?? all,
            "https" => https ?? all,
            _ => all,
        };
        if (proxy is null)
        {
            bypassed = false;
            return false;
        }

        bypassed = IsBypassed(Read(environment, "no_proxy", "NO_PROXY"), destination);
        return true;
    }

    internal static Uri? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
            text = "http://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string? Read(Func<string, string?> environment, string lower, string upper)
    {
        var value = environment(lower);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        value = environment(upper);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsBypassed(string? noProxy, Uri destination)
    {
        if (string.IsNullOrWhiteSpace(noProxy))
            return false;

        var host = destination.IdnHost;
        foreach (var raw in noProxy.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw == "*")
                return true;
            if (raw.Equals("<local>", StringComparison.OrdinalIgnoreCase))
            {
                if (!host.Contains('.'))
                    return true;
                continue;
            }

            var entry = raw.TrimStart('.');
            if (host.Equals(entry, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

internal static class ProxyServerText
{
    public static Uri? ForScheme(string? server, string scheme)
    {
        if (string.IsNullOrWhiteSpace(server))
            return null;

        var text = server.Trim();
        if (!text.Contains('=', StringComparison.Ordinal))
            return EnvironmentProxyRules.Parse(text);

        string? match = null;
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Equals(scheme, StringComparison.OrdinalIgnoreCase))
                match = value;
        }

        return EnvironmentProxyRules.Parse(match);
    }
}

internal static class SystemWebProxy
{
    public static IWebProxy Create()
    {
        if (OperatingSystem.IsWindows())
            return CreateWindows();
        return DirectProxy.Instance;
    }

    [SupportedOSPlatform("windows")]
    private static IWebProxy CreateWindows() => new WindowsRegistryProxy();
}

internal sealed class DirectProxy : IWebProxy
{
    public static DirectProxy Instance { get; } = new();

    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination) => destination;

    public bool IsBypassed(Uri host) => true;
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsRegistryProxy : IWebProxy
{
    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination)
    {
        var settings = Read();
        if (settings.Enable != 1)
            return destination;
        return ProxyServerText.ForScheme(settings.Server, destination.Scheme) ?? destination;
    }

    public bool IsBypassed(Uri host)
    {
        var settings = Read();
        if (settings.Enable != 1 || string.IsNullOrWhiteSpace(settings.Server))
            return true;
        if (ProxyServerText.ForScheme(settings.Server, host.Scheme) is null)
            return true;
        return OverrideMatches(settings.Override, host);
    }

    private static bool OverrideMatches(string? proxyOverride, Uri destination)
    {
        if (string.IsNullOrWhiteSpace(proxyOverride))
            return false;

        var host = destination.IdnHost;
        foreach (var raw in proxyOverride.Split([';', ' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Equals("<local>", StringComparison.OrdinalIgnoreCase) && !host.Contains('.'))
                return true;
            var entry = raw.TrimStart('*').TrimStart('.');
            if (entry.Length == 0)
                continue;
            if (host.Equals(entry, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static WindowsProxySettings Read()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key is null)
                return default;
            var enable = key.GetValue("ProxyEnable") is int value ? value : 0;
            var server = key.GetValue("ProxyServer") as string;
            var over = key.GetValue("ProxyOverride") as string;
            return new WindowsProxySettings(enable, server, over);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return default;
        }
    }

    private readonly record struct WindowsProxySettings(int Enable, string? Server, string? Override);
}
