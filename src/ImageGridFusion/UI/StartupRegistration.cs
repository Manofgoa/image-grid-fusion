using System.Security;
using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// "Start with Windows": a per-user value in the Run key, launching the exe hidden in the tray.
/// The registry is the only record of the setting.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ImageGridFusion";

    private static string Command => $"\"{Environment.ProcessPath ?? Application.ExecutablePath}\" {TrayApplicationContext.HiddenArgument}";

    /// <summary>Whether the Run value exists; false when the key cannot be read.</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception ex) when (IsRegistryError(ex))
            {
                return false;
            }
        }
    }

    /// <summary>Writes or deletes the Run value; throws an <see cref="IsRegistryError"/> exception on failure.</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Points an existing registration at this exe, in case the exe was moved since; silent on failure.</summary>
    public static void Refresh()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && current != Command)
            {
                key.SetValue(ValueName, Command);
            }
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
        }
    }

    public static bool IsRegistryError(Exception ex) => ex is UnauthorizedAccessException or SecurityException or IOException;
}
