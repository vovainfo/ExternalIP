namespace ExternalIpWidget.Core;

/// <summary>Прямоугольник в физических пикселях экрана.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Intersects(PixelRect other)
    {
        return X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    }
}

/// <summary>
/// Кладёт компактную плашку с IP на панель задач: у горизонтальной — слева от области уведомлений,
/// у вертикальной — вдоль неё, текстом внутрь экрана.
/// </summary>
public static class TaskbarBandLayout
{
    public static bool TryGetBounds(
        PixelRect taskbar,
        PixelRect? tray,
        PixelRect monitor,
        int textWidth,
        int textHeight,
        int nudge,
        out PixelRect bounds)
    {
        bounds = default;
        if (taskbar.Width < 16 || taskbar.Height < 16)
            return false;

        var edge = DockedEdge(taskbar, monitor);
        var trayOnBar = tray is PixelRect area && area.Intersects(taskbar) ? area : (PixelRect?)null;

        if (edge is DockEdge.Top or DockEdge.Bottom)
            return TryHorizontal(taskbar, trayOnBar, textWidth, textHeight, nudge, out bounds);

        return TryVertical(taskbar, trayOnBar, edge, textWidth, textHeight, nudge, out bounds);
    }

    private static bool TryHorizontal(
        PixelRect taskbar,
        PixelRect? tray,
        int textWidth,
        int textHeight,
        int nudge,
        out PixelRect bounds)
    {
        var bandHeight = Math.Clamp(textHeight, 16, Math.Max(16, taskbar.Height - 4));
        var bandWidth = Math.Clamp(textWidth, 48, Math.Max(48, taskbar.Width - 60));
        var minX = taskbar.X + Math.Min(48, Math.Max(0, taskbar.Width - bandWidth - 4));
        var maxX = Math.Max(minX, taskbar.Right - bandWidth - 4);
        var trayOnRight = tray is not PixelRect area
            || area.X + area.Width / 2 >= taskbar.X + taskbar.Width / 2;
        var anchor = tray is PixelRect known
            ? trayOnRight ? known.X - bandWidth - 4 : known.Right + 4
            : taskbar.Right - bandWidth - 14;
        var x = Math.Clamp(anchor + nudge, minX, maxX);
        var y = taskbar.Y + (taskbar.Height - bandHeight) / 2;
        bounds = new PixelRect(x, y, bandWidth, bandHeight);
        return true;
    }

    private static bool TryVertical(
        PixelRect taskbar,
        PixelRect? tray,
        DockEdge edge,
        int textWidth,
        int textHeight,
        int nudge,
        out PixelRect bounds)
    {
        var bandWidth = Math.Clamp(textWidth, 48, 280);
        var bandHeight = Math.Clamp(textHeight, 18, 36);
        var minY = taskbar.Y + Math.Min(48, Math.Max(0, taskbar.Height - bandHeight - 4));
        var maxY = Math.Max(minY, taskbar.Bottom - bandHeight - 4);
        var anchor = tray is PixelRect known
            ? known.Y - bandHeight - 4
            : taskbar.Bottom - bandHeight - 14;
        var y = Math.Clamp(anchor + nudge, minY, maxY);
        var x = edge == DockEdge.Left ? taskbar.Right - 2 : taskbar.X - bandWidth + 2;
        bounds = new PixelRect(x, y, bandWidth, bandHeight);
        return true;
    }

    private static DockEdge DockedEdge(PixelRect taskbar, PixelRect monitor)
    {
        if (taskbar.Height <= taskbar.Width)
        {
            var top = Math.Abs(taskbar.Y - monitor.Y);
            var bottom = Math.Abs(taskbar.Bottom - monitor.Bottom);
            return top <= bottom ? DockEdge.Top : DockEdge.Bottom;
        }

        var left = Math.Abs(taskbar.X - monitor.X);
        var right = Math.Abs(taskbar.Right - monitor.Right);
        return left <= right ? DockEdge.Left : DockEdge.Right;
    }

    private enum DockEdge
    {
        Left,
        Top,
        Right,
        Bottom,
    }
}
