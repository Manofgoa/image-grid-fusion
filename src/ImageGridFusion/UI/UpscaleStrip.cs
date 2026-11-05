using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>The Upscale tab's factors.</summary>
internal enum UpscaleFactor
{
    Times2,
    Times4,
}

/// <summary>
/// The Upscale tab's factors, ×2 and ×4: the one chosen pressed, one that would go past the PNG's cap faded and refused.
/// A pick only chooses — the <b>Upscale</b> button applies (workfiles/20261029-image-upscale.md § Effect and Options).
/// </summary>
internal sealed class UpscaleStrip : SelectionStrip<UpscaleFactor>
{
    private Func<int, bool> _fits = _ => true;

    public UpscaleStrip()
        : base(UpscaleFactor.Times2)
    {
        this.FitSize();
    }

    public static int FactorOf(UpscaleFactor factor) => factor == UpscaleFactor.Times4 ? 4 : 2;

    public static UpscaleFactor Of(int factor) => factor == 4 ? UpscaleFactor.Times4 : UpscaleFactor.Times2;

    /// <summary>Shows <paramref name="factor"/> chosen, the factors <paramref name="fits"/> refuses faded.</summary>
    public void Show(int factor, Func<int, bool> fits)
    {
        this._fits = fits;
        this.Selected = Of(factor);
        this.Invalidate();
    }

    protected override Size Box => EffectBox;

    protected override bool Picks(UpscaleFactor factor) => base.Picks(factor) && this._fits(FactorOf(factor));

    protected override string Label(UpscaleFactor factor) => $"×{FactorOf(factor)}";

    protected override string Tip(UpscaleFactor factor)
    {
        int times = FactorOf(factor);
        return this._fits(times)
            ? Texts.Format(Strings.UiUpscaleFactorTip, times)
            : Texts.Format(Strings.UiUpscaleTooLarge, times, CanvasSizer.MaxLongSide);
    }

    protected override void PaintThumbnail(Graphics g, UpscaleFactor factor, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);

        // The enlargement dashed in the marker's blue, the image as loaded at its bottom left, an arrow between them.
        int times = FactorOf(factor);
        int pad = Math.Max(2, box.Height / 8);
        var enlarged = new Rectangle(box.X + 2 * pad, box.Y + pad, box.Width - 4 * pad, box.Height - 2 * pad);
        var loaded = new Rectangle(enlarged.X, enlarged.Bottom - enlarged.Height / times, Math.Max(3, enlarged.Width / times), Math.Max(3, enlarged.Height / times));
        this.Fill(g, SystemColors.Window, enlarged);
        using (var dashed = new Pen(this.Shade(Marker)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(dashed, enlarged.X, enlarged.Y, enlarged.Width - 1, enlarged.Height - 1);
        }

        this.Fill(g, Marker, loaded);
        this.Outline(g, loaded);
        using (var arrow = new Pen(this.Shade(SystemColors.ControlDarkDark), 1.5f) { CustomEndCap = new AdjustableArrowCap(3, 3) })
        {
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawLine(arrow, loaded.Right + 1, loaded.Top - 1, enlarged.Right - pad, enlarged.Top + pad);
            g.SmoothingMode = smoothing;
        }

        // A factor refused: faded under the control's color.
        if (!this._fits(times))
        {
            using var veil = new SolidBrush(Color.FromArgb(160, SystemColors.Control));
            g.FillRectangle(veil, box);
        }
    }
}

/// <summary>
/// The Upscale tab's models, Quality and Fast: the one chosen pressed. A pick only chooses — the <b>Upscale</b> button
/// applies (workfiles/20261029-image-upscale.md § Effect and Options).
/// </summary>
internal sealed class UpscaleModelStrip : SelectionStrip<UpscaleModel>
{
    public UpscaleModelStrip()
        : base(UpscaleModel.Quality)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override string Label(UpscaleModel model) => model == UpscaleModel.Quality ? Strings.UiUpscaleQuality : Strings.UiUpscaleFast;

    protected override string Tip(UpscaleModel model) => model == UpscaleModel.Quality
        ? Strings.UiUpscaleQualityTip
        : Strings.UiUpscaleFastTip;

    protected override void PaintThumbnail(Graphics g, UpscaleModel model, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 5 / 7;
        int height = box.Height * 7 / 10;
        var image = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        this.Fill(g, SystemColors.Window, image);
        if (model == UpscaleModel.Quality)
        {
            // Fine detail: a checker of small blue squares.
            int side = Math.Max(2, box.Height / 10);
            for (int y = image.Top + side; y + side <= image.Bottom - side; y += side)
            {
                for (int x = image.Left + side + ((y - image.Top) / side % 2 * side); x + side <= image.Right - side; x += 2 * side)
                {
                    this.Fill(g, Marker, new Rectangle(x, y, side, side));
                }
            }
        }
        else
        {
            // Speed: two chevrons pointing right.
            int middle = image.Top + image.Height / 2, reach = image.Height / 3;
            using var pen = new Pen(this.Shade(Marker), Math.Max(2, box.Height / 14f)) { LineJoin = LineJoin.Miter };
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (int left in (int[])[image.Left + image.Width / 4, image.Left + image.Width / 2])
            {
                g.DrawLines(pen, [new Point(left, middle - reach), new Point(left + reach, middle), new Point(left, middle + reach)]);
            }

            g.SmoothingMode = smoothing;
        }

        this.Outline(g, image);
    }
}
