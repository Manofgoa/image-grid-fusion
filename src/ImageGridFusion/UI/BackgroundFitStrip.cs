namespace ImageGridFusion.UI;

/// <summary>
/// How the Grid background's image lies on the canvas, as its options choose it
/// (workfiles/20261104-grid-background-image.md § Fitting): Fill and Contain are its Zoom effect's fit modes, Stretch
/// and Tile its own placement.
/// </summary>
internal enum BackgroundFit
{
    Fill,
    Contain,
    Stretch,
    Tile,
}

/// <summary>
/// The Grid background image's placements, one thumbnail each in the Background's vocabulary — the canvas a grey
/// box, the image white —, the one in use pressed; none while the image has a free zoom. A click on any one picks it.
/// </summary>
internal sealed class BackgroundFitStrip : ThumbnailStrip<BackgroundFit>
{
    private BackgroundFit? _pressed = BackgroundFit.Fill;

    public BackgroundFitStrip()
        : base(Enum.GetValues<BackgroundFit>())
    {
        this.FitSize();
    }

    /// <summary>The placement shown pressed; <c>null</c> for none.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public BackgroundFit? Pressed
    {
        get => this._pressed;
        set
        {
            if (value != this._pressed)
            {
                this._pressed = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(BackgroundFit value) => value == this._pressed;

    protected override string Label(BackgroundFit value) => value.ToString();

    protected override string Tip(BackgroundFit value) => value switch
    {
        BackgroundFit.Fill => "Covers the whole grid, its ratio kept, cut where it overflows",
        BackgroundFit.Contain => "Shown whole, its ratio kept, the fill showing around it",
        BackgroundFit.Stretch => "Exactly the grid, its ratio not kept",
        _ => "Repeated over the grid from its top-left corner; its zoom sets the tiles' size",
    };

    protected override void PaintThumbnail(Graphics g, BackgroundFit value, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        var state = g.Save();
        g.SetClip(Rectangle.Inflate(box, -1, -1));
        switch (value)
        {
            case BackgroundFit.Fill:
                // A square image covering the wider canvas: cut at the top and the bottom.
                this.PaintImage(g, new Rectangle(box.X + 1, box.Y + box.Height / 2 - (box.Width - 2) / 2, box.Width - 2, box.Width - 2), ContentAlignment.TopLeft);
                break;
            case BackgroundFit.Contain:
            {
                int side = box.Height - 8;
                this.PaintImage(g, new Rectangle(box.X + (box.Width - side) / 2, box.Y + 4, side, side), ContentAlignment.TopLeft);
                break;
            }

            case BackgroundFit.Stretch:
                this.PaintImage(g, Rectangle.Inflate(box, -3, -3), ContentAlignment.TopLeft);
                break;
            default:
            {
                int width = (box.Width - 4) / 3, height = (box.Height - 4) / 2;
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        this.PaintImage(g, new Rectangle(box.X + 2 + column * width, box.Y + 2 + row * height, width - 1, height - 1), ContentAlignment.TopLeft);
                    }
                }

                break;
            }
        }

        g.Restore(state);
        this.Outline(g, box);
    }
}
