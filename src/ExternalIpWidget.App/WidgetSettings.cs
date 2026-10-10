using ExternalIpWidget.Core;

namespace ExternalIpWidget;

public sealed class WidgetSettings
{
    public bool AlwaysOnTop { get; set; } = true;

    public int RefreshMinutes { get; set; } = 5;

    public bool StartWithWindows { get; set; }

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public bool TrayHintShown { get; set; }

    public bool ShowOnTaskbar { get; set; } = true;

    public ProxyMode ProxyMode { get; set; }

    public bool ProxyChoiceSaved { get; set; }

    public string CustomProxy { get; set; } = "";

    public bool UseEnvironmentProxy { get; set; }

    public bool UseSystemProxy { get; set; }

    public int TaskbarNudge { get; set; }

    public bool TaskbarTwoLines { get; set; } = true;

    /// <summary>Размер шрифта IP на панели задач в пикселях; 0 — подбирать по толщине панели.</summary>
    public int TaskbarFontPx { get; set; } = TaskbarLabelStyle.AutoFontPx;

    public void Normalize()
    {
        if (RefreshMinutes < 1)
            RefreshMinutes = 1;
        if (RefreshMinutes > 120)
            RefreshMinutes = 120;
        TaskbarNudge = Math.Clamp(TaskbarNudge, -10000, 10000);
        TaskbarFontPx = TaskbarLabelStyle.NormalizeCustomFontPx(TaskbarFontPx);
        ProxyMode = ProxyModeMigration.Resolve(ProxyChoiceSaved, ProxyMode, UseEnvironmentProxy, UseSystemProxy);
        ProxyChoiceSaved = true;
        UseEnvironmentProxy = ProxyMode == ProxyMode.Environment;
        UseSystemProxy = ProxyMode == ProxyMode.System;
        CustomProxy = (CustomProxy ?? "").Trim();
    }
}
