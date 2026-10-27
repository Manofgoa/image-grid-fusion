using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ImageGridFusion.Web;

/// <summary>
/// A command-line tool the app never ships — yt-dlp and its companions: looked for in its own folder
/// next to the exe only, never on the <c>PATH</c>, and installed there with winget once the user accepts
/// the command shown. See workfiles/20261015-yt-dlp-best-format.md § Companions.
/// </summary>
internal sealed class WingetTool(string name, string folderName, string exeName, string packageId, bool skipDependencies = false)
{
    /// <summary>The tool's name, as the user is shown it.</summary>
    public string Name { get; } = name;

    public string Folder => Path.Combine(AppContext.BaseDirectory, folderName);

    /// <summary>The command <see cref="InstallAsync"/> runs, as the user is shown it before it does.</summary>
    public string InstallCommand => "winget " + string.Join(' ', this.InstallArguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

    private string[] InstallArguments =>
    [
        "install", "--id", packageId, "--exact", "--location", this.Folder, .. (string[])(skipDependencies ? ["--skip-dependencies"] : []),
        "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
    ];

    /// <summary>The tool's exe at the root of its folder, else the first one below it; null when it is not there.</summary>
    public string? Find()
    {
        try
        {
            string root = Path.Combine(this.Folder, exeName);
            return File.Exists(root) ? root
                : Directory.Exists(this.Folder) ? Directory.EnumerateFiles(this.Folder, exeName, SearchOption.AllDirectories).FirstOrDefault()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Installs the tool into its folder with winget. Throws a <see cref="UrlLoadException"/> naming the
    /// folder where it can be put by hand when winget is missing or fails.
    /// </summary>
    public async Task InstallAsync(CancellationToken token)
    {
        var info = Hidden("winget.exe");
        foreach (string argument in this.InstallArguments)
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
            throw new UrlLoadException($"winget isn't available: put {exeName} in {this.Folder}", e);
        }

        if (this.Find() is null)
        {
            throw new UrlLoadException($"winget couldn't install {this.Name} ({last ?? $"exit code {exitCode}"}): put {exeName} in {this.Folder}");
        }
    }

    /// <summary>A process run without a window, its output and error read as UTF-8.</summary>
    public static ProcessStartInfo Hidden(string fileName) => new(fileName)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    /// <summary>Runs the process, its output and error lines read as they come; killed, its children with it, when cancelled.</summary>
    public static async Task<int> RunAsync(ProcessStartInfo info, Action<string?> read, CancellationToken token)
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
}
