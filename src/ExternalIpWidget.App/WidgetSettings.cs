namespace ExternalIpWidget;

public sealed class WidgetSettings
{
    public bool AlwaysOnTop { get; set; } = true;

    public int RefreshMinutes { get; set; } = 5;

    public bool StartWithWindows { get; set; }

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public bool TrayHintShown { get; set; }

    public void Normalize()
    {
        if (RefreshMinutes < 1)
            RefreshMinutes = 1;
        if (RefreshMinutes > 120)
            RefreshMinutes = 120;
    }
}
