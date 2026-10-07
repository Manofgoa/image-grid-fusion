using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// A row of thumbnails, one per value of <typeparamref name="T"/>, each with its name below: the
/// selected one highlighted like the active layout of the layout strip, the hovered one lit, a click
/// picking one. The derived strip draws the thumbnails and names them.
/// </summary>
internal abstract class ThumbnailStrip<T> : Control
    where T : struct, Enum
{
    // In logical pixels: the padding around an item, between its thumbnail and its name, between items.
    private const int Pad = 4;
    private const int LabelGap = 2;
    private const int Spacing = 4;

    private readonly T[] _values = Enum.GetValues<T>();
    private readonly ToolTip _toolTip = new();
    private T _selected;
    private int _hovered = -1;

    protected ThumbnailStrip(T selected)
    {
        this._selected = selected;
        this.SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        this.SetStyle(ControlStyles.Selectable, false);
        this.Margin = Padding.Empty;
    }

    /// <summary>A thumbnail was clicked: another one than the selected, or any with <see cref="PicksSelected"/>.</summary>
    public event EventHandler<T>? Picked;

    /// <summary>The value shown as selected.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T Selected
    {
        get => this._selected;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(value, this._selected))
            {
                this._selected = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>The box every thumbnail fits in, in logical pixels.</summary>
    protected abstract Size Box { get; }

    /// <summary>A click on the selected thumbnail picks it too.</summary>
    protected virtual bool PicksSelected => false;

    /// <summary>The name below the thumbnail of <paramref name="value"/>.</summary>
    protected abstract string Label(T value);

    /// <summary>The tooltip of the thumbnail of <paramref name="value"/>.</summary>
    protected abstract string Tip(T value);

    /// <summary>Draws the thumbnail of <paramref name="value"/> into <paramref name="box"/>, the box of <see cref="Box"/> scaled to the DPI.</summary>
    protected abstract void PaintThumbnail(Graphics g, T value, Rectangle box, bool selected);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var items = this.Items();
        return new Size(items[^1].Right, this.ItemHeight());
    }

    /// <summary>Fits the control to its thumbnails; called once the derived strip is set up, and on every font or DPI change.</summary>
    protected void FitSize() => this.Size = this.GetPreferredSize(Size.Empty);

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
        if (e.Button == MouseButtons.Left && this.Enabled && index >= 0 && this.Picks(index))
        {
            this.Picked?.Invoke(this, this._values[index]);
        }
    }

    private bool Picks(int index) => this.PicksSelected || !EqualityComparer<T>.Default.Equals(this._values[index], this._selected);

    private void SetHovered(int index)
    {
        if (index == this._hovered)
        {
            return;
        }

        this._hovered = index;
        this.Cursor = index >= 0 && this.Picks(index) ? Cursors.Hand : Cursors.Default;
        this._toolTip.SetToolTip(this, index < 0 ? null : this.Tip(this._values[index]));
        this.Invalidate();
    }

    private int ItemHeight() => this.LogicalToDeviceUnits(Pad + this.Box.Height + LabelGap + Pad) + this.Font.Height;

    /// <summary>The items left to right, each as wide as its thumbnail box or its name.</summary>
    private List<Rectangle> Items()
    {
        int pad = this.LogicalToDeviceUnits(Pad);
        int box = this.LogicalToDeviceUnits(this.Box.Width);
        int height = this.ItemHeight();
        var items = new List<Rectangle>(this._values.Length);
        int x = 0;
        foreach (var value in this._values)
        {
            int label = TextRenderer.MeasureText(this.Label(value), this.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            var item = new Rectangle(x, 0, Math.Max(box, label) + 2 * pad, height);
            items.Add(item);
            x = item.Right + this.LogicalToDeviceUnits(Spacing);
        }

        return items;
    }

    private void PaintItem(Graphics g, T value, Rectangle item, bool hot)
    {
        bool selected = EqualityComparer<T>.Default.Equals(value, this._selected);
        if (selected)
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
        this.PaintThumbnail(g, value, new Rectangle(item.X + (item.Width - boxWidth) / 2, item.Y + pad, boxWidth, boxHeight), selected);

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
