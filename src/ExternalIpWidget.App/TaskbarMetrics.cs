using System.Runtime.InteropServices;
using ExternalIpWidget.Core;

namespace ExternalIpWidget;

internal static class TaskbarMetrics
{
    public static bool TryGet(out PixelRect taskbar, out PixelRect? tray, out PixelRect monitor)
    {
        taskbar = default;
        tray = null;
        monitor = default;

        var barWindow = FindWindow("Shell_TrayWnd", null);
        if (barWindow == IntPtr.Zero || !GetWindowRect(barWindow, out var barRect))
            return false;

        taskbar = ToRect(barRect);
        var trayWindow = FindWindowEx(barWindow, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayWindow != IntPtr.Zero && GetWindowRect(trayWindow, out var trayRect))
            tray = ToRect(trayRect);

        monitor = taskbar;
        var display = MonitorFromWindow(barWindow, 2);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (display != IntPtr.Zero && GetMonitorInfo(display, ref info))
            monitor = ToRect(info.rcMonitor);

        return taskbar.Width > 0 && taskbar.Height > 0;
    }

    private static PixelRect ToRect(RectNative native)
    {
        return new PixelRect(native.Left, native.Top, native.Right - native.Left, native.Bottom - native.Top);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectNative rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
    }
}
