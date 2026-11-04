using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>One thumbnail of the Crop tab: a ratio to keep — none for Free — or the 100 % or the Keep action.</summary>
internal readonly record struct CropChoice(double? Ratio, bool Whole = false, bool Keep = false);

/// <summary>
/// The Crop tab's options: Free then the listed ratios, the one the kept part keeps pressed, each drawing
/// the kept part at its ratio; then, set apart, two actions, never pressed: 100 %, bringing the kept part
/// back to the whole image, and Keep, making the kept part the cell's image (workfiles/20261029-image-upscale.md
/// § Crop to a New Image).
/// </summary>
internal sealed class CropStrip : ThumbnailStrip<CropChoice>
{
    private double? _kept;
    private string? _keepRefusal;

    public CropStrip()
        : base([new CropChoice(null), .. CropEffect.Ratios.Select(ratio => new CropChoice(ratio)), new CropChoice(null, Whole: true), new CropChoice(null, Keep: true)])
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

    /// <summary>Why Keep cannot make the kept part the cell's image now; <c>null</c> while it can.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? KeepRefusal
    {
        get => this._keepRefusal;
        set
        {
            if (value != this._keepRefusal)
            {
                this._keepRefusal = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool Picks(CropChoice choice) => !choice.Keep || this._keepRefusal is null;

    protected override bool IsPressed(CropChoice choice) => !choice.Whole && !choice.Keep && (choice.Ratio is { } ratio
        ? this._kept is { } kept && CropEffect.Same(ratio, kept)
        : this._kept is null);

    protected override bool IsApart(CropChoice choice) => choice.Whole;

    protected override string Label(CropChoice choice) => choice switch
    {
        { Whole: true } => "100 %",
        { Keep: true } => Strings.UiCropKeep,
        { Ratio: { } ratio } => RatioText(ratio),
        _ => Strings.UiCropFree,
    };

    protected override string Tip(CropChoice choice) => choice switch
    {
        { Whole: true } => Strings.UiCropWholeTip,
        { Keep: true } => this._keepRefusal is { } refusal ? refusal
            : Strings.UiCropKeepTip,
        { Ratio: { } ratio } => Texts.Format(Strings.UiCropRatioTip, RatioText(ratio)),
        _ => Strings.UiCropFreeTip,
    };

    protected override void PaintThumbnail(Graphics g, CropChoice choice, Rectangle box, bool pressed)
    {
        if (choice.Keep)
        {
            this.PaintKeep(g, box);
            return;
        }

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

    // Keep: the kept part, small in its image, then filling the cell, an arrow between them; faded while refused.
    private void PaintKeep(Graphics g, Rectangle box)
    {
        this.PaintCell(g, box);
        int pad = Math.Max(2, box.Height / 8);
        var whole = new Rectangle(box.X + pad, box.Y + pad, box.Width / 2 - 2 * pad, box.Height - 2 * pad);
        this.Fill(g, SystemColors.Window, whole);
        var kept = new Rectangle(whole.X + whole.Width / 4, whole.Y + whole.Height / 4, whole.Width / 2, whole.Height / 2);
        this.Fill(g, Marker, kept);
        this.Outline(g, whole);
        var made = new Rectangle(box.X + box.Width / 2 + pad, box.Y + pad, box.Width / 2 - 2 * pad, box.Height - 2 * pad);
        this.Fill(g, Marker, made);
        this.Outline(g, made);
        using (var arrow = new Pen(this.Shade(SystemColors.ControlDarkDark), 1.5f) { CustomEndCap = new AdjustableArrowCap(3, 3) })
        {
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawLine(arrow, kept.Right, kept.Y + kept.Height / 2, made.Left - 1, made.Y + made.Height / 2);
            g.SmoothingMode = smoothing;
        }

        if (this._keepRefusal is not null)
        {
            using var veil = new SolidBrush(Color.FromArgb(160, SystemColors.Control));
            g.FillRectangle(veil, box);
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
