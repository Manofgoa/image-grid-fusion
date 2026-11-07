using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ImageGridFusion.UI;

namespace ImageGridFusion.Web;

/// <summary>
/// The folder where the media loaded from a URL are downloaded: a subfolder of the app's temp folder,
/// emptied at start-up — a download lives as long as the session that loaded it.
/// See workfiles/20261009-url-content.md § Download.
/// </summary>
internal static partial class UrlDownloads
{
    public const string FolderName = "url-downloads";

    /// <summary>A file name's stem past this length is cut: the temp folder's path comes on top of it.</summary>
    public const int MaxStemLength = 80;

    // A telling name holds a run of letters at least this long: "photo", not "IMG" nor "DSC".
    private const int TellingLetters = 4;

    public static string Folder => Path.Combine(MainForm.TempVideoFolder, FolderName);

    /// <summary>The path of <paramref name="stem"/> + <paramref name="extension"/> in the folder, the stem made a valid file name.</summary>
    public static string PathOf(string stem, string extension) => Path.Combine(Folder, SafeStem(stem) + extension);

    /// <summary>A short, stable tag of <paramref name="url"/>: two links ending alike never share a file.</summary>
    public static string Tag(Uri url) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)))[..8];

    /// <summary>
    /// Deletes what earlier sessions downloaded. A file in use is left for a later session. Run by the
    /// normal instance only: a <c>--new-instance</c> one writes nothing of its own accord.
    /// </summary>
    public static void Clean()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use, or protected: left for a later session.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The folder unreadable: nothing to clean this time.
        }
    }

    /// <summary>
    /// Whether <paramref name="stem"/> names its content for a reader — <c>beach-sunset</c> — rather than identifies it
    /// for a machine: a run of <see cref="TellingLetters"/> letters at least, and not a hexadecimal id
    /// (<c>a3f9c0d1e2</c>, <c>deadbeefcafe</c>) — see workfiles/20261107-content-download-improvements.md § File Names.
    /// </summary>
    public static bool IsTelling(string stem)
    {
        int run = 0;
        int longest = 0;
        foreach (char c in stem)
        {
            run = char.IsLetter(c) ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest >= TellingLetters && !HexId().IsMatch(stem);
    }

    /// <summary>
    /// <paramref name="text"/> on one line, cut at the last space within <paramref name="max"/> characters — at
    /// <paramref name="max"/> itself when that space would keep less than half of it; empty for a <paramref name="max"/>
    /// of 0 or less.
    /// </summary>
    public static string Words(string text, int max)
    {
        string flat = Spaces().Replace(text, " ").Trim();
        if (max <= 0)
        {
            return "";
        }

        if (flat.Length <= max)
        {
            return flat;
        }

        int cut = flat.LastIndexOf(' ', max);
        if (cut <= max / 2)
        {
            cut = char.IsHighSurrogate(flat[max - 1]) ? max - 1 : max;
        }

        return flat[..cut].TrimEnd();
    }

    [GeneratedRegex(@"^[0-9a-fA-F_\-]{8,}$")]
    private static partial Regex HexId();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    internal static string SafeStem(string stem)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new StringBuilder(stem.Length);
        foreach (char c in stem)
        {
            safe.Append(Array.IndexOf(invalid, c) >= 0 || char.IsControl(c) ? '-' : c);
        }

        string result = safe.ToString().Trim(' ', '.', '-');
        if (result.Length > MaxStemLength)
        {
            result = result[..MaxStemLength].TrimEnd(' ', '.', '-');
        }

        return result.Length == 0 ? "url" : result;
    }
}
