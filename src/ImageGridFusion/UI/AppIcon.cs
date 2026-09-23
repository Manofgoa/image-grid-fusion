namespace ImageGridFusion.UI;

/// <summary>The app's 2×2 grid icon, shared by the exe, the window and the tray.</summary>
internal static class AppIcon
{
    /// <summary>Loads the multi-size icon; each consumer picks the frame it needs from it.</summary>
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("ImageGridFusion.app.ico")!;
        return new Icon(stream);
    }
}
