namespace ExternalIpWidget.Core;

/// <summary>
/// Возвращает окно на видимую часть рабочего стола.
/// Windows при скрытии формы часто уносит её в координаты вроде -32000.
/// </summary>
public static class WindowPlacement
{
    public const int MinimumVisibleWidth = 80;
    public const int MinimumVisibleHeight = 40;

    public static bool HasUsefulOverlap(PixelRect window, PixelRect workArea)
    {
        var width = Overlap(window.X, window.Right, workArea.X, workArea.Right);
        var height = Overlap(window.Y, window.Bottom, workArea.Y, workArea.Bottom);
        return width >= MinimumVisibleWidth && height >= MinimumVisibleHeight;
    }

    public static PixelRect MoveIntoView(PixelRect window, IReadOnlyList<PixelRect> workAreas)
    {
        if (workAreas.Count == 0 || workAreas.Any(area => HasUsefulOverlap(window, area)))
            return window;

        var target = workAreas[0];
        var width = Math.Min(Math.Max(window.Width, 1), Math.Max(target.Width, 1));
        var height = Math.Min(Math.Max(window.Height, 1), Math.Max(target.Height, 1));
        var x = target.X + Math.Max(0, (target.Width - width) / 2);
        var y = target.Y + Math.Max(0, (target.Height - height) / 2);
        return new PixelRect(x, y, width, height);
    }

    private static int Overlap(int start, int end, int areaStart, int areaEnd)
    {
        return Math.Max(0, Math.Min(end, areaEnd) - Math.Max(start, areaStart));
    }
}
