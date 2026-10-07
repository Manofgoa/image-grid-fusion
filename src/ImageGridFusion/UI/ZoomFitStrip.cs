using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Zoom tab's fit modes, Contain and Fill: at most one pressed, the one in force. A click on either
/// picks it — on the pressed one, it leaves it for the free zoom it gave.
/// </summary>
internal sealed class ZoomFitStrip : ThumbnailStrip<ZoomFit>
{
    private ZoomFit _fit;

    public ZoomFitStrip()
        : base([ZoomFit.Contain, ZoomFit.Fill])
    {
        this.FitSize();
    }

    /// <summary>The fit mode in force, pressed; <see cref="ZoomFit.None"/> for the free zoom.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ZoomFit Fit
    {
        get => this._fit;
        set
        {
            if (value != this._fit)
            {
                this._fit = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(ZoomFit fit) => fit == this._fit;

    protected override string Label(ZoomFit fit) => fit.ToString();

    protected override string Tip(ZoomFit fit) => fit == ZoomFit.Contain
        ? "Keeps the whole image in its cell, whatever its size: bands on one side"
        : "Keeps the cell covered by the image, whatever its size: the overflow cropped";

    protected override void PaintThumbnail(Graphics g, ZoomFit fit, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        if (fit == ZoomFit.Contain)
        {
            // A landscape image as wide as the cell, the bands above and below it.
            int height = box.Height * 11 / 20;
            this.PaintImage(g, new Rectangle(box.X, box.Y + (box.Height - height) / 2, box.Width, height), ContentAlignment.TopLeft);
        }
        else
        {
            // The cell covered, the image's edges beyond it.
            this.PaintImage(g, box, ContentAlignment.TopLeft);
            int inset = this.LogicalToDeviceUnits(3);
            using var dashed = this.OutlinePen();
            dashed.DashStyle = DashStyle.Dash;
            g.DrawRectangle(dashed, box.X + inset, box.Y + inset, box.Width - 1 - 2 * inset, box.Height - 1 - 2 * inset);
        }

        this.Outline(g, box);
    }
}
