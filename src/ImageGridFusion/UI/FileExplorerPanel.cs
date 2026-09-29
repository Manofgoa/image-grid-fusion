using System.ComponentModel;
using System.Diagnostics;
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

    private readonly TableLayoutPanel _content = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(6, 4, 6, 6) };
    private readonly TableLayoutPanel _header = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _title = new() { Text = "Files", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _collapse = new() { Text = "»", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Button _expand = new() { Text = "«", Dock = DockStyle.Fill, Visible = false, Margin = Padding.Empty };
    private readonly TableLayoutPanel _searchRow = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TextBox _search = new() { PlaceholderText = "Search files… (* for all)", Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _rescan = new() { Text = "↻", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right, Enabled = false };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Label _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
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
    private readonly ToolStripMenuItem _openLocation = new("Open file location");
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
    private int _pagesPerLoad = DefaultPagesPerLoad;

    // The whole list the search box asks for, computed once, and how many of its tiles are loaded.
    private IReadOnlyList<ExplorerRow> _list = [];
    private int _loaded;

    public FileExplorerPanel()
    {
        _title.Font = new Font(Font, FontStyle.Bold);
        _smallerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints);
        _largerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints + 6f);
        ApplyWidth();

        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _header.Controls.Add(_title, 0, 0);
        _header.Controls.Add(_collapse, 1, 0);
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.Controls.Add(_search, 0, 0);
        _searchRow.Controls.Add(_rescan, 1, 0);
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
        _content.Controls.Add(_status, 0, 2);
        _content.Controls.Add(_caption, 0, 3);
        _content.Controls.Add(_listHost, 0, 4);
        _content.Controls.Add(_sizeRow, 0, 5);
        Controls.Add(_content);
        Controls.Add(_expand);
        _menu.Items.Add(_openLocation);
        _grid.ContextMenuStrip = _menu;
        _grid.IsFavorite = _favorites.Contains;

        _toolTip.SetToolTip(_collapse, "Hide the file explorer");
        _toolTip.SetToolTip(_expand, "Show the file explorer");
        _toolTip.SetToolTip(_rescan, "Rescan the folder");
        _toolTip.SetToolTip(_smallerGlyph, "Smaller tiles");
        _toolTip.SetToolTip(_largerGlyph, "Larger tiles");
        _collapse.Click += (_, _) => SetOpen(false);
        _expand.Click += (_, _) => SetOpen(true);
        _size.ValueChanged += (_, _) => OnSliderChanged();
        _grid.SizeStepRequested += (_, direction) => StepSize(direction);
        _rescan.Click += (_, _) => Rescan();
        _chooseFolder.Click += (_, _) => ChooseFolderRequested?.Invoke(this, EventArgs.Empty);
        _search.TextChanged += (_, _) => RefreshRows();
        _search.KeyDown += OnSearchKeyDown;
        _grid.HeartClicked += (_, row) => ToggleFavorite(row);
        _grid.RowActivated += (_, row) => Activate(row);
        _grid.DragRequested += (_, row) => StartDrag(row);
        _grid.MoreRequested += (_, _) => LoadMore();
        _menu.Opening += (_, e) => e.Cancel = _grid.RowAt(_grid.PointToClient(MousePosition)) is null;
        _openLocation.Click += (_, _) => OpenLocation();
        _transient.Tick += (_, _) => ShowSummary();
        ApplyMetrics();
        SyncSize();
        RefreshRows();
    }

    /// <summary>The user opened or collapsed the panel.</summary>
    public event EventHandler? OpenChanged;

    /// <summary>The user changed the tile size — the slider, or the wheel over the tiles.</summary>
    public event EventHandler? TileSizeChanged;

    /// <summary>A tile was double-clicked or entered: its file, to be added like Add images.</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>The invitation's button: the base folder is to be chosen, as from the ⚙ menu.</summary>
    public event EventHandler? ChooseFolderRequested;

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

    /// <summary>The search box has the focus: its keys are its own, not the window's shortcuts.</summary>
    public bool IsEditingText => _search.Focused;

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
        _index = null;
        _rescan.Enabled = false;
        try
        {
            _baseFolder = folder is null ? null : FileIndex.NormalizeFolder(folder);
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

    /// <summary>The rows sized to the font: the status and caption lines, the slider's row, the invitation.</summary>
    private void ApplyMetrics()
    {
        int line = Font.Height + LogicalToDeviceUnits(4);
        _content.RowStyles[2].Height = line;
        _content.RowStyles[3].Height = line;
        _content.RowStyles[5].Height = Font.Height * 2 + LogicalToDeviceUnits(4);
        _inviteText.Height = Font.Height * 4 + LogicalToDeviceUnits(8);
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
        var loaded = await Task.Run(() => FileIndex.Load(FileIndex.DefaultPath, folder));
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        _index = loaded;
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
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                ShowStatus(p.Phase == ScanPhase.Counting ? $"Counting… {p.Done:N0}" : $"Indexing… {p.Done:N0}/{p.Total:N0}");
            }
        });
        try
        {
            if (!Directory.Exists(folder))
            {
                SetSummary($"Folder not found: {folder}", error: true);
                return;
            }

            var (index, saveError) = await Task.Run(
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

                    return (scanned, error);
                },
                cancellation);
            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            _index = index;
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
            }
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
        Show(_summary, _summaryError);
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
    /// The list as the search box stands: every favorite while it is blank, every file for <c>*</c>,
    /// else the matches of its words; without a base folder, a typed search shows the invitation
    /// instead. A new list shows its first load from the top; a refreshed one
    /// (<paramref name="keepPlace"/>: a rescan, a favorite or a missing file gone) keeps as many tiles
    /// loaded as before, the scroll and the selection.
    /// </summary>
    private void RefreshRows(bool keepPlace = false)
    {
        bool everything = FileSearch.IsEverything(_search.Text);
        string[] words = FileSearch.Words(_search.Text);
        bool inviting = words.Length > 0 && _baseFolder is null;
        _invite.Visible = inviting;
        _grid.Visible = !inviting;
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
            var found = everything ? FileSearch.All(_index.Entries) : FileSearch.Search(_index.Entries, words);
            foreach (var entry in found)
            {
                rows.Add(new ExplorerRow(_index.FullPath(entry), entry.Name));
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

        _list = rows;
        ShowLoaded(keepPlace ? Math.Max(_loaded, FirstLoad()) : FirstLoad(), keepPlace);
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
            case Keys.Down when _grid.Visible && LoadedRows.Count > 0:
                e.Handled = true;
                if (_grid.SelectedRow is null)
                {
                    _grid.SelectFirst();
                }

                _grid.Focus();
                break;
        }
    }

    private void Activate(ExplorerRow row)
    {
        if (Exists(row))
        {
            FileActivated?.Invoke(this, row.FullPath);
        }
    }

    /// <summary>A tile dragged past the threshold becomes a file drag, like one from the Explorer.</summary>
    private void StartDrag(ExplorerRow row)
    {
        if (Exists(row))
        {
            _grid.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { row.FullPath }), DragDropEffects.Copy);
        }
    }

    private void ToggleFavorite(ExplorerRow row)
    {
        try
        {
            _favorites.Toggle(row.FullPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            ShowTransient($"Favorites not saved: {ex.Message}", error: true);
        }

        if (FileSearch.Words(_search.Text).Length == 0)
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
            Process.Start("explorer.exe", $"/select,\"{row.FullPath}\"")?.Dispose();
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
        if (File.Exists(row.FullPath))
        {
            return true;
        }

        string? error = null;
        if (_index?.Remove(row.FullPath) == true)
        {
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
