using ImageGridFusion.Explorer;
using ImageGridFusion.Resources;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>
/// Save… on a downloaded content — the URL zone's media, a GIPHY result, a cell holding one — its file copied where the
/// user chooses, and the folder every save dialog of the app opens on. See workfiles/20261107-save-downloaded-content.md.
/// </summary>
internal sealed partial class FileExplorerPanel
{
    internal static string SaveText => Strings.ExplorerMenuSave;

    // A GIPHY result's menu: Save… alone, greyed until the result is downloaded (§ GIPHY Tiles).
    private readonly ToolStripMenuItem _saveDownloaded = new(SaveText);
    private ExplorerRow? _onlineMenuRow;

    /// <summary>
    /// The folder the folder view browses, whatever view is shown: the open folder under the base folder, or under
    /// the secondary source open; null without a base folder.
    /// </summary>
    public string? BrowsedFolder =>
        (this._openSource?.Folder ?? this._baseFolder) is { } root ? Path.Combine(root, this._openFolder) : null;

    /// <summary>Where every save dialog of the app opens: <see cref="BrowsedFolder"/>, or its nearest parent still there.</summary>
    public string SaveFolder => DownloadedContent.InitialFolder(this.BrowsedFolder);

    /// <summary>
    /// The Save As dialog on <see cref="SaveFolder"/>, <paramref name="path"/>'s name proposed, then a plain copy of the file
    /// where the user chose, added to the index when it lands under the base folder or a source; the status line says
    /// where it went. Returns the file written, null when cancelled or failed.
    /// </summary>
    public async Task<string?> SaveDownloadedAsync(string path)
    {
        if (!File.Exists(path))
        {
            this.Report(Texts.Format(Strings.ExplorerStatusSaveFailed, Path.GetFileName(path)), error: true);
            return null;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = DownloadedContent.Filter(path),
            DefaultExt = Path.GetExtension(path),
            FileName = Path.GetFileName(path),
            InitialDirectory = this.SaveFolder,
        };
        if (dialog.ShowDialog(this.FindForm()) != DialogResult.OK)
        {
            return null;
        }

        string target = dialog.FileName;
        try
        {
            if (!string.Equals(Path.GetFullPath(target), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => File.Copy(path, target, overwrite: true));
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.Report(Texts.Format(Strings.ExplorerStatusSaveFailed, ex.Message), error: true);
            return null;
        }

        this.AddSavedToIndex(target);
        this.Report(Texts.Format(Strings.ExplorerStatusSaved, Path.GetFileName(target), Path.GetDirectoryName(target)), error: false);
        return target;
    }

    /// <summary>
    /// A file just saved, recorded at once in the index it belongs to — the base folder's or a secondary source's —
    /// with no rescan, the list shown again; its content passes run with the next ones. Nothing outside them all.
    /// </summary>
    private void AddSavedToIndex(string path)
    {
        if (this.Locate(path) is not { } located || this.IndexAt(located.Source) is not { } index || !index.Add(path))
        {
            return;
        }

        if (located.Source is { } source)
        {
            source.SetIndex(index);
        }
        else
        {
            this._tree = null;
            this._entriesByPath = null;
        }

        // A file overwritten has another content: its texts go, read again by the next passes.
        this.ContentsAt(located.Source)?.Remove(located.Relative);
        try
        {
            index.Save((located.Source?.Files ?? IndexFiles.Main).Index);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.Report(Texts.Format(Strings.ExplorerStatusSaveFailed, ex.Message), error: true);
        }

        this.RefreshRows(keepPlace: true);
    }

    /// <summary>A GIPHY result's file once downloaded — or hearted —, still there; null before.</summary>
    private string? GiphyFileOf(ExplorerRow row)
    {
        if (row.Online is not { } item)
        {
            return null;
        }

        if (this._giphyFetched.TryGetValue(row.Key, out string? fetched) && File.Exists(fetched))
        {
            return fetched;
        }

        string favorite = GiphyFiles.FavoritePath(item);
        return File.Exists(favorite) ? favorite : null;
    }

    /// <summary>The tile menu's Save… entry, wired once (§ GIPHY Tiles).</summary>
    private void InitSaveDownloaded()
    {
        this._menu.Items.Add(this._saveDownloaded);
        UiIcons.Set(this._saveDownloaded, UiIcons.Save);
        this._saveDownloaded.Click += async (_, _) =>
        {
            if (this._onlineMenuRow is { } row && this.GiphyFileOf(row) is { } file)
            {
                await this.SaveDownloadedAsync(file);
            }
        };
    }

    /// <summary>
    /// The tile menu opening on <paramref name="row"/>: on a GIPHY result, Save… alone — greyed, saying why, until it is
    /// downloaded —, every other entry hidden; on any other tile, Save… hidden. Called last by the menu's Opening.
    /// </summary>
    private void ShowSaveDownloaded(ExplorerRow? row)
    {
        bool online = row?.Online is not null;
        this._onlineMenuRow = online ? row : null;
        if (!online)
        {
            this._saveDownloaded.Visible = false;
            return;
        }

        foreach (ToolStripItem item in this._menu.Items)
        {
            item.Visible = item == this._saveDownloaded;
        }

        bool downloaded = this.GiphyFileOf(row!) is not null;
        this._saveDownloaded.Enabled = downloaded;
        this._saveDownloaded.ToolTipText = downloaded ? null : Strings.ExplorerTipSaveNotDownloaded;
    }
}
