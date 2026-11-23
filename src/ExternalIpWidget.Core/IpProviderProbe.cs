namespace ExternalIpWidget.Core;

/// <summary>
/// Ответ одного сервиса при опросе всего списка. Неудача не отменяет остальные.
/// </summary>
public sealed class IpProviderProbe
{
    public IpProviderProbe(int index, string name, string url, string? address, string? error)
    {
        Index = index;
        Name = name;
        Url = url;
        Address = address;
        Error = error;
    }

    public int Index { get; }

    public string Name { get; }

    public string Url { get; }

    /// <summary>Внешний IPv4, если сервис его вернул.</summary>
    public string? Address { get; }

    /// <summary>Почему ответ не принят. Пусто, когда адрес есть.</summary>
    public string? Error { get; }
}
