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
/// loaded in the background, the heart in a medallion at its corner, the name below — in 1 to 5
/// columns, scrolling vertically. See workfiles/20260926-file-explorer.md § Panel.
/// </summary>
internal sealed class ThumbnailGrid : ScrollableControl
{
    // In logical pixels.
    public const int TileWidth = 200;
    public const int TileHeight = 150;
    public const int Gap = 8;
    public const int Inset = 4;
    public const int MinColumns = 1;
    public const int MaxColumns = 5;
    private const int Medallion = 24;
    private const int MedallionInset = 4;

    private readonly ToolTip _toolTip = new();
    private readonly ThumbnailCache _thumbnails;
    private Font _heartFont;
    private Font _nameFont;
    private IReadOnlyList<ExplorerRow> _rows = [];
    private int _columns = MinColumns;
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

    /// <summary>Whether a file is a favorite: its heart is drawn full.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, bool> IsFavorite { get; set; } = _ => false;

    /// <summary>The tiles, top-left first; setting them scrolls back to the top and drops the thumbnails still to load.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ExplorerRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            _selected = -1;
            _pressed = -1;
            _thumbnails.DropPending();
            AutoScrollPosition = Point.Empty;
            UpdateExtent();
            Hover(-1);
        }
    }

    /// <summary>How many tiles per row, <see cref="MinColumns"/> to <see cref="MaxColumns"/>.</summary>
    [DefaultValue(MinColumns)]
    public int Columns
    {
        get => _columns;
        set
        {
            value = Math.Clamp(value, MinColumns, MaxColumns);
            if (value != _columns)
            {
                _columns = value;
                UpdateExtent();
            }
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
        UpdateBox();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateBox();
        UpdateExtent();
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
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Home or Keys.End || base.IsInputKey(keyData);

    /// <summary>The arrows move the selection, staying in the column at the top; Enter activates it.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0)
        {
            return;
        }

        int last = _rows.Count - 1;
        int target;
        switch (e.KeyCode)
        {
            case Keys.Left:
                target = Math.Max(0, _selected - 1);
                break;
            case Keys.Right:
                target = Math.Min(last, _selected + 1);
                break;
            case Keys.Up:
                target = _selected < 0 ? 0 : Math.Max(_selected % _columns, _selected - _columns);
                break;
            case Keys.Down:
                target = _selected < 0 ? 0 : Math.Min(last, _selected + _columns);
                break;
            case Keys.Home:
                target = 0;
                break;
            case Keys.End:
                target = last;
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
        Select(target);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        var clip = e.ClipRectangle;
        for (int i = 0; i < _rows.Count; i++)
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

        // Already fitted to the box by the loader: drawn at its size, centered.
        if (_thumbnails.TryGet(row.FullPath, out var image) && image is not null)
        {
            g.DrawImage(image, new Rectangle(tile.X + (tile.Width - image.Width) / 2, tile.Y + (tile.Height - image.Height) / 2, image.Width, image.Height));
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

    private int TileW => LogicalToDeviceUnits(TileWidth);

    private int TileH => LogicalToDeviceUnits(TileHeight);

    private int GapPx => LogicalToDeviceUnits(Gap);

    private int InsetPx => LogicalToDeviceUnits(Inset);

    private int NameH => Font.Height + LogicalToDeviceUnits(4);

    private int CellW => TileW + GapPx;

    private int CellH => TileH + NameH + GapPx;

    // Content coordinates: unscrolled.
    private Rectangle TileBounds(int index) => new(InsetPx + index % _columns * CellW, InsetPx + index / _columns * CellH, TileW, TileH);

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
        if (column >= _columns || x % CellW >= TileW || y % CellH >= TileH + NameH)
        {
            return -1;
        }

        int index = y / CellH * _columns + column;
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
        int rows = (_rows.Count + _columns - 1) / _columns;
        AutoScrollMinSize = new Size(0, rows == 0 ? 0 : InsetPx * 2 + rows * CellH - GapPx);
        Invalidate();
    }

    private void UpdateBox() => _thumbnails.SetBox(new Size(TileW, TileH), Math.Max(256, TileW));

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
    /// cap. A thumbnail is asked for by painting a tile that has none yet.
    /// </summary>
    private sealed class ThumbnailCache : IDisposable
    {
        private const int MaxCached = 200;

        private readonly Control _owner;
        private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<Entry> _order = new();
        private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _queue = new();
        private readonly object _lock = new();
        private Size _box;
        private int _side;
        private bool _working;
        private volatile bool _disposed;

        public ThumbnailCache(Control owner)
        {
            _owner = owner;
        }

        /// <summary>A thumbnail arrived (or none exists) for the path; on the UI thread.</summary>
        public event Action<string>? Loaded;

        /// <summary>The box the thumbnails are scaled to, and the size asked from the Shell; changing it drops them all.</summary>
        public void SetBox(Size box, int side)
        {
            if (box == _box && side == _side)
            {
                return;
            }

            _box = box;
            _side = side;
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
            while (_order.Count > MaxCached)
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
