using ImageGridFusion.UI;

namespace ImageGridFusion;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The hidden-start argument is not a file to load.
        static bool IsHidden(string arg) => arg.Equals(TrayApplicationContext.HiddenArgument, StringComparison.OrdinalIgnoreCase);
        string[] files = args.Where(arg => !IsHidden(arg)).ToArray();
        Application.Run(new TrayApplicationContext(new MainForm(files), hidden: args.Any(IsHidden)));
    }
}
