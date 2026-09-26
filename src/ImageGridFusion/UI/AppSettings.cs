using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// The app's settings remembered between sessions, per user, in the registry — no settings file: the
/// border color, whether the borders' Twitter corners are on by default, the file explorer's base
/// folder, whether its panel is open and its column count, and the window's size.
/// </summary>
internal static class AppSettings
{
    private const string Key = @"Software\ImageGridFusion";
    private const string BorderColorName = "BorderColor";
    private const string TwitterCornersName = "TwitterCornersByDefault";
    private const string ExplorerFolderName = "ExplorerFolder";
    private const string ExplorerPanelOpenName = "ExplorerPanelOpen";
    private const string ExplorerColumnsName = "ExplorerColumns";
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

    /// <summary>How many columns of tiles the file explorer shows, 1 to 5; one when nothing was saved or the key cannot be read.</summary>
    public static int ExplorerColumns
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Key);
                return key?.GetValue(ExplorerColumnsName) is int value ? Math.Clamp(value, ThumbnailGrid.MinColumns, ThumbnailGrid.MaxColumns) : ThumbnailGrid.MinColumns;
            }
            catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
            {
                return ThumbnailGrid.MinColumns;
            }
        }
    }

    /// <summary>Saves the file explorer's column count; throws an <see cref="StartupRegistration.IsRegistryError"/> exception on failure.</summary>
    public static void SaveExplorerColumns(int columns)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(ExplorerColumnsName, columns, RegistryValueKind.DWord);
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
