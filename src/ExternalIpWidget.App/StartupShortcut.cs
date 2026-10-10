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

        var target = CurrentTarget();
        WithShell(shell =>
        {
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? AppContext.BaseDirectory;
            shortcut.WindowStyle = 1;
            shortcut.Description = "External IP";
            shortcut.Save();
        });
    }

    /// <summary>
    /// Если автозапуск включён, но ярлык ведёт на другой exe (например, скачан и запущен новый релиз
    /// из другой папки), ярлык переводится на запущенную программу. Возвращает true, если ярлык переписан.
    /// </summary>
    public static bool RetargetToCurrent()
    {
        if (!OperatingSystem.IsWindows() || !Exists())
            return false;

        var target = CurrentTarget();
        string? existing = null;
        WithShell(shell => existing = (string?)shell.CreateShortcut(ShortcutPath).TargetPath);
        if (!string.IsNullOrWhiteSpace(existing)
            && string.Equals(Path.GetFullPath(existing), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            return false;

        SetEnabled(true);
        return true;
    }

    private static string CurrentTarget()
    {
        return Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к программе.");
    }

    private static void WithShell(Action<dynamic> use)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Компонент WScript.Shell недоступен.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Не удалось создать WScript.Shell.");
        try
        {
            use(shell);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
