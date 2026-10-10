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
    /// Если автозапуск включён, а ярлык ведёт на удалённый exe или на более старую версию
    /// (скачан и запущен новый релиз из другой папки), ярлык переводится на запущенную программу.
    /// Копия той же или более старой версии автозапуск не перехватывает. Возвращает true, если ярлык переписан.
    /// </summary>
    public static bool RetargetToCurrent()
    {
        if (!OperatingSystem.IsWindows() || !Exists())
            return false;

        var target = CurrentTarget();
        string? existing = null;
        WithShell(shell => existing = (string?)shell.CreateShortcut(ShortcutPath).TargetPath);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            if (string.Equals(Path.GetFullPath(existing), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                return false;
            if (File.Exists(existing) && !IsNewer(target, existing))
                return false;
        }

        SetEnabled(true);
        return true;
    }

    private static bool IsNewer(string candidate, string current)
    {
        var next = FileVersion(candidate);
        var previous = FileVersion(current);
        return next is not null && previous is not null && next > previous;
    }

    private static Version? FileVersion(string path)
    {
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
        if (info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0)
            return null;
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
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
