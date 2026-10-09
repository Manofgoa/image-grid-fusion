using System.Security.Cryptography;
using System.Text;
using ImageGridFusion.UI;

namespace ImageGridFusion.Web;

/// <summary>
/// The folder where the media loaded from a URL are downloaded: a subfolder of the app's temp folder,
/// emptied at start-up — a download lives as long as the session that loaded it.
/// See workfiles/20261009-url-content.md § Download.
/// </summary>
internal static class UrlDownloads
{
    public const string FolderName = "url-downloads";

    // A file name's stem past this length is cut: the temp folder's path comes on top of it.
    private const int MaxStemLength = 80;

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

    private static string SafeStem(string stem)
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
