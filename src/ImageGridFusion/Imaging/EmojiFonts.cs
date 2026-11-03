using ImageGridFusion.Explorer;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The emoji fonts a text layer's emojis may be drawn in beside Windows' Segoe UI Emoji (workfiles/20261102-emoji-fonts.md
/// § Shipped Fonts): four free fonts, never part of the build — installed and updated from the Component updates
/// window (offered at the first launch), into <see cref="FolderName"/> next to the exe, from pinned URLs each checked
/// against its SHA-256 or their latest release (<see cref="Components.ComponentRegistry"/>). Never installed into
/// Windows: <see cref="TextRaster"/> loads them from memory.
/// </summary>
internal static class EmojiFonts
{
    /// <summary>The emoji font of a text by default: Windows' own, drawn through the system's font fallback.</summary>
    public const string Default = "Segoe UI Emoji";

    /// <summary>The fonts' folder next to the exe.</summary>
    public const string FolderName = "emoji-fonts";

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

    /// <summary>Whether any font is in <see cref="Folder"/>: what the update works on.</summary>
    public static bool AnyPresent => Fonts.Any(f => File.Exists(PathOf(f)));

    /// <summary>
    /// Changes whenever the fonts' files change — downloaded, updated —: <see cref="TextRaster"/> loads them again, and the
    /// texts drawn before are drawn again.
    /// </summary>
    public static int Generation => Volatile.Read(ref _generation);

    /// <summary>The family and the file of each font there, as DirectWrite names it.</summary>
    public static IEnumerable<(string Family, string Path)> Present =>
        Fonts.Where(f => File.Exists(PathOf(f))).Select(f => (f.Family, PathOf(f)));

    private static string PathOf(Font font) => System.IO.Path.Combine(Folder, font.File.Name);

    /// <summary>Each font as pinned: its file, its family, and where its latest release is — the Component updates window's rows.</summary>
    public static IReadOnlyList<Font> Pinned => Fonts;

    /// <summary>The fonts' files changed — installed, updated, gone back to another version —: drawn again (<see cref="Generation"/>).</summary>
    public static void NotifyChanged() => Interlocked.Increment(ref _generation);

    /// <summary>Whether the file starts as a TrueType or OpenType font does.</summary>
    public static bool IsFont(string path)
    {
        Span<byte> head = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(head) == 4
            && (head.SequenceEqual(stackalloc byte[] { 0, 1, 0, 0 }) || head.SequenceEqual("OTTO"u8) || head.SequenceEqual("true"u8));
    }

    /// <summary>A font: its pinned file, its family, and where its latest release is — <paramref name="Asset"/> or <paramref name="Path"/>.</summary>
    public sealed record Font(EngineFile File, string Family, string Repository, string? Asset, string? Path);
}
