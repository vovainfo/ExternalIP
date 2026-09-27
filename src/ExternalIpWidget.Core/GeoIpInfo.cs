using System.Globalization;

namespace ExternalIpWidget.Core;

/// <summary>
/// Приблизительное местоположение внешнего IP по базе GeoIP.
/// Это не координаты компьютера и не адрес сетевой карты.
/// </summary>
public sealed record GeoIpInfo(
    string Address,
    string? City,
    string? Region,
    string? Country,
    string? CountryCode,
    string? Organization,
    string? TimeZone,
    string ProviderName,
    string ProviderUrl)
{
    public bool BelongsToRussia()
    {
        var code = CountryCode?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            return code.Equals("RU", StringComparison.OrdinalIgnoreCase)
                || code.Equals("RUS", StringComparison.OrdinalIgnoreCase);
        }

        var name = Country?.Trim();
        if (string.IsNullOrEmpty(name))
            return false;

        return name.Equals("Россия", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Russia", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Russian Federation", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Российская Федерация", StringComparison.OrdinalIgnoreCase);
    }

    public string FormatPlace()
    {
        var place = string.Join(", ", PlaceParts());
        if (string.IsNullOrWhiteSpace(Organization))
            return place;

        return place.Length == 0 ? Organization : $"{place} · {Organization}";
    }

    public string FormatDetails()
    {
        var lines = new List<string>
        {
            "Приблизительное местоположение внешнего IP по базе GeoIP. Это не координаты компьютера.",
        };
        if (!string.IsNullOrWhiteSpace(TimeZone))
            lines.Add("Часовой пояс: " + TimeZone);
        lines.Add("Источник GeoIP: " + ProviderName);
        lines.Add(ProviderUrl);
        return string.Join(Environment.NewLine, lines);
    }

    private IEnumerable<string> PlaceParts()
    {
        if (!string.IsNullOrWhiteSpace(City))
            yield return City;

        if (!string.IsNullOrWhiteSpace(Region)
            && !string.Equals(Region, City, StringComparison.OrdinalIgnoreCase))
            yield return Region;

        var country = DisplayCountry();
        if (!string.IsNullOrWhiteSpace(country))
            yield return country;
    }

    private string? DisplayCountry()
    {
        if (!string.IsNullOrWhiteSpace(Country) && !LooksLikeCountryCode(Country))
            return Country;

        if (!string.IsNullOrWhiteSpace(CountryCode))
        {
            try
            {
                return new RegionInfo(CountryCode).DisplayName;
            }
            catch (ArgumentException)
            {
            }
        }

        return string.IsNullOrWhiteSpace(Country) ? null : Country;
    }

    private static bool LooksLikeCountryCode(string value)
    {
        return value.Length == 2 && value.All(char.IsLetter);
    }
}
