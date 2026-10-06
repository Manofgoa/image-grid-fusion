using ImageGridFusion.UI;

namespace ImageGridFusion;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        RegistryMigration.Run();
        StartupRegistration.Refresh();

        // The hidden-start argument and the second title, with its value, are not files to load.
        static bool IsHidden(string arg) => arg.Equals(TrayApplicationContext.HiddenArgument, StringComparison.OrdinalIgnoreCase);
        static bool IsTitle(string arg) => arg.Equals(MainForm.TitleArgument, StringComparison.OrdinalIgnoreCase);
        var files = new List<string>();
        string? secondTitle = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (IsTitle(args[i]))
            {
                // Its value is the next argument, unless that one is an option itself. Missing or blank, it is
                // ignored; given twice, the last one wins.
                if (i + 1 < args.Length && !IsHidden(args[i + 1]) && !IsTitle(args[i + 1]))
                {
                    i++;
                    if (!string.IsNullOrWhiteSpace(args[i]))
                    {
                        secondTitle = args[i].Trim();
                    }
                }
            }
            else if (!IsHidden(args[i]))
            {
                files.Add(args[i]);
            }
        }

        Application.Run(new TrayApplicationContext(new MainForm(files.ToArray(), secondTitle), hidden: args.Any(IsHidden)));
    }
}
