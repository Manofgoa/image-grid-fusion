using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Vertical strip of schematic thumbnails, one per layout of the current image count, with the
/// mirror toggle below them. Always shown, so the preview keeps its size: with no image, it shows
/// the single-image thumbnail greyed out and does not react to the mouse.
/// </summary>
internal sealed class LayoutStrip : Control
{
    private static readonly Color DisabledColor = Color.FromArgb(96, 96, 96);

    private readonly ToolTip _toolTip = new();
    private GridLayout? _active;
    private IReadOnlyList<GridLayout> _layouts = GridLayout.For(1);

    // Index in _layouts, or _layouts.Count for the mirror toggle.
    private int _hovered = -1;

    public LayoutStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.FromArgb(48, 48, 48);
        ForeColor = Color.Gainsboro;
    }

    /// <summary>Raised with the catalog layout clicked, unmirrored.</summary>
    public event EventHandler<GridLayout>? LayoutPicked;

    public event EventHandler? MirrorToggled;

    /// <summary>Layout shown as active; its image count decides which thumbnails are offered.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridLayout? ActiveLayout
    {
        get => _active;
        set
        {
            _active = value;
            _layouts = GridLayout.For(value?.Count ?? 1);
            SetHovered(-1);
            Invalidate();
        }
    }

    private int MirrorIndex => _layouts.Count;

    private bool MirrorEnabled => _active is { MirrorAxis: not MirrorAxis.None };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        for (int i = 0; i < _layouts.Count; i++)
        {
            // The active thumbnail shows the layout as applied, mirror included.
            bool active = _layouts[i].Id == _active?.Id;
            var bounds = ItemBounds(i);
            PaintButton(g, bounds, active, i == _hovered);
            var cellsColor = _active is null ? DisabledColor : active ? ForeColor : Color.Gray;
            PaintCells(g, active ? _active! : _layouts[i], Rectangle.Inflate(bounds, -LogicalToDeviceUnits(4), -LogicalToDeviceUnits(4)), cellsColor);
        }

        var mirror = ItemBounds(MirrorIndex);
        PaintButton(g, mirror, _active?.IsMirrored == true, MirrorEnabled && _hovered == MirrorIndex);
        PaintMirrorIcon(g, mirror);
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
        if (index == MirrorIndex)
        {
            if (MirrorEnabled)
            {
                MirrorToggled?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (index >= 0 && _layouts[index].Id != _active.Id)
        {
            LayoutPicked?.Invoke(this, _layouts[index]);
        }
    }

    private void SetHovered(int index)
    {
        if (index == _hovered)
        {
            return;
        }

        _hovered = index;
        bool clickable = index >= 0 && (index != MirrorIndex || MirrorEnabled);
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
        _toolTip.SetToolTip(this, index < 0 ? null : index == MirrorIndex ? MirrorTip() : _layouts[index].Name);
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

        for (int i = 0; i <= MirrorIndex; i++)
        {
            if (ItemBounds(i).Contains(location))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Thumbnails stacked from the top, at the output ratio; the mirror toggle below them.</summary>
    private Rectangle ItemBounds(int index)
    {
        int margin = LogicalToDeviceUnits(12);
        int top = LogicalToDeviceUnits(16);
        int spacing = LogicalToDeviceUnits(6);
        int width = Math.Max(1, ClientSize.Width - 2 * margin);
        int height = GridLayout.HeightFor(width - 2 * LogicalToDeviceUnits(4)) + 2 * LogicalToDeviceUnits(4);

        if (index < _layouts.Count)
        {
            return new Rectangle(margin, top + index * (height + spacing), width, height);
        }

        int mirrorTop = top + _layouts.Count * (height + spacing) + LogicalToDeviceUnits(6);
        return new Rectangle(margin, mirrorTop, width, LogicalToDeviceUnits(28));
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
}
