using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Cutout tab's modes: Subject, the main subject found by the model, and Pick, which arms the click
/// picking an object — pressed while armed or while the cutout is a pick. A click on either picks it, the
/// one pressed included: Subject brings the subject back, Pick arms again for a new pick.
/// </summary>
internal sealed class CutoutModeStrip : ThumbnailStrip<CutoutMode>
{
    private CutoutMode _mode;
    private bool _armed;

    public CutoutModeStrip()
        : base(Enum.GetValues<CutoutMode>())
    {
        this.FitSize();
    }

    /// <summary>The mode of the selected image's cutout.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CutoutMode Mode
    {
        get => this._mode;
        set
        {
            if (value != this._mode)
            {
                this._mode = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>Whether Pick waits for a click on the selected cell.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Armed
    {
        get => this._armed;
        set
        {
            if (value != this._armed)
            {
                this._armed = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(CutoutMode mode) =>
        mode == CutoutMode.Picked ? this._armed || this._mode == CutoutMode.Picked : !this._armed && this._mode == CutoutMode.Subject;

    protected override string Label(CutoutMode mode) => mode == CutoutMode.Subject ? "Subject" : "Pick";

    protected override string Tip(CutoutMode mode) => mode == CutoutMode.Subject
        ? "Keeps the image's main subject, found by the model; the rest becomes transparent"
        : "Click an object of the selected cell to keep it instead; Shift + click adds a part, Ctrl + click leaves one out; Escape cancels";

    protected override void PaintThumbnail(Graphics g, CutoutMode mode, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 5 / 7;
        int height = box.Height * 7 / 10;
        var image = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        this.Fill(g, SystemColors.Window, image);

        // The part kept, a blue blob: centered for the subject, aside for a pick, its point marked.
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int side = image.Height * 3 / 5;
        var blob = mode == CutoutMode.Subject
            ? new Rectangle(image.X + (image.Width - side) / 2, image.Y + (image.Height - side) / 2, side, side)
            : new Rectangle(image.Right - side - image.Width / 8, image.Y + (image.Height - side) / 2, side, side);
        using (var brush = new SolidBrush(this.Shade(Marker)))
        {
            g.FillEllipse(brush, blob);
        }

        if (mode == CutoutMode.Picked)
        {
            // A small crosshair on the blob: the click.
            var center = new Point(blob.X + blob.Width / 2, blob.Y + blob.Height / 2);
            int arm = side / 2 + 2;
            using var pen = new Pen(this.Shade(Color.FromArgb(40, 40, 40)), Math.Max(1, side / 8));
            g.DrawLine(pen, center.X - arm, center.Y, center.X + arm, center.Y);
            g.DrawLine(pen, center.X, center.Y - arm, center.X, center.Y + arm);
        }

        g.SmoothingMode = smoothing;
        this.Outline(g, image);
    }
}
