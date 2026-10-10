using System.Runtime.Versioning;

namespace ExternalIpWidget.Core;

/// <summary>
/// Строки журнала о прокси: настройки Windows из реестра и адреса без логина и пароля.
/// </summary>
public static class ProxyDiagnostics
{
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

    internal static bool SameEndpoint(Uri proxy, Uri destination)
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
