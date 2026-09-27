using System.Runtime.InteropServices;

namespace ExternalIpWidget;

public static class StartupShortcut
{
    public const string FileName = "Внешний IP.lnk";

    public static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        FileName);

    public static bool Exists()
    {
        try
        {
            return File.Exists(ShortcutPath);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Автозапуск поддерживается только в Windows.");

        if (!enabled)
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
            return;
        }

        var target = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к программе.");

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Компонент WScript.Shell недоступен.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Не удалось создать WScript.Shell.");
        try
        {
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? AppContext.BaseDirectory;
            shortcut.WindowStyle = 1;
            shortcut.Description = "Виджет внешнего IP-адреса";
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
