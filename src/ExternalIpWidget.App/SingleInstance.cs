using System.Runtime.InteropServices;

namespace ExternalIpWidget;

internal static class SingleInstance
{
    public const string ShowMessage = "ExternalIpWidget.Show";
    public const string WindowProperty = "ExternalIpWidget.Main";
    private const int SwRestore = 9;

    public static void Mark(IntPtr handle)
    {
        SetProp(handle, WindowProperty, new IntPtr(1));
    }

    public static bool TryActivate()
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (GetProp(hwnd, WindowProperty) == IntPtr.Zero)
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        if (found == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(found, out var processId);
        AllowSetForegroundWindow(processId);
        PostMessage(found, RegisterShowMessage(), IntPtr.Zero, IntPtr.Zero);
        ShowWindow(found, SwRestore);
        SetForegroundWindow(found);
        return true;
    }

    public static uint RegisterShowMessage() => RegisterWindowMessage(ShowMessage);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetProp(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);
}
