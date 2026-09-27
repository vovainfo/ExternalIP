using System.Runtime.InteropServices;

namespace ExternalIpWidget;

public static class StartupShortcut
{
    public const string FileName = "External IP.lnk";
    private const string PreviousFileName = "Внешний IP.lnk";

    public static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        FileName);

    private static string PreviousShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        PreviousFileName);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

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
            DeleteIfExists(ShortcutPath);
            DeleteIfExists(PreviousShortcutPath);
            return;
        }

        DeleteIfExists(PreviousShortcutPath);

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
            shortcut.Description = "External IP";
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
