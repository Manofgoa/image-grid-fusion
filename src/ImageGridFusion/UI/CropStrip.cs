using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>One thumbnail of the Crop tab: a ratio to keep — none for Free — or the 100 % action.</summary>
internal readonly record struct CropChoice(double? Ratio, bool Whole = false);

/// <summary>
/// The Crop tab's options: Free then the listed ratios, the one the kept part keeps pressed, each drawing
/// the kept part at its ratio; then, set apart, 100 % — an action, never pressed — bringing the kept part
/// back to the whole image.
/// </summary>
internal sealed class CropStrip : ThumbnailStrip<CropChoice>
{
    private double? _kept;

    public CropStrip()
        : base([new CropChoice(null), .. CropEffect.Ratios.Select(ratio => new CropChoice(ratio)), new CropChoice(null, Whole: true)])
    {
        this.FitSize();
    }

    /// <summary>The ratio the kept part keeps, as the image is seen; null when it is free.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? Kept
    {
        get => this._kept;
        set
        {
            if (value != this._kept)
            {
                this._kept = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(CropChoice choice) => !choice.Whole && (choice.Ratio is { } ratio
        ? this._kept is { } kept && CropEffect.Same(ratio, kept)
        : this._kept is null);

    protected override bool IsApart(CropChoice choice) => choice.Whole;

    protected override string Label(CropChoice choice) => choice switch
    {
        { Whole: true } => "100 %",
        { Ratio: { } ratio } => RatioText(ratio),
        _ => "Free",
    };

    protected override string Tip(CropChoice choice) => choice switch
    {
        { Whole: true } => "Keeps the whole image: the bars back on its edges, the ratio freed",
        { Ratio: { } ratio } => $"Keeps the kept part at {RatioText(ratio)}",
        _ => "Frees the kept part's ratio",
    };

    protected override void PaintThumbnail(Graphics g, CropChoice choice, Rectangle box, bool pressed)
    {
        if (choice.Whole)
        {
            // The whole image kept, the crop bars along its edges.
            this.Fill(g, SystemColors.Window, box);
            int inset = this.LogicalToDeviceUnits(4);
            var bars = Rectangle.Inflate(box, -inset, -inset);
            using (var halo = new Pen(this.Shade(GridPreview.HelperHalo), this.LogicalToDeviceUnits(4)))
            {
                g.DrawRectangle(halo, bars);
            }

            using (var bar = new Pen(this.Shade(GridPreview.HelperColor), this.LogicalToDeviceUnits(2)))
            {
                g.DrawRectangle(bar, bars);
            }

            this.Outline(g, box);
            return;
        }

        this.PaintCell(g, box);
        if (choice.Ratio is { } ratio)
        {
            // The largest kept part at the ratio within the cell, a margin around it.
            int margin = box.Height / 8;
            int width = Math.Min(box.Width - 2 * margin, (int)Math.Round((box.Height - 2 * margin) * ratio));
            int height = (int)Math.Round(width / ratio);
            var kept = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
            this.Fill(g, SystemColors.Window, kept);
            this.Outline(g, kept);
        }
        else
        {
            // A kept part of no particular ratio, dashed.
            int width = box.Width * 4 / 7;
            int height = box.Height * 3 / 5;
            var kept = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
            this.Fill(g, SystemColors.Window, kept);
            using var dashed = this.OutlinePen();
            dashed.DashStyle = DashStyle.Dash;
            g.DrawRectangle(dashed, kept.X, kept.Y, kept.Width - 1, kept.Height - 1);
        }
    }

    private static string RatioText(double ratio) => ratio switch
    {
        1 => "1:1",
        < 1 => $"{Math.Round(16 * ratio)}:16",
        _ when CropEffect.Same(ratio, 4 / 3.0) => "4:3",
        _ => $"{Math.Round(9 * ratio)}:9",
    };
}
