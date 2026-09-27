namespace ExternalIpWidget;

static class Program
{
    [STAThread]
    static void Main()
    {
        var mutex = AcquireSingleInstance();
        using (mutex)
        {
            if (mutex is null)
            {
                if (!SingleInstance.TryActivate())
                {
                    MessageBox.Show(
                        "Виджет внешнего IP уже запущен. Его значок находится в области уведомлений рядом с часами.",
                        "Внешний IP",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    private static Mutex? AcquireSingleInstance()
    {
        const string name = @"Local\ExternalIpWidget";
        try
        {
            var mutex = new Mutex(true, name, out var created);
            return created ? mutex : Released(mutex);
        }
        catch (AbandonedMutexException ex)
        {
            return ex.Mutex ?? new Mutex(true, name, out _);
        }
    }

    private static Mutex? Released(Mutex mutex)
    {
        mutex.Dispose();
        return null;
    }
}
