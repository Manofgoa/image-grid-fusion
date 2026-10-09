using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ImageGridFusion.Web;

/// <summary>
/// yt-dlp, the fallback for the sites the built-in resolvers do not know. Not shipped with the app:
/// looked for in its own folder next to the exe only, and installed there with winget when the user
/// accepts the URL zone's offer. Asked for a single-file format, so no ffmpeg is needed.
/// See workfiles/20261009-url-content.md § yt-dlp.
/// </summary>
internal static partial class YtDlp
{
    public const string FolderName = "yt-dlp";

    private const string ExeName = "yt-dlp.exe";
    private const string WingetPackage = "yt-dlp.yt-dlp";

    // How often the progress is reported, in ms.
    private const int ReportInterval = 100;

    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>The command <see cref="InstallAsync"/> runs, as the user is shown it before it does.</summary>
    public static string InstallCommand => "winget " + string.Join(' ', InstallArguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

    private static string[] InstallArguments =>
    [
        "install", "--id", WingetPackage, "--exact", "--location", Folder, "--skip-dependencies",
        "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
    ];

    /// <summary>yt-dlp.exe at the root of its folder, else the first one below it; null when it is not there.</summary>
    public static string? Find()
    {
        try
        {
            string root = Path.Combine(Folder, ExeName);
            return File.Exists(root) ? root
                : Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, ExeName, SearchOption.AllDirectories).FirstOrDefault()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What yt-dlp downloads for <paramref name="url"/>; returns its path. Throws a
    /// <see cref="YtDlpMissingException"/> when yt-dlp is not installed, a <see cref="UrlLoadException"/>
    /// with its error when it fails.
    /// </summary>
    public static async Task<string> LoadAsync(Uri url, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        string exe = Find() ?? throw new YtDlpMissingException();
        Directory.CreateDirectory(UrlDownloads.Folder);
        var info = Hidden(exe);
        info.Environment["NO_COLOR"] = "1";
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (string argument in (string[])
        [
            "--no-playlist", "--newline", "--progress", "--encoding", "utf-8",
            "--print", "after_move:filepath",
            "-f", "b[ext=mp4]/b",
            "-o", Path.Combine(UrlDownloads.Folder, "%(extractor)s-%(id)s.%(ext)s"),
            "--", url.AbsoluteUri,
        ])
        {
            info.ArgumentList.Add(argument);
        }

        string? file = null;
        string? error = null;
        long reported = 0;
        void Read(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            line = line.Trim();
            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                error = line["ERROR:".Length..].Trim();
            }
            else if (Percent().Match(line) is { Success: true } match
                && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                if (Environment.TickCount64 - Interlocked.Read(ref reported) >= ReportInterval || percent >= 100)
                {
                    Interlocked.Exchange(ref reported, Environment.TickCount64);
                    progress.Report(new DownloadingStep(0, null, percent));
                }
            }
            else if (!line.StartsWith('[') && Path.IsPathFullyQualified(line) && File.Exists(line))
            {
                file = line;
            }
        }

        int exitCode = await RunAsync(info, Read, token);
        if (file is not null)
        {
            return file;
        }

        throw new UrlLoadException(error is null ? $"yt-dlp failed (exit code {exitCode})" : $"yt-dlp: {error}");
    }

    /// <summary>
    /// Installs yt-dlp into its folder with winget. Throws a <see cref="UrlLoadException"/> naming the
    /// folder where it can be put by hand when winget is missing or fails.
    /// </summary>
    public static async Task InstallAsync(CancellationToken token)
    {
        var info = Hidden("winget.exe");
        foreach (string argument in InstallArguments)
        {
            info.ArgumentList.Add(argument);
        }

        string? last = null;
        void Read(string? line)
        {
            // winget draws its progress with bars and spinners: the last line saying something is its reason.
            if (line is not null && line.Trim() is { Length: > 2 } text && text.Any(char.IsLetter))
            {
                last = text;
            }
        }

        int exitCode;
        try
        {
            exitCode = await RunAsync(info, Read, token);
        }
        catch (Win32Exception e)
        {
            throw new UrlLoadException($"winget isn't available: put yt-dlp.exe in {Folder}", e);
        }

        if (Find() is null)
        {
            throw new UrlLoadException($"winget couldn't install yt-dlp ({last ?? $"exit code {exitCode}"}): put yt-dlp.exe in {Folder}");
        }
    }

    private static ProcessStartInfo Hidden(string fileName) => new(fileName)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    /// <summary>Runs the process, its output and error lines read as they come; killed, its children with it, when cancelled.</summary>
    private static async Task<int> RunAsync(ProcessStartInfo info, Action<string?> read, CancellationToken token)
    {
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => read(e.Data);
        process.ErrorDataReceived += (_, e) => read(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
                // Already gone.
            }

            throw;
        }

        // The last lines read before the exit is reported.
        process.WaitForExit();
        return process.ExitCode;
    }

    // A progress line: "[download]  42.3% of 12.34MiB at 1.2MiB/s ETA 00:05".
    [GeneratedRegex(@"^\[download\]\s+(\d+(?:\.\d+)?)%")]
    private static partial Regex Percent();
}
