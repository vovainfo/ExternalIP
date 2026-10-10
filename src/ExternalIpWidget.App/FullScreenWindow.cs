using System.Runtime.InteropServices;
using System.Text;
using ExternalIpWidget.Core;

namespace ExternalIpWidget;

/// <summary>
/// Полноэкранное окно (видео, игра, презентация) закрывает панель задач. Плашка в это время
/// не должна висеть поверх него.
/// </summary>
internal static class FullScreenWindow
{
    private const int QunsRunningD3DFullScreen = 3;
    private const int QunsPresentationMode = 4;

    private static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    public static bool Covers(PixelRect monitor, IntPtr ownWindow)
    {
        if (SHQueryUserNotificationState(out var state) == 0
            && state is QunsRunningD3DFullScreen or QunsPresentationMode)
            return true;

        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == ownWindow || IsShell(foreground))
            return false;
        if (!GetWindowRect(foreground, out var rect))
            return false;

        return rect.Left <= monitor.X
            && rect.Top <= monitor.Y
            && rect.Right >= monitor.Right
            && rect.Bottom >= monitor.Bottom;
    }

    private static bool IsShell(IntPtr window)
    {
        var name = new StringBuilder(64);
        if (GetClassName(window, name, name.Capacity) == 0)
            return false;
        var text = name.ToString();
        return ShellClasses.Any(shell => shell.Equals(text, StringComparison.Ordinal));
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectNative rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
