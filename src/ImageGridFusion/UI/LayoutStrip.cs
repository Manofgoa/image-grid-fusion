using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Vertical strip holding the mirror toggle, then one schematic thumbnail per basic layout of the
/// current image count, then a "More" header over the advanced ones, collapsed by default.
/// Scrolls vertically when taller than the window. Always shown, so the preview keeps its size: with
/// no image, it shows the single-image thumbnail greyed out and does not react to the mouse.
/// </summary>
internal sealed class LayoutStrip : ScrollableControl
{
    private static readonly Color DisabledColor = Color.FromArgb(96, 96, 96);

    private readonly ToolTip _toolTip = new();
    private GridLayout? _active;
    private IReadOnlyList<GridLayout> _layouts = GridLayout.For(1);
    private bool _expanded;
    private List<Item> _items = [];

    // Index in _items.
    private int _hovered = -1;

    public LayoutStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.FromArgb(48, 48, 48);
        ForeColor = Color.Gainsboro;
        AutoScroll = true;
    }

    /// <summary>Raised with the catalog layout clicked, unmirrored.</summary>
    public event EventHandler<GridLayout>? LayoutPicked;

    /// <summary>Raised when the thumbnail of the active layout is clicked again.</summary>
    public event EventHandler? ActiveLayoutClicked;

    public event EventHandler? MirrorToggled;

    private enum ItemKind
    {
        Mirror,
        Layout,
        AdvancedHeader,
    }

    /// <summary>
    /// Layout shown as active; its image count decides which thumbnails are offered. A new image count
    /// collapses the advanced group.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridLayout? ActiveLayout
    {
        get => _active;
        set
        {
            if ((value?.Count ?? 1) != _layouts[0].Count)
            {
                _expanded = false;
            }

            _active = value;
            _layouts = GridLayout.For(value?.Count ?? 1);
            Arrange();
        }
    }

    private bool MirrorEnabled => _active is { MirrorAxis: not MirrorAxis.None };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    // Also raised when the scrollbar shows or hides, which the items make room for.
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        Arrange();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            switch (item.Kind)
            {
                case ItemKind.Mirror:
                    PaintButton(g, item.Bounds, _active?.IsMirrored == true, MirrorEnabled && i == _hovered);
                    PaintMirrorIcon(g, item.Bounds);
                    break;
                case ItemKind.AdvancedHeader:
                    PaintButton(g, item.Bounds, false, i == _hovered);
                    PaintHeader(g, item.Bounds);
                    break;
                default:
                    // The active thumbnail shows the layout as applied, mirror included, on its own proportions.
                    var layout = item.Layout!;
                    bool active = layout.Id == _active?.Id;
                    PaintButton(g, item.Bounds, active, i == _hovered);
                    var cellsColor = _active is null ? DisabledColor : active ? ForeColor : Color.Gray;
                    PaintCells(g, active ? _active!.WithDefaultSizes() : layout, Rectangle.Inflate(item.Bounds, -LogicalToDeviceUnits(4), -LogicalToDeviceUnits(4)), cellsColor);
                    break;
            }
        }
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        FollowMouse();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        FollowMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHovered(ItemAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(-1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _active is null)
        {
            return;
        }

        int index = ItemAt(e.Location);
        if (index < 0)
        {
            return;
        }

        var item = _items[index];
        if (item.Kind == ItemKind.Mirror)
        {
            if (MirrorEnabled)
            {
                MirrorToggled?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (item.Kind == ItemKind.AdvancedHeader)
        {
            _expanded = !_expanded;
            Arrange();
        }
        else if (item.Layout!.Id != _active.Id)
        {
            LayoutPicked?.Invoke(this, item.Layout);
        }
        else
        {
            ActiveLayoutClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lays the items out again and sizes the scrollable area to them.</summary>
    private void Arrange()
    {
        _items = LayOut(out int height);
        AutoScrollMinSize = new Size(0, height);
        SetHovered(-1);
        FollowMouse();
    }

    /// <summary>
    /// Items stacked from the top: the mirror toggle, the basic thumbnails at the output ratio, then
    /// the "More" header over the advanced thumbnails — all of them expanded, only the active one
    /// collapsed. No header when the image count has no advanced layout. The items keep the size the
    /// whole strip gives them: a scrollbar only pushes them left into the margin.
    /// </summary>
    private List<Item> LayOut(out int height)
    {
        int margin = LogicalToDeviceUnits(12);
        int spacing = LogicalToDeviceUnits(6);
        int width = Math.Max(1, Width - 2 * margin);
        int left = Math.Max(0, Math.Min(margin, ClientSize.Width - width - LogicalToDeviceUnits(3)));
        int thumbnailHeight = GridLayout.HeightFor(width - 2 * LogicalToDeviceUnits(4)) + 2 * LogicalToDeviceUnits(4);
        int y = LogicalToDeviceUnits(16);
        var items = new List<Item>();

        void Add(ItemKind kind, GridLayout? layout, int itemHeight, int gapAfter)
        {
            items.Add(new Item(kind, layout, new Rectangle(left, y, width, itemHeight)));
            y += itemHeight + gapAfter;
        }

        Add(ItemKind.Mirror, null, LogicalToDeviceUnits(28), spacing + LogicalToDeviceUnits(6));
        foreach (var layout in _layouts.Where(l => !l.IsAdvanced))
        {
            Add(ItemKind.Layout, layout, thumbnailHeight, spacing);
        }

        var advanced = _layouts.Where(l => l.IsAdvanced).ToList();
        if (advanced.Count > 0)
        {
            y += LogicalToDeviceUnits(6);
            Add(ItemKind.AdvancedHeader, null, LogicalToDeviceUnits(22), spacing);
            foreach (var layout in advanced.Where(l => _expanded || l.Id == _active?.Id))
            {
                Add(ItemKind.Layout, layout, thumbnailHeight, spacing);
            }
        }

        height = y - spacing + LogicalToDeviceUnits(16);
        return items;
    }

    /// <summary>Hover follows the items under the mouse once they moved beneath it.</summary>
    private void FollowMouse()
    {
        if (IsHandleCreated)
        {
            var mouse = PointToClient(MousePosition);
            SetHovered(ClientRectangle.Contains(mouse) ? ItemAt(mouse) : -1);
        }

        Invalidate();
    }

    private void SetHovered(int index)
    {
        if (index == _hovered)
        {
            return;
        }

        _hovered = index;
        bool clickable = index >= 0 && (_items[index].Kind != ItemKind.Mirror || MirrorEnabled);
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
        _toolTip.SetToolTip(this, index < 0 ? null : _items[index].Kind switch
        {
            ItemKind.Mirror => MirrorTip(),
            ItemKind.AdvancedHeader => _expanded ? "Hide the extra layouts" : "Show more layouts",
            _ => _items[index].Layout!.Name,
        });
        Invalidate();
    }

    private string MirrorTip() => _active?.MirrorAxis switch
    {
        MirrorAxis.Horizontal => "Mirror left ↔ right",
        MirrorAxis.Vertical => "Mirror top ↔ bottom",
        _ => "Mirror (not available for a symmetric layout)",
    };

    private int ItemAt(Point location)
    {
        if (_active is null)
        {
            return -1;
        }

        // The items are laid out on the whole strip; the view shows it scrolled.
        location.Offset(-AutoScrollPosition.X, -AutoScrollPosition.Y);
        return _items.FindIndex(item => item.Bounds.Contains(location));
    }

    private void PaintButton(Graphics g, Rectangle bounds, bool on, bool hot)
    {
        if (on)
        {
            using var fill = new SolidBrush(Color.FromArgb(90, SystemColors.Highlight));
            g.FillRectangle(fill, bounds);
            using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(2)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, bounds);
        }
        else if (hot)
        {
            using var fill = new SolidBrush(Color.FromArgb(40, Color.White));
            g.FillRectangle(fill, bounds);
        }
    }

    private void PaintCells(Graphics g, GridLayout layout, Rectangle area, Color color)
    {
        using var brush = new SolidBrush(color);
        int gap = LogicalToDeviceUnits(1);
        foreach (var cell in layout.Cells(area.Size))
        {
            var bounds = Rectangle.Inflate(cell, -gap, -gap);
            bounds.Offset(area.Location);
            g.FillRectangle(brush, bounds);
        }
    }

    /// <summary>"More" with a chevron telling whether the advanced group is open.</summary>
    private void PaintHeader(Graphics g, Rectangle bounds)
    {
        TextRenderer.DrawText(
            g, _expanded ? "More ▾" : "More ▸", Font, bounds, _active is null ? DisabledColor : ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsTranslateTransform);
    }

    /// <summary>Two triangles facing away from a dashed axis, turned upright for a vertical mirror.</summary>
    private void PaintMirrorIcon(Graphics g, Rectangle bounds)
    {
        var color = !MirrorEnabled ? DisabledColor : _active!.IsMirrored ? Color.White : ForeColor;
        float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
        float half = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(3), depth = LogicalToDeviceUnits(7);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(cx, cy);
        if (_active?.MirrorAxis == MirrorAxis.Vertical)
        {
            g.RotateTransform(90);
        }

        using (var pen = new Pen(color, LogicalToDeviceUnits(1)) { DashStyle = DashStyle.Dash })
        {
            g.DrawLine(pen, 0, -half - 2, 0, half + 2);
        }

        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, new PointF[] { new(-gap, -half), new(-gap, half), new(-gap - depth, 0) });
        g.FillPolygon(brush, new PointF[] { new(gap, -half), new(gap, half), new(gap + depth, 0) });
        g.Restore(state);
    }

    /// <summary>One clickable row of the strip, in the coordinates of the whole (unscrolled) strip.</summary>
    private readonly record struct Item(ItemKind Kind, GridLayout? Layout, Rectangle Bounds);
}
