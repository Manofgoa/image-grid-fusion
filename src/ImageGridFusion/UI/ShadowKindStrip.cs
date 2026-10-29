using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// A text layer's shadow kind (workfiles/20261029-text-layer-style-presets.md § New Settings): Halo, a
/// darker band all around the white letter, or Drop, the same band moved down to the right.
/// </summary>
internal sealed class ShadowKindStrip : SelectionStrip<ShadowKind>
{
    public ShadowKindStrip()
        : base(ShadowKind.Halo)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    // A click on the pressed kind turns the shadow on too.
    protected override bool PicksSelected => true;

    protected override string Label(ShadowKind kind) => kind.ToString();

    protected override string Tip(ShadowKind kind) => kind == ShadowKind.Halo
        ? "Halo — a soft shadow all around the letters, as memes have it"
        : "Drop — a soft shadow down to the right of the letters";

    protected override void PaintThumbnail(Graphics g, ShadowKind kind, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 2 / 5, height = box.Height / 2;
        var letter = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        int band = Math.Max(2, height / 5);
        var shadow = kind == ShadowKind.Halo ? Rectangle.Inflate(letter, band, band) : letter with { X = letter.X + band, Y = letter.Y + band };
        this.Fill(g, Color.FromArgb(160, Color.Black), shadow);
        this.Fill(g, SystemColors.Window, letter);
        this.Outline(g, letter);
    }
}
