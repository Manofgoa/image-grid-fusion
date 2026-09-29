using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>A file shown by the explorer's grid: a favorite, or a search result.</summary>
internal sealed record ExplorerRow(string FullPath, string Name);

/// <summary>
/// The file explorer's list: a grid of tiles, one per file — its thumbnail from the Shell's cache,
/// loaded in the background, the heart in a medallion at its corner, the name below — as many per
/// row as fit at the tile size, stretched to fill the row, scrolling vertically. The owner loads the
/// tiles part by part: while more remain, a Loading… slot ends them, and its coming into view asks
/// for the next part. See workfiles/20260926-file-explorer.md § Panel,
/// workfiles/20260928-tile-size-slider.md and workfiles/20260927-file-explorer-show-all.md.
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

    // The sizes the thumbnails are asked from the Shell at: the smallest not below the drawn tile width.
    private static readonly int[] Buckets = [256, 512, 1024];

    private readonly ToolTip _toolTip = new();
    private readonly ThumbnailCache _thumbnails;
    private Font _heartFont;
    private Font _nameFont;
    private IReadOnlyList<ExplorerRow> _rows = [];
    private bool _hasMore;
    private bool _moreAsked;
    private int _tileSize = DefaultTileSize;
    private int _perRow = 1;
    private int _tileW = 1;
    private int _tileH = 1;
    private int _wheelRest;
    private bool _wheelWithControl;
    private int _selected = -1;
    private int _hovered = -1;
    private int _pressed = -1;
    private Point _pressedAt;

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
        (_heartFont, _nameFont) = MakeFonts();
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

    /// <summary>Whether a file is a favorite: its heart is drawn full.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, bool> IsFavorite { get; set; } = _ => false;

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
        string? selected = SelectedRow?.FullPath;
        int scroll = -AutoScrollPosition.Y;
        _rows = rows;
        _hasMore = hasMore;
        _moreAsked = false;
        _pressed = -1;
        _hovered = -1;
        if (keepPlace)
        {
            _selected = selected is null ? -1 : IndexOf(selected);
            UpdateExtent();
            AutoScrollPosition = new Point(0, scroll);
        }
        else
        {
            _selected = -1;
            _thumbnails.DropPending();
            AutoScrollPosition = Point.Empty;
            UpdateExtent();
        }

        _toolTip.SetToolTip(this, null);
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

    /// <summary>Repaints a tile, e.g. when its heart changed.</summary>
    public void InvalidateRow(ExplorerRow row) => InvalidateTileOf(row.FullPath);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnails.Dispose();
            _toolTip.Dispose();
            _heartFont.Dispose();
            _nameFont.Dispose();
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
        UpdateExtent();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
        || base.IsInputKey(keyData);

    /// <summary>
    /// The arrows move the selection — up and down by a row of tiles, staying in its column at the
    /// top — the page keys by a view of tiles; Enter activates it. Moving past the last tile loaded
    /// brings the Loading… slot into view, which loads the next ones.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
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
                PaintTile(g, i);
            }
        }

        if (_hasMore)
        {
            var slot = Scrolled(TileBounds(_rows.Count));
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

    /// <summary>The Loading… slot: a tile's box with the text alone — no thumbnail, heart nor name.</summary>
    private void PaintLoading(Graphics g, Rectangle slot)
    {
        using (var fill = new SolidBrush(SystemColors.ControlLight))
        {
            g.FillRectangle(fill, slot);
        }

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
        var tile = Scrolled(TileBounds(index));
        bool selected = index == _selected;
        using (var fill = new SolidBrush(SystemColors.ControlLight))
        {
            g.FillRectangle(fill, tile);
        }

        // Loaded at its bucket's size: scaled to the tile — enlarged when smaller — its proportions kept, centered.
        if (_thumbnails.TryGet(row.FullPath, out var image) && image is not null)
        {
            double scale = Math.Min((double)tile.Width / image.Width, (double)tile.Height / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            var target = new Rectangle(tile.X + (tile.Width - width) / 2, tile.Y + (tile.Height - height) / 2, width, height);
            g.DrawImage(image, target, new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
        }

        var border = selected ? SystemColors.Highlight : index == _hovered ? SystemColors.HotTrack : SystemColors.ControlDark;
        using (var pen = new Pen(border, selected ? 2 : 1))
        {
            g.DrawRectangle(pen, selected ? Rectangle.Inflate(tile, -1, -1) : new Rectangle(tile.X, tile.Y, tile.Width - 1, tile.Height - 1));
        }

        var heart = Scrolled(HeartBounds(index));
        bool favorite = IsFavorite(row.FullPath);
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(Brushes.White, heart);
        g.DrawEllipse(SystemPens.ControlDark, heart);
        g.SmoothingMode = smoothing;
        TextRenderer.DrawText(
            g,
            favorite ? "♥" : "♡",
            _heartFont,
            heart,
            favorite ? Color.Crimson : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(
            g,
            row.Name,
            _nameFont,
            Scrolled(NameBounds(index)),
            selected ? SystemColors.Highlight : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    // The drawn tile, from LayoutRows(): the row's width shared by as many tiles as fit at the tile size, at 4:3.
    private int TileW => _tileW;

    private int TileH => _tileH;

    private int GapPx => LogicalToDeviceUnits(Gap);

    private int InsetPx => LogicalToDeviceUnits(Inset);

    private int NameH => Font.Height + LogicalToDeviceUnits(4);

    private int CellW => TileW + GapPx;

    private int CellH => TileH + NameH + GapPx;

    // Content coordinates: unscrolled.
    private Rectangle TileBounds(int index) => new(InsetPx + index % _perRow * CellW, InsetPx + index / _perRow * CellH, TileW, TileH);

    private Rectangle NameBounds(int index)
    {
        var tile = TileBounds(index);
        return new Rectangle(tile.X, tile.Bottom, tile.Width, NameH);
    }

    private Rectangle CellBounds(int index)
    {
        var tile = TileBounds(index);
        return new Rectangle(tile.X, tile.Y, tile.Width, TileH + NameH);
    }

    private Rectangle HeartBounds(int index)
    {
        var tile = TileBounds(index);
        int size = LogicalToDeviceUnits(Medallion);
        int inset = LogicalToDeviceUnits(MedallionInset);
        return new Rectangle(tile.X + inset, tile.Y + inset, size, size);
    }

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

        int column = x / CellW;
        if (column >= _perRow || x % CellW >= TileW || y % CellH >= TileH + NameH)
        {
            return -1;
        }

        int index = y / CellH * _perRow + column;
        return index < _rows.Count ? index : -1;
    }

    private bool OnHeart(int index, Point client) => index >= 0 && HeartBounds(index).Contains(Unscrolled(client));

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

    private void InvalidateTileOf(string path)
    {
        int index = IndexOf(path);
        if (index >= 0)
        {
            Invalidate(Scrolled(CellBounds(index)));
        }
    }

    private void UpdateExtent()
    {
        int slots = _rows.Count + (_hasMore ? 1 : 0);
        int rows = (slots + _perRow - 1) / _perRow;
        AutoScrollMinSize = new Size(0, rows == 0 ? 0 : InsetPx * 2 + rows * CellH - GapPx);
        Invalidate();
    }

    /// <summary>
    /// The rows from the client width and the tile size: as many tiles per row as fit, the row's width
    /// shared between them — one at least, and never from the number of files, so a lone tile gets
    /// the width it would have in a full row.
    /// </summary>
    private void LayoutRows()
    {
        int available = ClientSize.Width - InsetPx * 2;
        int size = LogicalToDeviceUnits(_tileSize);
        _perRow = Math.Max(1, (available + GapPx) / (size + GapPx));
        _tileW = Math.Max(1, (available - (_perRow - 1) * GapPx) / _perRow);
        _tileH = _tileW * 3 / 4;
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
            AutoScrollPosition = new Point(0, Math.Max(0, TileBounds(top).Y - InsetPx));
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

        _toolTip.SetToolTip(this, index >= 0 ? _rows[index].FullPath : null);
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
        private Size _box;
        private int _side;
        private int _capacity = MaxCached;
        private bool _working;
        private volatile bool _disposed;

        public ThumbnailCache(Control owner)
        {
            _owner = owner;
        }

        /// <summary>A thumbnail arrived (or none exists) for the path; on the UI thread.</summary>
        public event Action<string>? Loaded;

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
            _capacity = (int)Math.Clamp(Budget / Math.Max(1L, (long)box.Width * box.Height * 4), MinCached, MaxCached);
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
                        return;
                    }

                    path = _queue.Dequeue();
                    box = _box;
                    side = _side;
                }

                Bitmap? image = null;
                try
                {
                    using var thumbnail = ShellThumbnail.TryLoad(path, side);
                    image = thumbnail is null ? null : Fit(thumbnail, box);
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
