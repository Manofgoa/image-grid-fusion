using ImageGridFusion.UI;

namespace ImageGridFusion.Web;

/// <summary>
/// Where a GIPHY result becomes a file: downloaded into a subfolder of the app's temp folder before it is
/// dragged or added — emptied at start-up, as the URL zone's — or, hearted, into <see cref="FavoritesFolderName"/>
/// next to the exe, an ordinary favorite then. A result's file is named after its title and its GIPHY id, so the
/// same result always finds its file again. See workfiles/20261105-online-library-giphy.md § Reaching the Grid,
/// § Favorites.
/// </summary>
internal static class GiphyFiles
{
    public const string FolderName = "giphy-downloads";

    public const string FavoritesFolderName = "giphy-favorites";

    public static string Folder => Path.Combine(MainForm.TempVideoFolder, FolderName);

    public static string FavoritesFolder => Path.Combine(AppContext.BaseDirectory, FavoritesFolderName);

    /// <summary>The file of <paramref name="item"/> in the temp folder.</summary>
    public static string DownloadPath(GiphyItem item) => Path.Combine(Folder, NameOf(item));

    /// <summary>The file of <paramref name="item"/> once hearted.</summary>
    public static string FavoritePath(GiphyItem item) => Path.Combine(FavoritesFolder, NameOf(item));

    /// <summary>Whether <paramref name="fullPath"/> is a file of the favorites' folder, not of a subfolder.</summary>
    public static bool HoldsFavorite(string fullPath) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(fullPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(FavoritesFolder)), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="item"/>'s file at <paramref name="path"/>: the one already there, copied from the temp
    /// folder when it was downloaded there, else downloaded; returns the path.
    /// </summary>
    public static async Task<string> FetchAsync(GiphyItem item, string path, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        if (File.Exists(path))
        {
            return path;
        }

        string downloaded = DownloadPath(item);
        if (path != downloaded && File.Exists(downloaded))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(downloaded, path, overwrite: true);
            return path;
        }

        return await Downloader.DownloadAsync(item.Media, path, progress, token);
    }

    /// <summary>
    /// Deletes what earlier sessions downloaded into the temp folder. A file in use is left for a later session. Run by
    /// the normal instance only: a <c>--new-instance</c> one writes nothing of its own accord.
    /// </summary>
    public static void Clean()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(Folder))
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

    private static string NameOf(GiphyItem item) => $"{UrlDownloads.SafeStem(item.Title)}-giphy-{UrlDownloads.SafeStem(item.Id)}{item.Extension}";
}
