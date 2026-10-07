using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Blur tab's kinds, Gaussian and Pixelate: the one in use pressed. A click on either picks it — the
/// one already shown included, so it still turns the blur on.
/// </summary>
internal sealed class BlurKindStrip : SelectionStrip<BlurKind>
{
    // The blues of the pixelated squares, lightest to darkest.
    private static readonly Color LightBlue = Color.FromArgb(133, 183, 235);
    private static readonly Color DarkBlue = Color.FromArgb(24, 95, 165);

    public BlurKindStrip()
        : base(BlurKind.Gaussian)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override bool PicksSelected => true;

    protected override string Label(BlurKind kind) => kind.ToString();

    protected override string Tip(BlurKind kind) => kind == BlurKind.Gaussian
        ? "A smooth blur, fading every detail"
        : "Coarse squares, each the average of its pixels";

    protected override void PaintThumbnail(Graphics g, BlurKind kind, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 5 / 7;
        int height = box.Height * 7 / 10;
        var image = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        this.Fill(g, SystemColors.Window, image);
        var center = new Point(image.X + image.Width / 2, image.Y + image.Height / 2);
        if (kind == BlurKind.Gaussian)
        {
            // A soft disc, its edge fading outward.
            (int Radius, int Alpha)[] rings = [(box.Height * 11 / 40, 50), (box.Height * 8 / 40, 90), (box.Height * 5 / 40, 150)];
            foreach (var (radius, alpha) in rings)
            {
                this.FillDisc(g, Color.FromArgb(alpha, Marker), center, radius);
            }
        }
        else
        {
            // Two by two coarse squares.
            int side = box.Height / 5;
            this.Fill(g, LightBlue, new Rectangle(center.X - side, center.Y - side, side, side));
            this.Fill(g, Marker, new Rectangle(center.X, center.Y - side, side, side));
            this.Fill(g, Marker, new Rectangle(center.X - side, center.Y, side, side));
            this.Fill(g, DarkBlue, new Rectangle(center.X, center.Y, side, side));
        }

        this.Outline(g, image);
    }

    private void FillDisc(Graphics g, Color color, Point center, int radius)
    {
        using var brush = new SolidBrush(this.Shade(color));
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(brush, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        g.SmoothingMode = smoothing;
    }
}
