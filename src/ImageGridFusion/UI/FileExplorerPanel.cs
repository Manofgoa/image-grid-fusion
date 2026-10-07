using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer, at the right of the preview (see workfiles/20260926-file-explorer.md and
/// workfiles/20260928-tile-size-slider.md): a search box over the index of the base folder, its
/// matches as the user types — every file, the most recently created first, for <c>*</c> — and the
/// favorites — all of them, the newest first — while the box is empty, as a grid of thumbnail tiles
/// filling their rows at the size of the slider below them, loaded a few pages at a time as the list
/// scrolls (workfiles/20260927-file-explorer-show-all.md);
/// the panel's width is the user's, dragged from its edge. A tile is dragged onto a cell like a file
/// from the Explorer, double-clicked to be added like Add images, hearted to become a favorite.
/// Collapses to a strip. The cached index is loaded, then rescanned in the background, at every start.
/// The 📁 toggle switches to the folder view (workfiles/20260930-file-explorer-folder-view.md): the
/// open folder's subfolders and files as the disk holds them, a folder tile opening its folder, the
/// breadcrumb in the caption's line going back up, the search limited to the open folder.
/// </summary>
internal sealed class FileExplorerPanel : Panel
{
    // In logical pixels: the strip, and what the panel adds around its grid (paddings, the list's
    // border, the grid's insets, a vertical scrollbar).
    private const int StripWidth = 20;
    private const int Surround = 39;

    /// <summary>The narrowest open panel, in logical pixels: the smallest tile and the surround.</summary>
    public const int MinOpenWidth = ThumbnailGrid.MinTileSize + Surround;

    /// <summary>The open width before the user drags it, in logical pixels: one 200 px tile.</summary>
    public const int DefaultOpenWidth = 240;

    /// <summary>How many pages of tiles a load holds: the ⚙ menu's choices, and its default.</summary>
    public const int MinPagesPerLoad = 1;
    public const int MaxPagesPerLoad = 10;
    public const int DefaultPagesPerLoad = 2;

    private const int TransientDuration = 5000;

    // The content extraction (workfiles/20260926-ocr-search.md § Extraction), in ms: how long it waits
    // after a keystroke, how often it looks again while paused, how often it saves its texts and
    // refreshes the search shown.
    private const int KeystrokePause = 1000;
    private const int PausePoll = 200;
    private const int ContentSaveInterval = 30000;
    private const int ContentRefreshInterval = 5000;

    // The texts are also saved every ContentSaveEvery files extracted, no sooner than ContentSaveFloor
    // ms after the previous save (workfiles/20261007-index-write-every-50-files.md § Design).
    private const int ContentSaveEvery = 50;
    private const int ContentSaveFloor = 5000;

    // How long a folder may take to list before the status line says it is being read, in ms.
    private const int ReadingNoticeDelay = 150;

    private const string SearchPlaceholder = "Search files… (Ctrl+F, * for all)";
    private const string FolderSearchPlaceholder = "Search this folder… (Ctrl+F, * for all)";
    private const string OpenFileLocationText = "Open file location";
    private const string OpenInExplorerText = "Open in Explorer";

    private readonly TableLayoutPanel _content = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(6, 4, 6, 6) };
    private readonly TableLayoutPanel _header = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _title = new() { Text = "Files", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _collapse = new() { Text = "»", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Button _expand = new() { Text = "«", Dock = DockStyle.Fill, Visible = false, Margin = Padding.Empty };
    private readonly TableLayoutPanel _searchRow = new() { ColumnCount = 5, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly CheckBox _folderToggle = new() { Text = "📁", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0) };

    // The search criteria (workfiles/20261007-search-options.md): pressed, the search looks in the
    // relative paths, in the content texts; both at start-up, never none, not remembered. Toggled by
    // hand (AutoCheck off), so the last one pressed stays pressed.
    private readonly CheckBox _byName = new() { Text = "Aa", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), Checked = true, AutoCheck = false };
    private readonly CheckBox _byContent = new() { Text = "💡", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), Checked = true, AutoCheck = false };
    private readonly TextBox _search = new() { PlaceholderText = SearchPlaceholder, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _rescan = new() { Text = "↻", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right, Enabled = false };
    private readonly IndexingBar _progress = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Panel _captionRow = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Breadcrumb _breadcrumb = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _listHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BorderStyle = BorderStyle.FixedSingle };
    private readonly ThumbnailGrid _grid = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _sizeRow = new() { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _smallerGlyph = new() { Text = "▭", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 4, 0) };
    // No ticks: the thumb then sits on the row's centre line, level with the glyphs at any DPI.
    private readonly TrackBar _size = new() { AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty, Minimum = ThumbnailGrid.MinTileSize, Maximum = ThumbnailGrid.MaxTileSize, TickStyle = TickStyle.None, SmallChange = 20, LargeChange = 100 };
    private readonly Label _largerGlyph = new() { Text = "▭", AutoSize = true, Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(4, 0, 0, 0) };
    private readonly Panel _invite = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _inviteText = new()
    {
        Dock = DockStyle.Top,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "No base folder yet. The search looks through one folder and its subfolders: choose it to index your files.",
    };
    private readonly Button _chooseFolder = new() { Text = "Choose folder…", Dock = DockStyle.Top, AutoSize = true };
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _openLocation = new(OpenFileLocationText);
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _transient = new() { Interval = TransientDuration };
    private readonly Favorites _favorites = new(Favorites.DefaultPath);

    private bool _open = true;
    private int _openWidth = DefaultOpenWidth;
    private int _tileSize = ThumbnailGrid.DefaultTileSize;
    private bool _syncingSize;
    private string? _baseFolder;
    private FileIndex? _index;
    private CancellationTokenSource? _scan;
    private string _summary = "No base folder";
    private bool _summaryError;

    // The content texts of the base folder's files, once loaded; whether the content search is on (the
    // ⚙ setting); the extraction running, cancelled with the scan; whether the index came from a scan,
    // with its stamps; whether a scan runs; the last keystroke in the search box (Environment.TickCount64,
    // read by the extraction's worker); what the extraction does, after the summary.
    private ContentIndex? _contents;
    private bool _contentSearch;
    private CancellationTokenSource? _extraction;
    private bool _indexScanned;
    private bool _scanning;
    private long _lastKeystroke;
    private string? _activity;
    private int _pagesPerLoad = DefaultPagesPerLoad;

    // A tile of this panel being dragged: not a source of favorites, so the panel refuses it.
    private bool _draggingOwnTile;
    private bool _dropFrame;

    // The whole list the search box asks for, computed once, and how many of its tiles are loaded.
    private IReadOnlyList<ExplorerRow> _list = [];
    private int _loaded;

    // The folder view (workfiles/20260930-file-explorer-folder-view.md): whether it shows, the open
    // folder relative to the base folder ("" for the base folder itself), the index's folders once
    // derived, and the list being built — a later refresh making an earlier folder read stale.
    private bool _folderView;
    private string _openFolder = "";
    private FolderTree? _tree;
    private int _listVersion;
    private bool _clearingSearch;
    private bool _syncingToggle;

    public FileExplorerPanel()
    {
        _title.Font = new Font(Font, FontStyle.Bold);
        _smallerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints);
        _largerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints + 6f);
        ApplyWidth();

        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, IndexingBar.Thickness));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _header.Controls.Add(_title, 0, 0);
        _header.Controls.Add(_collapse, 1, 0);
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.Controls.Add(_folderToggle, 0, 0);
        this._searchRow.Controls.Add(this._byName, 1, 0);
        this._searchRow.Controls.Add(this._byContent, 2, 0);
        _searchRow.Controls.Add(_search, 3, 0);
        _searchRow.Controls.Add(_rescan, 4, 0);
        _captionRow.Controls.Add(_caption);
        _captionRow.Controls.Add(_breadcrumb);
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _sizeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sizeRow.Controls.Add(_smallerGlyph, 0, 0);
        _sizeRow.Controls.Add(_size, 1, 0);
        _sizeRow.Controls.Add(_largerGlyph, 2, 0);

        // Docked to the top in reverse order of addition: the text above the button.
        _invite.Controls.Add(_chooseFolder);
        _invite.Controls.Add(_inviteText);
        _listHost.Controls.Add(_grid);
        _listHost.Controls.Add(_invite);
        _content.Controls.Add(_header, 0, 0);
        _content.Controls.Add(_searchRow, 0, 1);
        _content.Controls.Add(this._progress, 0, 2);
        _content.Controls.Add(_status, 0, 3);
        _content.Controls.Add(_captionRow, 0, 4);
        _content.Controls.Add(_listHost, 0, 5);
        _content.Controls.Add(_sizeRow, 0, 6);
        Controls.Add(_content);
        Controls.Add(_expand);
        _menu.Items.Add(_openLocation);
        _grid.ContextMenuStrip = _menu;
        _grid.IsFavorite = _favorites.Contains;
        this._grid.ContentExcerpt = this.ExcerptOf;
        this._grid.ContentSpot = this.SpotOf;

        _toolTip.SetToolTip(_collapse, "Hide the file explorer");
        _toolTip.SetToolTip(_expand, "Show the file explorer");
        _toolTip.SetToolTip(_rescan, "Rescan the folder");
        _toolTip.SetToolTip(_folderToggle, FolderToggleTip());
        this.ApplyCriteriaTips();
        _toolTip.SetToolTip(_smallerGlyph, "Smaller tiles");
        _toolTip.SetToolTip(_largerGlyph, "Larger tiles");
        _collapse.Click += (_, _) => SetOpen(false);
        _expand.Click += (_, _) => SetOpen(true);
        _size.ValueChanged += (_, _) => OnSliderChanged();
        _grid.SizeStepRequested += (_, direction) => StepSize(direction);
        _rescan.Click += (_, _) => Rescan();
        _chooseFolder.Click += (_, _) => ChooseFolderRequested?.Invoke(this, EventArgs.Empty);
        _search.TextChanged += (_, _) =>
        {
            Interlocked.Exchange(ref this._lastKeystroke, Environment.TickCount64);
            if (!_clearingSearch)
            {
                RefreshRows();
            }
        };
        _search.KeyDown += OnSearchKeyDown;
        _folderToggle.CheckedChanged += (_, _) => SetFolderView(_folderToggle.Checked);
        this._byName.Click += (_, _) => this.ToggleCriterion(this._byName, this._byContent);
        this._byContent.Click += (_, _) => this.ToggleCriterion(this._byContent, this._byName);
        _breadcrumb.UpRequested += (_, _) => GoUp();
        _breadcrumb.SegmentClicked += (_, depth) => OpenFolderAtDepth(depth);
        _grid.HeartClicked += (_, row) => ToggleFavorite(row);
        _grid.RowActivated += (_, row) => Activate(row);
        _grid.DragRequested += (_, row) => StartDrag(row);
        _grid.MoreRequested += (_, _) => LoadMore();
        _grid.UpRequested += (_, _) => GoUp();
        _menu.Opening += (_, e) =>
        {
            var row = _grid.RowAt(_grid.PointToClient(MousePosition));
            e.Cancel = row is null;
            _openLocation.Text = row?.IsFolder == true ? OpenInExplorerText : OpenFileLocationText;
        };
        _openLocation.Click += (_, _) => OpenLocation();
        _transient.Tick += (_, _) => ShowSummary();

        // Files dropped anywhere on the panel, open or collapsed, become favorites (see
        // workfiles/20260930-favorites-drag-drop.md); none of its children is a drop target of its own.
        AllowDrop = true;
        _content.Paint += (_, e) => PaintDropFrame(e.Graphics);
        ApplyMetrics();
        SyncSize();
        RefreshRows();
    }

    /// <summary>The user opened or collapsed the panel.</summary>
    public event EventHandler? OpenChanged;

    /// <summary>The user changed the tile size — the slider, or the wheel over the tiles.</summary>
    public event EventHandler? TileSizeChanged;

    /// <summary>The view was switched, or another folder opened: <see cref="FolderView"/> and <see cref="OpenFolder"/> to remember.</summary>
    public event EventHandler? FolderViewChanged;

    /// <summary>A tile was double-clicked or entered: its file, to be added like Add images.</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>The invitation's button: the base folder is to be chosen, as from the ⚙ menu.</summary>
    public event EventHandler? ChooseFolderRequested;

    /// <summary>A message the collapsed panel cannot show, its status line hidden: for the window's status line.</summary>
    public event EventHandler<(string Text, bool Error)>? MessageWhileCollapsed;

    /// <summary>Escape in the search box: the focus is to go back to the grid, the search kept.</summary>
    public event EventHandler? SearchEscaped;

    /// <summary>Whether the panel is open, or collapsed to its strip. Setting it raises nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Open
    {
        get => _open;
        set
        {
            if (value == _open)
            {
                return;
            }

            _open = value;
            SuspendLayout();
            _content.Visible = value;
            _expand.Visible = !value;
            ApplyWidth();
            ResumeLayout();
        }
    }

    /// <summary>
    /// The panel's width while open, in logical pixels — the user's, dragged from the panel's edge:
    /// the window sets it when the splitter stops, and it is applied again when the panel reopens or
    /// the DPI changes. Setting it raises nothing; <see cref="MinOpenWidth"/> at least.
    /// </summary>
    [DefaultValue(DefaultOpenWidth)]
    public int OpenWidth
    {
        get => _openWidth;
        set
        {
            value = Math.Max(MinOpenWidth, value);
            if (value == _openWidth)
            {
                return;
            }

            _openWidth = value;
            ApplyWidth();
        }
    }

    /// <summary>
    /// The tile size in logical pixels, <see cref="ThumbnailGrid.MinTileSize"/> to
    /// <see cref="ThumbnailGrid.MaxTileSize"/>: the width at which one more tile fits on a row, the
    /// tiles stretched to fill it. Setting it raises nothing.
    /// </summary>
    [DefaultValue(ThumbnailGrid.DefaultTileSize)]
    public int TileSize
    {
        get => _tileSize;
        set
        {
            value = Math.Clamp(value, ThumbnailGrid.MinTileSize, ThumbnailGrid.MaxTileSize);
            if (value == _tileSize)
            {
                return;
            }

            _tileSize = value;
            _grid.TileSize = value;
            SyncSize();
        }
    }

    /// <summary>
    /// How many pages of tiles a load holds, <see cref="MinPagesPerLoad"/> to
    /// <see cref="MaxPagesPerLoad"/> — a page being what the list shows at once. Applies from the next
    /// load. Setting it raises nothing.
    /// </summary>
    [DefaultValue(DefaultPagesPerLoad)]
    public int PagesPerLoad
    {
        get => _pagesPerLoad;
        set => _pagesPerLoad = Math.Clamp(value, MinPagesPerLoad, MaxPagesPerLoad);
    }

    /// <summary>
    /// Whether the files' content texts are extracted — the ⚙ menu's Search file contents (OCR): on, the
    /// extraction runs after each scan; turned off, it stops, the texts already extracted kept and
    /// still searched. Setting it raises nothing.
    /// </summary>
    [DefaultValue(false)]
    public bool ContentSearch
    {
        get => this._contentSearch;
        set
        {
            if (value == this._contentSearch)
            {
                return;
            }

            this._contentSearch = value;
            if (value)
            {
                this.StartExtraction();
            }
            else
            {
                this.StopExtraction();
            }

            this.ApplyCriteriaTips();
        }
    }

    /// <summary>Whether the content texts can be rebuilt: the content search on, a base folder set.</summary>
    public bool CanRebuildContent => this._contentSearch && this._baseFolder is not null;

    /// <summary>
    /// Forgets every content text and extracts them all again — after an OCR language was installed,
    /// for instance: the folder rescanned first, the list of files keeping the priority.
    /// </summary>
    public void RebuildContentIndex()
    {
        if (!this.CanRebuildContent)
        {
            return;
        }

        this.StopExtraction();
        if (this._contents is { } contents)
        {
            contents.Clear();
        }
        else
        {
            this._contents = new ContentIndex(this._baseFolder!);
        }

        this.Rescan();
    }

    /// <summary>
    /// Whether the folder view shows — the open folder's subfolders and files — rather than the search
    /// view — the favorites, <c>*</c> or the matches. Setting it raises nothing.
    /// </summary>
    [DefaultValue(false)]
    public bool FolderView
    {
        get => _folderView;
        set
        {
            if (value == _folderView)
            {
                return;
            }

            _folderView = value;
            _syncingToggle = true;
            try
            {
                _folderToggle.Checked = value;
            }
            finally
            {
                _syncingToggle = false;
            }

            ApplyView();
            RefreshRows();
        }
    }

    /// <summary>
    /// The folder the folder view shows, relative to the base folder — "" for the base folder itself.
    /// Set before <see cref="Start"/> to the remembered one; one gone from the disk gives way to its
    /// nearest parent still there when listed. Setting it raises nothing.
    /// </summary>
    [DefaultValue("")]
    public string OpenFolder
    {
        get => _openFolder;
        set
        {
            string folder = Path.TrimEndingDirectorySeparator(value ?? "");
            if (folder == _openFolder)
            {
                return;
            }

            _openFolder = folder;
            if (_folderView)
            {
                RefreshRows();
            }
        }
    }

    /// <summary>Whether <paramref name="screenPoint"/> is over the panel, open or collapsed.</summary>
    public bool ContainsScreenPoint(Point screenPoint) => Visible && RectangleToScreen(ClientRectangle).Contains(screenPoint);

    /// <summary>
    /// Frames the panel while a drop of favorites hovers it — a drag over the panel, or a cell's ✥
    /// handle driven by the window. The preview's drop-target highlight, not a helper indicator.
    /// </summary>
    public void ShowDropFrame(bool shown)
    {
        if (shown == _dropFrame)
        {
            return;
        }

        _dropFrame = shown;
        _content.Invalidate();
        _expand.UseVisualStyleBackColor = !shown;
        _expand.BackColor = shown ? SystemColors.Highlight : SystemColors.Control;
        _expand.ForeColor = shown ? SystemColors.HighlightText : SystemColors.ControlText;
    }

    /// <summary>
    /// Adds dropped files to the favorites, in their order, the last one the newest; a folder is
    /// skipped. The favorites list keeps its place, a search stays; the status line says what was added.
    /// </summary>
    public void AddFavorites(IReadOnlyList<string> paths)
    {
        var files = new List<string>();
        int folders = 0;
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                folders++;
            }
            else if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        if (files.Count == 0)
        {
            Report(folders > 0 ? "Nothing added: folders do not become favorites" : "Nothing added: no file to add", error: false);
            return;
        }

        string? error = null;
        try
        {
            _favorites.Add(files);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        if (ShowingFavorites)
        {
            RefreshRows(keepPlace: true);
        }
        else
        {
            _grid.Invalidate();
        }

        string added = files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} files";
        string skipped = folders switch { 0 => "", 1 => " — 1 folder skipped", _ => $" — {folders} folders skipped" };
        Report(error is null ? $"Added to favorites: {added}{skipped}" : $"Added to favorites: {added}, not saved: {error}", error is not null);
    }

    /// <summary>The search box has the focus: its keys are its own, not the window's shortcuts.</summary>
    public bool IsEditingText => _search.Focused;

    /// <summary>
    /// Ctrl+F: the panel opened if collapsed, as its « button does, then the search box focused, its
    /// text selected (see workfiles/20261007-ctrl-f-search-focus.md).
    /// </summary>
    public void FocusSearch()
    {
        if (!this._open)
        {
            this.SetOpen(true);
        }

        this._search.Focus();
        this._search.SelectAll();
    }

    /// <summary>
    /// Loads the favorites and the cached index of <paramref name="baseFolder"/> — usable at once —
    /// then rescans the folder in the background; null when no folder was chosen yet.
    /// </summary>
    public void Start(string? baseFolder)
    {
        _favorites.Load();
        SetBaseFolder(baseFolder);
    }

    /// <summary>Points the explorer at another folder, or none: its cached index is loaded, then the folder rescanned.</summary>
    public void SetBaseFolder(string? folder)
    {
        _scan?.Cancel();
        SetIndex(null, null);
        this.StopExtraction();
        this._contents = null;
        this._indexScanned = false;
        this._scanning = false;
        this._activity = null;
        this._progress.HideProgress();
        _rescan.Enabled = false;
        string? previous = _baseFolder;
        try
        {
            _baseFolder = folder is null ? null : FileIndex.NormalizeFolder(folder);

            // Another base folder: the folder view opens at its root. The first one, at start-up, keeps the remembered folder.
            if (previous is not null && !string.Equals(previous, _baseFolder, StringComparison.OrdinalIgnoreCase) && _openFolder.Length > 0)
            {
                _openFolder = "";
                FolderViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (ArgumentException)
        {
            _baseFolder = null;
            SetSummary($"Invalid folder: {folder}", error: true);
            RefreshRows();
            return;
        }

        if (_baseFolder is null)
        {
            SetSummary("No base folder");
            RefreshRows();
            return;
        }

        _ = LoadAndScanAsync(_baseFolder, Restart());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scan?.Cancel();
            _transient.Dispose();
            _toolTip.Dispose();
            _menu.Dispose();
            _title.Font.Dispose();
            _smallerGlyph.Font.Dispose();
            _largerGlyph.Font.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        OnDragOver(e);

        // As the window does: the shell helper keeps Explorer's thumbnail over the panel.
        if (e.Effect == DragDropEffects.Copy)
        {
            e.DropImageType = DropImageType.Copy;
            e.Message = "Add to %1";
            e.MessageReplacementToken = "Favorites";
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        bool files = !_draggingOwnTile && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = files ? DragDropEffects.Copy : DragDropEffects.None;
        ShowDropFrame(files);
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        ShowDropFrame(false);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        ShowDropFrame(false);
        if (!_draggingOwnTile && e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddFavorites(paths);
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    // The width is left to the window's own scaling: inside this handler the panel's DeviceDpi still
    // reads the old value, so setting it again here would scale it twice.
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    /// <summary>The panel's width, from its state: logical before the window scales its controls, device units after.</summary>
    private void ApplyWidth()
    {
        int logical = _open ? _openWidth : StripWidth;
        Width = IsHandleCreated ? LogicalToDeviceUnits(logical) : logical;
    }

    /// <summary>The rows sized to the font and the DPI: the progress bar's, the status and caption lines, the slider's row, the invitation.</summary>
    private void ApplyMetrics()
    {
        int line = Font.Height + LogicalToDeviceUnits(4);
        _content.RowStyles[2].Height = this.LogicalToDeviceUnits(IndexingBar.Thickness);
        _content.RowStyles[3].Height = line;
        _content.RowStyles[4].Height = line;
        _content.RowStyles[6].Height = Font.Height * 2 + LogicalToDeviceUnits(4);
        _inviteText.Height = Font.Height * 4 + LogicalToDeviceUnits(8);
    }

    /// <summary>The frame in the content's padding, around everything the open panel shows.</summary>
    private void PaintDropFrame(Graphics g)
    {
        if (!_dropFrame)
        {
            return;
        }

        using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(3)) { Alignment = PenAlignment.Inset };
        g.DrawRectangle(pen, new Rectangle(Point.Empty, _content.ClientSize));
    }

    /// <summary>A message on the status line, else on the window's while the panel is collapsed.</summary>
    public void Report(string message, bool error)
    {
        if (_open)
        {
            ShowTransient(message, error);
        }
        else
        {
            MessageWhileCollapsed?.Invoke(this, (message, error));
        }
    }

    private void SetOpen(bool open)
    {
        Open = open;
        OpenChanged?.Invoke(this, EventArgs.Empty);
        if (open)
        {
            _search.Focus();
        }
    }

    /// <summary>The slider on the tile size, its tooltip saying it.</summary>
    private void SyncSize()
    {
        _syncingSize = true;
        try
        {
            _size.Value = _tileSize;
        }
        finally
        {
            _syncingSize = false;
        }

        _toolTip.SetToolTip(_size, $"Tile size: {_tileSize} px");
    }

    /// <summary>The slider moved: its size applied and reported.</summary>
    private void OnSliderChanged()
    {
        if (!_syncingSize)
        {
            ChangeTileSize(_size.Value);
        }
    }

    /// <summary>The wheel over the tiles: 15 % larger or smaller per notch, the same feel at every size.</summary>
    private void StepSize(int direction)
    {
        double factor = direction > 0 ? 1.15 : 1 / 1.15;
        ChangeTileSize((int)Math.Round(_tileSize * factor));
    }

    /// <summary>A size chosen by the user: applied, and reported when it changed.</summary>
    private void ChangeTileSize(int size)
    {
        int before = _tileSize;
        TileSize = size;
        if (_tileSize != before)
        {
            TileSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private CancellationToken Restart()
    {
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        return _scan.Token;
    }

    private async Task LoadAndScanAsync(string folder, CancellationToken cancellation)
    {
        ShowStatus("Loading the index…");
        var (loaded, tree, contents) = await Task.Run(() =>
        {
            FileIndex.MoveLegacy();
            var index = FileIndex.Load(FileIndex.DefaultPath, folder);
            return (index, index is null ? null : FolderTree.Of(index.Entries), ContentIndex.Load(ContentIndex.DefaultPath, folder));
        });
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        SetIndex(loaded, tree);
        this._contents ??= contents;
        SetSummary(loaded is null ? "No index yet" : Summary(loaded));
        RefreshRows(keepPlace: true);
        await ScanAsync(folder, cancellation);
    }

    private void Rescan()
    {
        if (_baseFolder is { } folder)
        {
            _ = ScanAsync(folder, Restart());
        }
    }

    /// <summary>
    /// Scans the folder off the UI thread, the progress on the status line, and takes the result over
    /// unless another scan or folder replaced this one meanwhile. The index file is written from the
    /// worker too.
    /// </summary>
    private async Task ScanAsync(string folder, CancellationToken cancellation)
    {
        _rescan.Enabled = false;
        this._scanning = true;
        this._activity = null;
        this._progress.ShowUnknown();
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                ShowStatus(p.Phase == ScanPhase.Counting ? $"Counting… {p.Done:N0}" : $"Indexing… {p.Done:N0}/{p.Total:N0}");
                if (p.Phase == ScanPhase.Counting || p.Total == 0)
                {
                    this._progress.ShowUnknown();
                }
                else
                {
                    this._progress.ShowProgress((double)p.Done / p.Total);
                }
            }
        });
        var known = this._contents;
        bool scanned = false;
        try
        {
            if (!Directory.Exists(folder))
            {
                SetSummary($"Folder not found: {folder}", error: true);
                return;
            }

            var (index, tree, saveError, contents) = await Task.Run(
                () =>
                {
                    var scanned = FileIndex.Scan(folder, progress, cancellation);
                    string? error = null;
                    try
                    {
                        scanned.Save(FileIndex.DefaultPath);
                    }
                    catch (Exception ex) when (FileIndex.IsFileError(ex))
                    {
                        error = ex.Message;
                    }

                    // A rescan that cut the cached index's loading short loads the texts itself.
                    return (scanned, FolderTree.Of(scanned.Entries), error, known ?? ContentIndex.Load(ContentIndex.DefaultPath, folder));
                },
                cancellation);
            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            SetIndex(index, tree);
            this._contents ??= contents;
            this._indexScanned = true;
            scanned = true;
            SetSummary(saveError is null ? Summary(index) : $"{Summary(index)} · not saved: {saveError}", error: saveError is not null);
            RefreshRows(keepPlace: true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                SetSummary($"Scan failed: {ex.Message}", error: true);
            }
        }
        finally
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                _rescan.Enabled = true;
                this._scanning = false;
                this._progress.HideProgress();
            }
        }

        // The list of files first, the search needing it; then their content texts.
        if (scanned)
        {
            this.StartExtraction();
        }
    }

    /// <summary>
    /// Starts extracting the content texts the scanned index lacks, when the content search is on —
    /// cancelled with the scan, so a rescan, another folder or closing stops it — any extraction
    /// running stopped first.
    /// </summary>
    private void StartExtraction()
    {
        if (!this._contentSearch || !this._indexScanned || this._scanning || this._index is not { } index
            || this._contents is not { } contents || this._scan is not { IsCancellationRequested: false } scan)
        {
            return;
        }

        this.StopExtraction();
        this._extraction = CancellationTokenSource.CreateLinkedTokenSource(scan.Token);
        _ = this.ExtractAsync(index, contents, this._extraction.Token);
    }

    private void StopExtraction()
    {
        this._extraction?.Cancel();
        this._extraction = null;
    }

    /// <summary>
    /// Extracts the content texts off the UI thread, one file at a time: the bar and the status line
    /// following it, the search shown refreshed every few seconds and at the end.
    /// </summary>
    private async Task ExtractAsync(FileIndex index, ContentIndex contents, CancellationToken cancellation)
    {
        bool finished = false;
        var progress = new Progress<ExtractionProgress>(p =>
        {
            if (finished || cancellation.IsCancellationRequested || this.IsDisposed)
            {
                return;
            }

            this._progress.ShowProgress(p.Total == 0 ? 1 : (double)p.Done / p.Total);
            if (p.Refresh)
            {
                this.RefreshContentResults();
                return;
            }

            // The file's rank in the pass, then its name — none for a file that has no text to find.
            this.SetActivity(p.File is null ? null
                : p.Kind == ContentKind.None ? $"OCR: {p.Done + 1:N0}/{p.Total:N0}"
                : $"OCR: {p.Done + 1:N0}/{p.Total:N0} ({Path.GetFileName(p.File)})");
        });
        try
        {
            await Task.Run(() => this.Extract(index, contents, progress, cancellation), cancellation);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            finished = true;
            if (!this.IsDisposed)
            {
                this.SetActivity(null);
                if (!this._scanning)
                {
                    this._progress.HideProgress();
                }

                if (!cancellation.IsCancellationRequested)
                {
                    this.RefreshContentResults();
                }
            }
        }
    }

    /// <summary>
    /// The extraction's worker: the texts of the files gone from the index dropped, then every file
    /// whose text is missing or older than the file extracted, waiting between two while the search
    /// is busy; the texts saved every 50 files — no sooner than 5 s after the previous save — every half
    /// minute whatever the count, and at the end, so a pass cut short resumes where it stopped.
    /// </summary>
    private void Extract(FileIndex index, ContentIndex contents, IProgress<ExtractionProgress> progress, CancellationToken cancellation)
    {
        bool changed = contents.RetainOnly(index.Entries);
        var queue = index.Entries.Where(e => e.Stamp is not null && !contents.IsCurrent(e)).ToArray();
        var extractor = new ContentExtractor();
        int done = 0;
        int unsaved = 0;
        long saved = Environment.TickCount64;
        long refreshed = saved;
        try
        {
            foreach (var entry in queue)
            {
                cancellation.ThrowIfCancellationRequested();
                if (this.ExtractionPaused())
                {
                    progress.Report(new ExtractionProgress(done, queue.Length, null, ContentKind.None, false));
                    while (this.ExtractionPaused())
                    {
                        cancellation.WaitHandle.WaitOne(PausePoll);
                        cancellation.ThrowIfCancellationRequested();
                    }
                }

                string path = index.FullPath(entry);
                var kind = ContentExtractor.KindOf(path);
                progress.Report(new ExtractionProgress(done, queue.Length, path, kind, false));
                var (text, boxes) = extractor.Extract(path);
                contents.Set(entry.RelativePath, entry.Stamp!.Value, text, boxes);
                changed = true;
                done++;
                unsaved++;

                long now = Environment.TickCount64;
                if (now - saved >= ContentSaveInterval || (unsaved >= ContentSaveEvery && now - saved >= ContentSaveFloor))
                {
                    TrySave(contents);
                    saved = now;
                    unsaved = 0;
                }

                if (now - refreshed >= ContentRefreshInterval)
                {
                    refreshed = now;
                    progress.Report(new ExtractionProgress(done, queue.Length, null, ContentKind.None, true));
                }
            }
        }
        finally
        {
            if (changed)
            {
                TrySave(contents);
            }
        }
    }

    /// <summary>Whether the extraction waits: thumbnails are loading, or the search box was typed in a moment ago. Read from the worker.</summary>
    private bool ExtractionPaused() =>
        this._grid.LoadingThumbnails || Environment.TickCount64 - Interlocked.Read(ref this._lastKeystroke) < KeystrokePause;

    /// <summary>Saves the content texts, a failure left for the next save.</summary>
    private static void TrySave(ContentIndex contents)
    {
        try
        {
            contents.Save(ContentIndex.DefaultPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }
    }

    /// <summary>The search shown — not the favorites, nor <c>*</c>, nor a folder as the disk holds it — run again, its place kept, as texts arrived.</summary>
    private void RefreshContentResults()
    {
        if (FileSearch.Words(_search.Text).Length > 0 && !FileSearch.IsEverything(_search.Text))
        {
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>What the extraction does — the file it reads — after the summary on the status line; null for nothing.</summary>
    private void SetActivity(string? activity)
    {
        if (activity == this._activity)
        {
            return;
        }

        this._activity = activity;
        if (!this._transient.Enabled)
        {
            this.ShowSummary();
        }
    }

    private static string Summary(FileIndex index)
    {
        string when = index.ScannedAt.Date == DateTime.Today ? index.ScannedAt.ToString("t") : index.ScannedAt.ToString("g");
        return $"{Files(index.Count)} · indexed {when}";
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count:N0} files";

    /// <summary>The line shown while nothing else is: the index summary, or what stands in its way.</summary>
    private void SetSummary(string summary, bool error = false)
    {
        _summary = summary;
        _summaryError = error;
        ShowSummary();
    }

    private void ShowSummary()
    {
        _transient.Stop();
        Show(this._activity is null ? this._summary : $"{this._summary} · {this._activity}", this._summaryError);
    }

    /// <summary>A progress text, kept until the next status.</summary>
    private void ShowStatus(string message)
    {
        _transient.Stop();
        Show(message, error: false);
    }

    /// <summary>A message that gives way to the summary after a few seconds.</summary>
    private void ShowTransient(string message, bool error = false)
    {
        Show(message, error);
        _transient.Stop();
        _transient.Start();
    }

    private void Show(string message, bool error)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = message;
        _toolTip.SetToolTip(_status, message);
    }

    /// <summary>
    /// The list as the view and the search box stand. The search view: every favorite while the box
    /// is blank, every file for <c>*</c>, else the matches of its words. The folder view: the open
    /// folder's content, read from the disk, while the box is blank; else the folders then the files
    /// below the open folder, from the index. Without a base folder, a typed search or the folder view
    /// shows the invitation instead. A new list shows its first load from the top; a refreshed one
    /// (<paramref name="keepPlace"/>: a rescan, a favorite or a missing file gone) keeps as many tiles
    /// loaded as before, the scroll and the selection. <paramref name="select"/>: a path to select
    /// once listed — the folder just left when going up.
    /// </summary>
    private void RefreshRows(bool keepPlace = false, string? select = null)
    {
        int version = ++_listVersion;
        bool everything = FileSearch.IsEverything(_search.Text);
        string[] words = FileSearch.Words(_search.Text);
        bool inviting = _baseFolder is null && (words.Length > 0 || _folderView);
        _invite.Visible = inviting;
        _grid.Visible = !inviting;
        if (_folderView)
        {
            if (inviting)
            {
                ShowRows([], keepPlace, null);
                ShowBreadcrumb("");
            }
            else if (words.Length == 0)
            {
                _ = ListFolderAsync(version, keepPlace, select);
            }
            else if (_index is null)
            {
                ShowRows([], keepPlace, null);
                ShowBreadcrumb("Waiting for the index…");
            }
            else
            {
                var (found, trailing) = SearchFolder(_index, words, everything);
                ShowRows(found, keepPlace, select);
                ShowBreadcrumb(trailing);
            }

            return;
        }

        var rows = new List<ExplorerRow>();
        if (words.Length == 0)
        {
            foreach (string path in _favorites.Newest)
            {
                rows.Add(new ExplorerRow(path, Path.GetFileName(path)));
            }

            _caption.Text = $"Favorites ({_favorites.Count})";
        }
        else if (_index is null)
        {
            _caption.Text = inviting ? "" : "Waiting for the index…";
        }
        else
        {
            var found = this.Find(_index.Entries, words, everything);
            foreach (var (entry, contentWord) in found)
            {
                rows.Add(new ExplorerRow(_index.FullPath(entry), entry.Name, ContentWord: contentWord));
            }

            _caption.Text = everything
                ? $"All files ({found.Count:N0})"
                : found.Count switch
                {
                    0 => "No result",
                    1 => "1 result",
                    _ => $"{found.Count:N0} results",
                };
        }

        ShowRows(rows, keepPlace, null);
    }

    /// <summary>
    /// A new list: its first load — or, keeping the place, as many tiles as were loaded — and enough
    /// of it for <paramref name="select"/>, then selected.
    /// </summary>
    private void ShowRows(IReadOnlyList<ExplorerRow> rows, bool keepPlace, string? select)
    {
        _list = rows;
        int count = keepPlace ? Math.Max(_loaded, FirstLoad()) : FirstLoad();
        if (select is not null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].FullPath, select, StringComparison.OrdinalIgnoreCase))
                {
                    count = Math.Max(count, i + 1);
                    break;
                }
            }
        }

        ShowLoaded(count, keepPlace);
        if (select is not null)
        {
            _grid.SelectPath(select);
        }
    }

    /// <summary>
    /// The open folder's content, read from the disk off the UI thread — the status line saying so when
    /// it takes a while — unless another list was asked for meanwhile. The open folder gone from the
    /// disk gives way to its nearest parent still there, and the status line says so.
    /// </summary>
    private async Task ListFolderAsync(int version, bool keepPlace, string? select)
    {
        string root = _baseFolder!;
        string asked = _openFolder;
        var reading = Task.Run(() => ReadFolder(root, asked));
        bool noticed = false;
        if (await Task.WhenAny(reading, Task.Delay(ReadingNoticeDelay)) != reading && version == _listVersion && !IsDisposed)
        {
            ShowStatus("Reading the folder…");
            noticed = true;
        }

        var listing = await reading;
        if (IsDisposed)
        {
            return;
        }

        if (noticed)
        {
            ShowSummary();
        }

        if (version != _listVersion)
        {
            return;
        }

        if (!string.Equals(listing.Folder, asked, StringComparison.OrdinalIgnoreCase))
        {
            _openFolder = listing.Folder;
            keepPlace = false;
            select = null;
            FolderViewChanged?.Invoke(this, EventArgs.Empty);
            Report($"Folder not found: {asked} — back to {SegmentsOf(listing.Folder)[^1]}", error: false);
        }

        var rows = new List<ExplorerRow>(listing.Folders.Count + listing.Files.Count);
        foreach (string folder in listing.Folders)
        {
            rows.Add(FolderRow(folder));
        }

        foreach (var (path, _) in listing.Files)
        {
            rows.Add(new ExplorerRow(path, Path.GetFileName(path)));
        }

        ShowRows(rows, keepPlace, select);
        ShowBreadcrumb(listing.Error ?? $"{Folders(listing.Folders.Count)}, {Files(listing.Files.Count)}");
    }

    /// <summary>
    /// The folder as the disk holds it, on a worker: <paramref name="relative"/>, or its nearest parent
    /// still there, the base folder at worst; nothing listed, and why, when that cannot be read.
    /// </summary>
    private static (string Folder, IReadOnlyList<string> Folders, IReadOnlyList<(string Path, DateTime Created)> Files, string? Error) ReadFolder(string root, string relative)
    {
        while (relative.Length > 0 && !Directory.Exists(Path.Combine(root, relative)))
        {
            relative = FolderTree.Parent(relative);
        }

        try
        {
            var (folders, files) = FolderListing.Read(Path.Combine(root, relative));
            return (relative, folders, files, null);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            return (relative, [], [], ex is DirectoryNotFoundException ? "Folder not found" : $"Not readable: {ex.Message}");
        }
    }

    /// <summary>
    /// A search in the folder view, from the index: the folders below the open one, then its files —
    /// every one of each for <c>*</c>, the folders A→Z by their path so each is followed by its
    /// subfolders, the files the newest first; else those matching the words, best first.
    /// </summary>
    private (IReadOnlyList<ExplorerRow> Rows, string Trailing) SearchFolder(FileIndex index, string[] words, bool everything)
    {
        _tree ??= FolderTree.Of(index.Entries);
        var folders = _tree.Folders.Where(f => FolderTree.IsUnder(f.RelativePath, _openFolder)).ToArray();
        var files = _openFolder.Length == 0 ? index.Entries : index.Entries.Where(e => FolderTree.IsUnder(e.RelativePath, _openFolder)).ToArray();
        IReadOnlyList<IndexEntry> foundFolders;
        if (everything)
        {
            Array.Sort(folders, (a, b) => FolderTree.ComparePaths(a.RelativePath, b.RelativePath));
            foundFolders = folders;
        }
        else if (this._byName.Checked)
        {
            foundFolders = FileSearch.Search(folders, words).Select(m => m.Entry).ToArray();
        }
        else
        {
            // Content only: a folder has no content text, so none is found.
            foundFolders = [];
        }

        var foundFiles = this.Find(files, words, everything);

        var rows = new List<ExplorerRow>(foundFolders.Count + foundFiles.Count);
        foreach (var folder in foundFolders)
        {
            rows.Add(FolderRow(index.FullPath(folder)));
        }

        foreach (var (file, contentWord) in foundFiles)
        {
            rows.Add(new ExplorerRow(index.FullPath(file), file.Name, ContentWord: contentWord));
        }

        int total = rows.Count;
        string trailing = everything
            ? $"All: {Folders(foundFolders.Count)}, {Files(foundFiles.Count)}"
            : total switch
            {
                0 => "No result",
                1 => "1 result",
                _ => $"{total:N0} results",
            };
        return (rows, trailing);
    }

    /// <summary>
    /// The files of a search: every one for <c>*</c>, the most recently created first; else those
    /// matching the words — by their path or by their content text, as the search criteria ask — best first.
    /// </summary>
    private IReadOnlyList<SearchMatch> Find(IReadOnlyList<IndexEntry> entries, string[] words, bool everything)
    {
        if (everything)
        {
            return FileSearch.All(entries).Select(e => new SearchMatch(e, null)).ToArray();
        }

        var contents = this._byContent.Checked ? this._contents : null;
        return FileSearch.Search(entries, words, contents is null ? null : e => contents.FoldedOf(e.RelativePath), this._byName.Checked);
    }

    /// <summary>The words around the one the search found in a tile's content text, 4 on each side.</summary>
    private string? ExcerptOf(ExplorerRow row) =>
        row.ContentWord is { } word && this._contents is { } contents && this._baseFolder is { } root
            ? contents.ExcerptOf(Path.GetRelativePath(root, row.FullPath), word, 4)
            : null;

    /// <summary>Where the OCR recognised the word the search found in a tile's content, for the bulb's arrow.</summary>
    private RectangleF? SpotOf(ExplorerRow row) =>
        row.ContentWord is { } word && this._contents is { } contents && this._baseFolder is { } root
            ? contents.SpotOf(Path.GetRelativePath(root, row.FullPath), word)
            : null;

    /// <summary>A folder's tile: its name, then every file below it as the index counts them, once the index is there.</summary>
    private ExplorerRow FolderRow(string fullPath)
    {
        string name = Path.GetFileName(fullPath);
        if (_index is null || _baseFolder is null)
        {
            return new ExplorerRow(fullPath, name, IsFolder: true);
        }

        _tree ??= FolderTree.Of(_index.Entries);
        int count = _tree.CountBelow(Path.GetRelativePath(_baseFolder, fullPath), _index.Count);
        return new ExplorerRow(fullPath, $"{name} ({count:N0})", IsFolder: true);
    }

    private static string Folders(int count) => count == 1 ? "1 folder" : $"{count:N0} folders";

    /// <summary>The base folder's name, then every folder down to <paramref name="relative"/>.</summary>
    private string[] SegmentsOf(string relative)
    {
        string root = _baseFolder is null ? "" : Path.GetFileName(_baseFolder) is { Length: > 0 } name ? name : _baseFolder;
        return relative.Length == 0 ? [root] : [root, .. relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>The breadcrumb on the open folder, <paramref name="trailing"/> after it.</summary>
    private void ShowBreadcrumb(string trailing) => _breadcrumb.SetPath(_baseFolder is null ? [] : SegmentsOf(_openFolder), trailing);

    /// <summary>The index, and the folders derived from it, taken over together.</summary>
    private void SetIndex(FileIndex? index, FolderTree? tree)
    {
        _index = index;
        _tree = tree;
    }

    /// <summary>Whether the list is the favorites: the search view with a blank box.</summary>
    private bool ShowingFavorites => !_folderView && FileSearch.Words(_search.Text).Length == 0;

    /// <summary>The 📁 toggle was pressed or released by the user: the other view shows, remembered.</summary>
    private void SetFolderView(bool on)
    {
        if (_syncingToggle || on == _folderView)
        {
            return;
        }

        _folderView = on;
        ApplyView();
        RefreshRows();
        FolderViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What the view changes around the grid: the caption or the breadcrumb, the search box's hint, the toggle's tooltip.</summary>
    private void ApplyView()
    {
        _caption.Visible = !_folderView;
        _breadcrumb.Visible = _folderView;
        _search.PlaceholderText = _folderView ? FolderSearchPlaceholder : SearchPlaceholder;
        _toolTip.SetToolTip(_folderToggle, FolderToggleTip());
    }

    /// <summary>
    /// A search criterion button clicked: pressed or released, the search shown run again — unless it is
    /// the last one pressed, which stays pressed. Nothing else moves: the favorites and <c>*</c> stay as
    /// they are, the criteria counting once words are typed.
    /// </summary>
    private void ToggleCriterion(CheckBox criterion, CheckBox other)
    {
        if (criterion.Checked && !other.Checked)
        {
            return;
        }

        criterion.Checked = !criterion.Checked;
        this.ApplyCriteriaTips();
        if (FileSearch.Words(this._search.Text).Length > 0 && !FileSearch.IsEverything(this._search.Text))
        {
            this.RefreshRows();
        }
    }

    /// <summary>The criteria buttons' tooltips: what each searches, whether it is on, why the last one pressed stays pressed, the extraction stopped.</summary>
    private void ApplyCriteriaTips()
    {
        this._toolTip.SetToolTip(this._byName, CriterionTip("Search in the file names and their folders", this._byName.Checked, this._byContent.Checked, null));
        this._toolTip.SetToolTip(this._byContent, CriterionTip(
            "Search in the text inside the files",
            this._byContent.Checked,
            this._byName.Checked,
            this._contentSearch ? null : "The extraction is stopped (⚙ Search file contents (OCR)): only the texts already extracted are searched"));
    }

    private static string CriterionTip(string what, bool on, bool otherOn, string? note)
    {
        string state = !on ? "off — click to turn on"
            : otherOn ? "on — click to turn off"
            : "on — at least one criterion stays on";
        return note is null ? $"{what}: {state}" : $"{what}: {state}\n{note}";
    }

    private string FolderToggleTip() => _folderView
        ? "Back to the favorites and the search over every file"
        : "Browse the base folder, folder by folder";

    /// <summary>Opens a folder of the folder view — its content from the top, the search box cleared — and remembers it.</summary>
    private void Navigate(string relative, string? select = null)
    {
        _openFolder = relative;
        if (_search.TextLength > 0)
        {
            _clearingSearch = true;
            try
            {
                _search.Text = "";
            }
            finally
            {
                _clearingSearch = false;
            }
        }

        RefreshRows(select: select);
        FolderViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>↑, Backspace or Alt+↑: the parent folder, the folder just left selected.</summary>
    private void GoUp()
    {
        if (_folderView && _baseFolder is not null && _openFolder.Length > 0)
        {
            Navigate(FolderTree.Parent(_openFolder), Path.Combine(_baseFolder, _openFolder));
        }
    }

    /// <summary>A folder of the breadcrumb clicked (0: the base folder), the one below it on the way to the open folder selected.</summary>
    private void OpenFolderAtDepth(int depth)
    {
        if (_baseFolder is null)
        {
            return;
        }

        string[] path = SegmentsOf(_openFolder)[1..];
        if (depth >= path.Length)
        {
            return;
        }

        Navigate(string.Join('\\', path[..depth]), Path.Combine(_baseFolder, string.Join('\\', path[..(depth + 1)])));
    }

    /// <summary>A load's slots: the pages per load times what the list shows at once.</summary>
    private int LoadSize() => _pagesPerLoad * _grid.PageSize;

    /// <summary>The first load's tiles: its slots but the last, left to the Loading… slot.</summary>
    private int FirstLoad() => LoadSize() - 1;

    /// <summary>
    /// The Loading… slot came into view: the next load, the first new tile in its slot, a new slot
    /// ending the load a load further.
    /// </summary>
    private void LoadMore()
    {
        if (_loaded < _list.Count)
        {
            ShowLoaded(_loaded + LoadSize(), keepPlace: true);
        }
    }

    /// <summary>
    /// Shows the first <paramref name="count"/> tiles of the list and the Loading… slot after them —
    /// or the whole list, without the slot, when the rest would fit in it.
    /// </summary>
    private void ShowLoaded(int count, bool keepPlace)
    {
        _loaded = count + 1 >= _list.Count ? _list.Count : Math.Max(1, count);
        bool hasMore = _loaded < _list.Count;
        var rows = hasMore ? _list.Take(_loaded).ToArray() : _list;
        _grid.SetRows(rows, hasMore, keepPlace);
    }

    /// <summary>The tiles loaded, the Loading… slot aside.</summary>
    private IReadOnlyList<ExplorerRow> LoadedRows => _grid.Rows;

    /// <summary>Enter activates the first tile; ↓ moves to the grid.</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_grid.Visible && LoadedRows.Count > 0)
                {
                    Activate(LoadedRows[0]);
                }

                break;
            case Keys.Up when e.Alt:
                e.Handled = true;
                e.SuppressKeyPress = true;
                GoUp();
                break;
            case Keys.Down when _grid.Visible && LoadedRows.Count > 0:
                e.Handled = true;
                if (_grid.SelectedRow is null)
                {
                    _grid.SelectFirst();
                }

                _grid.Focus();
                break;
            case Keys.Escape:
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.SearchEscaped?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>A file tile is added like Add images; a folder tile opens its folder.</summary>
    private void Activate(ExplorerRow row)
    {
        if (!Exists(row))
        {
            return;
        }

        if (row.IsFolder)
        {
            Navigate(Path.GetRelativePath(_baseFolder!, row.FullPath));
        }
        else
        {
            FileActivated?.Invoke(this, row.FullPath);
        }
    }

    /// <summary>A file tile dragged past the threshold becomes a file drag, like one from the Explorer; a folder tile does not drag.</summary>
    private void StartDrag(ExplorerRow row)
    {
        if (!row.IsFolder && Exists(row))
        {
            _draggingOwnTile = true;
            try
            {
                _grid.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { row.FullPath }), DragDropEffects.Copy);
            }
            finally
            {
                _draggingOwnTile = false;
            }
        }
    }

    /// <summary>
    /// The heart of a tile. A favorite of <see cref="PastedFavorites.Folder"/> un-hearted goes to the
    /// Recycle Bin: the app made that file for the favorite.
    /// </summary>
    private void ToggleFavorite(ExplorerRow row)
    {
        bool added = true;
        try
        {
            added = _favorites.Toggle(row.FullPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            ShowTransient($"Favorites not saved: {ex.Message}", error: true);
        }

        if (!added && PastedFavorites.Holds(row.FullPath))
        {
            try
            {
                PastedFavorites.Recycle(row.FullPath);
                ShowTransient($"{Path.GetFileName(row.FullPath)} sent to the Recycle Bin");
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                ShowTransient($"{Path.GetFileName(row.FullPath)} not sent to the Recycle Bin: {ex.Message}", error: true);
            }
        }

        if (ShowingFavorites)
        {
            // The favorites list: the tile leaves it, the place kept — once the grid is done with the click.
            BeginInvoke(() => RefreshRows(keepPlace: true));
        }
        else
        {
            _grid.InvalidateRow(row);
        }
    }

    private void OpenLocation()
    {
        if (_grid.SelectedRow is not { } row || !Exists(row))
        {
            return;
        }

        try
        {
            Process.Start("explorer.exe", row.IsFolder ? $"\"{row.FullPath}\"" : $"/select,\"{row.FullPath}\"")?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowTransient($"Explorer could not be opened: {ex.Message}", error: true);
        }
    }

    /// <summary>
    /// Whether the tile's file is still there. A missing one leaves the index — the file rewritten —
    /// and the favorites, the grid refreshed, and the status line says so for a few seconds.
    /// </summary>
    private bool Exists(ExplorerRow row)
    {
        if (row.IsFolder)
        {
            if (Directory.Exists(row.FullPath))
            {
                return true;
            }

            // A folder read from the disk, or derived from the index: the list is read again.
            RefreshRows(keepPlace: true);
            ShowTransient("Folder not found");
            return false;
        }

        if (File.Exists(row.FullPath))
        {
            return true;
        }

        string? error = null;
        if (_index?.Remove(row.FullPath) == true)
        {
            _tree = null;
            this._contents?.Remove(Path.GetRelativePath(_index.BaseFolder, row.FullPath));
            try
            {
                _index.Save(FileIndex.DefaultPath);
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                error = ex.Message;
            }
        }

        try
        {
            _favorites.Remove(row.FullPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error ??= ex.Message;
        }

        RefreshRows(keepPlace: true);
        ShowTransient(error is null ? "File not found — removed from the index" : $"File not found — removed from the index, not saved: {error}", error: error is not null);
        return false;
    }
}

/// <summary>
/// Where the content extraction stands: the files done out of those to do, the file it reads now
/// (null while it waits or between two), how it has it, and whether the search shown is to be refreshed.
/// </summary>
internal readonly record struct ExtractionProgress(int Done, int Total, string? File, ContentKind Kind, bool Refresh);
