using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// A tile of the explorer's grid: a file — a favorite, a search result, a file of the open folder — or,
/// in the folder view, a folder (<paramref name="IsFolder"/>), with the number of files below it once the
/// index counts them (<paramref name="Count"/>). <paramref name="ContentWord"/>: for a search result found thanks to its content text, the first
/// query word found there, folded. <paramref name="Size"/>: the file's size in bytes when the list
/// knows it — the index's, the folder listing's. <paramref name="Preset"/>: an effect preset of the file,
/// its own tile; <paramref name="DescriptionWord"/>: for a preset a search found by its description, the
/// first query word found there, folded. In the People view (workfiles/20261027-face-detection.md):
/// <paramref name="GroupId"/>, a face group's tile — its best face's file in <paramref name="FullPath"/>,
/// its files counted in <paramref name="Count"/>; <paramref name="Face"/>, the face whose picture the tile
/// shows, cut out of the file; <paramref name="FaceKey"/>, the face the tile stands for — dragged onto a
/// group, removed from one.
/// </summary>
internal sealed record ExplorerRow(string FullPath, string Name, bool IsFolder = false, string? ContentWord = null, long? Size = null, int? Count = null, EffectPreset? Preset = null, string? DescriptionWord = null, string? GroupId = null, RectangleF? Face = null, string? FaceKey = null)
{
    /// <summary>What tells a tile from the others: a file's presets share its path, and its faces.</summary>
    public string Key => this.GroupId is { } group ? "group:" + group : this.FaceKey ?? this.Preset?.Id ?? this.FullPath;

    /// <summary>A face group's tile: no heart, opened rather than added.</summary>
    public bool IsGroup => this.GroupId is not null;

    /// <summary>A file's tile — its heart, its copies, its drag into a cell — rather than a folder's or a group's.</summary>
    public bool IsFile => !this.IsFolder && !this.IsGroup;

    /// <summary>What its picture is asked for by: its file's thumbnail, or a face cut out of it.</summary>
    public string ThumbnailKey => this.Face is { } face ? FaceCrops.Key(this.FullPath, face) : this.FullPath;

    /// <summary>The tile's tooltip: its path, after a preset's description; a group's name and files.</summary>
    public string Tip => this.IsGroup ? $"{this.Name}\n{(this.Count == 1 ? "1 file" : $"{this.Count:N0} files")}"
        : this.Preset is { Description.Length: > 0 } preset ? $"{preset.Description.Trim()}\n\n{this.FullPath}" : this.FullPath;

    /// <summary>The name with the file count after it, as a Details row shows a folder.</summary>
    public string NameAndCount => this.Count is { } count ? $"{this.Name} ({count:N0})" : this.Name;

    /// <summary>Whether the search found it thanks to its content text: it shows the light bulb.</summary>
    public bool ByContent => this.ContentWord is not null;
}

/// <summary>
/// The file explorer's list: a grid of tiles, one per file — its thumbnail from the Shell's cache,
/// loaded in the background, the heart in a medallion at its corner, the name below — as many per
/// row as fit at the tile size, stretched to fill the row, scrolling vertically. The owner loads the
/// tiles part by part: while more remain, a Loading… slot ends them, and its coming into view asks
/// for the next part. In the <see cref="ExplorerView.Details"/> view, one row per file instead: its
/// thumbnail a quarter of the tile size, its name and size, the excerpt of its content a search found
/// it by, the search's words highlighted; a folder's row, a strip of its first files' thumbnails. See
/// workfiles/20260926-file-explorer.md § Panel, workfiles/20260928-tile-size-slider.md,
/// workfiles/20260927-file-explorer-show-all.md and workfiles/20261009-explorer-details-view.md.
/// </summary>
internal sealed class ThumbnailGrid : ScrollableControl
{
    // In logical pixels: the tile size is the width at which one more tile fits on a row.
    public const int MinTileSize = 100;
    public const int MaxTileSize = 1000;
    public const int DefaultTileSize = 200;
    public const int Gap = 8;
    public const int Inset = 4;
    private const int Medallion = 24;
    private const int MedallionInset = 4;

    // The Details view: its thumbnail's share of the tile size, the smallest its medallions shrink to,
    // the gap between a folder strip's thumbnails, and the box those are loaded at — the largest they
    // are drawn at (half a 250 px thumbnail), so the strip's cache never changes box.
    private const int DetailsThumbnailShare = 4;
    private const int MinDetailsMedallion = 14;
    private const int StripGap = 3;
    private static readonly Size StripBox = new(128, 96);
    private const int StripCached = 1000;

    // The excerpt's highlight: emoji-selector's opaque yellow, behind the search's words.
    private static readonly Color HighlightColor = Color.FromArgb(255, 255, 0);

    // An effect preset's ★ FX badge.
    private static readonly Color PresetBadgeFill = Color.FromArgb(230, 241, 251);
    private static readonly Color PresetBadgeText = Color.FromArgb(12, 68, 124);

    private const TextFormatFlags ExcerptFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    // The light bulb of a tile found by its content: amber, the glyph drawn in one color.
    private static readonly Color BulbColor = Color.FromArgb(214, 150, 0);

    // The curved arrow's handles, as a share of the way they cover: leaving the bulb, arriving on the word.
    private const float CurveHandle = 0.6f;

    // The sizes the thumbnails are asked from the Shell at: the smallest not below the drawn tile width.
    private static readonly int[] Buckets = [256, 512, 1024];

    private readonly ToolTip _toolTip = new();
    private readonly ThumbnailCache _thumbnails;
    private Font _heartFont;
    private Font _nameFont;
    private Font _bulbFont;

    // The Details view: the strips' thumbnails, apart from the rows' — a box of their own, more of them
    // kept — and the folder row each was drawn in, to repaint it when one arrives; the excerpts fitted
    // to a width; the medallions' size and fonts, shrunk with the thumbnail.
    private readonly ThumbnailCache _stripThumbnails;
    private readonly Dictionary<string, string> _stripOwners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Key, int Width), string?> _excerpts = new();

    // The list holds effect presets: every tile gets a second line under its name, for a preset's description.
    private bool _presetLines;

    // The ★ FX badge, drawn for the name font and the resolution it was drawn at.
    private Bitmap? _presetBadge;
    private Font? _presetBadgeFont;
    private float _presetBadgeDpi;
    private ExplorerView _view = ExplorerView.Thumbnails;
    private string[] _highlightWords = [];
    private int _rowW = 1;
    private int _rowH = 1;
    private int _detailsMedallion;
    private Font? _detailsHeartFont;
    private Font? _detailsBulbFont;

    // The tile whose light bulb the pointer is over, its tooltip then the excerpt; -1 for none.
    private int _bulbTip = -1;
    private IReadOnlyList<ExplorerRow> _rows = [];

    // The folders whose name starts with words a sibling shares, by full path: drawn split on their flap.
    private Dictionary<string, SplitName> _splitNames = [];
    private bool _hasMore;
    private bool _moreAsked;
    private int _tileSize = DefaultTileSize;
    private OcrArrowStyle _arrowStyle = OcrArrowStyle.Default;
    private int _perRow = 1;
    private int _tileW = 1;
    private int _tileH = 1;
    private int _wheelRest;
    private bool _wheelWithControl;
    private int _selected = -1;
    private int _hovered = -1;
    private int _pressed = -1;
    private Point _pressedAt;
    private string? _dropTarget;
    private Font? _groupFont;

    public ThumbnailGrid()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = SystemColors.Window;
        AutoScroll = true;
        _thumbnails = new ThumbnailCache(this);
        _thumbnails.Loaded += InvalidateTileOf;
        this._stripThumbnails = new ThumbnailCache(this, StripCached);
        this._stripThumbnails.SetBox(StripBox, Buckets[0]);
        this._stripThumbnails.Loaded += this.InvalidateStripOf;
        (_heartFont, _nameFont) = MakeFonts();
        this._bulbFont = this.MakeBulbFont();
    }

    /// <summary>The heart of a tile was clicked.</summary>
    public event EventHandler<ExplorerRow>? HeartClicked;

    /// <summary>A tile was double-clicked outside its heart, or Enter pressed on the selected one.</summary>
    public event EventHandler<ExplorerRow>? RowActivated;

    /// <summary>A pressed tile was moved past the drag threshold: the owner may start a drag.</summary>
    public event EventHandler<ExplorerRow>? DragRequested;

    /// <summary>The wheel turned over the tiles: larger tiles asked for (+1) or smaller ones (−1), one step per notch.</summary>
    public event EventHandler<int>? SizeStepRequested;

    /// <summary>The Loading… slot came into view: the owner is to give the next tiles, once per <see cref="SetRows"/>.</summary>
    public event EventHandler? MoreRequested;

    /// <summary>Backspace or Alt+↑: the parent folder is asked for.</summary>
    public event EventHandler? UpRequested;

    /// <summary>F2 on the selected tile: its renaming is asked for — a face group's.</summary>
    public event EventHandler<ExplorerRow>? RenameRequested;

    /// <summary>
    /// The tile a drag hovers as its drop target — a face group, in the People view — outlined and
    /// tinted; null for none. See workfiles/20261027-face-detection.md § Decisions.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? DropTargetKey
    {
        get => this._dropTarget;
        set
        {
            if (value == this._dropTarget)
            {
                return;
            }

            foreach (string? key in new[] { this._dropTarget, value })
            {
                int index = key is null ? -1 : this.IndexOfKey(key);
                if (index >= 0)
                {
                    this.Invalidate(this.Scrolled(this.CellBounds(index)));
                }
            }

            this._dropTarget = value;
        }
    }

    /// <summary>Whether a file is a favorite: its heart is drawn full.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, bool> IsFavorite { get; set; } = _ => false;

    /// <summary>The words around the one a search found in a tile's content text, for its light bulb's tooltip; null when unknown.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ExplorerRow, string?> ContentExcerpt { get; set; } = _ => null;

    /// <summary>
    /// Where the OCR recognised the word a search found in a tile's content, in fractions of its image —
    /// the light bulb's arrow points there; null when unknown (a text file, say).
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ExplorerRow, RectangleF?> ContentSpot { get; set; } = _ => null;

    /// <summary>
    /// A tile's content text as words, with where the word a search found stands — the Details view fits
    /// as many around it as its row holds; null when it was not found by its content.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ExplorerRow, (string[] Words, int At)?> ContentWords { get; set; } = _ => null;

    /// <summary>A file's size in bytes for the Details view; null when unknown.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ExplorerRow, long?> SizeOf { get; set; } = row => row.Size;

    /// <summary>The first files of a folder row, as full paths — the given number at most — for its strip in the Details view.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ExplorerRow, int, IReadOnlyList<string>> FolderFiles { get; set; } = (_, _) => [];

    /// <summary>The search's words, folded: highlighted wherever they stand in a Details row's excerpt.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] HighlightWords
    {
        get => this._highlightWords;
        set
        {
            this._highlightWords = value;
            this.Invalidate();
        }
    }

    /// <summary>Tiles, or a row per file; the list is laid out again, the tile that topped the view still in view.</summary>
    [DefaultValue(ExplorerView.Thumbnails)]
    public ExplorerView View
    {
        get => this._view;
        set
        {
            if (value == this._view)
            {
                return;
            }

            int top = this.TopIndex();
            this._view = value;
            this.Relayout(top);
        }
    }

    /// <summary>
    /// Whether thumbnails are being loaded in the background — the file explorer's content extraction
    /// waits meanwhile. Safe to read from any thread.
    /// </summary>
    [Browsable(false)]
    public bool LoadingThumbnails => this._thumbnails.Busy || this._stripThumbnails.Busy;

    /// <summary>The tiles loaded, top-left first; the Loading… slot, when shown, comes after them.</summary>
    [Browsable(false)]
    public IReadOnlyList<ExplorerRow> Rows => _rows;

    /// <summary>
    /// How many tiles the view shows at once: the tiles per row times the rows its height holds, a
    /// partly visible one counted — at the current width and tile size.
    /// </summary>
    [Browsable(false)]
    public int PageSize => _perRow * Math.Max(1, (ClientSize.Height - InsetPx + CellH - 1) / CellH);

    /// <summary>
    /// Shows <paramref name="rows"/>, followed by the Loading… slot when <paramref name="hasMore"/>.
    /// A new list scrolls back to the top, the selection and the thumbnails still to load dropped; a
    /// list that grew or was refreshed (<paramref name="keepPlace"/>) keeps the scroll and the
    /// selected file.
    /// </summary>
    public void SetRows(IReadOnlyList<ExplorerRow> rows, bool hasMore, bool keepPlace)
    {
        string? selected = SelectedRow?.Key;
        int scroll = -AutoScrollPosition.Y;
        _rows = rows;
        this._presetLines = rows.Any(row => row.Preset is not null);
        this._splitNames = SharedPrefix.Of(rows.Where(row => row.IsFolder).Select(row => row.FullPath));
        _hasMore = hasMore;
        _moreAsked = false;
        _pressed = -1;
        _hovered = -1;
        if (keepPlace)
        {
            _selected = selected is null ? -1 : this.IndexOfKey(selected);
            UpdateExtent();
            AutoScrollPosition = new Point(0, scroll);
        }
        else
        {
            _selected = -1;
            _thumbnails.DropPending();
            this._stripThumbnails.DropPending();
            this._stripOwners.Clear();
            AutoScrollPosition = Point.Empty;
            UpdateExtent();
        }

        this._excerpts.Clear();
        _toolTip.SetToolTip(this, null);
        this._bulbTip = -1;
    }

    /// <summary>
    /// The tile size in logical pixels, <see cref="MinTileSize"/> to <see cref="MaxTileSize"/>: the
    /// width at which one more tile fits on a row — the rows hold as many as fit, stretched to fill
    /// the row, never from the number of files.
    /// </summary>
    [DefaultValue(DefaultTileSize)]
    public int TileSize
    {
        get => _tileSize;
        set
        {
            value = Math.Clamp(value, MinTileSize, MaxTileSize);
            if (value == _tileSize)
            {
                return;
            }

            int top = TopIndex();
            _tileSize = value;
            Relayout(top);
        }
    }

    /// <summary>How the light bulb's arrow is drawn; the tiles are painted again when it changes.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public OcrArrowStyle ArrowStyle
    {
        get => this._arrowStyle;
        set
        {
            if (value == this._arrowStyle)
            {
                return;
            }

            this._arrowStyle = value;
            this.Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ExplorerRow? SelectedRow => _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;

    /// <summary>The tile under a client point, its name included; null between tiles.</summary>
    public ExplorerRow? RowAt(Point client)
    {
        int index = IndexAt(client);
        return index >= 0 ? _rows[index] : null;
    }

    /// <summary>Selects the first tile, for the keyboard.</summary>
    public void SelectFirst()
    {
        if (_rows.Count > 0)
        {
            Select(0);
        }
    }

    /// <summary>Selects the tile of a path, scrolled into view; nothing when it is not loaded.</summary>
    public void SelectPath(string path)
    {
        int index = IndexOf(path);
        if (index >= 0)
        {
            Select(index);
        }
    }

    /// <summary>Selects the tile of a key (<see cref="ExplorerRow.Key"/>), scrolled into view; nothing when it is not loaded.</summary>
    public void SelectKey(string key)
    {
        int index = this.IndexOfKey(key);
        if (index >= 0)
        {
            this.Select(index);
        }
    }

    /// <summary>Repaints a tile, e.g. when its heart changed.</summary>
    public void InvalidateRow(ExplorerRow row)
    {
        int index = this.IndexOfKey(row.Key);
        if (index >= 0)
        {
            this.Invalidate(this.Scrolled(this.CellBounds(index)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnails.Dispose();
            this._stripThumbnails.Dispose();
            _toolTip.Dispose();
            _heartFont.Dispose();
            _nameFont.Dispose();
            this._presetBadge?.Dispose();
            this._bulbFont.Dispose();
            this._detailsHeartFont?.Dispose();
            this._detailsBulbFont?.Dispose();
            this._groupFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Relayout(-1);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        int top = TopIndex();
        Relayout(top);
    }

    /// <summary>The width changed — the splitter, the scrollbar coming or going, the DPI: the rows are laid out again.</summary>
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (IsHandleCreated)
        {
            int top = TopIndex();
            Relayout(top);
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        var (heart, name) = (_heartFont, _nameFont);
        (_heartFont, _nameFont) = MakeFonts();
        heart.Dispose();
        name.Dispose();
        var bulb = this._bulbFont;
        this._bulbFont = this.MakeBulbFont();
        bulb.Dispose();
        this._groupFont?.Dispose();
        this._groupFont = null;

        // A Details row is as tall as its two lines of text at least: laid out again, its medallions'
        // fonts made again from the new ones.
        this._detailsMedallion = 0;
        int top = this.TopIndex();
        this.Relayout(top);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
            or Keys.Back or (Keys.Alt | Keys.Up)
        || base.IsInputKey(keyData);

    /// <summary>
    /// The arrows move the selection — up and down by a row of tiles, staying in its column at the
    /// top — the page keys by a view of tiles; Enter activates it. Moving past the last tile loaded
    /// brings the Loading… slot into view, which loads the next ones.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyData is Keys.Back or (Keys.Alt | Keys.Up))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            UpRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.KeyData == Keys.F2 && this.SelectedRow is { } renamed)
        {
            e.Handled = true;
            this.RenameRequested?.Invoke(this, renamed);
            return;
        }

        if (_rows.Count == 0)
        {
            return;
        }

        int last = _rows.Count - 1;
        int wanted;
        switch (e.KeyCode)
        {
            case Keys.Left:
                wanted = Math.Max(0, _selected - 1);
                break;
            case Keys.Right:
                wanted = _selected + 1;
                break;
            case Keys.Up:
                wanted = _selected < 0 ? 0 : Math.Max(_selected % _perRow, _selected - _perRow);
                break;
            case Keys.Down:
                wanted = _selected < 0 ? 0 : _selected + _perRow;
                break;
            case Keys.PageUp:
                wanted = _selected < 0 ? 0 : Math.Max(_selected % _perRow, _selected - PageSize);
                break;
            case Keys.PageDown:
                wanted = _selected < 0 ? 0 : _selected + PageSize;
                break;
            case Keys.Home:
                wanted = 0;
                break;
            case Keys.End:
                wanted = int.MaxValue;
                break;
            case Keys.Enter:
                if (SelectedRow is { } row)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    RowActivated?.Invoke(this, row);
                }

                return;
            default:
                return;
        }

        e.Handled = true;
        Select(Math.Min(last, wanted));
        if (_hasMore && wanted > last)
        {
            EnsureVisible(_rows.Count);
        }
    }

    /// <summary>On the heart: the owner is told. Elsewhere on a tile: selected, a left press arming a drag.</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _pressed = -1;
        int index = IndexAt(e.Location);
        if (index < 0)
        {
            return;
        }

        if (e.Button == MouseButtons.Left && OnHeart(index, e.Location))
        {
            HeartClicked?.Invoke(this, _rows[index]);
            return;
        }

        Select(index);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = index;
            _pressedAt = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Hover(IndexAt(e.Location));
        this.ShowBulbTip(IndexAt(e.Location), e.Location);
        if (_pressed < 0 || (e.Button & MouseButtons.Left) == 0)
        {
            return;
        }

        var threshold = SystemInformation.DragSize;
        if (Math.Abs(e.X - _pressedAt.X) < threshold.Width / 2 && Math.Abs(e.Y - _pressedAt.Y) < threshold.Height / 2)
        {
            return;
        }

        int index = _pressed;
        _pressed = -1;
        if (index < _rows.Count)
        {
            DragRequested?.Invoke(this, _rows[index]);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = -1;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Hover(-1);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int index = IndexAt(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0 && !OnHeart(index, e.Location))
        {
            RowActivated?.Invoke(this, _rows[index]);
        }
    }

    /// <summary>Remembers whether a wheel message came with Control held: the message's own flag, set by Windows with the event.</summary>
    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEWHEEL = 0x020A;
        const int MK_CONTROL = 0x0008;
        if (m.Msg == WM_MOUSEWHEEL)
        {
            _wheelWithControl = ((int)(long)m.WParam & MK_CONTROL) != 0;
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// The wheel over the tiles scrolls the list; with Control held it asks for larger (up) or smaller
    /// (down) tiles instead, one step per notch — the deltas of a free-spinning wheel accumulated —
    /// and the message goes no further.
    /// </summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_wheelWithControl)
        {
            base.OnMouseWheel(e);
            return;
        }

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        int notch = SystemInformation.MouseWheelScrollDelta;
        _wheelRest += e.Delta;
        int steps = _wheelRest / notch;
        _wheelRest -= steps * notch;
        for (int i = 0; i < Math.Abs(steps); i++)
        {
            SizeStepRequested?.Invoke(this, Math.Sign(steps));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var clip = e.ClipRectangle;

        // From the row the clip starts in: a long list loaded far down is not walked from its top.
        int first = Math.Max(0, (clip.Top - AutoScrollPosition.Y - InsetPx) / CellH) * _perRow;
        for (int i = first; i < _rows.Count; i++)
        {
            var cell = Scrolled(CellBounds(i));
            if (cell.Top > clip.Bottom)
            {
                break;
            }

            if (cell.IntersectsWith(clip))
            {
                if (this.Details)
                {
                    this.PaintRow(g, i);
                }
                else
                {
                    PaintTile(g, i);
                }
            }
        }

        if (_hasMore)
        {
            var slot = this.Scrolled(this.Details ? this.CellBounds(_rows.Count) : TileBounds(_rows.Count));
            if (slot.IntersectsWith(clip))
            {
                PaintLoading(g, slot);
            }

            // Painted in view: the next tiles are asked for, out of the paint.
            if (slot.IntersectsWith(ClientRectangle) && !_moreAsked)
            {
                _moreAsked = true;
                BeginInvoke(() => MoreRequested?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    /// <summary>
    /// An effect preset's badge, <c>★ FX</c>, at the thumbnail's top-right corner — the heart has the
    /// top-left one: a pale blue label, its content dark blue. Drawn as shapes measured on their ink —
    /// a star as tall as the letters' capitals, then <c>FX</c> in bold — so the content is centered with
    /// even margins: text boxes count their leading and descent, and a fallback font draws the ★.
    /// </summary>
    private void PaintPresetBadge(Graphics g, Rectangle tile)
    {
        // Drawn once, kept until the name font or the resolution changes.
        if (this._presetBadge is null || !ReferenceEquals(this._presetBadgeFont, this._nameFont) || this._presetBadgeDpi != g.DpiY)
        {
            this._presetBadge?.Dispose();
            this._presetBadge = this.PresetBadge(g.DpiY);
            this._presetBadgeFont = this._nameFont;
            this._presetBadgeDpi = g.DpiY;
        }

        var badge = this._presetBadge;
        int inset = this.LogicalToDeviceUnits(MedallionInset);

        // Copied pixel for pixel: the grid paints with a half-pixel offset and bicubic interpolation.
        var offset = g.PixelOffsetMode;
        var interpolation = g.InterpolationMode;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(badge, new Rectangle(tile.Right - inset - badge.Width, tile.Y + inset, badge.Width, badge.Height));
        g.PixelOffsetMode = offset;
        g.InterpolationMode = interpolation;
    }

    /// <summary>
    /// The ★ FX badge as a bitmap, in pixel-edge coordinates: the capitals of <c>FX</c> scaled to a whole
    /// number of pixels and set on whole rows, the star as tall as them, the content centered with equal
    /// margins left and right, top and bottom, a 1 px border inside the bitmap.
    /// </summary>
    internal Bitmap PresetBadge(float dpi)
    {
        float emSize = this._nameFont.SizeInPoints * dpi / 72;
        using var text = new GraphicsPath();
        text.AddString("FX", this._nameFont.FontFamily, (int)FontStyle.Bold, emSize, PointF.Empty, StringFormat.GenericTypographic);
        var ink = text.GetBounds();
        int height = Math.Max(5, (int)Math.Round(ink.Height));
        float scale = height / ink.Height;
        using (var fit = new Matrix())
        {
            fit.Scale(scale, scale);
            fit.Translate(-ink.X, -ink.Y);
            text.Transform(fit);
        }

        float textWidth = ink.Width * scale;

        // A five-pointed star centered on the letters' middle, as a font centers ★: its tip a little above
        // the capitals, its two lower points level with their base.
        float radius = height * 0.6f;
        float starWidth = 2 * radius * MathF.Sin(2 * MathF.PI / 5);

        // The content on whole pixels: the star from the left margin, the text up to the right one, the
        // gap taking the fraction — equal margins on every side.
        int pad = Math.Max(3, (int)Math.Round(height * 0.5f));
        int content = (int)Math.Round(starWidth + height * 0.4f + textWidth);
        float gap = content - starWidth - textWidth;
        int width = content + 2 * pad;
        int badgeHeight = height + 2 * pad;

        var bitmap = new Bitmap(width, badgeHeight);
        using var g = Graphics.FromImage(bitmap);
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using var fill = new SolidBrush(PresetBadgeFill);
        using var ink2 = new SolidBrush(PresetBadgeText);
        g.FillRectangle(fill, 0, 0, width, badgeHeight);
        g.FillRectangle(ink2, 0, 0, width, 1);
        g.FillRectangle(ink2, 0, badgeHeight - 1, width, 1);
        g.FillRectangle(ink2, 0, 0, 1, badgeHeight);
        g.FillRectangle(ink2, width - 1, 0, 1, badgeHeight);

        float left = pad;
        var star = new PointF[10];
        var center = new PointF(left + starWidth / 2, pad + height / 2f);
        for (int i = 0; i < star.Length; i++)
        {
            float r = i % 2 == 0 ? radius : radius * 0.4f;
            float angle = -MathF.PI / 2 + i * MathF.PI / 5;
            star[i] = new PointF(center.X + r * MathF.Cos(angle), center.Y + r * MathF.Sin(angle));
        }

        using var place = new Matrix();
        place.Translate(left + starWidth + gap, pad);
        text.Transform(place);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPolygon(ink2, star);
        g.FillPath(ink2, text);
        return bitmap;
    }

    /// <summary>The Loading… slot: a tile's box, its checkerboard and the text alone — no thumbnail, heart nor name.</summary>
    private void PaintLoading(Graphics g, Rectangle slot)
    {
        Checkerboard.Fill(g, slot);

        g.DrawRectangle(SystemPens.ControlDark, new Rectangle(slot.X, slot.Y, slot.Width - 1, slot.Height - 1));
        TextRenderer.DrawText(
            g,
            "Loading…",
            Font,
            slot,
            SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private void PaintTile(Graphics g, int index)
    {
        var row = _rows[index];
        bool selected = index == _selected;
        this.PaintPicture(g, index, selected);

        // A face group's name in bold under its picture, its file count on it.
        if (row.IsGroup)
        {
            TextRenderer.DrawText(
                g,
                row.Name,
                this.GroupFont,
                this.Scrolled(this.NameBounds(index)),
                selected ? SystemColors.Highlight : SystemColors.ControlText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return;
        }

        // A folder's name is drawn on the folder: its file count goes below.
        TextRenderer.DrawText(
            g,
            row.IsFolder && row.Count is { } count ? FileCount(count) : row.Name,
            _nameFont,
            Scrolled(NameBounds(index)),
            selected ? SystemColors.Highlight : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // A preset: its description's first line under the file's name, the whole description in the tooltip.
        if (row.Preset is { } preset)
        {
            var name = this.Scrolled(this.NameBounds(index));
            TextRenderer.DrawText(
                g,
                preset.FirstLine,
                this._nameFont,
                new Rectangle(name.X, name.Bottom, name.Width, this.SmallLineH),
                SystemColors.ControlText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>
    /// A Details row: a faint wash while selected or hovered, the thumbnail and its medallions as on a
    /// tile, then the name — the size right-aligned on its line for a file — and below them the excerpt
    /// of a file found by its content, or a folder's strip.
    /// </summary>
    private void PaintRow(Graphics g, int index)
    {
        var row = this._rows[index];
        var cell = this.Scrolled(this.CellBounds(index));
        bool selected = index == this._selected;
        if (selected || index == this._hovered)
        {
            using var wash = new SolidBrush(Color.FromArgb(selected ? 48 : 20, SystemColors.Highlight));
            g.FillRectangle(wash, cell);
        }

        this.PaintPicture(g, index, selected);
        var tile = this.Scrolled(this.TileBounds(index));
        int left = tile.Right + this.GapPx;
        int width = cell.Right - left;
        if (width <= 0)
        {
            return;
        }

        // Without padding, as the excerpt below: the name and the excerpt start on the same column.
        const TextFormatFlags LineFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        var line = new Rectangle(left, cell.Y, width, this.LineH);
        int nameWidth = width;
        if (row.IsFile)
        {
            string size = FileSizes.Text(this.SizeOf(row));
            int sizeWidth = TextRenderer.MeasureText(g, size, this._nameFont, Size.Empty, LineFlags).Width;
            TextRenderer.DrawText(g, size, this._nameFont, line, SystemColors.GrayText, LineFlags | TextFormatFlags.Right);
            nameWidth = Math.Max(0, width - sizeWidth - this.GapPx);
        }

        TextRenderer.DrawText(
            g,
            row.NameAndCount,
            this.Font,
            new Rectangle(left, cell.Y, nameWidth, this.LineH),
            selected ? SystemColors.Highlight : SystemColors.ControlText,
            LineFlags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        var below = Rectangle.FromLTRB(left, cell.Y + this.LineH, cell.Right, cell.Bottom);
        if (row.IsFolder)
        {
            this.PaintStrip(g, row, below);
        }
        else if (row.ByContent || row.DescriptionWord is not null)
        {
            this.PaintExcerpt(g, row, new Rectangle(below.X, below.Y, below.Width, this.SmallLineH));
        }
        else if (row.Preset is { } preset)
        {
            TextRenderer.DrawText(
                g,
                preset.FirstLine,
                this._nameFont,
                new Rectangle(below.X, below.Y, below.Width, this.SmallLineH),
                SystemColors.GrayText,
                LineFlags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// The excerpt of a file found by its content: as many words around the one found as the line holds,
    /// over an opaque yellow behind every word of the search — emoji-selector's highlight, each span
    /// measured with the text's own flags so it lies exactly under its letters.
    /// </summary>
    private void PaintExcerpt(Graphics g, ExplorerRow row, Rectangle line)
    {
        if (this.ExcerptOf(row, line.Width) is not { } text)
        {
            return;
        }

        var font = this._nameFont;
        int y = line.Y + (line.Height - font.Height) / 2;
        var spans = HighlightSpans.Of(text, this._highlightWords);
        if (spans.Count > 0)
        {
            using var highlight = new SolidBrush(HighlightColor);
            foreach (var (start, length) in spans)
            {
                int x = line.X + this.MeasureExcerpt(text[..start]);
                var span = Rectangle.Intersect(new Rectangle(x, y, this.MeasureExcerpt(text.Substring(start, length)), font.Height), line);
                if (!span.IsEmpty)
                {
                    g.FillRectangle(highlight, span);
                }
            }
        }

        TextRenderer.DrawText(g, text, font, new Rectangle(line.X, y, line.Width, font.Height), SystemColors.GrayText, ExcerptFlags | TextFormatFlags.EndEllipsis);
    }

    /// <summary>
    /// The excerpt fitting <paramref name="width"/>: the word found, then words added on each side in
    /// turn while the line holds them — the word staying near the middle while the text goes on both
    /// sides — an ellipsis where it goes on; null when the file was not found by its content. Kept per
    /// width until the list changes.
    /// </summary>
    private string? ExcerptOf(ExplorerRow row, int width)
    {
        var key = (row.Key, width);
        if (this._excerpts.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        string? excerpt = null;
        if (this.ContentWords(row) is var (words, at))
        {
            int last = words.Length - 1;
            string Build(int from, int to) =>
                $"{(from > 0 ? "… " : "")}{string.Join(' ', words, from, to - from + 1)}{(to < last ? " …" : "")}";

            int first = at;
            int end = at;
            bool canLeft = first > 0;
            bool canRight = end < last;
            bool leftTurn = true;
            while (canLeft || canRight)
            {
                bool left = canLeft && (leftTurn || !canRight);
                int from = left ? first - 1 : first;
                int to = left ? end : end + 1;
                if (this.MeasureExcerpt(Build(from, to)) <= width)
                {
                    (first, end) = (from, to);
                }
                else if (left)
                {
                    canLeft = false;
                }
                else
                {
                    canRight = false;
                }

                canLeft &= first > 0;
                canRight &= end < last;
                leftTurn = !left;
            }

            excerpt = Build(first, end);
        }

        this._excerpts[key] = excerpt;
        return excerpt;
    }

    private int MeasureExcerpt(string text) =>
        text.Length == 0 ? 0 : TextRenderer.MeasureText(text, this._nameFont, Size.Empty, ExcerptFlags).Width;

    /// <summary>
    /// A folder row's strip: the thumbnails of its first files — directly in it, in the sort order — each
    /// half the row's thumbnail, side by side as far right as the row goes, as many as fit. A picture of
    /// the folder only: they take no click of their own.
    /// </summary>
    private void PaintStrip(Graphics g, ExplorerRow row, Rectangle area)
    {
        int width = Math.Max(1, this.TileW / 2);
        int height = Math.Min(area.Height, Math.Max(1, this.TileH / 2));
        int gap = this.LogicalToDeviceUnits(StripGap);
        int count = (area.Width + gap) / (width + gap);
        if (count <= 0 || height <= 0)
        {
            return;
        }

        var files = this.FolderFiles(row, count);
        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i];
            this._stripOwners[path] = row.FullPath;
            var box = new Rectangle(area.X + i * (width + gap), area.Y, width, height);
            Checkerboard.Fill(g, box);

            if (this._stripThumbnails.TryGet(path, out var image) && image is not null)
            {
                double scale = Math.Min((double)box.Width / image.Width, (double)box.Height / image.Height);
                int w = Math.Max(1, (int)Math.Round(image.Width * scale));
                int h = Math.Max(1, (int)Math.Round(image.Height * scale));
                g.DrawImage(image, new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h), new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }

            g.DrawRectangle(SystemPens.ControlDark, new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1));
        }
    }

    /// <summary>A strip's thumbnail arrived: the folder row it was drawn in is painted again.</summary>
    private void InvalidateStripOf(string path)
    {
        if (this._stripOwners.TryGetValue(path, out string? folder))
        {
            this.InvalidateTileOf(folder);
        }
    }

    /// <summary>
    /// A tile's picture, in the tile's box or a Details row's thumbnail: the thumbnail — or a drawn
    /// folder — over the checkerboard showing its transparency, a folder's name on its front flap on a
    /// tile, its border, the heart in its medallion, and the light bulb with its arrow for a file found by
    /// its content. A folder sits on the panel's background, without checkerboard nor resting border —
    /// see workfiles/20261022-folder-tile-cleanup.md.
    /// </summary>
    private void PaintPicture(Graphics g, int index, bool selected)
    {
        var row = _rows[index];
        var tile = Scrolled(TileBounds(index));
        if (row.IsGroup)
        {
            this.PaintGroupPicture(g, index, tile, selected);
            return;
        }

        if (!row.IsFolder)
        {
            Checkerboard.Fill(g, tile);
        }

        // Loaded at its bucket's size: scaled to the tile — enlarged when smaller — its proportions kept,
        // centered. A folder Windows has no thumbnail for gets a drawn folder.
        bool known = _thumbnails.TryGet(row.ThumbnailKey, out var image);
        Rectangle? drawn = null;
        Rectangle? flap = null;
        if (known && image is null && row.IsFolder)
        {
            flap = PaintFolderGlyph(g, tile);
        }
        else if (image is not null)
        {
            double scale = Math.Min((double)tile.Width / image.Width, (double)tile.Height / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            var target = new Rectangle(tile.X + (tile.Width - width) / 2, tile.Y + (tile.Height - height) / 2, width, height);
            g.DrawImage(image, target, new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            drawn = target;
            if (row.IsFolder)
            {
                flap = FolderFlap(target);
            }
        }

        if (!this.Details && flap is { } front)
        {
            this.PaintFolderName(g, row, front);
        }

        // A folder at rest has no border: only the hover and the selection outline it.
        if (selected || index == _hovered || !row.IsFolder)
        {
            var border = selected ? SystemColors.Highlight : index == _hovered ? SystemColors.HotTrack : SystemColors.ControlDark;
            using var pen = new Pen(border, selected ? 2 : 1);
            g.DrawRectangle(pen, selected ? Rectangle.Inflate(tile, -1, -1) : new Rectangle(tile.X, tile.Y, tile.Width - 1, tile.Height - 1));
        }

        this.PaintDropTarget(g, row, tile);

        // Favorites never hold folders: a folder tile has no heart.
        if (!row.IsFolder)
        {
            var heart = Scrolled(HeartBounds(index));
            bool favorite = row.Preset is not null || IsFavorite(row.FullPath);
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.White, heart);
            g.DrawEllipse(SystemPens.ControlDark, heart);
            g.SmoothingMode = smoothing;
            TextRenderer.DrawText(
                g,
                favorite ? "♥" : "♡",
                this.HeartFont,
                heart,
                favorite ? Color.Crimson : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        if (row.Preset is not null)
        {
            this.PaintPresetBadge(g, tile);
        }

        // A search result found thanks to its content text: a light bulb in a medallion, under the heart.
        if (row.ByContent)
        {
            var bulb = this.Scrolled(this.BulbBounds(index));
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (drawn is { } picture && this.ContentSpot(row) is { } spot)
            {
                this.PaintArrow(g, bulb, picture, spot);
            }

            g.FillEllipse(Brushes.White, bulb);
            g.DrawEllipse(SystemPens.ControlDark, bulb);
            g.SmoothingMode = smoothing;
            TextRenderer.DrawText(
                g,
                "\U0001F4A1",
                this.BulbFont,
                bulb,
                BulbColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// A face group's picture — a tile meaning "a group", not drawn as a folder: its best face over two
    /// cards stacked behind it, up and to the right, and its file count in a dark label at its bottom
    /// right. Outlined while hovered or selected, as a file; no heart. See
    /// workfiles/20261027-face-detection.md § People View.
    /// </summary>
    private void PaintGroupPicture(Graphics g, int index, Rectangle tile, bool selected)
    {
        var row = this._rows[index];
        int step = Math.Max(2, this.LogicalToDeviceUnits(4));
        var picture = new Rectangle(tile.X, tile.Y + 2 * step, tile.Width - 2 * step, tile.Height - 2 * step);
        for (int card = 2; card >= 1; card--)
        {
            var back = picture with { X = picture.X + card * step, Y = picture.Y - card * step };
            g.FillRectangle(card == 2 ? SystemBrushes.ControlLight : SystemBrushes.Control, back);
            g.DrawRectangle(SystemPens.ControlDark, new Rectangle(back.X, back.Y, back.Width - 1, back.Height - 1));
        }

        g.FillRectangle(SystemBrushes.ControlDark, picture);
        if (this._thumbnails.TryGet(row.ThumbnailKey, out var image) && image is not null)
        {
            // Covering the card, its middle kept: the crop is cut at the tile's own shape already.
            double scale = Math.Max((double)picture.Width / image.Width, (double)picture.Height / image.Height);
            double w = picture.Width / scale;
            double h = picture.Height / scale;
            var source = new RectangleF((float)((image.Width - w) / 2), (float)((image.Height - h) / 2), (float)w, (float)h);
            g.DrawImage(image, picture, source, GraphicsUnit.Pixel);
        }

        var border = selected ? SystemColors.Highlight : index == this._hovered ? SystemColors.HotTrack : SystemColors.ControlDark;
        using (var pen = new Pen(border, selected ? 2 : 1))
        {
            g.DrawRectangle(pen, selected ? Rectangle.Inflate(picture, -1, -1) : new Rectangle(picture.X, picture.Y, picture.Width - 1, picture.Height - 1));
        }

        if (row.Count is { } count)
        {
            string text = FileCount(count);
            const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            var size = TextRenderer.MeasureText(g, text, this._nameFont, Size.Empty, Flags);
            int pad = this.LogicalToDeviceUnits(3);
            int inset = this.LogicalToDeviceUnits(MedallionInset);
            var label = new Rectangle(picture.Right - inset - size.Width - 2 * pad, picture.Bottom - inset - size.Height - pad, size.Width + 2 * pad, size.Height + pad);
            using var fill = new SolidBrush(Color.FromArgb(200, 24, 24, 24));
            g.FillRectangle(fill, label);
            TextRenderer.DrawText(g, text, this._nameFont, label, Color.White, Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        this.PaintDropTarget(g, row, picture);
    }

    /// <summary>The tile a drag would drop onto: a thick highlight outline over a faint tint.</summary>
    private void PaintDropTarget(Graphics g, ExplorerRow row, Rectangle picture)
    {
        if (this._dropTarget is null || row.Key != this._dropTarget)
        {
            return;
        }

        using var tint = new SolidBrush(Color.FromArgb(60, SystemColors.Highlight));
        g.FillRectangle(tint, picture);
        using var pen = new Pen(SystemColors.Highlight, this.LogicalToDeviceUnits(3)) { Alignment = PenAlignment.Inset };
        g.DrawRectangle(pen, picture);
    }

    private Font GroupFont => this._groupFont ??= new Font(this._nameFont, FontStyle.Bold);

    /// <summary>
    /// A manila folder centered in the tile, its tab at the top left, 90 % of the tile at its 4:3 shape;
    /// returns where its name goes, on its front panel.
    /// </summary>
    private static Rectangle PaintFolderGlyph(Graphics g, Rectangle tile)
    {
        int height = Math.Max(8, tile.Height * 9 / 10);
        int width = height * 4 / 3;
        if (width > tile.Width * 9 / 10)
        {
            width = Math.Max(8, tile.Width * 9 / 10);
            height = width * 3 / 4;
        }

        var body = new Rectangle(tile.X + (tile.Width - width) / 2, tile.Y + (tile.Height - height) / 2 + height / 10, width, height - height / 10);
        var tab = new Rectangle(body.X, body.Y - height / 8, width * 2 / 5, height / 8 + 2);
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Color.FromArgb(222, 170, 60)))
        {
            g.FillRectangle(back, tab);
            g.FillRectangle(back, body);
        }

        var panel = new Rectangle(body.X, body.Y + body.Height / 5, body.Width, body.Height - body.Height / 5);
        using (var front = new SolidBrush(Color.FromArgb(247, 205, 95)))
        {
            g.FillRectangle(front, panel);
        }

        g.SmoothingMode = smoothing;
        return Rectangle.Inflate(panel, -panel.Width * 8 / 100, -panel.Height * 5 / 100);
    }

    /// <summary>
    /// The front flap of Windows' folder thumbnail, trimmed to the folder
    /// (<see cref="ShellThumbnail.TryLoadFolder"/>) and drawn in <paramref name="folder"/>: its lower part,
    /// from 58 % to 96 % of its height, 8 % in from each side.
    /// </summary>
    private static Rectangle FolderFlap(Rectangle folder) => Rectangle.FromLTRB(
        folder.X + folder.Width * 8 / 100,
        folder.Y + folder.Height * 58 / 100,
        folder.Right - folder.Width * 8 / 100,
        folder.Y + folder.Height * 96 / 100);

    /// <summary>
    /// A folder's name in bold on its front <paramref name="flap"/>: centered, two lines at most, cut with
    /// "…", a third of the flap's height — never under the tile names' size — almost black over a soft
    /// white shadow. A prefix shared with a sibling (<see cref="SharedPrefix"/>) goes small on the flap's
    /// first line, followed by "›", the rest of the name large below it. See
    /// workfiles/20261022-explorer-transparency-checkerboard.md and workfiles/20261022-folder-tile-cleanup.md.
    /// </summary>
    private void PaintFolderName(Graphics g, ExplorerRow row, Rectangle flap)
    {
        if (flap.Width <= 0 || flap.Height <= 0 || row.Name.Length == 0)
        {
            return;
        }

        float minimum = this._nameFont.SizeInPoints * g.DpiY / 72f;
        float em = Math.Max(minimum, flap.Height / 3f);
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var format = new StringFormat(StringFormatFlags.LineLimit)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        using var path = new GraphicsPath();
        if (!this._splitNames.TryGetValue(row.FullPath, out var split))
        {
            path.AddString(row.Name, this._nameFont.FontFamily, (int)FontStyle.Bold, em, flap, format);
            PaintShadowed(g, path, em);
            g.SmoothingMode = smoothing;
            return;
        }

        // The rest laid out in the flap below the small line, then the two moved to sit together — the
        // small line right above the rest's letters — centered on the flap.
        float small = Math.Max(minimum, flap.Height / 7f);
        int line = Math.Min(flap.Height / 2, (int)Math.Ceiling(small * 1.3f));
        float gap = small * 0.15f;
        path.AddString(split.Rest, this._nameFont.FontFamily, (int)FontStyle.Bold, em, Rectangle.FromLTRB(flap.X, flap.Y + line, flap.Right, flap.Bottom), format);
        var rest = path.GetBounds();
        float block = line + gap + rest.Height;
        float top = flap.Y + Math.Max(0f, (flap.Height - block) / 2f);
        using (var shift = new Matrix())
        {
            shift.Translate(0f, top + line + gap - rest.Top);
            path.Transform(shift);
        }

        using var single = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.LineLimit)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        using var prefix = new GraphicsPath();
        prefix.AddString($"{split.Prefix} ›", this._nameFont.FontFamily, (int)FontStyle.Bold, small, new RectangleF(flap.X, top, flap.Width, line), single);
        PaintShadowed(g, prefix, small);
        PaintShadowed(g, path, em);
        g.SmoothingMode = smoothing;
    }

    /// <summary>
    /// Text drawn as a <paramref name="path"/>, almost black over a soft white shadow offset down-right —
    /// a sharp core with a fainter spread around it, GDI+ having no blur — both scaled to its
    /// <paramref name="em"/> size.
    /// </summary>
    private static void PaintShadowed(Graphics g, GraphicsPath path, float em)
    {
        float offset = Math.Max(1f, em / 16f);
        using (var shadow = (GraphicsPath)path.Clone())
        {
            using var shift = new Matrix();
            shift.Translate(offset, offset);
            shadow.Transform(shift);
            using (var spread = new Pen(Color.FromArgb(70, Color.White), offset * 2f) { LineJoin = LineJoin.Round })
            {
                g.DrawPath(spread, shadow);
            }

            using var core = new SolidBrush(Color.FromArgb(180, Color.White));
            g.FillPath(core, shadow);
        }

        using var text = new SolidBrush(Color.FromArgb(32, 32, 32));
        g.FillPath(text, path);
    }

    private static string FileCount(int count) => count == 1 ? "1 file" : $"{count:N0} files";

    // The drawn tile, from LayoutRows(): the row's width shared by as many tiles as fit at the tile size, at 4:3.
    private int TileW => _tileW;

    private int TileH => _tileH;

    private int GapPx => LogicalToDeviceUnits(Gap);

    private int InsetPx => LogicalToDeviceUnits(Inset);

    private int NameH => Font.Height + LogicalToDeviceUnits(4);

    private bool Details => this._view == ExplorerView.Details;

    // A Details row's lines: the name's, then the excerpt's in the smaller font.
    private int LineH => this.NameH;

    private int SmallLineH => this._nameFont.Height + this.LogicalToDeviceUnits(4);

    // Between two rows of tiles, the gap; between two Details rows, the inset.
    private int RowGapPx => this.Details ? this.InsetPx : this.GapPx;

    private int CellW => TileW + GapPx;

    private int CellH => this.Details ? this._rowH + this.RowGapPx : TileH + this.CaptionH + GapPx;

    // Under a tile: its name, and a preset's description line when the list holds presets.
    private int CaptionH => this.NameH + (this._presetLines ? this.SmallLineH : 0);

    private Font HeartFont => this.Details && this._detailsHeartFont is { } font ? font : this._heartFont;

    private Font BulbFont => this.Details && this._detailsBulbFont is { } font ? font : this._bulbFont;

    // Content coordinates: unscrolled. A Details row's thumbnail is centered in its height.
    private Rectangle TileBounds(int index) =>
        this.Details
            ? new Rectangle(this.InsetPx, this.InsetPx + index * this.CellH + (this._rowH - this.TileH) / 2, this.TileW, this.TileH)
            : new(InsetPx + index % _perRow * CellW, InsetPx + index / _perRow * CellH, TileW, TileH);

    private Rectangle NameBounds(int index)
    {
        var tile = TileBounds(index);
        return new Rectangle(tile.X, tile.Bottom, tile.Width, NameH);
    }

    private Rectangle CellBounds(int index)
    {
        if (this.Details)
        {
            return new Rectangle(this.InsetPx, this.InsetPx + index * this.CellH, this._rowW, this._rowH);
        }

        var tile = TileBounds(index);
        return new Rectangle(tile.X, tile.Y, tile.Width, TileH + this.CaptionH);
    }

    private Rectangle HeartBounds(int index)
    {
        var tile = TileBounds(index);
        int size = this.Details ? this._detailsMedallion : LogicalToDeviceUnits(Medallion);
        int inset = LogicalToDeviceUnits(MedallionInset);
        return new Rectangle(tile.X + inset, tile.Y + inset, size, size);
    }

    /// <summary>The light bulb's medallion: the heart's size, right under it.</summary>
    private Rectangle BulbBounds(int index)
    {
        var heart = this.HeartBounds(index);
        int inset = this.LogicalToDeviceUnits(MedallionInset);
        return new Rectangle(heart.X, heart.Bottom + inset, heart.Width, heart.Height);
    }

    private bool OnBulb(int index, Point client) => index >= 0 && this._rows[index].ByContent && this.BulbBounds(index).Contains(this.Unscrolled(client));

    /// <summary>
    /// The pointer over a tile's light bulb: its tooltip says the tile was found in its content, with the
    /// words around; elsewhere on the tile, the tile's own tooltip.
    /// </summary>
    private void ShowBulbTip(int index, Point client)
    {
        int bulb = this.OnBulb(index, client) ? index : -1;
        if (bulb == this._bulbTip)
        {
            return;
        }

        this._bulbTip = bulb;
        if (bulb >= 0)
        {
            string? excerpt = this.ContentExcerpt(this._rows[bulb]);
            this._toolTip.SetToolTip(this, excerpt is null ? "Found in its content" : $"Found in its content: {excerpt}");
        }
        else
        {
            this._toolTip.SetToolTip(this, index >= 0 ? this._rows[index].Tip : null);
        }
    }

    /// <summary>
    /// The light bulb's arrow, from the bulb to the word recognised in the drawn thumbnail: to the
    /// middle of its top edge, or of its bottom edge when the word sits above the bulb; drawn in the
    /// <see cref="ArrowStyle"/> — curved or straight, its stroke's thickness, its color — over a dark
    /// halo 1 px wider on each side, legible on any picture. None at thickness 0, nor when the word is
    /// under the bulb. See workfiles/20261007-ocr-result-style.md § Drawing.
    /// </summary>
    private void PaintArrow(Graphics g, Rectangle bulb, Rectangle picture, RectangleF spot)
    {
        var style = this._arrowStyle;
        if (style.Thickness <= 0)
        {
            return;
        }

        var word = new RectangleF(
            picture.X + spot.X * picture.Width,
            picture.Y + spot.Y * picture.Height,
            spot.Width * picture.Width,
            spot.Height * picture.Height);
        var from = new PointF(bulb.X + bulb.Width / 2f, bulb.Y + bulb.Height / 2f);
        bool down = word.Y + word.Height / 2f >= from.Y;
        var to = new PointF(word.X + word.Width / 2f, down ? word.Y : word.Bottom);
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        float radius = bulb.Width / 2f;
        float head = this.LogicalToDeviceUnits(style.Head);
        if (length <= radius + head)
        {
            return;
        }

        var arrow = style.Curved ? CurvedArrow(from, radius, to, down, head) : StraightArrow(from, radius, to, length, head);
        if (arrow is not { } drawn)
        {
            return;
        }

        var (stroke, tip) = drawn;
        using (stroke)
        {
            // The outline stands 1 px out all around: along the stroke, a pen 2 px wider than it; around
            // the head, a 2 px pen on its edges, its inner half under the fill.
            float width = this.LogicalToDeviceUnits(style.Thickness);
            int outline = this.LogicalToDeviceUnits(1);
            var haloColor = Color.FromArgb(170, 0, 0, 0);
            using (var halo = new Pen(haloColor, width + 2 * outline))
            {
                g.DrawPath(halo, stroke);
            }

            using (var halo = new Pen(haloColor, 2 * outline) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(halo, tip);
            }

            using (var pen = new Pen(style.Color, width))
            {
                g.DrawPath(pen, stroke);
            }

            using var fill = new SolidBrush(style.Color);
            g.FillPolygon(fill, tip);
        }
    }

    /// <summary>
    /// The straight arrow: its stroke from the bulb's edge along the line to the word, up to the base
    /// of its head, the head along the line, <paramref name="head"/> long and wide.
    /// </summary>
    private static (GraphicsPath Stroke, PointF[] Tip) StraightArrow(PointF from, float radius, PointF to, float length, float head)
    {
        // Unit vectors along the arrow and across it.
        float ux = (to.X - from.X) / length;
        float uy = (to.Y - from.Y) / length;
        var start = new PointF(from.X + ux * radius, from.Y + uy * radius);
        var neck = new PointF(to.X - ux * head, to.Y - uy * head);
        float half = head / 2f;
        var stroke = new GraphicsPath();
        stroke.AddLine(start, neck);
        return (stroke, [to, new PointF(neck.X - uy * half, neck.Y + ux * half), new PointF(neck.X + uy * half, neck.Y - ux * half)]);
    }

    /// <summary>
    /// The curved arrow, arriving vertically: its head upright — pointing straight down onto the word's
    /// top edge, or straight up onto its bottom edge — and its stroke an arc leaving the bulb's edge
    /// horizontally, towards the word's side, and entering the head through the middle of its flat
    /// base, vertically, from outside: its last handle never shorter than 1.5 heads, so a word level
    /// with the bulb gets an arc swinging past the base and back into it. A word right under (or over)
    /// the bulb gets a stroke leaving it vertically; null when that word overlaps the bulb.
    /// </summary>
    private static (GraphicsPath Stroke, PointF[] Tip)? CurvedArrow(PointF from, float radius, PointF to, bool down, float head)
    {
        float side = down ? 1f : -1f;
        var neck = new PointF(to.X, to.Y - side * head);
        float half = head / 2f;
        PointF[] tip = [to, new PointF(neck.X - half, neck.Y), new PointF(neck.X + half, neck.Y)];
        float across = neck.X - from.X;
        PointF start;
        PointF leave;
        if (MathF.Abs(across) <= radius)
        {
            start = new PointF(from.X, from.Y + side * radius);
            if (side * (neck.Y - start.Y) <= 0)
            {
                return null;
            }

            leave = new PointF(start.X, start.Y + (neck.Y - start.Y) * CurveHandle);
        }
        else
        {
            start = new PointF(from.X + MathF.Sign(across) * radius, from.Y);
            leave = new PointF(start.X + (neck.X - start.X) * CurveHandle, start.Y);
        }

        float arrive = MathF.Max(MathF.Abs(neck.Y - start.Y) * CurveHandle, head * 1.5f);
        var stroke = new GraphicsPath();
        stroke.AddBezier(start, leave, new PointF(neck.X, neck.Y - side * arrive), neck);
        return (stroke, tip);
    }

    private Font MakeBulbFont() => new("Segoe UI Emoji", this.Font.SizeInPoints);

    /// <summary>Content coordinates to client ones.</summary>
    private Rectangle Scrolled(Rectangle content)
    {
        content.Offset(AutoScrollPosition);
        return content;
    }

    private Point Unscrolled(Point client) => new(client.X - AutoScrollPosition.X, client.Y - AutoScrollPosition.Y);

    private int IndexAt(Point client)
    {
        var p = Unscrolled(client);
        int x = p.X - InsetPx;
        int y = p.Y - InsetPx;
        if (x < 0 || y < 0)
        {
            return -1;
        }

        if (this.Details)
        {
            int rowIndex = y / this.CellH;
            return x < this._rowW && y % this.CellH < this._rowH && rowIndex < this._rows.Count ? rowIndex : -1;
        }

        int column = x / CellW;
        if (column >= _perRow || x % CellW >= TileW || y % CellH >= TileH + this.CaptionH)
        {
            return -1;
        }

        int index = y / CellH * _perRow + column;
        return index < _rows.Count ? index : -1;
    }

    private bool OnHeart(int index, Point client) => index >= 0 && _rows[index].IsFile && HeartBounds(index).Contains(Unscrolled(client));

    private int IndexOf(string path)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (string.Equals(_rows[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private int IndexOfKey(string key)
    {
        for (int i = 0; i < this._rows.Count; i++)
        {
            if (string.Equals(this._rows[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // Every tile of the thumbnail: a file's presets show its thumbnail too.
    private void InvalidateTileOf(string key)
    {
        for (int i = 0; i < this._rows.Count; i++)
        {
            if (string.Equals(this._rows[i].ThumbnailKey, key, StringComparison.OrdinalIgnoreCase))
            {
                this.Invalidate(this.Scrolled(this.CellBounds(i)));
            }
        }
    }

    private void UpdateExtent()
    {
        int slots = _rows.Count + (_hasMore ? 1 : 0);
        int rows = (slots + _perRow - 1) / _perRow;
        AutoScrollMinSize = new Size(0, rows == 0 ? 0 : InsetPx * 2 + rows * CellH - this.RowGapPx);
        Invalidate();
    }

    /// <summary>
    /// The rows from the client width and the tile size: as many tiles per row as fit, the row's width
    /// shared between them — one at least, and never from the number of files, so a lone tile gets
    /// the width it would have in a full row. In the Details view, one file per row, its thumbnail a
    /// quarter of the tile size — half the row at most — the row as tall as the thumbnail, its two
    /// lines of text — the name over the excerpt or a folder's strip of half thumbnails — and its two
    /// medallions, shrunk with the thumbnail down to <see cref="MinDetailsMedallion"/>.
    /// </summary>
    private void LayoutRows()
    {
        int available = ClientSize.Width - InsetPx * 2;
        int size = LogicalToDeviceUnits(_tileSize);
        this._excerpts.Clear();
        if (this.Details)
        {
            this._perRow = 1;
            this._rowW = Math.Max(1, available);
            this._tileW = Math.Clamp(size / DetailsThumbnailShare, 1, Math.Max(1, available / 2));
            this._tileH = Math.Max(1, this._tileW * 3 / 4);
            int inset = this.LogicalToDeviceUnits(MedallionInset);
            int medallion = Math.Clamp((this._tileH - 3 * inset) / 2, this.LogicalToDeviceUnits(MinDetailsMedallion), this.LogicalToDeviceUnits(Medallion));
            this.UpdateDetailsFonts(medallion);
            this._rowH = Math.Max(Math.Max(this._tileH, this.LineH + Math.Max(this.SmallLineH, this._tileH / 2)), 2 * medallion + 3 * inset);
            return;
        }

        _perRow = Math.Max(1, (available + GapPx) / (size + GapPx));
        _tileW = Math.Max(1, (available - (_perRow - 1) * GapPx) / _perRow);
        _tileH = _tileW * 3 / 4;
    }

    /// <summary>The Details view's medallions at <paramref name="medallion"/> px, their glyphs' fonts sized to them as a tile's are to its own.</summary>
    private void UpdateDetailsFonts(int medallion)
    {
        if (medallion == this._detailsMedallion && this._detailsHeartFont is not null)
        {
            return;
        }

        this._detailsMedallion = medallion;
        var (heart, bulb) = (this._detailsHeartFont, this._detailsBulbFont);
        float scale = (float)medallion / this.LogicalToDeviceUnits(Medallion);
        this._detailsHeartFont = new Font(this._heartFont.FontFamily, Math.Max(4f, this._heartFont.SizeInPoints * scale));
        this._detailsBulbFont = new Font(this._bulbFont.FontFamily, Math.Max(4f, this._bulbFont.SizeInPoints * scale));
        heart?.Dispose();
        bulb?.Dispose();
    }

    /// <summary>
    /// The box the thumbnails are fitted to and the size asked from the Shell: the tile's bucket, the
    /// smallest not below its width, so a drag reloads nothing within one.
    /// </summary>
    private void UpdateBox()
    {
        int side = Buckets[^1];
        foreach (int bucket in Buckets)
        {
            if (_tileW <= bucket)
            {
                side = bucket;
                break;
            }
        }

        _thumbnails.SetBox(new Size(side, side * 3 / 4), side);
    }

    /// <summary>The first tile of the row at the top of the view; -1 with no tile.</summary>
    private int TopIndex()
    {
        if (_rows.Count == 0)
        {
            return -1;
        }

        int row = Math.Max(0, (-AutoScrollPosition.Y - InsetPx) / CellH);
        return Math.Min(_rows.Count - 1, row * _perRow);
    }

    /// <summary>The size or the width changed: the rows, the box and the extent follow, the tile that topped the view (<paramref name="top"/>) still in view.</summary>
    private void Relayout(int top)
    {
        LayoutRows();
        UpdateBox();
        UpdateExtent();
        if (top > 0)
        {
            AutoScrollPosition = new Point(0, Math.Max(0, this.CellBounds(top).Y - InsetPx));
        }
    }

    private (Font Heart, Font Name) MakeFonts() =>
        (new Font("Segoe UI Symbol", Font.SizeInPoints + 1f), new Font(Font.FontFamily, Math.Max(6f, Font.SizeInPoints - 1f), Font.Style));

    private void Hover(int index)
    {
        if (index == _hovered)
        {
            return;
        }

        int previous = _hovered;
        _hovered = index;
        if (previous >= 0 && previous < _rows.Count)
        {
            Invalidate(Scrolled(CellBounds(previous)));
        }

        if (index >= 0)
        {
            Invalidate(Scrolled(CellBounds(index)));
        }

        _toolTip.SetToolTip(this, index >= 0 ? _rows[index].Tip : null);
        this._bulbTip = -1;
    }

    private void Select(int index)
    {
        if (index == _selected)
        {
            return;
        }

        int previous = _selected;
        _selected = index;
        if (previous >= 0 && previous < _rows.Count)
        {
            Invalidate(Scrolled(CellBounds(previous)));
        }

        if (index >= 0)
        {
            Invalidate(Scrolled(CellBounds(index)));
            EnsureVisible(index);
        }
    }

    private void EnsureVisible(int index)
    {
        var cell = CellBounds(index);
        int top = -AutoScrollPosition.Y;
        if (cell.Top < top)
        {
            AutoScrollPosition = new Point(0, Math.Max(0, cell.Top - InsetPx));
        }
        else if (cell.Bottom > top + ClientSize.Height)
        {
            AutoScrollPosition = new Point(0, cell.Bottom + InsetPx - ClientSize.Height);
        }
    }

    /// <summary>
    /// The thumbnails, keyed by path: loaded one at a time on a worker, from the Shell's cache, scaled
    /// to the tile's box and handed back on the UI thread; the least recently used dropped past a
    /// cap — 200, fewer for large boxes, within a byte budget. A thumbnail is asked for by painting a
    /// tile that has none yet.
    /// </summary>
    private sealed class ThumbnailCache : IDisposable
    {
        private const int MaxCached = 200;
        private const int MinCached = 16;
        private const long Budget = 64L << 20;

        private readonly Control _owner;
        private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<Entry> _order = new();
        private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _queue = new();
        private readonly object _lock = new();
        private readonly int _maxCached;
        private Size _box;
        private int _side;
        private int _capacity = MaxCached;
        private bool _working;
        private volatile bool _disposed;

        /// <param name="maxCached">The most thumbnails kept, the byte budget allowing — more for the Details view's small strip thumbnails.</param>
        public ThumbnailCache(Control owner, int maxCached = MaxCached)
        {
            _owner = owner;
            this._maxCached = maxCached;
            this._capacity = maxCached;
        }

        /// <summary>A thumbnail arrived (or none exists) for the path; on the UI thread.</summary>
        public event Action<string>? Loaded;

        /// <summary>Whether thumbnails are being loaded, or wait to be; safe from any thread.</summary>
        public bool Busy
        {
            get
            {
                lock (this._lock)
                {
                    return this._working;
                }
            }
        }

        /// <summary>
        /// The box the thumbnails are scaled to, and the size asked from the Shell; changing it drops
        /// them all, and sets the cap: as many 32-bit bitmaps of the box as the budget holds, within bounds.
        /// </summary>
        public void SetBox(Size box, int side)
        {
            if (box == _box && side == _side)
            {
                return;
            }

            _box = box;
            _side = side;
            _capacity = (int)Math.Clamp(Budget / Math.Max(1L, (long)box.Width * box.Height * 4), MinCached, this._maxCached);
            Clear();
        }

        /// <summary>
        /// True when the path is known — <paramref name="image"/> then being its thumbnail, or null
        /// when it has none; false when it is being asked for.
        /// </summary>
        public bool TryGet(string path, out Bitmap? image)
        {
            if (_entries.TryGetValue(path, out var node))
            {
                _order.Remove(node);
                _order.AddLast(node);
                image = node.Value.Image;
                return true;
            }

            image = null;
            Request(path);
            return false;
        }

        /// <summary>Forgets the thumbnails still to load; the one in flight still lands.</summary>
        public void DropPending()
        {
            lock (_lock)
            {
                _queue.Clear();
                _pending.Clear();
            }
        }

        public void Clear()
        {
            DropPending();
            foreach (var entry in _order)
            {
                entry.Image?.Dispose();
            }

            _order.Clear();
            _entries.Clear();
        }

        public void Dispose()
        {
            _disposed = true;
            Clear();
        }

        private void Request(string path)
        {
            lock (_lock)
            {
                if (_disposed || !_pending.Add(path))
                {
                    return;
                }

                _queue.Enqueue(path);
                if (_working)
                {
                    return;
                }

                _working = true;
            }

            Task.Run(Work);
        }

        private void Work()
        {
            while (true)
            {
                string path;
                Size box;
                int side;
                lock (_lock)
                {
                    if (_disposed || _queue.Count == 0)
                    {
                        _working = false;

                        // The faces' last file decoded: its memory given back once the tiles are loaded.
                        FaceCrops.Release();
                        return;
                    }

                    path = _queue.Dequeue();
                    box = _box;
                    side = _side;
                }

                Bitmap? image = null;
                try
                {
                    // A face cut out of its file, read from the full image; a folder at its own size, the
                    // only one Windows always gives with its transparency.
                    if (FaceCrops.TryParse(path, out string file, out var face))
                    {
                        image = FaceCrops.Load(file, face, box);
                    }
                    else
                    {
                        using var thumbnail = Directory.Exists(path) ? ShellThumbnail.TryLoadFolder(path) : ShellThumbnail.TryLoad(path, side);
                        image = thumbnail is null ? null : Fit(thumbnail, box);
                    }
                }
                catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException)
                {
                }

                bool wanted;
                lock (_lock)
                {
                    wanted = _pending.Remove(path) && !_disposed;
                }

                if (!wanted)
                {
                    image?.Dispose();
                    continue;
                }

                try
                {
                    _owner.BeginInvoke(() => Store(path, image));
                }
                catch (InvalidOperationException)
                {
                    image?.Dispose();
                }
            }
        }

        private void Store(string path, Bitmap? image)
        {
            if (_disposed)
            {
                image?.Dispose();
                return;
            }

            if (_entries.TryGetValue(path, out var existing))
            {
                _order.Remove(existing);
                existing.Value.Image?.Dispose();
            }

            _entries[path] = _order.AddLast(new Entry(path, image));
            while (_order.Count > _capacity)
            {
                var oldest = _order.First!.Value;
                _order.RemoveFirst();
                _entries.Remove(oldest.Path);
                oldest.Image?.Dispose();
            }

            Loaded?.Invoke(path);
        }

        /// <summary>The thumbnail within the box, its proportions kept; a smaller one is copied as is.</summary>
        private static Bitmap Fit(Bitmap source, Size box)
        {
            double scale = Math.Min((double)box.Width / source.Width, (double)box.Height / source.Height);
            if (scale >= 1 || box.IsEmpty)
            {
                return new Bitmap(source);
            }

            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var fitted = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(fitted);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, width, height), new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            return fitted;
        }

        private sealed record Entry(string Path, Bitmap? Image);
    }
}
