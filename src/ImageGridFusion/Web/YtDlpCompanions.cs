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
}
