using System.ComponentModel;
using System.Diagnostics;
using ImageGridFusion.Explorer;
using ImageGridFusion.Resources;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>
/// The GIPHY view, the file explorer's first online library: its search — on Enter only, each search one of the
/// key's 100 calls an hour — its trending in the empty box, its GIFs / Stickers toggle, its results fetched page by
/// page, their thumbnails playing; a click downloads a result, which can then be dragged, a double-click adds it;
/// its heart keeps its file next to the exe as an ordinary favorite. See workfiles/20261105-online-library-giphy.md.
/// </summary>
internal sealed partial class FileExplorerPanel
{
    // How often the thumbnails in view are drawn again, in ms: their frames last 40 ms and more.
    private const int GiphyFrameInterval = 40;

    // A result's key, never a file: GIPHY's kind and id.
    private const string GiphyKeyPrefix = "giphy:";

    private readonly CheckBox _giphyGifs = new() { AccessibleName = Strings.ExplorerAccGiphyGifs, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), Checked = true, AutoCheck = false, Visible = false };
    private readonly CheckBox _giphyStickers = new() { AccessibleName = Strings.ExplorerAccGiphyStickers, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), AutoCheck = false, Visible = false };

    // GIPHY's attribution, required by its terms wherever its content shows: at the right end of the breadcrumb line.
    private readonly Label _giphyAttribution = new() { Text = Strings.GiphyAttribution, Dock = DockStyle.Right, AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Visible = false, Margin = Padding.Empty };
    private readonly LinkLabel _inviteLink = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false, Text = Giphy.DeveloperSite.Host };
    private readonly System.Windows.Forms.Timer _giphyFrames = new() { Interval = GiphyFrameInterval };
    private OnlineThumbnails? _giphyThumbnails;

    private bool _giphyView;

    // Whether the list shows a GIPHY list — false when the view opens, the previous view's list still there.
    private bool _giphyListShown;
    private string? _giphyKey;
    private string _giphyRating = Giphy.DefaultRating;
    private GiphyKind _giphyKind = GiphyKind.Gifs;

    // The list shown: what it searched (null: the trending), whether GIPHY holds more after it, the call running —
    // a newer one cancelling it — and its version, so an older answer is dropped.
    private string? _giphyQuery;
    private bool _giphyHasMore;
    private CancellationTokenSource? _giphyCall;
    private int _giphyListVersion;

    // The trending of each kind, kept for the session: the empty box costs one call.
    private readonly Dictionary<GiphyKind, (List<ExplorerRow> Rows, bool HasMore)> _giphyTrending = [];

    // The results being downloaded, with how far they are, and those whose file is there, with its path.
    private readonly Dictionary<string, double> _giphyProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _giphyFetched = new(StringComparer.Ordinal);

    /// <summary>The GIPHY API key was asked for, from the GIPHY view's invitation.</summary>
    public event EventHandler? GiphyKeyRequested;

    /// <summary>
    /// The user's GIPHY API key; null for none — the GIPHY view then invites to set one, and Ctrl+F skips it.
    /// Setting it raises nothing; the GIPHY view, shown, lists again.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? GiphyApiKey
    {
        get => this._giphyKey;
        set
        {
            if (value == this._giphyKey)
            {
                return;
            }

            this._giphyKey = value;
            this._giphyTrending.Clear();
            this._giphyListShown = false;
            if (this._giphyView)
            {
                this.RefreshRows();
            }
        }
    }

    /// <summary>The content rating every GIPHY request carries; changing it forgets the trending kept. Setting it raises nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string GiphyRating
    {
        get => this._giphyRating;
        set
        {
            if (value == this._giphyRating)
            {
                return;
            }

            this._giphyRating = value;
            this._giphyTrending.Clear();
            if (this._giphyView)
            {
                this.SearchGiphy(this._giphyQuery);
            }
        }
    }

    /// <summary>The GIPHY view's controls and their events, from the constructor.</summary>
    private void InitGiphy()
    {
        this._giphyThumbnails = new OnlineThumbnails(this);
        this._giphyThumbnails.Loaded += _ => this._grid.InvalidateOnline();
        this._grid.OnlineFrame = row => row.Online is { } item ? this._giphyThumbnails.Frame(row.Key, item.Preview) : null;
        this._grid.Fetched = this.FetchedOf;
        this._grid.RowPressed += (_, row) =>
        {
            if (row.Online is not null)
            {
                _ = this.FetchGiphyAsync(row);
            }
        };
        this._giphyFrames.Tick += (_, _) => this.PlayGiphyFrames();
        this._giphyGifs.Click += (_, _) => this.ChangeGiphyKind(GiphyKind.Gifs);
        this._giphyStickers.Click += (_, _) => this.ChangeGiphyKind(GiphyKind.Stickers);
        this._toolTip.SetToolTip(this._giphyGifs, Strings.ExplorerTipGiphyGifs);
        this._toolTip.SetToolTip(this._giphyStickers, Strings.ExplorerTipGiphyStickers);
        this._toolTip.SetToolTip(this._giphyAttribution, Strings.ExplorerTipGiphyAttribution);
        this._giphyAttribution.Font = new Font(this.Font, FontStyle.Bold);
        this._inviteLink.LinkClicked += (_, _) => OpenDeveloperSite();
    }

    /// <summary>Whether a path stands for a GIPHY result rather than a file.</summary>
    private static bool IsGiphyKey(string path) => path.StartsWith(GiphyKeyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The GIPHY view's list: the invitation without a key; the trending of the kind chosen for an empty box —
    /// kept for the session — else the list of the last search, until Enter asks for another.
    /// </summary>
    private void RefreshGiphyRows(bool keepPlace)
    {
        this.ShowGroupTitle(null);
        this._similarClear.Visible = false;
        bool inviting = this._giphyKey is null;
        this.SetInvite(InviteAction.GiphyKey);
        this._invite.Visible = inviting;
        this._grid.Visible = !inviting;
        if (inviting)
        {
            this.ShowGiphyRows([], hasMore: false, query: null, keepPlace);
            this._giphyListShown = false;
            return;
        }

        // The view just opened, or its box emptied: the trending; a text typed waits for Enter.
        if (!this._giphyListShown || (this._search.Text.Trim().Length == 0 && this._giphyQuery is not null))
        {
            this.SearchGiphy(null);
            return;
        }

        if (keepPlace)
        {
            this.ShowLoaded(Math.Max(this._loaded, this.FirstLoad()), keepPlace: true);
        }

        this.ShowGiphyTrail();
    }

    /// <summary>The breadcrumb's GIPHY or ↑ clicked: the box emptied, the trending shown.</summary>
    private void ShowGiphyTrending()
    {
        if (this._search.TextLength > 0)
        {
            this._search.Text = "";
        }
        else if (this._giphyQuery is not null)
        {
            this.SearchGiphy(null);
        }
    }

    /// <summary>
    /// Enter in the GIPHY view's box: its text searched — the trending for an empty one, kept for the session —
    /// unless that search shows already.
    /// </summary>
    private void SearchGiphy(string? query)
    {
        query = query?.Trim() is { Length: > 0 } text ? text : null;
        if (this._giphyKey is not { } key)
        {
            this.RefreshRows();
            return;
        }

        if (query is null && this._giphyTrending.TryGetValue(this._giphyKind, out var trending))
        {
            this._giphyCall?.Cancel();
            this.ShowGiphyRows(trending.Rows, trending.HasMore, query: null, keepPlace: false);
            return;
        }

        _ = this.FetchGiphyPageAsync(key, query, append: false);
    }

    /// <summary>
    /// A page of results asked of GIPHY — the first of a new search, or the next of the list shown — its rows shown
    /// once it answers, unless a newer call took its place; a refusal said on the status line.
    /// </summary>
    private async Task FetchGiphyPageAsync(string key, string? query, bool append)
    {
        this._giphyCall?.Cancel();
        var call = new CancellationTokenSource();
        this._giphyCall = call;
        int version = ++this._giphyListVersion;
        var kind = this._giphyKind;
        int offset = append ? this._list.Count : 0;
        if (!append)
        {
            this._giphyQuery = query;
            this.ShowGiphyRows([], hasMore: false, query, keepPlace: false);
        }

        this._breadcrumb.SetPath(this.GiphySegments(query), Strings.ExplorerGiphySearching);
        try
        {
            var page = await Giphy.FetchAsync(key, kind, query, offset, this._giphyRating, call.Token);
            if (version != this._giphyListVersion || this.IsDisposed || !this._giphyView)
            {
                return;
            }

            var rows = append ? new List<ExplorerRow>(this._list) : [];
            var shown = new HashSet<string>(rows.Select(row => row.Key), StringComparer.Ordinal);
            foreach (var item in page.Items)
            {
                var row = this.GiphyRow(item);
                if (shown.Add(row.Key))
                {
                    rows.Add(row);
                }
            }

            if (query is null)
            {
                this._giphyTrending[kind] = (rows, page.HasMore);
            }

            this.ShowGiphyRows(rows, page.HasMore, query, keepPlace: append, append ? this._loaded + this.LoadSize() : null);
        }
        catch (Exception ex) when (ex is UrlLoadException or OperationCanceledException)
        {
            if (version != this._giphyListVersion || this.IsDisposed || call.IsCancellationRequested)
            {
                return;
            }

            // A refused next page ends the list as it is: Enter asks again.
            this._giphyHasMore = false;
            this.ShowLoaded(this._loaded, keepPlace: true);
            this.ShowGiphyTrail();
            this.ShowTransient(ex.Message, error: true);
        }
        finally
        {
            if (this._giphyCall == call)
            {
                this._giphyCall = null;
            }

            call.Dispose();
        }
    }

    /// <summary>The rows of a GIPHY list shown, the thumbnails of the previous one forgotten unless it grew.</summary>
    private void ShowGiphyRows(IReadOnlyList<ExplorerRow> rows, bool hasMore, string? query, bool keepPlace, int? count = null)
    {
        if (!keepPlace)
        {
            this._giphyThumbnails?.Clear();
        }

        this._giphyQuery = query;
        this._giphyHasMore = hasMore;
        this._giphyListShown = true;
        this._list = rows;
        this.ShowLoaded(count ?? (keepPlace ? Math.Max(this._loaded, this.FirstLoad()) : this.FirstLoad()), keepPlace);
        this.ShowGiphyTrail();
    }

    /// <summary>A GIPHY result as a tile: its key standing for no file, its title its name; already fetched when its file is there.</summary>
    private ExplorerRow GiphyRow(GiphyItem item)
    {
        var row = new ExplorerRow($"{GiphyKeyPrefix}{item.Kind.ToString().ToLowerInvariant()}:{item.Id}", item.Title, Online: item);
        if (!this._giphyFetched.ContainsKey(row.Key) && File.Exists(GiphyFiles.DownloadPath(item)))
        {
            this._giphyFetched[row.Key] = GiphyFiles.DownloadPath(item);
        }

        return row;
    }

    /// <summary>The breadcrumb of the GIPHY view: GIPHY, the search, what the list holds — or the key typed, waiting for Enter.</summary>
    private void ShowGiphyTrail()
    {
        string typed = this._search.Text.Trim();
        string trailing = this._giphyKey is null ? ""
            : typed.Length > 0 && typed != this._giphyQuery ? Strings.ExplorerGiphyPressEnter
            : Texts.Format(this._giphyKind == GiphyKind.Stickers ? Strings.ExplorerGiphyStickersCount : Strings.ExplorerGiphyGifsCount, this._list.Count)
                + (this._giphyHasMore ? "+" : "");
        this._breadcrumb.SetPath(this.GiphySegments(this._giphyQuery), trailing);
    }

    private string[] GiphySegments(string? query) => query is null ? [Strings.ExplorerModeGiphy, Strings.ExplorerGiphyTrending] : [Strings.ExplorerModeGiphy, query];

    /// <summary>The Loading… slot of the GIPHY view came into view: the tiles fetched shown, else the next page asked for.</summary>
    private void LoadMoreGiphy()
    {
        if (this._loaded < this._list.Count)
        {
            this.ShowLoaded(this._loaded + this.LoadSize(), keepPlace: true);
        }
        else if (this._giphyHasMore && this._giphyCall is null && this._giphyKey is { } key)
        {
            _ = this.FetchGiphyPageAsync(key, this._giphyQuery, append: true);
        }
    }

    /// <summary>The GIFs / Stickers toggle: the other kind searched — the same text, or its trending.</summary>
    private void ChangeGiphyKind(GiphyKind kind)
    {
        if (kind == this._giphyKind)
        {
            return;
        }

        this._giphyKind = kind;
        this._giphyGifs.Checked = kind == GiphyKind.Gifs;
        this._giphyStickers.Checked = kind == GiphyKind.Stickers;
        this.SearchGiphy(this._giphyQuery);
    }

    /// <summary>A result's state for its tile: downloading, how far; fetched, 1; else nothing.</summary>
    private double? FetchedOf(ExplorerRow row) =>
        this._giphyProgress.TryGetValue(row.Key, out double progress) ? progress
        : this._giphyFetched.TryGetValue(row.Key, out string? path) && File.Exists(path) ? 1
        : null;

    /// <summary>
    /// A result downloaded into the temp folder — once: a click on it, a drag from it or a double-click while it
    /// downloads waits for the same download — its gauge on its tile; returns its file, null when it failed.
    /// </summary>
    private async Task<string?> FetchGiphyAsync(ExplorerRow row)
    {
        if (row.Online is not { } item)
        {
            return null;
        }

        if (this._giphyFetched.TryGetValue(row.Key, out string? done) && File.Exists(done))
        {
            return done;
        }

        if (this._giphyProgress.ContainsKey(row.Key))
        {
            while (this._giphyProgress.ContainsKey(row.Key) && !this.IsDisposed)
            {
                await Task.Delay(100);
            }

            return this._giphyFetched.GetValueOrDefault(row.Key);
        }

        return await this.DownloadGiphyAsync(row, item, GiphyFiles.DownloadPath(item));
    }

    /// <summary>A result's file written at <paramref name="path"/>, its tile's gauge following; null on failure, said on the status line.</summary>
    private async Task<string?> DownloadGiphyAsync(ExplorerRow row, GiphyItem item, string path)
    {
        this._giphyProgress[row.Key] = 0;
        this._grid.InvalidateRow(row);
        var progress = new Progress<UrlLoadStep>(step =>
        {
            if (step is DownloadingStep { Total: > 0 } downloading && this._giphyProgress.ContainsKey(row.Key))
            {
                this._giphyProgress[row.Key] = Math.Min(0.99, (double)downloading.Received / downloading.Total.Value);
                this._grid.InvalidateRow(row);
            }
        });
        try
        {
            string file = await GiphyFiles.FetchAsync(item, path, progress, CancellationToken.None);
            if (path == GiphyFiles.DownloadPath(item))
            {
                this._giphyFetched[row.Key] = file;
            }

            return file;
        }
        catch (Exception ex) when (ex is UrlLoadException or IOException or UnauthorizedAccessException)
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerGiphyDownloadFailed, item.Title, ex.Message), error: true);
            return null;
        }
        finally
        {
            this._giphyProgress.Remove(row.Key);
            this._grid.InvalidateRow(row);
        }
    }

    /// <summary>A result's tile dragged: its file once fetched, as a file tile's; before, the drag only starts the download.</summary>
    private void StartGiphyDrag(ExplorerRow row)
    {
        if (!this._giphyFetched.TryGetValue(row.Key, out string? path) || !File.Exists(path))
        {
            _ = this.FetchGiphyAsync(row);
            return;
        }

        this._draggingOwnTile = true;
        try
        {
            this._grid.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { path }), DragDropEffects.Copy);
        }
        finally
        {
            this._draggingOwnTile = false;
        }
    }

    /// <summary>A result double-clicked: downloaded if needed, then added as a file tile is.</summary>
    private async Task ActivateGiphyAsync(ExplorerRow row)
    {
        if (await this.FetchGiphyAsync(row) is { } path)
        {
            this.FileActivated?.Invoke(this, path);
        }
    }

    /// <summary>The favorite a result's heart stands for: its file next to the exe.</summary>
    private bool IsFavoritePath(string path) =>
        IsGiphyKey(path)
            ? this._list.FirstOrDefault(row => row.FullPath == path)?.Online is { } item && this._favorites.Contains(GiphyFiles.FavoritePath(item))
            : this._favorites.Contains(path);

    /// <summary>
    /// A result's heart: hearted, its file kept in <see cref="GiphyFiles.FavoritesFolder"/> — downloaded, or copied
    /// from the temp folder — and made an ordinary favorite; un-hearted, the favorite dropped, its file recycled once
    /// nothing holds it, as a pasted favorite's.
    /// </summary>
    private async Task ToggleGiphyFavoriteAsync(ExplorerRow row, GiphyItem item)
    {
        string path = GiphyFiles.FavoritePath(item);
        if (!this._favorites.Contains(path))
        {
            if (this._giphyProgress.ContainsKey(row.Key) || await this.DownloadGiphyAsync(row, item, path) is null)
            {
                return;
            }
        }

        bool added = true;
        try
        {
            added = this._favorites.Toggle(path);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerFavoritesNotSaved, ex.Message), error: true);
        }

        this.RecycleUnheld(path, added);
        this._grid.InvalidateRow(row);
    }

    /// <summary>A file the app made for a favorite — pasted, or kept from GIPHY — sent to the Recycle Bin once no favorite holds it.</summary>
    private void RecycleUnheld(string path, bool added)
    {
        if (added || (!PastedFavorites.Holds(path) && !GiphyFiles.HoldsFavorite(path)) || this._favorites.Uses(path))
        {
            return;
        }

        try
        {
            PastedFavorites.Recycle(path);
            this.ShowTransient(Texts.Format(Strings.ExplorerRecycled, Path.GetFileName(path)));
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerRecycleFailed, Path.GetFileName(path), ex.Message), error: true);
        }
    }

    /// <summary>The thumbnails in view drawn again while the GIPHY view shows a list and the window can be seen.</summary>
    private void PlayGiphyFrames()
    {
        if (this._giphyView && this._open && this._grid.Visible && this._giphyThumbnails?.Any == true && this.FindForm() is { WindowState: not FormWindowState.Minimized, Visible: true })
        {
            this._grid.InvalidateOnline();
        }
    }

    /// <summary>What the GIPHY view changes around the grid: its toggle and attribution shown, the search criteria, the sort and the groups buttons hidden.</summary>
    private void ApplyGiphyView()
    {
        bool giphy = this._giphyView;
        this._byName.Visible = !giphy;
        this._byContent.Visible = !giphy;
        this._giphyGifs.Visible = giphy;
        this._giphyStickers.Visible = giphy;
        this._sort.Visible = !giphy;
        this._folderGroupsButton.Visible = !giphy;
        this._seriesButton.Visible = !giphy;
        this._searchGroupsButton.Visible = !giphy;
        this._giphyAttribution.Visible = giphy;
        this._giphyFrames.Enabled = giphy;
        if (!giphy)
        {
            this._giphyCall?.Cancel();
            this._giphyThumbnails?.Clear();
        }
    }

    /// <summary>GIPHY's developer dashboard, where a key is made, in the default browser.</summary>
    private static void OpenDeveloperSite()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Giphy.DeveloperSite.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser: the address shows in the link itself.
        }
    }
}
