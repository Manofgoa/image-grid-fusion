using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// The app's settings remembered between sessions, per user, in the registry — no settings file: the
/// border color, whether the borders' Twitter corners are on by default, the file explorer's base
/// folder, whether its panel is open, its width and its tile size, and the window's size.
/// </summary>
internal static class AppSettings
{
    private const string Key = @"Software\ImageGridFusion";
    private const string BorderColorName = "BorderColor";
    private const string TwitterCornersName = "TwitterCornersByDefault";
    private const string ExplorerFolderName = "ExplorerFolder";
    private const string ExplorerPanelOpenName = "ExplorerPanelOpen";
    private const string ExplorerWidthName = "ExplorerWidth";
    private const string ExplorerTileSizeName = "ExplorerTileSize";
    private const string ExplorerPagesPerLoadName = "ExplorerPagesPerLoad";
    private const string WindowWidthName = "WindowWidth";
    private const string WindowHeightName = "WindowHeight";

    /// <summary>The border color before one is chosen.</summary>
    public static readonly Color DefaultBorderColor = Color.HotPink;

    /// <summary>The color of the borders; <see cref="DefaultBorderColor"/> when none was saved or the key cannot be read.</summary>
    public static Color BorderColor
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(BorderColorName) is int argb ? Color.FromArgb(argb) : DefaultBorderColor;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return DefaultBorderColor;
            }
        }
    }

    /// <summary>Saves the border color; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveBorderColor(Color color)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(BorderColorName, color.ToArgb(), RegistryValueKind.DWord);
    }

    /// <summary>Whether the borders start with their Twitter corners; true when nothing was saved or the key cannot be read.</summary>
    public static bool TwitterCornersByDefault
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(TwitterCornersName) is not int value || value != 0;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return true;
            }
        }
    }

    /// <summary>Saves whether the borders start with their Twitter corners; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveTwitterCornersByDefault(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(TwitterCornersName, on ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>The folder the file explorer indexes, with its subfolders; null when none was saved or the key cannot be read.</summary>
    public static string? ExplorerFolder
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerFolderName) is string { Length: > 0 } folder ? folder : null;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return null;
            }
        }
    }

    /// <summary>Saves the file explorer's folder; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerFolder(string folder)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerFolderName, folder, RegistryValueKind.String);
    }

    /// <summary>Whether the file explorer's panel is open; true when nothing was saved or the key cannot be read.</summary>
    public static bool ExplorerPanelOpen
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerPanelOpenName) is not int value || value != 0;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return true;
            }
        }
    }

    /// <summary>Saves whether the file explorer's panel is open; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerPanelOpen(bool open)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerPanelOpenName, open ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>
    /// The file explorer panel's open width in logical pixels, <see cref="FileExplorerPanel.MinOpenWidth"/>
    /// at least; the default width when nothing was saved or the key cannot be read.
    /// </summary>
    public static int ExplorerWidth
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerWidthName) is int value ? Math.Clamp(value, FileExplorerPanel.MinOpenWidth, 10000) : FileExplorerPanel.DefaultOpenWidth;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return FileExplorerPanel.DefaultOpenWidth;
            }
        }
    }

    /// <summary>Saves the file explorer panel's open width; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerWidth(int width)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerWidthName, width, RegistryValueKind.DWord);
    }

    /// <summary>
    /// The file explorer's tile size in logical pixels, <see cref="ThumbnailGrid.MinTileSize"/> to
    /// <see cref="ThumbnailGrid.MaxTileSize"/>; the default size when nothing was saved or the key
    /// cannot be read.
    /// </summary>
    public static int ExplorerTileSize
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerTileSizeName) is int value ? Math.Clamp(value, ThumbnailGrid.MinTileSize, ThumbnailGrid.MaxTileSize) : ThumbnailGrid.DefaultTileSize;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return ThumbnailGrid.DefaultTileSize;
            }
        }
    }

    /// <summary>Saves the file explorer's tile size; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerTileSize(int size)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerTileSizeName, size, RegistryValueKind.DWord);
    }

    /// <summary>
    /// How many pages of tiles the file explorer loads at a time, <see cref="FileExplorerPanel.MinPagesPerLoad"/>
    /// to <see cref="FileExplorerPanel.MaxPagesPerLoad"/>; the default when nothing was saved, the value
    /// is out of range, or the key cannot be read.
    /// </summary>
    public static int ExplorerPagesPerLoad
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerPagesPerLoadName) is int value
                    && value is >= FileExplorerPanel.MinPagesPerLoad and <= FileExplorerPanel.MaxPagesPerLoad
                    ? value
                    : FileExplorerPanel.DefaultPagesPerLoad;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return FileExplorerPanel.DefaultPagesPerLoad;
            }
        }
    }

    /// <summary>Saves the file explorer's pages per load; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerPagesPerLoad(int pages)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerPagesPerLoadName, pages, RegistryValueKind.DWord);
    }

    /// <summary>
    /// The client size the window had at its last use, in logical (96 DPI) pixels; null when none was
    /// saved, a value is missing or not positive, or the key cannot be read.
    /// </summary>
    public static Size? WindowClientSize
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key is not null
                    && key.GetValue(WindowWidthName) is int width && width > 0
                    && key.GetValue(WindowHeightName) is int height && height > 0
                    ? new Size(width, height)
                    : null;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return null;
            }
        }
    }

    /// <summary>Saves the window's client size, in logical pixels; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveWindowClientSize(Size size)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(WindowWidthName, size.Width, RegistryValueKind.DWord);
        key.SetValue(WindowHeightName, size.Height, RegistryValueKind.DWord);
    }
}
