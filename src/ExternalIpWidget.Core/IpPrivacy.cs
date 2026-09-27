using System.Net;
using System.Net.Sockets;

namespace ExternalIpWidget.Core;

/// <summary>
/// Адреса, которые не являются публичными в интернете:
/// локальные сети, link-local и CGNAT провайдера.
/// </summary>
public static class IpPrivacy
{
    public static bool IsNonPublic(string address)
    {
        return IPAddress.TryParse(address, out var ip) && IsNonPublic(ip);
    }

    public static bool IsNonPublic(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127)
                return true;
            if (b[0] == 172 && b[1] is >= 16 and <= 31)
                return true;
            if (b[0] == 192 && b[1] == 168)
                return true;
            if (b[0] == 169 && b[1] == 254)
                return true;
            // CGNAT: 100.64.0.0/10. Такой адрес может быть у роутера, но сайты видят другой.
            if (b[0] == 100 && b[1] is >= 64 and <= 127)
                return true;
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            // fc00::/7 — unique local.
            if ((b[0] & 0xFE) == 0xFC)
                return true;
            // fe80::/10 — link-local.
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)
                return true;
        }

        return false;
    }
}
