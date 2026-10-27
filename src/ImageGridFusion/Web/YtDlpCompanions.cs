namespace ImageGridFusion.Web;

/// <summary>
/// yt-dlp's companions, offered and installed together: Deno, the JavaScript runtime YouTube's formats
/// need, and ffmpeg, joining a video track and its sound into one file. Each optional — missing, yt-dlp
/// does what it can without it — and each in its own folder next to the exe, never on the <c>PATH</c>.
/// See workfiles/20261015-yt-dlp-best-format.md § Companions.
/// </summary>
internal static class YtDlpCompanions
{
    public const string DenoFolderName = "deno";
    public const string FfmpegFolderName = "ffmpeg";

    public static readonly WingetTool Deno = new("Deno", DenoFolderName, "deno.exe", "DenoLand.Deno");
    public static readonly WingetTool Ffmpeg = new("ffmpeg", FfmpegFolderName, "ffmpeg.exe", "yt-dlp.FFmpeg");

    private static readonly WingetTool[] All = [Deno, Ffmpeg];

    /// <summary>The companions not installed yet, in the order they are installed.</summary>
    public static IReadOnlyList<WingetTool> Missing => [.. All.Where(t => t.Find() is null)];

    /// <summary>"Deno and ffmpeg", or the one name: the companions <paramref name="tools"/> names, as the user reads them.</summary>
    public static string Names(IReadOnlyList<WingetTool> tools) => string.Join(" and ", tools.Select(t => t.Name));

    /// <summary>The winget commands installing <paramref name="tools"/>, one per line, as the user is shown them.</summary>
    public static string Commands(IReadOnlyList<WingetTool> tools) => string.Join("\n\n", tools.Select(t => t.InstallCommand));

    /// <summary>Installs <paramref name="tools"/> one after the other; the first that fails stops the others.</summary>
    public static async Task InstallAsync(IReadOnlyList<WingetTool> tools, CancellationToken token)
    {
        foreach (var tool in tools)
        {
            await tool.InstallAsync(token);
        }
    }
}
