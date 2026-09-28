namespace ExternalIpWidget.Core;

/// <summary>
/// Справочник GeoIP относится к адресу. Повторная проверка того же адреса не запрашивает его снова:
/// иначе короткая строка «определение…» сжимает окно, а прежнее местоположение тут же раздвигает его обратно.
/// </summary>
public static class GeoRefreshPolicy
{
    public static bool ShouldQuery(GeoIpInfo? known, string nextAddress)
    {
        if (string.IsNullOrWhiteSpace(nextAddress))
            return false;

        if (known is null || string.IsNullOrWhiteSpace(known.Address))
            return true;

        return !string.Equals(known.Address.Trim(), nextAddress.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
