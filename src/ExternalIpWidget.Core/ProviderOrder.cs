namespace ExternalIpWidget.Core;

/// <summary>
/// Порядок обхода сервисов по сохранённому списку имён. Незнакомые и повторные имена
/// пропускаются, а сервисы, которых нет в списке, идут следом в исходном порядке:
/// так новый сервис из обновления не теряется, а удалённый не ломает настройки.
/// </summary>
public static class ProviderOrder
{
    public static IReadOnlyList<T> Arrange<T>(IEnumerable<string>? names, IReadOnlyList<T> available)
        where T : class, INamedProvider
    {
        ArgumentNullException.ThrowIfNull(available);

        var ordered = new List<T>(available.Count);
        foreach (var name in names ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var provider = available.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (provider is not null && !ordered.Contains(provider))
                ordered.Add(provider);
        }

        foreach (var provider in available)
        {
            if (!ordered.Contains(provider))
                ordered.Add(provider);
        }

        return ordered;
    }

    public static List<string> Names<T>(IEnumerable<string>? names, IReadOnlyList<T> available)
        where T : class, INamedProvider
    {
        return Arrange(names, available).Select(provider => provider.Name).ToList();
    }
}
