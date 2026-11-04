using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The shared fill's kinds — Solid, Linear, Radial — one thumbnail each, the fill drawn over a schematic cell,
/// its name below: in the Background's options and in the Grid background's (workfiles/20260926-forced-background.md).
/// A click on any one, the selected included, picks it, so it turns its effect on like its other options.
/// </summary>
internal sealed class FillKindStrip : SelectionStrip<FillKind>
{
    // The pictograms' two colors: the first one light, the second deep, as a gradient from one to the other.
    private static readonly Color First = Color.FromArgb(160, 205, 245);
    private static readonly Color Second = Color.FromArgb(30, 75, 140);

    public FillKindStrip()
        : base(FillKind.Solid)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override bool PicksSelected => true;

    protected override string Label(FillKind kind) => kind.ToString();

    protected override string Tip(FillKind kind) => kind switch
    {
        FillKind.Solid => "One color",
        FillKind.Linear => "From the first color to the second, along the angle",
        _ => "From the first color at the center to the second at the corners",
    };

    protected override void PaintThumbnail(Graphics g, FillKind kind, Rectangle box, bool pressed)
    {
        var first = this.Shade(First);
        var second = this.Shade(Second);
        switch (kind)
        {
            case FillKind.Solid:
                this.Fill(g, First, box);
                break;
            case FillKind.Linear:
                using (var linear = new LinearGradientBrush(Rectangle.Inflate(box, 1, 1), first, second, 0f) { WrapMode = WrapMode.TileFlipXY })
                {
                    g.FillRectangle(linear, box);
                }

                break;
            default:
                float rx = (float)(box.Width / Math.Sqrt(2)), ry = (float)(box.Height / Math.Sqrt(2));
                var center = new PointF(box.X + box.Width / 2f, box.Y + box.Height / 2f);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(center.X - rx, center.Y - ry, 2 * rx, 2 * ry);
                    using var radial = new PathGradientBrush(path) { CenterPoint = center, CenterColor = first, SurroundColors = [second] };
                    g.FillRectangle(radial, box);
                }

                break;
        }

        this.Outline(g, box);
    }
}
