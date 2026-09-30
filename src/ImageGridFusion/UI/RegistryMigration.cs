using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace ImageGridFusion.UI;

/// <summary>
/// The registry's last use: the app once kept its settings there and its "Start with Windows" in the Run
/// key. At start-up, whatever is still found is moved — the settings into <see cref="AppSettings"/>' file,
/// the Run value into the Startup folder's shortcut — then deleted, so it happens once. Silent on failure:
/// what could not be moved stays in the registry and is tried again at the next start-up.
/// </summary>
internal static class RegistryMigration
{
    private const string SettingsKey = @"Software\ImageGridFusion";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ImageGridFusion";

    public static void Run()
    {
        MoveSettings();
        MoveStartWithWindows();
    }

    /// <summary>The registry's values the file does not have yet are copied into it, then the key is deleted.</summary>
    private static void MoveSettings()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                if (key is null)
                {
                    return;
                }

                var values = new List<(string, JsonNode?)>();
                foreach (string name in AppSettings.RegistryNames.Where(n => !AppSettings.Has(n)))
                {
                    switch (key.GetValue(name))
                    {
                        case int number:
                            values.Add((name, number));
                            break;
                        case string text:
                            values.Add((name, text));
                            break;
                    }
                }

                if (values.Count > 0)
                {
                    AppSettings.Save([.. values]);
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(SettingsKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (StartupRegistration.IsRegistryError(ex) || AppSettings.IsSaveError(ex))
        {
        }
    }

    /// <summary>A Run value becomes the Startup folder's shortcut, then is deleted.</summary>
    private static void MoveStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(RunValueName) is not string)
            {
                return;
            }

            StartupRegistration.SetEnabled(true);
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (StartupRegistration.IsRegistryError(ex) || StartupRegistration.IsStartupError(ex))
        {
        }
    }
}
