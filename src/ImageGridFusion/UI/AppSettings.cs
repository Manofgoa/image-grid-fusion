using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// The app's settings remembered between sessions, per user, in the registry — no settings file. The
/// border color only, for now.
/// </summary>
internal static class AppSettings
{
    private const string Key = @"Software\ImageGridFusion";
    private const string BorderColorName = "BorderColor";

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
}
