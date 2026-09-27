using System.Net;
using System.Net.Sockets;

namespace ExternalIpWidget.Core;

/// <summary>
/// Две строки для компактной плашки: у IPv4 сверху первые два октета, снизу два последних.
/// </summary>
public readonly record struct AddressLines(string Top, string Bottom)
{
    public bool HasSecondLine => Bottom.Length > 0;

    public static AddressLines From(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return new AddressLines("…", "");

        var text = address.Trim();
        if (IPAddress.TryParse(text, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return FromOctets(ip.GetAddressBytes());

            if (ip.IsIPv4MappedToIPv6)
                return FromOctets(ip.MapToIPv4().GetAddressBytes());

            return SplitAtColon(ip.ToString());
        }

        var parts = text.Split('.');
        if (parts.Length == 4 && parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit)))
            return new AddressLines($"{parts[0]}.{parts[1]}", $"{parts[2]}.{parts[3]}");

        return new AddressLines(text, "");
    }

    private static AddressLines FromOctets(byte[] octets)
    {
        return new AddressLines($"{octets[0]}.{octets[1]}", $"{octets[2]}.{octets[3]}");
    }

    private static AddressLines SplitAtColon(string text)
    {
        var mid = text.Length / 2;
        var at = text.LastIndexOf(':', mid);
        if (at <= 0)
            at = text.IndexOf(':', mid);
        if (at <= 0 || at >= text.Length - 1)
            return new AddressLines(text, "");

        return new AddressLines(text[..at], text[(at + 1)..]);
    }
}
