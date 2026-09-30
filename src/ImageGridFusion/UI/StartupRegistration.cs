using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;

namespace ImageGridFusion.UI;

/// <summary>
/// "Start with Windows": a shortcut in the user's Startup folder, launching the exe hidden in the tray.
/// The shortcut is the only record of the setting — never the registry.
/// </summary>
internal static class StartupRegistration
{
    private const string ShortcutName = "ImageGridFusion.lnk";

    private static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);

    private static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    /// <summary>Whether the shortcut exists.</summary>
    public static bool IsEnabled => File.Exists(ShortcutPath);

    /// <summary>Writes or deletes the shortcut; throws an <see cref="IsStartupError"/> exception on failure.</summary>
    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            File.Delete(ShortcutPath);
            return;
        }

        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(ExePath);
            link.SetArguments(TrayApplicationContext.HiddenArgument);
            link.SetWorkingDirectory(Path.GetDirectoryName(ExePath) ?? "");
            link.SetDescription("Image Grid Fusion, started hidden in the tray");
            ((IPersistFile)link).Save(ShortcutPath, fRemember: true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    /// <summary>Points an existing shortcut at this exe, in case the exe was moved since; silent on failure.</summary>
    public static void Refresh()
    {
        try
        {
            if (IsEnabled)
            {
                SetEnabled(true);
            }
        }
        catch (Exception ex) when (IsStartupError(ex))
        {
        }
    }

    /// <summary>Whether writing or deleting the shortcut failed: the exceptions <see cref="SetEnabled"/> throws.</summary>
    public static bool IsStartupError(Exception ex) => ex is UnauthorizedAccessException or SecurityException or IOException or COMException;

    /// <summary>The registry's own failures, for <see cref="RegistryMigration"/> only.</summary>
    public static bool IsRegistryError(Exception ex) => ex is UnauthorizedAccessException or SecurityException or IOException;

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);

        void GetIDList(out IntPtr ppidl);

        void SetIDList(IntPtr pidl);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        void GetHotkey(out short pwHotkey);

        void SetHotkey(short wHotkey);

        void GetShowCmd(out int piShowCmd);

        void SetShowCmd(int iShowCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);

        void Resolve(IntPtr hwnd, int fFlags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
