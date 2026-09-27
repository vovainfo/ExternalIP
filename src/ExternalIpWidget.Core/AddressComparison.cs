using System.Net;

namespace ExternalIpWidget.Core;

/// <summary>
/// Поясняет, почему внешний адрес и адрес сетевой карты — разные вещи.
/// </summary>
public static class AddressComparison
{
    public const string NicUnavailable =
        "Адрес сетевой карты сейчас недоступен. Внешний адрес всё равно запрашивается у стороннего сервиса.";

    public const string Same =
        "Совпадает с адресом сетевой карты: компьютер выходит в интернет напрямую, без NAT.";

    public const string Different =
        "Отличается от адреса сетевой карты. Показан адрес, который увидел внешний сервис.";

    public static string Describe(string externalAddress, string? nicAddress)
    {
        if (string.IsNullOrWhiteSpace(nicAddress))
            return NicUnavailable;

        if (string.Equals(externalAddress, nicAddress, StringComparison.OrdinalIgnoreCase))
            return Same;

        if (IPAddress.TryParse(nicAddress, out var nic) && IpPrivacy.IsNonPublic(nic))
            return "";

        return Different;
    }
}
