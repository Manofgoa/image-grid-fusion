using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// A row of thumbnails, one per item, each with its name below: the pressed ones highlighted like the
/// active layout of the layout strip, the hovered one lit, a click picking one. Every choice among
/// values in a cell effect's options is one (RULES.md § Options Toolbar). The derived strip lists the
/// items, says which are pressed, draws the thumbnails and names them.
/// </summary>
internal abstract class ThumbnailStrip<T> : Control
    where T : notnull
{
    // In logical pixels: the padding around an item, between its thumbnail and its name, between items.
    private const int Pad = 4;
    private const int LabelGap = 2;
    private const int Spacing = 4;

    // In logical pixels: the space on either side of the line before an item set apart.
    private const int ApartSpacing = 8;

    /// <summary>The color of the image's marker in the pictograms, the Background's top edge color; a pictogram may draw in it beyond the marker.</summary>
    protected static readonly Color Marker = Color.FromArgb(55, 138, 221);

    private readonly T[] _values;
    private readonly ToolTip _toolTip = new();
    private int _hovered = -1;

    protected ThumbnailStrip(IEnumerable<T> values)
    {
        this._values = [.. values];
        this.SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        this.SetStyle(ControlStyles.Selectable, false);
        this.Margin = Padding.Empty;
    }

    /// <summary>A thumbnail was clicked, one that <see cref="Picks"/>.</summary>
    public event EventHandler<T>? Picked;

    /// <summary>The box of the cell effects' thumbnails, the Background's: every strip of their options has it, so they are all as tall.</summary>
    protected static Size EffectBox => new(56, 40);

    /// <summary>The box every thumbnail fits in, in logical pixels.</summary>
    protected abstract Size Box { get; }

    /// <summary>Whether <paramref name="value"/> is shown pressed.</summary>
    protected abstract bool IsPressed(T value);

    /// <summary>Whether a click on <paramref name="value"/> picks it; every one does unless the strip says otherwise.</summary>
    protected virtual bool Picks(T value) => true;

    /// <summary>Whether <paramref name="value"/> stands apart from the items before it, past a wider gap and a line: an action among choices.</summary>
    protected virtual bool IsApart(T value) => false;

    /// <summary>The name below the thumbnail of <paramref name="value"/>.</summary>
    protected abstract string Label(T value);

    /// <summary>The tooltip of the thumbnail of <paramref name="value"/>.</summary>
    protected abstract string Tip(T value);

    /// <summary>Draws the thumbnail of <paramref name="value"/> into <paramref name="box"/>, the box of <see cref="Box"/> scaled to the DPI.</summary>
    protected abstract void PaintThumbnail(Graphics g, T value, Rectangle box, bool pressed);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var items = this.Items();
        return new Size(items[^1].Right, this.ItemHeight());
    }

    /// <summary>Fits the control to its thumbnails; called once the derived strip is set up, and on every font or DPI change.</summary>
    protected void FitSize() => this.Size = this.GetPreferredSize(Size.Empty);

    /// <summary>The color as drawn: faded toward the control's color while the strip is disabled.</summary>
    protected Color Shade(Color color)
    {
        if (this.Enabled)
        {
            return color;
        }

        var back = SystemColors.Control;
        return Color.FromArgb(color.A, (color.R + 2 * back.R) / 3, (color.G + 2 * back.G) / 3, (color.B + 2 * back.B) / 3);
    }

    protected void Fill(Graphics g, Color color, Rectangle bounds)
    {
        using var brush = new SolidBrush(this.Shade(color));
        g.FillRectangle(brush, bounds);
    }

    protected void Fill(Graphics g, Color color, Point[] polygon)
    {
        using var brush = new SolidBrush(this.Shade(color));
        g.FillPolygon(brush, polygon);
    }

    /// <summary>The pen of the pictograms' outlines: the cell's and the image's.</summary>
    protected Pen OutlinePen() => new(this.Enabled ? SystemColors.ControlDarkDark : SystemColors.ControlDark);

    /// <summary>Outlines <paramref name="bounds"/> inside it.</summary>
    protected void Outline(Graphics g, Rectangle bounds)
    {
        using var pen = this.OutlinePen();
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    /// <summary>The cell of a pictogram, in the Background's vocabulary: a grey box, outlined dark.</summary>
    protected void PaintCell(Graphics g, Rectangle box)
    {
        this.Fill(g, SystemColors.ControlDark, box);
        this.Outline(g, box);
    }

    /// <summary>
    /// The image of a pictogram, in the Background's vocabulary: a white rectangle outlined dark, the
    /// marker — a small triangle in one of its corners — showing which way it is turned or mirrored.
    /// </summary>
    protected void PaintImage(Graphics g, Rectangle image, ContentAlignment marker)
    {
        this.Fill(g, SystemColors.Window, image);
        int leg = Math.Max(3, Math.Min(image.Width, image.Height) * 2 / 5);
        bool right = marker is ContentAlignment.TopRight or ContentAlignment.BottomRight;
        bool bottom = marker is ContentAlignment.BottomLeft or ContentAlignment.BottomRight;
        var corner = new Point(right ? image.Right : image.Left, bottom ? image.Bottom : image.Top);
        this.Fill(g, Marker, [corner, new(corner.X + (right ? -leg : leg), corner.Y), new(corner.X, corner.Y + (bottom ? -leg : leg))]);
        this.Outline(g, image);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        this.FitSize();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.FitSize();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        this.SetHovered(-1);
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var items = this.Items();
        for (int i = 0; i < this._values.Length; i++)
        {
            this.PaintItem(e.Graphics, this._values[i], items[i], i == this._hovered);
            if (i > 0 && this.IsApart(this._values[i]))
            {
                int x = (items[i - 1].Right + items[i].Left) / 2;
                int pad = this.LogicalToDeviceUnits(Pad);
                using var line = new Pen(SystemColors.ControlDark);
                e.Graphics.DrawLine(line, x, items[i].Top + pad, x, items[i].Bottom - pad);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetHovered(this.Enabled ? this.Items().FindIndex(item => item.Contains(e.Location)) : -1);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(-1);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = this.Items().FindIndex(item => item.Contains(e.Location));
        if (e.Button == MouseButtons.Left && this.Enabled && index >= 0 && this.Picks(this._values[index]))
        {
            this.Picked?.Invoke(this, this._values[index]);
        }
    }

    private void SetHovered(int index)
    {
        if (index == this._hovered)
        {
            return;
        }

        this._hovered = index;
        this.Cursor = index >= 0 && this.Picks(this._values[index]) ? Cursors.Hand : Cursors.Default;
        this._toolTip.SetToolTip(this, index < 0 ? null : this.Tip(this._values[index]));
        this.Invalidate();
    }

    private int ItemHeight() => this.LogicalToDeviceUnits(Pad + this.Box.Height + LabelGap + Pad) + this.Font.Height;

    /// <summary>The items left to right, each as wide as its thumbnail box or its name; an item set apart past a wider gap.</summary>
    private List<Rectangle> Items()
    {
        int pad = this.LogicalToDeviceUnits(Pad);
        int box = this.LogicalToDeviceUnits(this.Box.Width);
        int height = this.ItemHeight();
        var items = new List<Rectangle>(this._values.Length);
        int x = 0;
        foreach (var value in this._values)
        {
            if (items.Count > 0 && this.IsApart(value))
            {
                x += this.LogicalToDeviceUnits(2 * ApartSpacing - Spacing);
            }

            int label = TextRenderer.MeasureText(this.Label(value), this.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            var item = new Rectangle(x, 0, Math.Max(box, label) + 2 * pad, height);
            items.Add(item);
            x = item.Right + this.LogicalToDeviceUnits(Spacing);
        }

        return items;
    }

    private void PaintItem(Graphics g, T value, Rectangle item, bool hot)
    {
        bool pressed = this.IsPressed(value);
        if (pressed)
        {
            using var fill = new SolidBrush(Color.FromArgb(this.Enabled ? 60 : 30, SystemColors.Highlight));
            g.FillRectangle(fill, item);
            using var pen = new Pen(this.Enabled ? SystemColors.Highlight : SystemColors.ControlDark, this.LogicalToDeviceUnits(2)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, item);
        }
        else if (hot)
        {
            using var fill = new SolidBrush(SystemColors.ControlLight);
            g.FillRectangle(fill, item);
        }

        int pad = this.LogicalToDeviceUnits(Pad);
        int boxWidth = this.LogicalToDeviceUnits(this.Box.Width);
        int boxHeight = this.LogicalToDeviceUnits(this.Box.Height);
        this.PaintThumbnail(g, value, new Rectangle(item.X + (item.Width - boxWidth) / 2, item.Y + pad, boxWidth, boxHeight), pressed);

        var label = new Rectangle(item.X, item.Y + pad + boxHeight + this.LogicalToDeviceUnits(LabelGap), item.Width, this.Font.Height);
        TextRenderer.DrawText(
            g,
            this.Label(value),
            this.Font,
            label,
            this.Enabled ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
