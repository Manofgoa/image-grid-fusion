using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>The Upscale tab's thumbnails: its two factors, then the save action.</summary>
internal enum UpscaleChoice
{
    Times2,
    Times4,
    Save,
}

/// <summary>
/// The Upscale tab's factors, ×2 and ×4 — the one in use pressed, one that would go past the PNG's cap faded and
/// refused — then, set apart, <i>Save…</i>, the action writing the upscaled image to a file, never pressed. A click on
/// a factor picks it — the one already shown included, so it still turns the Upscale on.
/// See workfiles/20261029-image-upscale.md § Effect and Options.
/// </summary>
internal sealed class UpscaleStrip : ThumbnailStrip<UpscaleChoice>
{
    private int _factor = UpscaleEffect.Default.Factor;
    private Func<int, bool> _fits = _ => true;
    private bool _saves;

    public UpscaleStrip()
        : base(Enum.GetValues<UpscaleChoice>())
    {
        this.FitSize();
    }

    /// <summary>The factor of a thumbnail; 0 for the save action.</summary>
    public static int FactorOf(UpscaleChoice choice) => choice switch
    {
        UpscaleChoice.Times2 => 2,
        UpscaleChoice.Times4 => 4,
        _ => 0,
    };

    /// <summary>
    /// Shows <paramref name="factor"/> pressed, the factors <paramref name="fits"/> refuses faded, and the save action
    /// usable while <paramref name="saves"/>.
    /// </summary>
    public void Show(int factor, Func<int, bool> fits, bool saves)
    {
        this._factor = factor;
        this._fits = fits;
        this._saves = saves;
        this.Invalidate();
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(UpscaleChoice choice) => choice != UpscaleChoice.Save && FactorOf(choice) == this._factor;

    protected override bool Picks(UpscaleChoice choice) => choice == UpscaleChoice.Save ? this._saves : this._fits(FactorOf(choice));

    protected override bool IsApart(UpscaleChoice choice) => choice == UpscaleChoice.Save;

    protected override string Label(UpscaleChoice choice) => choice == UpscaleChoice.Save ? "Save…" : $"×{FactorOf(choice)}";

    protected override string Tip(UpscaleChoice choice)
    {
        if (choice == UpscaleChoice.Save)
        {
            return this._saves
                ? "Save upscaled image… — the image enlarged, whole, as a PNG (an animated GIF for a GIF); the cell stays as it is"
                : "Save upscaled image… — turn the Upscale on first";
        }

        int factor = FactorOf(choice);
        return this._fits(factor)
            ? $"Enlarges the image {factor} times with Real-ESRGAN, computed on this computer"
            : $"Too large for ×{factor}: the image would go past {CanvasSizer.MaxLongSide} px on its longer side";
    }

    protected override void PaintThumbnail(Graphics g, UpscaleChoice choice, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int pad = Math.Max(2, box.Height / 8);
        if (choice == UpscaleChoice.Save)
        {
            this.PaintSave(g, box, pad);
            return;
        }

        // The enlargement dashed in the marker's blue, the image as loaded at its bottom left, an arrow between them.
        int factor = FactorOf(choice);
        var enlarged = new Rectangle(box.X + 2 * pad, box.Y + pad, box.Width - 4 * pad, box.Height - 2 * pad);
        var loaded = new Rectangle(enlarged.X, enlarged.Bottom - enlarged.Height / factor, Math.Max(3, enlarged.Width / factor), Math.Max(3, enlarged.Height / factor));
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
        if (!this._fits(factor))
        {
            using var veil = new SolidBrush(Color.FromArgb(160, SystemColors.Control));
            g.FillRectangle(veil, box);
        }
    }

    // The save action: the image, and a blue arrow going down into a tray beside it.
    private void PaintSave(Graphics g, Rectangle box, int pad)
    {
        var image = new Rectangle(box.X + 2 * pad, box.Y + pad, box.Width / 2 - 2 * pad, box.Height - 2 * pad);
        this.Fill(g, SystemColors.Window, image);
        this.Outline(g, image);
        int x = box.X + box.Width * 3 / 4, top = box.Y + pad, bottom = box.Bottom - 2 * pad, half = Math.Max(3, box.Width / 10);
        this.Fill(g, Marker, new Rectangle(x - half / 3, top, Math.Max(2, 2 * half / 3), bottom - top - half));
        this.Fill(g, Marker, [new Point(x - half, bottom - half), new Point(x + half, bottom - half), new Point(x, bottom)]);
        this.Fill(g, SystemColors.ControlDarkDark, new Rectangle(x - half - 1, bottom + 1, 2 * half + 2, Math.Max(1, pad / 2)));
        if (!this._saves)
        {
            using var veil = new SolidBrush(Color.FromArgb(160, SystemColors.Control));
            g.FillRectangle(veil, box);
        }
    }
}
