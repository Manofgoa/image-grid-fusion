using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGridFusion.Explorer;
using ImageGridFusion.Web;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The emoji fonts a text layer's emojis may be drawn in beside Windows' Segoe UI Emoji (workfiles/20261102-emoji-fonts.md
/// § Shipped Fonts): four free fonts, never part of the build — downloaded at the first launch after asking, into
/// <see cref="FolderName"/> next to the exe, from pinned URLs each checked against its SHA-256
/// (<see cref="DirectEngineDownload"/>), and updated from the ⚙ menu to their latest release. Never installed into
/// Windows: <see cref="TextRaster"/> loads them from memory.
/// </summary>
internal static class EmojiFonts
{
    /// <summary>The emoji font of a text by default: Windows' own, drawn through the system's font fallback.</summary>
    public const string Default = "Segoe UI Emoji";

    /// <summary>The fonts' folder next to the exe.</summary>
    public const string FolderName = "emoji-fonts";

    private const string UpdateSuffix = ".~updating";

    // Each font: its file, pinned, its family as DirectWrite names it, and where its latest release is — a release's asset,
    // else a file of the repository at the release's tag. Licenses: Twemoji's graphics CC-BY 4.0, Noto OFL 1.1, OpenMoji
    // CC BY-SA 4.0, Blobmoji Apache 2.0 (the README's Tech paragraph credits them).
    private static readonly Font[] Fonts =
    [
        new(new("Twemoji.Mozilla.ttf", "https://github.com/mozilla/twemoji-colr/releases/download/v0.7.0/Twemoji.Mozilla.ttf", "6d90152ee0d29e82fe2a87793af5aa4b7ad13e6538360889e141e81ed299ee8e", 1_474_284),
            "Twemoji Mozilla", "mozilla/twemoji-colr", Asset: "Twemoji.Mozilla.ttf", Path: null),
        new(new("Noto-COLRv1.ttf", "https://raw.githubusercontent.com/googlefonts/noto-emoji/v2026-09-24-unicode18_0/2D/fonts/Noto-COLRv1.ttf", "b8e25ea68db82f9e4d0aee921f4420be2be39887bd5c893a2ad98710531f9d0c", 5_038_236),
            "Noto Color Emoji", "googlefonts/noto-emoji", Asset: null, Path: "2D/fonts/Noto-COLRv1.ttf"),
        new(new("OpenMoji-color-glyf_colr_0.ttf", "https://raw.githubusercontent.com/hfg-gmuend/openmoji/17.0.0/font/OpenMoji-color-glyf_colr_0/OpenMoji-color-glyf_colr_0.ttf", "8376fee074649ece235faba0d157d851f3d93632f0540ad1165feba1aba9ba37", 2_681_320),
            "OpenMoji", "hfg-gmuend/openmoji", Asset: null, Path: "font/OpenMoji-color-glyf_colr_0/OpenMoji-color-glyf_colr_0.ttf"),
        new(new("Blobmoji.ttf", "https://github.com/C1710/blobmoji/releases/download/v15.0/Blobmoji.ttf", "dcc3d6675036ba9b35ef574d2221532770b5bd9886cba23c645ef46947eb78f9", 12_877_044),
            "Blobmoji", "C1710/blobmoji", Asset: "Blobmoji.ttf", Path: null),
    ];

    private static int _generation;

    /// <summary>The fonts' folder: <c>emoji-fonts\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether every font is in <see cref="Folder"/>.</summary>
    public static bool AllPresent => DirectEngineDownload.AllPresent(Folder, Fonts.Select(f => f.File));

    /// <summary>Whether any font is in <see cref="Folder"/>: what the update works on.</summary>
    public static bool AnyPresent => Fonts.Any(f => File.Exists(PathOf(f)));

    /// <summary>The fonts' names, for the download's confirmation.</summary>
    public static IEnumerable<string> Names => Fonts.Select(f => f.Family);

    /// <summary>The download's size, in MB, rounded up.</summary>
    public static int DownloadMegabytes => (int)Math.Ceiling(Fonts.Sum(f => f.File.Size) / 1e6);

    /// <summary>
    /// Changes whenever the fonts' files change — downloaded, updated —: <see cref="TextRaster"/> loads them again, and the
    /// texts drawn before are drawn again.
    /// </summary>
    public static int Generation => Volatile.Read(ref _generation);

    /// <summary>The family and the file of each font there, as DirectWrite names it.</summary>
    public static IEnumerable<(string Family, string Path)> Present =>
        Fonts.Where(f => File.Exists(PathOf(f))).Select(f => (f.Family, PathOf(f)));

    /// <summary>
    /// Downloads the fonts missing from <see cref="Folder"/>, reporting the share done, 0 to 1. Throws a
    /// <see cref="UrlLoadException"/> when it cannot; the fonts downloaded before the failure are kept.
    /// </summary>
    public static async Task DownloadAsync(IProgress<double> progress, CancellationToken token)
    {
        try
        {
            await DirectEngineDownload.DownloadAsync(Folder, [.. Fonts.Select(f => f.File)], progress, token);
        }
        finally
        {
            Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>
    /// Replaces each font there by its latest release — no pinned hash, HTTPS only — reporting the share done, 0 to 1:
    /// the families updated, and those whose update failed with why. A font whose download fails keeps its file.
    /// </summary>
    public static async Task<(IReadOnlyList<string> Updated, IReadOnlyList<string> Failed)> UpdateAsync(IProgress<double> progress, CancellationToken token)
    {
        var present = Fonts.Where(f => File.Exists(PathOf(f))).ToArray();
        var updated = new List<string>();
        var failed = new List<string>();
        try
        {
            for (int i = 0; i < present.Length; i++)
            {
                var font = present[i];
                string path = PathOf(font);
                string partial = path + UpdateSuffix;
                try
                {
                    var url = await LatestUrlAsync(font, token);
                    int done = i;
                    await DirectEngineDownload.SaveAsync(url, partial, share => progress.Report((done + share) / present.Length), token);
                    if (!IsFont(partial))
                    {
                        throw new UrlLoadException("not a font");
                    }

                    if (string.Equals(DirectEngineDownload.Sha256Of(partial), DirectEngineDownload.Sha256Of(path), StringComparison.OrdinalIgnoreCase))
                    {
                        DirectEngineDownload.TryDelete(partial);
                    }
                    else
                    {
                        File.Move(partial, path, overwrite: true);
                        updated.Add(font.Family);
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or UrlLoadException or JsonException or InvalidOperationException
                    || (e is OperationCanceledException && !token.IsCancellationRequested))
                {
                    DirectEngineDownload.TryDelete(partial);
                    failed.Add($"{font.Family} ({e.Message})");
                }
                catch (OperationCanceledException)
                {
                    DirectEngineDownload.TryDelete(partial);
                    throw;
                }
            }
        }
        finally
        {
            if (updated.Count > 0)
            {
                Interlocked.Increment(ref _generation);
            }
        }

        progress.Report(1);
        return (updated, failed);
    }

    private static string PathOf(Font font) => Path.Combine(Folder, font.File.Name);

    /// <summary>The file of <paramref name="font"/>'s latest release, read from GitHub's API.</summary>
    private static async Task<Uri> LatestUrlAsync(Font font, CancellationToken token)
    {
        using var response = await Downloader.GetAsync(new Uri($"https://api.github.com/repos/{font.Repository}/releases/latest"), token);
        Downloader.EnsureSuccess(response);
        var release = JsonNode.Parse(await response.Content.ReadAsStringAsync(token)) as JsonObject
            ?? throw new UrlLoadException("no release");
        if (font.Asset is { } asset)
        {
            return release["assets"] is JsonArray assets
                && assets.OfType<JsonObject>().FirstOrDefault(a => (string?)a["name"] == asset)?["browser_download_url"] is JsonValue link
                && link.TryGetValue(out string? url)
                    ? new Uri(url)
                    : throw new UrlLoadException($"no {asset} in its latest release");
        }

        string tag = (string?)release["tag_name"] ?? throw new UrlLoadException("no release tag");
        return new Uri($"https://raw.githubusercontent.com/{font.Repository}/{Uri.EscapeDataString(tag)}/{font.Path}");
    }

    /// <summary>Whether the file starts as a TrueType or OpenType font does.</summary>
    private static bool IsFont(string path)
    {
        Span<byte> head = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(head) == 4
            && (head.SequenceEqual(stackalloc byte[] { 0, 1, 0, 0 }) || head.SequenceEqual("OTTO"u8) || head.SequenceEqual("true"u8));
    }

    /// <summary>A font: its pinned file, its family, and where its latest release is — <paramref name="Asset"/> or <paramref name="Path"/>.</summary>
    private sealed record Font(EngineFile File, string Family, string Repository, string? Asset, string? Path);
}
