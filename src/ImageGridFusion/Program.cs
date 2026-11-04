using ImageGridFusion.Components;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using ImageGridFusion.UI;
using ImageGridFusion.Web;

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

        // Started again by the Component updates window: the instance before is waited for, so its lock and its files are
        // free (workfiles/20261102-component-update-verification.md § Restart).
        WaitForRestartedInstance();

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

        // The components manifest, then the swaps left for this start — before anything loads a native runtime. Written
        // by the normal instance on its own; a --new-instance writes it only to finish a user's swap.
        var manifest = ComponentManifest.Load(ComponentRegistry.All, writes: !newInstance);
        var swapped = ComponentInstaller.ApplyPending(manifest, ComponentRegistry.All);

        ApplicationConfiguration.Initialize();

        // An exception nothing caught is logged next to the exe and told in a plain message box, the app
        // staying up — never the .NET dialog (workfiles/20261010-optimisation-lack-of-memory.md § 1).
        ErrorLog.Install();

        // Writes of the app's own accord, left to the normal instance: a test instance never moves the user's settings.
        if (!newInstance)
        {
            RegistryMigration.Run();
            StartupRegistration.Refresh();
            Favorites.Migrate();
            _ = Task.Run(UrlDownloads.Clean);
            _ = Task.Run(Imaging.UpscaleCache.Clean);
        }

        // Before the main window: its zoom slider takes its range from it.
        ImageLook.MaxZoom = AppSettings.MaxZoom / 100.0;
        var form = new MainForm(files.ToArray(), secondTitle, savesWindowSize: !newInstance);
        form.ComponentsAtStart(manifest.LoadError, swapped);
        var context = new TrayApplicationContext(form, hidden);
        instance?.Listen(context.Launched);
        Application.Run(context);
    }

    /// <summary>
    /// The UI language, read once, before any code of the app runs — <see cref="Main"/> and every type initializer, such as
    /// the components' purposes in <c>ComponentRegistry.All</c>: every text is looked up in it from then on, the background
    /// threads' included; a change in the ⚙ menu applies at the next start. Only the UI culture: sorting and casing keep
    /// the system's (workfiles/20261105-french-translation.md § UI Language Setting).
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void SetUiLanguage()
    {
        var uiCulture = Resources.Texts.CultureOf(AppSettings.Language);
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        System.Globalization.CultureInfo.CurrentUICulture = uiCulture;
    }

    /// <summary>
    /// The process id a restart gives (<see cref="MainForm.RestartWaitVariable"/>), waited for at most 30 s; the variable
    /// cleared, so the processes this one starts do not wait.
    /// </summary>
    private static void WaitForRestartedInstance()
    {
        string? id = Environment.GetEnvironmentVariable(MainForm.RestartWaitVariable);
        if (id is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable(MainForm.RestartWaitVariable, null);
        if (!int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int pid))
        {
            return;
        }

        try
        {
            using var before = System.Diagnostics.Process.GetProcessById(pid);
            before.WaitForExit(30_000);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }
}
