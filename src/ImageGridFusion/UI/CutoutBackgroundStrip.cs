using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Cutout tab's treatments of the part left out: Transparent, Opacity, Color, Blur, Pixelate, the one
/// in use pressed. A click on any picks it — the one shown included, so it still turns the cutout on.
/// See workfiles/20261029-cutout.md § The Background Kept.
/// </summary>
internal sealed class CutoutBackgroundStrip : SelectionStrip<CutoutBackground>
{
    // The pictograms' squares: the checkerboard's grey, the mask's dark, the pixelated blues.
    private static readonly Color Checker = Color.FromArgb(200, 200, 200);
    private static readonly Color Mask = Color.FromArgb(60, 60, 60);
    private static readonly Color LightBlue = Color.FromArgb(133, 183, 235);
    private static readonly Color DarkBlue = Color.FromArgb(24, 95, 165);

    public CutoutBackgroundStrip()
        : base(CutoutBackground.Transparent)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override bool PicksSelected => true;

    protected override string Label(CutoutBackground background) => background.ToString();

    protected override string Tip(CutoutBackground background) => background switch
    {
        CutoutBackground.Transparent => "The part left out becomes transparent",
        CutoutBackground.Opacity => "The part left out is kept, see-through at the opacity set",
        CutoutBackground.Color => "The part left out is masked by a color, at the opacity set",
        CutoutBackground.Blur => "The part left out is kept, blurred",
        _ => "The part left out is kept, in squares",
    };

    protected override void PaintThumbnail(Graphics g, CutoutBackground background, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 5 / 7;
        int height = box.Height * 7 / 10;
        var image = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);

        // The part left out: the whole image, then the kept subject, a blue disc, over it.
        switch (background)
        {
            case CutoutBackground.Transparent:
                this.Fill(g, SystemColors.Window, image);
                int square = Math.Max(2, image.Height / 4);
                for (int y = image.Top; y < image.Bottom; y += square)
                {
                    for (int x = image.Left + ((y - image.Top) / square % 2 * square); x < image.Right; x += 2 * square)
                    {
                        this.Fill(g, Checker, Rectangle.Intersect(image, new Rectangle(x, y, square, square)));
                    }
                }

                break;
            case CutoutBackground.Opacity:
                this.Fill(g, Color.FromArgb(205, 225, 245), image);
                break;
            case CutoutBackground.Color:
                this.Fill(g, Mask, image);
                break;
            case CutoutBackground.Blur:
                using (var brush = new LinearGradientBrush(image, Color.FromArgb(170, 205, 240), SystemColors.Window, 45f))
                {
                    g.FillRectangle(brush, image);
                }

                break;
            default:
                int side = Math.Max(3, image.Height / 3);
                Color[] shades = [LightBlue, DarkBlue, Marker];
                for (int y = image.Top, row = 0; y < image.Bottom; y += side, row++)
                {
                    for (int x = image.Left, column = 0; x < image.Right; x += side, column++)
                    {
                        this.Fill(g, shades[(row + column) % shades.Length], Rectangle.Intersect(image, new Rectangle(x, y, side, side)));
                    }
                }

                break;
        }

        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int disc = image.Height * 3 / 5;
        using (var subject = new SolidBrush(this.Shade(Color.FromArgb(240, 140, 0))))
        {
            g.FillEllipse(subject, image.X + (image.Width - disc) / 2, image.Y + (image.Height - disc) / 2, disc, disc);
        }

        g.SmoothingMode = smoothing;
        this.Outline(g, image);
    }
}
