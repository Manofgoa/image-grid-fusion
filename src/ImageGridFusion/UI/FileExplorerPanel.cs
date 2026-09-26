using System.ComponentModel;
using System.Diagnostics;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer, at the right of the preview (see workfiles/20260926-file-explorer.md): a search
/// box over the index of the base folder, its 10 best matches as the user types, and the favorites —
/// all of them, the newest first — while the box is empty. A row is dragged onto a cell like a file
/// from the Explorer, double-clicked to be added like Add images, hearted to become a favorite.
/// Collapses to a strip. The cached index is loaded, then rescanned in the background, at every start.
/// </summary>
internal sealed class FileExplorerPanel : Panel
{
    // In logical pixels.
    private const int OpenWidth = 280;
    private const int StripWidth = 20;
    private const int HeartZone = 22;

    private const int TransientDuration = 5000;

    private readonly TableLayoutPanel _content = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(6, 4, 6, 6) };
    private readonly TableLayoutPanel _header = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _title = new() { Text = "Files", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _collapse = new() { Text = "»", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Button _expand = new() { Text = "«", Dock = DockStyle.Fill, Visible = false, Margin = Padding.Empty };
    private readonly TableLayoutPanel _searchRow = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TextBox _search = new() { PlaceholderText = "Search files…", Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _rescan = new() { Text = "↻", Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right, Enabled = false };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Label _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Panel _listHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
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
    private readonly Font _heartFont;

    private bool _open = true;
    private string? _baseFolder;
    private FileIndex? _index;
    private CancellationTokenSource? _scan;
    private string _summary = "No base folder";
    private bool _summaryError;
    private int _hovered = -1;
    private int _pressedRow = -1;
    private Point _pressedAt;

    public FileExplorerPanel()
    {
        Width = OpenWidth;
        _heartFont = new Font("Segoe UI Symbol", Font.SizeInPoints + 1f);
        _title.Font = new Font(Font, FontStyle.Bold);

        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _header.Controls.Add(_title, 0, 0);
        _header.Controls.Add(_collapse, 1, 0);
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.Controls.Add(_search, 0, 0);
        _searchRow.Controls.Add(_rescan, 1, 0);

        // Docked to the top in reverse order of addition: the text above the button.
        _invite.Controls.Add(_chooseFolder);
        _invite.Controls.Add(_inviteText);
        _listHost.Controls.Add(_list);
        _listHost.Controls.Add(_invite);
        _content.Controls.Add(_header, 0, 0);
        _content.Controls.Add(_searchRow, 0, 1);
        _content.Controls.Add(_status, 0, 2);
        _content.Controls.Add(_caption, 0, 3);
        _content.Controls.Add(_listHost, 0, 4);
        Controls.Add(_content);
        Controls.Add(_expand);
        _menu.Items.Add(_openLocation);
        _list.ContextMenuStrip = _menu;

        _toolTip.SetToolTip(_collapse, "Hide the file explorer");
        _toolTip.SetToolTip(_expand, "Show the file explorer");
        _toolTip.SetToolTip(_rescan, "Rescan the folder");
        _collapse.Click += (_, _) => SetOpen(false);
        _expand.Click += (_, _) => SetOpen(true);
        _rescan.Click += (_, _) => Rescan();
        _chooseFolder.Click += (_, _) => ChooseFolderRequested?.Invoke(this, EventArgs.Empty);
        _search.TextChanged += (_, _) => RefreshRows();
        _search.KeyDown += OnSearchKeyDown;
        _list.DrawItem += OnDrawRow;
        _list.MouseDown += OnListMouseDown;
        _list.MouseMove += OnListMouseMove;
        _list.MouseUp += (_, _) => _pressedRow = -1;
        _list.MouseLeave += (_, _) => Hover(-1);
        _list.MouseDoubleClick += OnListDoubleClick;
        _list.KeyDown += OnListKeyDown;
        _menu.Opening += (_, e) => e.Cancel = RowAt(_list.PointToClient(MousePosition)) is null;
        _openLocation.Click += (_, _) => OpenLocation();
        _transient.Tick += (_, _) => ShowSummary();
        ApplyMetrics();
        RefreshRows();
    }

    /// <summary>The user opened or collapsed the panel.</summary>
    public event EventHandler? OpenChanged;

    /// <summary>A row was double-clicked or entered: its file, to be added like Add images.</summary>
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

            // From the current width, whatever DPI scaling it got: the ratio of the two logical widths.
            Width = value ? Width * OpenWidth / StripWidth : Math.Max(1, Width * StripWidth / OpenWidth);
            ResumeLayout();
        }
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
            _heartFont.Dispose();
            _title.Font.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    /// <summary>The rows sized to the font: the status and caption lines, the list's rows, the invitation.</summary>
    private void ApplyMetrics()
    {
        int line = Font.Height + LogicalToDeviceUnits(4);
        _content.RowStyles[2].Height = line;
        _content.RowStyles[3].Height = line;
        _list.ItemHeight = Font.Height + LogicalToDeviceUnits(6);
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
        RefreshRows();
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
            RefreshRows();
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
    /// The list as the search box stands: every favorite while it is blank, else the best matches of
    /// its words; without a base folder, a typed search shows the invitation instead.
    /// </summary>
    private void RefreshRows()
    {
        string[] words = FileSearch.Words(_search.Text);
        bool inviting = words.Length > 0 && _baseFolder is null;
        _invite.Visible = inviting;
        _list.Visible = !inviting;
        _list.BeginUpdate();
        _list.Items.Clear();
        if (words.Length == 0)
        {
            foreach (string path in _favorites.Newest)
            {
                _list.Items.Add(new Row(path, Path.GetFileName(path)));
            }

            _caption.Text = $"Favorites ({_favorites.Count})";
        }
        else if (_index is null)
        {
            _caption.Text = inviting ? "" : "Waiting for the index…";
        }
        else
        {
            var (best, total) = FileSearch.Search(_index.Entries, words);
            foreach (var entry in best)
            {
                _list.Items.Add(new Row(_index.FullPath(entry), entry.Name));
            }

            _caption.Text = total switch
            {
                0 => "No result",
                1 => "1 result",
                <= FileSearch.Limit => $"{total} results",
                _ => $"{total:N0} results — first {FileSearch.Limit}",
            };
        }

        _list.EndUpdate();
        Hover(-1);
    }

    private Row? RowAt(Point location)
    {
        int index = _list.IndexFromPoint(location);
        return index >= 0 && index < _list.Items.Count ? _list.Items[index] as Row : null;
    }

    private void OnDrawRow(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _list.Items.Count || _list.Items[e.Index] is not Row row)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool favorite = _favorites.Contains(row.FullPath);
        int heart = LogicalToDeviceUnits(HeartZone);
        var heartBounds = new Rectangle(e.Bounds.X, e.Bounds.Y, heart, e.Bounds.Height);
        var heartColor = favorite ? Color.Crimson : selected ? SystemColors.HighlightText : SystemColors.GrayText;
        TextRenderer.DrawText(e.Graphics, favorite ? "♥" : "♡", _heartFont, heartBounds, heartColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        var nameBounds = new Rectangle(e.Bounds.X + heart, e.Bounds.Y, Math.Max(0, e.Bounds.Width - heart - 2), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.Name,
            e.Font ?? Font,
            nameBounds,
            selected ? SystemColors.HighlightText : SystemColors.WindowText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        e.DrawFocusRectangle();
    }

    /// <summary>On the heart: toggles the favorite. Elsewhere: selects the row, a left press arming a drag.</summary>
    private void OnListMouseDown(object? sender, MouseEventArgs e)
    {
        _pressedRow = -1;
        int index = _list.IndexFromPoint(e.Location);
        if (index < 0 || index >= _list.Items.Count)
        {
            return;
        }

        if (e.Button == MouseButtons.Left && e.X < LogicalToDeviceUnits(HeartZone))
        {
            ToggleFavorite(index);
            return;
        }

        _list.SelectedIndex = index;
        if (e.Button == MouseButtons.Left)
        {
            _pressedRow = index;
            _pressedAt = e.Location;
        }
    }

    /// <summary>Past the drag threshold, the pressed row is dragged as a file, like one from the Explorer.</summary>
    private void OnListMouseMove(object? sender, MouseEventArgs e)
    {
        Hover(_list.IndexFromPoint(e.Location));
        if (_pressedRow < 0 || (e.Button & MouseButtons.Left) == 0)
        {
            return;
        }

        var threshold = SystemInformation.DragSize;
        if (Math.Abs(e.X - _pressedAt.X) < threshold.Width / 2 && Math.Abs(e.Y - _pressedAt.Y) < threshold.Height / 2)
        {
            return;
        }

        int index = _pressedRow;
        _pressedRow = -1;
        if (index < _list.Items.Count && _list.Items[index] is Row row && Exists(row))
        {
            _list.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { row.FullPath }), DragDropEffects.Copy);
        }
    }

    private void OnListDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.X >= LogicalToDeviceUnits(HeartZone) && RowAt(e.Location) is { } row)
        {
            Activate(row);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _list.SelectedItem is Row row)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            Activate(row);
        }
    }

    /// <summary>Enter activates the first row; ↓ moves to the list.</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_list.Visible && _list.Items.Count > 0 && _list.Items[0] is Row first)
                {
                    Activate(first);
                }

                break;
            case Keys.Down when _list.Visible && _list.Items.Count > 0:
                e.Handled = true;
                if (_list.SelectedIndex < 0)
                {
                    _list.SelectedIndex = 0;
                }

                _list.Focus();
                break;
        }
    }

    /// <summary>The full path of the hovered row, as its tooltip.</summary>
    private void Hover(int index)
    {
        if (index == _hovered)
        {
            return;
        }

        _hovered = index;
        _toolTip.SetToolTip(_list, index >= 0 && index < _list.Items.Count && _list.Items[index] is Row row ? row.FullPath : null);
    }

    private void Activate(Row row)
    {
        if (Exists(row))
        {
            FileActivated?.Invoke(this, row.FullPath);
        }
    }

    private void ToggleFavorite(int index)
    {
        if (index >= _list.Items.Count || _list.Items[index] is not Row row)
        {
            return;
        }

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
            // The favorites list: the row leaves it — once the list is done with the click.
            BeginInvoke(RefreshRows);
        }
        else
        {
            _list.Invalidate(_list.GetItemRectangle(index));
        }
    }

    private void OpenLocation()
    {
        if (_list.SelectedItem is not Row row || !Exists(row))
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
    /// Whether the row's file is still there. A missing one leaves the index — the file rewritten — and
    /// the favorites, the list refreshed, and the status line says so for a few seconds.
    /// </summary>
    private bool Exists(Row row)
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

        RefreshRows();
        ShowTransient(error is null ? "File not found — removed from the index" : $"File not found — removed from the index, not saved: {error}", error: error is not null);
        return false;
    }

    /// <summary>A row of the list: a favorite, or a search result.</summary>
    private sealed record Row(string FullPath, string Name);
}
