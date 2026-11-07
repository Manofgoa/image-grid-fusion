using ImageGridFusion.Resources;

namespace ImageGridFusion.Web;

/// <summary>
/// The content the app downloaded rather than the user's own files — the URL zone's media, GIPHY's results,
/// downloaded or hearted — and where every save dialog opens. See workfiles/20261107-save-downloaded-content.md.
/// </summary>
internal static class DownloadedContent
{
    /// <summary>The folders holding a downloaded content: the URL zone's downloads, GIPHY's, GIPHY's favorites.</summary>
    public static IReadOnlyList<string> Folders => [UrlDownloads.Folder, GiphyFiles.Folder, GiphyFiles.FavoritesFolder];

    /// <summary>Whether <paramref name="path"/> is a downloaded content: a file directly in one of <see cref="Folders"/>.</summary>
    public static bool Holds(string? path) => Holds(path, Folders);

    /// <summary>Whether <paramref name="path"/> is a file directly in one of <paramref name="folders"/>, never in a subfolder.</summary>
    public static bool Holds(string? path, IEnumerable<string> folders)
    {
        if (string.IsNullOrEmpty(path) || Path.GetDirectoryName(Path.GetFullPath(path)) is not { } folder)
        {
            return false;
        }

        return folders.Any(f => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(f)), Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Where a save dialog opens: <paramref name="browsed"/> — the file explorer's open folder — or its nearest parent
    /// still there; the user's Pictures folder when there is none.
    /// </summary>
    public static string InitialFolder(string? browsed)
    {
        for (string? current = browsed; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    /// <summary>A save dialog's filter for a copy of <paramref name="path"/>: its own type first, then every file.</summary>
    public static string Filter(string path)
    {
        string extension = Path.GetExtension(path);
        string all = $"{Strings.MainPickerAllFiles}|*.*";
        return extension.Length > 1
            ? $"{Texts.Format(Strings.ExplorerSaveFilterType, extension[1..].ToUpperInvariant(), extension)}|*{extension}|{all}"
            : all;
    }
}
