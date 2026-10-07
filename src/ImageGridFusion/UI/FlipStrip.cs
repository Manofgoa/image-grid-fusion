using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>The two mirrors of the Flip tab.</summary>
internal enum FlipAxis
{
    Horizontal,
    Vertical,
}

/// <summary>
/// The Flip tab's mirrors, Horizontal and Vertical, each on its own: either, both or none pressed. A
/// click on one toggles it.
/// </summary>
internal sealed class FlipStrip : ThumbnailStrip<FlipAxis>
{
    private bool _flipX;
    private bool _flipY;

    public FlipStrip()
        : base(Enum.GetValues<FlipAxis>())
    {
        this.FitSize();
    }

    /// <summary>Whether the image is mirrored left to right: Horizontal pressed.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FlipX
    {
        get => this._flipX;
        set
        {
            if (value != this._flipX)
            {
                this._flipX = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>Whether the image is mirrored top to bottom: Vertical pressed.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FlipY
    {
        get => this._flipY;
        set
        {
            if (value != this._flipY)
            {
                this._flipY = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(FlipAxis axis) => axis == FlipAxis.Horizontal ? this._flipX : this._flipY;

    protected override string Label(FlipAxis axis) => axis.ToString();

    protected override string Tip(FlipAxis axis) => axis == FlipAxis.Horizontal
        ? "Mirrors the image left to right"
        : "Mirrors the image top to bottom";

    protected override void PaintThumbnail(Graphics g, FlipAxis axis, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);

        // The image and its mirror on either side of the axis, their markers mirrored.
        int center;
        Point axisStart, axisEnd;
        if (axis == FlipAxis.Horizontal)
        {
            center = box.X + box.Width / 2;
            int gap = box.Width * 3 / 56;
            int width = box.Width * 5 / 14;
            int height = box.Height * 9 / 20;
            int top = box.Y + (box.Height - height) / 2;
            this.PaintImage(g, new Rectangle(center - gap - width, top, width, height), ContentAlignment.TopLeft);
            this.PaintImage(g, new Rectangle(center + gap, top, width, height), ContentAlignment.TopRight);
            axisStart = new Point(center, box.Y + gap);
            axisEnd = new Point(center, box.Bottom - 1 - gap);
        }
        else
        {
            center = box.Y + box.Height / 2;
            int gap = box.Height * 3 / 40;
            int width = box.Width * 3 / 7;
            int height = box.Height * 7 / 20;
            int left = box.X + (box.Width - width) / 2;
            this.PaintImage(g, new Rectangle(left, center - gap - height, width, height), ContentAlignment.TopLeft);
            this.PaintImage(g, new Rectangle(left, center + gap, width, height), ContentAlignment.BottomLeft);
            axisStart = new Point(box.X + gap, center);
            axisEnd = new Point(box.Right - 1 - gap, center);
        }

        using var dashed = this.OutlinePen();
        dashed.DashStyle = DashStyle.Dash;
        g.DrawLine(dashed, axisStart, axisEnd);
    }
}
