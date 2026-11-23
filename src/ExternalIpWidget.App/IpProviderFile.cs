using System.Text.Json;
using System.Text.Json.Nodes;
using ExternalIpWidget.Core;

namespace ExternalIpWidget;

/// <summary>
/// Список сервисов проверки IP лежит рядом с программой.
/// Нет файла — используется встроенный порядок.
/// </summary>
internal static class IpProviderFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public const string FileName = "ip-providers.json";

    public static string FilePath => Path.Combine(ProgramDirectory(), FileName);

    public static IReadOnlyList<IpProvider> Load()
    {
        var stored = TryRead(FilePath);
        if (stored is not null)
            return stored;

        var legacy = TryReadLegacySettings();
        if (legacy is null)
            return PublicIpLookup.DefaultProviders;

        try
        {
            var saved = Save(legacy);
            RemoveLegacySettings();
            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return legacy.Count == 0 ? PublicIpLookup.DefaultProviders : legacy;
        }
    }

    public static IReadOnlyList<IpProvider> Save(IReadOnlyList<IpProvider> providers)
    {
        var valid = Sanitize(providers);
        if (valid.Count == 0 || MatchesDefault(valid))
        {
            TryDelete(FilePath);
            return PublicIpLookup.DefaultProviders;
        }

        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(valid.Select(SavedProvider.From).ToArray(), JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
        return valid;
    }

    private static IReadOnlyList<IpProvider>? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var items = JsonSerializer.Deserialize<List<SavedProvider>>(File.ReadAllText(path), JsonOptions);
            var valid = Sanitize(items);
            return valid.Count == 0 ? PublicIpLookup.DefaultProviders : valid;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return PublicIpLookup.DefaultProviders;
        }
    }

    private static IReadOnlyList<IpProvider>? TryReadLegacySettings()
    {
        try
        {
            var path = SettingsStore.FilePath;
            if (!File.Exists(path))
                return null;

            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root?["ipProviders"] is not JsonArray array)
                return null;

            var items = array.Deserialize<List<SavedProvider>>(JsonOptions);
            return Sanitize(items);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void RemoveLegacySettings()
    {
        try
        {
            var path = SettingsStore.FilePath;
            if (!File.Exists(path))
                return;

            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root || root["ipProviders"] is null)
                return;

            root.Remove("ipProviders");
            var temp = path + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(JsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
    }

    private static List<IpProvider> Sanitize(IReadOnlyList<IpProvider>? providers)
    {
        return Sanitize(providers?.Select(SavedProvider.From));
    }

    private static List<IpProvider> Sanitize(IEnumerable<SavedProvider>? providers)
    {
        var valid = new List<IpProvider>();
        if (providers is null)
            return valid;

        foreach (var item in providers)
        {
            var name = (item?.Name ?? "").Trim();
            var url = (item?.Url ?? "").Trim();
            if (name.Length == 0 || url.Length == 0)
                continue;

            try
            {
                valid.Add(new IpProvider(name, url));
            }
            catch (ArgumentException)
            {
            }
        }

        return valid;
    }

    private static bool MatchesDefault(IReadOnlyList<IpProvider> providers)
    {
        var builtIn = PublicIpLookup.DefaultProviders;
        if (providers.Count != builtIn.Count)
            return false;

        for (var i = 0; i < providers.Count; i++)
        {
            if (!string.Equals(providers[i].Name, builtIn[i].Name, StringComparison.Ordinal)
                || !string.Equals(providers[i].Url, builtIn[i].Url, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string ProgramDirectory()
    {
        var process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process)
            && !Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(process);
            if (!string.IsNullOrWhiteSpace(directory))
                return directory;
        }

        return AppContext.BaseDirectory;
    }

    private sealed class SavedProvider
    {
        public string Name { get; set; } = "";

        public string Url { get; set; } = "";

        public static SavedProvider From(IpProvider provider)
        {
            return new SavedProvider { Name = provider.Name, Url = provider.Url };
        }
    }
}
