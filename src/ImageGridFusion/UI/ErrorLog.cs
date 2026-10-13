using System.Security;

namespace ImageGridFusion.UI;

/// <summary>
/// The last resort for an exception nothing caught: appended to <see cref="FileName"/> next to the
/// exe — the app's data lives beside it (RULES.md § App Settings) — and, on the UI thread, told in a
/// plain message box, the app staying up; never the .NET dialog
/// (workfiles/20261010-optimisation-lack-of-memory.md § 1).
/// </summary>
internal static class ErrorLog
{
    public const string FileName = "errors.log";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// Installs the handlers, before any window exists: the UI thread's exceptions are reported and
    /// survived; the others are logged before the process ends.
    /// </summary>
    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown error"));
    }

    /// <summary>What an exception means to the user: a memory shortage named as such, with what to do about it.</summary>
    public static string Describe(Exception exception) => exception is OutOfMemoryException
        ? "Not enough memory. Close other applications, or lower the video size (⚙ menu)."
        : exception.Message;

    /// <summary>Appends <paramref name="exception"/> to the log; false when the file cannot be written.</summary>
    public static bool Write(Exception exception)
    {
        try
        {
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {exception}{Environment.NewLine}{Environment.NewLine}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    /// <summary>Logs <paramref name="exception"/>, then tells the user in a message box; the app keeps running.</summary>
    private static void Report(Exception exception)
    {
        string details = Write(exception)
            ? $"The details were written to {FileName}, next to the app."
            : $"The details could not be written to {FileName}, next to the app.";
        MessageBox.Show(
            $"Something went wrong; the app keeps running.\n\n{Describe(exception)}\n\n{details}",
            "Image Grid Fusion",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
