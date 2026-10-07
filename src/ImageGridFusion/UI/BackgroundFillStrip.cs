using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Background tab's fill modes: one thumbnail per mode, a schematic cell — an image in its middle,
/// the bands around it, the corners — its name below. A click on any one, the selected included, picks
/// it, so it turns the Background on like its other options.
/// </summary>
internal sealed class BackgroundFillStrip : SelectionStrip<BackgroundFill>
{
    // The thumbnails' colors: each edge of the image its own, the corner pixel another, the flat fill gray.
    private static readonly Color TopEdge = Color.FromArgb(55, 138, 221);
    private static readonly Color RightEdge = Color.FromArgb(186, 117, 23);
    private static readonly Color BottomEdge = Color.FromArgb(216, 90, 48);
    private static readonly Color LeftEdge = Color.FromArgb(99, 153, 34);
    private static readonly Color CornerPixel = Color.FromArgb(127, 119, 221);

    public BackgroundFillStrip()
        : base(BackgroundFill.Color)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override bool PicksSelected => true;

    protected override string Label(BackgroundFill fill) => fill switch
    {
        BackgroundFill.Color => "Color",
        BackgroundFill.CornerPixel => "Corner pixel",
        BackgroundFill.Miter => "Miter",
        _ => "Background corners",
    };

    protected override string Tip(BackgroundFill fill) => fill switch
    {
        BackgroundFill.Color => "A flat color around the image",
        BackgroundFill.CornerPixel => "The image's edge pixels stretched to the cell's edges; each corner the color of the image's corner pixel",
        BackgroundFill.Miter => "The image's edge pixels stretched to the cell's edges; each corner split on its diagonal, each half the color of its edge's pixel next to the image's corner, the corner pixel's color over the diagonal",
        _ => "The image's edge pixels stretched to the cell's edges; the corners keep the background color",
    };

    protected override void PaintThumbnail(Graphics g, BackgroundFill fill, Rectangle box, bool pressed)
    {
        var image = new Rectangle(box.X + box.Width * 3 / 10, box.Y + box.Height * 3 / 10, box.Width * 2 / 5, box.Height * 2 / 5);
        this.Fill(g, SystemColors.ControlDark, box);
        if (fill != BackgroundFill.Color)
        {
            int left = image.Left - box.Left;
            int right = box.Right - image.Right;
            int top = image.Top - box.Top;
            int bottom = box.Bottom - image.Bottom;
            this.Fill(g, TopEdge, new Rectangle(image.Left, box.Top, image.Width, top));
            this.Fill(g, BottomEdge, new Rectangle(image.Left, image.Bottom, image.Width, bottom));
            this.Fill(g, LeftEdge, new Rectangle(box.Left, image.Top, left, image.Height));
            this.Fill(g, RightEdge, new Rectangle(image.Right, image.Top, right, image.Height));

            // Each corner from the image's corner to the cell's: its row's side and its column's side.
            (Point Image, Point Cell, Color Row, Color Column)[] corners =
            [
                (new(image.Left, image.Top), new(box.Left, box.Top), TopEdge, LeftEdge),
                (new(image.Right, image.Top), new(box.Right, box.Top), TopEdge, RightEdge),
                (new(image.Left, image.Bottom), new(box.Left, box.Bottom), BottomEdge, LeftEdge),
                (new(image.Right, image.Bottom), new(box.Right, box.Bottom), BottomEdge, RightEdge),
            ];
            foreach (var (inner, outer, row, column) in corners)
            {
                if (fill == BackgroundFill.CornerPixel)
                {
                    this.Fill(g, CornerPixel, Rectangle.FromLTRB(Math.Min(inner.X, outer.X), Math.Min(inner.Y, outer.Y), Math.Max(inner.X, outer.X), Math.Max(inner.Y, outer.Y)));
                }
                else if (fill == BackgroundFill.Miter)
                {
                    this.Fill(g, row, [inner, outer, new(inner.X, outer.Y)]);
                    this.Fill(g, column, [inner, outer, new(outer.X, inner.Y)]);
                    using var line = new Pen(Color.FromArgb(128, this.Shade(CornerPixel)));
                    g.DrawLine(line, inner, outer);
                }
            }
        }

        this.Fill(g, SystemColors.Window, image);
        this.Outline(g, image);
        this.Outline(g, box);
    }
}
