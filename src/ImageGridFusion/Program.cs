using ImageGridFusion.Composition;
using ImageGridFusion.UI;

namespace ImageGridFusion;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // The hidden-start argument, the second title with its value, and the new-instance argument are not files to load.
        static bool IsHidden(string arg) => arg.Equals(TrayApplicationContext.HiddenArgument, StringComparison.OrdinalIgnoreCase);
        static bool IsTitle(string arg) => arg.Equals(MainForm.TitleArgument, StringComparison.OrdinalIgnoreCase);
        static bool IsNewInstance(string arg) => arg.Equals(SingleInstance.NewInstanceArgument, StringComparison.OrdinalIgnoreCase);
        static bool IsOption(string arg) => IsHidden(arg) || IsTitle(arg) || IsNewInstance(arg);
        var files = new List<string>();
        string? secondTitle = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (IsTitle(args[i]))
            {
                // Its value is the next argument, unless that one is an option itself. Missing or blank, it is
                // ignored; given twice, the last one wins.
                if (i + 1 < args.Length && !IsOption(args[i + 1]))
                {
                    i++;
                    if (!string.IsNullOrWhiteSpace(args[i]))
                    {
                        secondTitle = args[i].Trim();
                    }
                }
            }
            else if (!IsOption(args[i]))
            {
                files.Add(args[i]);
            }
        }

        bool hidden = args.Any(IsHidden);

        // One instance per exe location: a later launch hands its files over to the running one and ends — with
        // --tray (Windows start-up), it leaves that instance as it is. --new-instance runs outside the lock.
        bool newInstance = args.Any(IsNewInstance);
        using var instance = newInstance ? null : SingleInstance.TryAcquire();
        if (!newInstance && instance is null)
        {
            if (!hidden)
            {
                SingleInstance.HandOver(files);
            }

            return;
        }

        ApplicationConfiguration.Initialize();

        // Writes of the app's own accord, left to the normal instance: a test instance never moves the user's settings.
        if (!newInstance)
        {
            RegistryMigration.Run();
            StartupRegistration.Refresh();
        }

        // Before the main window: its zoom slider takes its range from it.
        ImageLook.MaxZoom = AppSettings.MaxZoom / 100.0;
        var context = new TrayApplicationContext(new MainForm(files.ToArray(), secondTitle, savesWindowSize: !newInstance), hidden);
        instance?.Listen(context.Launched);
        Application.Run(context);
    }
}
