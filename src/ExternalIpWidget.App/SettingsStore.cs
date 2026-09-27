using System.Text.Json;

namespace ExternalIpWidget;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExternalIpWidget",
        "settings.json");

    public static WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new WidgetSettings();

            var settings = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(FilePath), JsonOptions)
                ?? new WidgetSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new WidgetSettings();
        }
    }

    public static void Save(WidgetSettings settings)
    {
        settings.Normalize();
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
