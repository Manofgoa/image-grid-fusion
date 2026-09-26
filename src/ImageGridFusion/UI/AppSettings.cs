using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// The app's settings remembered between sessions, per user, in the registry — no settings file: the
/// border color, whether the borders' Twitter corners are on by default, the file explorer's base
/// folder and whether its panel is open.
/// </summary>
internal static class AppSettings
{
    private const string Key = @"Software\ImageGridFusion";
    private const string BorderColorName = "BorderColor";
    private const string TwitterCornersName = "TwitterCornersByDefault";
    private const string ExplorerFolderName = "ExplorerFolder";
    private const string ExplorerPanelOpenName = "ExplorerPanelOpen";

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
}
