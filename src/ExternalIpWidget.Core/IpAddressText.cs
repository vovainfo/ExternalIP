using System.Net;
using System.Net.Sockets;

namespace ExternalIpWidget.Core;

/// <summary>
/// Достаёт IP-адрес из короткого текстового ответа сервиса.
/// </summary>
public static class IpAddressText
{
    public static bool TryParse(string? body, out string address)
    {
        address = "";
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var text = body.Trim().Trim('\uFEFF');
        var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var token = line.Trim().Trim('"', '\'').Trim();
        var space = token.IndexOfAny([' ', '\t']);
        if (space >= 0)
            token = token[..space];
        token = token.TrimEnd('.', ',', ';');

        if (!IPAddress.TryParse(token, out var ip))
            return false;
        if (!IsRoutableHostAddress(ip))
            return false;

        address = ip.ToString();
        return true;
    }

    public static string Parse(string body)
    {
        if (!TryParse(body, out var address))
            throw new FormatException("Ответ сервиса не содержит IP-адрес.");
        return address;
    }

    private static bool IsRoutableHostAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return false;
        if (ip.IsIPv6Multicast || IpPrivacy.IsNonPublic(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = ip.GetAddressBytes()[0];
            // Multicast, reserved и broadcast: это не внешний адрес хоста.
            if (first >= 224)
                return false;
        }

        return ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
    }
}
