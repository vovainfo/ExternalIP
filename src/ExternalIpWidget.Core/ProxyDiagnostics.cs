using System.Net;
using System.Runtime.Versioning;

namespace ExternalIpWidget.Core;

/// <summary>
/// Откуда берётся адрес, к которому реально открывается сокет.
/// Сервисы вроде ipify его не резолвят в 127.0.0.1: локальный порт — это прокси.
/// </summary>
public static class ProxyDiagnostics
{
    private static readonly string[] ProxyVariables = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "NO_PROXY"];

    public static IReadOnlyList<string> Explain(
        Uri destination,
        IWebProxy proxy,
        Func<string, string?>? environment = null,
        Func<IReadOnlyList<string>>? windowsSettings = null)
    {
        var read = environment ?? Environment.GetEnvironmentVariable;
        var lines = new List<string>
        {
            "правило .NET: если задана HTTP_PROXY, HTTPS_PROXY или ALL_PROXY, берётся она и системный прокси Windows не используется. Иначе берётся системный прокси.",
            "свой Proxy у HttpClient не задан, поэтому действует это правило.",
            $"пример URL сервиса: {destination}",
        };

        var variables = ReadVariables(read);
        if (variables.Count == 0)
            lines.Add("переменные HTTP_PROXY, HTTPS_PROXY, ALL_PROXY, NO_PROXY не заданы.");
        else
        {
            foreach (var (name, value) in variables)
                lines.Add($"переменная {name}={Redact(value)}");
        }

        string? proxyText = null;
        var direct = false;
        try
        {
            direct = proxy.IsBypassed(destination);
            if (!direct)
            {
                var chosen = proxy.GetProxy(destination);
                if (chosen is null || SameEndpoint(chosen, destination))
                    direct = true;
                else
                    proxyText = Redact(chosen.ToString());
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lines.Add($"не удалось спросить прокси: {ex.GetType().Name}: {ex.Message}");
        }

        if (direct || proxyText is null)
        {
            lines.Add("для этого URL прокси не используется, сокет откроется напрямую к имени сервиса.");
        }
        else
        {
            lines.Add($"для этого URL прокси: {proxyText}");
            lines.Add("это не адрес сервиса. Сокет откроется к прокси, а не к ipify или другому сервису. Имя сервиса в DNS не превращалось в 127.0.0.1.");
            if (variables.Count > 0)
                lines.Add("переменные прокси заданы, поэтому 127.0.0.1 берётся из них, даже если в Windows записан другой адрес.");
            else
                lines.Add("переменных прокси нет, поэтому адрес прокси вернули системные настройки Windows.");

            if (proxyText.Contains("127.0.0.1:10808", StringComparison.Ordinal) || proxyText.Contains("[::1]:10808", StringComparison.Ordinal))
                lines.Add("порт 10808 — обычный локальный порт прокси. HttpClient обращается к нему как к HTTP-прокси.");
        }

        foreach (var line in (windowsSettings ?? ReadWindowsInternetSettings)())
            lines.Add(line);

        return lines;
    }

    public static IReadOnlyList<string> ReadWindowsInternetSettings()
    {
        if (!OperatingSystem.IsWindows())
            return ["системный прокси Windows: реестр не читался, процесс не на Windows."];

        return ReadWindowsInternetSettingsCore();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> ReadWindowsInternetSettingsCore()
    {
        var lines = new List<string>();
        ReadHive(lines, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings", Microsoft.Win32.Registry.CurrentUser);
        ReadHive(lines, @"HKLM\Software\Microsoft\Windows\CurrentVersion\Internet Settings", Microsoft.Win32.Registry.LocalMachine);
        return lines;
    }

    internal static string Redact(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
            return value;

        return value.Replace(uri.UserInfo + "@", "***@", StringComparison.Ordinal);
    }

    private static List<(string Name, string Value)> ReadVariables(Func<string, string?> read)
    {
        var found = new List<(string Name, string Value)>();
        foreach (var name in ProxyVariables)
        {
            var value = First(read, name, name.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(value))
                found.Add((name, value));
        }

        return found;
    }

    private static string? First(Func<string, string?> read, params string[] names)
    {
        foreach (var name in names)
        {
            var value = read(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool SameEndpoint(Uri proxy, Uri destination)
    {
        return proxy.Host.Equals(destination.Host, StringComparison.OrdinalIgnoreCase)
            && proxy.Port == destination.Port;
    }

    [SupportedOSPlatform("windows")]
    private static void ReadHive(List<string> lines, string label, Microsoft.Win32.RegistryKey hive)
    {
        try
        {
            using var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key is null)
            {
                lines.Add($"{label}: ключ не найден");
                return;
            }

            var enable = key.GetValue("ProxyEnable");
            var server = key.GetValue("ProxyServer") as string;
            var over = key.GetValue("ProxyOverride") as string;
            var pac = key.GetValue("AutoConfigURL") as string;
            lines.Add(
                $"{label}: ProxyEnable={enable ?? "(нет)"}, ProxyServer={Blank(server)}, ProxyOverride={Blank(over)}, AutoConfigURL={Blank(pac)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            lines.Add($"{label}: не прочитан ({ex.GetType().Name})");
        }
    }

    private static string Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(нет)" : value;
    }
}
