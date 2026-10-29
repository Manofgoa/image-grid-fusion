using System.Drawing.Drawing2D;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>
/// Where a layer stands on the grid, in fractions of it, so it keeps its place when the canvas changes
/// size or ratio: its center (<see cref="X"/>, <see cref="Y"/>, of the grid's width and height), its size
/// (<see cref="Size"/>, of the grid's shorter side) and its <see cref="Angle"/> in degrees, clockwise. The
/// size measures the longer side of the layer's content as shown — or, for a text, one of its lines,
/// <see cref="LinePixels"/> high in its bitmap — so typing does not shrink the letters
/// (workfiles/20261029-layers.md).
/// </summary>
public sealed record LayerPlacement(double X, double Y, double Size, double Angle, double LinePixels = 0)
{
    public const double MinSize = 0.03;
    public const double MaxSize = 1.0;

    /// <summary>The size of a new image layer, and of a new text's line.</summary>
    public const double ImageSize = 0.4;
    public const double TextSize = 0.12;

    /// <summary>The layer's box on a <paramref name="canvas"/>, for a content shown at <paramref name="shown"/>: its proportions, scaled.</summary>
    public SizeF BoxSize(Size shown, Size canvas)
    {
        double side = Math.Min(canvas.Width, canvas.Height) * this.Size;
        double reference = this.LinePixels > 0 ? this.LinePixels : Math.Max(shown.Width, shown.Height);
        double scale = reference > 0 ? side / reference : 0;
        return new SizeF((float)(shown.Width * scale), (float)(shown.Height * scale));
    }

    public PointF Center(Size canvas) => new((float)(this.X * canvas.Width), (float)(this.Y * canvas.Height));

    /// <summary>From the box's own space — its center at the origin, upright — to the canvas: the caller's to dispose.</summary>
    public Matrix ToCanvas(Size canvas)
    {
        var center = this.Center(canvas);
        var matrix = new Matrix();
        matrix.Translate(center.X, center.Y);
        matrix.Rotate((float)this.Angle);
        return matrix;
    }

    /// <summary>The box's corners on the canvas, clockwise from its top left as it stands upright.</summary>
    public PointF[] Corners(Size shown, Size canvas)
    {
        var box = this.BoxSize(shown, canvas);
        PointF[] corners = [new(-box.Width / 2, -box.Height / 2), new(box.Width / 2, -box.Height / 2), new(box.Width / 2, box.Height / 2), new(-box.Width / 2, box.Height / 2)];
        using var matrix = this.ToCanvas(canvas);
        matrix.TransformPoints(corners);
        return corners;
    }

    /// <summary>Whether <paramref name="point"/> of the canvas falls in the box, turned with it.</summary>
    public bool Contains(PointF point, Size shown, Size canvas)
    {
        var box = this.BoxSize(shown, canvas);
        var center = this.Center(canvas);
        double radians = -this.Angle * Math.PI / 180;
        double dx = point.X - center.X, dy = point.Y - center.Y;
        double x = dx * Math.Cos(radians) - dy * Math.Sin(radians);
        double y = dx * Math.Sin(radians) + dy * Math.Cos(radians);
        return Math.Abs(x) <= box.Width / 2 && Math.Abs(y) <= box.Height / 2;
    }

    /// <summary>Moved by <paramref name="delta"/> pixels of the canvas, its center kept inside the grid.</summary>
    public LayerPlacement MovedBy(SizeF delta, Size canvas) => this with
    {
        X = Math.Clamp(this.X + delta.Width / Math.Max(1, canvas.Width), 0, 1),
        Y = Math.Clamp(this.Y + delta.Height / Math.Max(1, canvas.Height), 0, 1),
    };

    /// <summary>
    /// A copy's place: its center moved by <paramref name="step"/> of the grid to the right and down — the other
    /// way on an axis where that would leave the grid — so it never lands on its original
    /// (workfiles/20261029-duplicate-layer.md § Where the Copy Goes).
    /// </summary>
    public LayerPlacement Beside(double step) => this with
    {
        X = this.X + step <= 1 ? this.X + step : Math.Max(0, this.X - step),
        Y = this.Y + step <= 1 ? this.Y + step : Math.Max(0, this.Y - step),
    };

    public LayerPlacement WithSize(double size) => this with { Size = Math.Clamp(size, MinSize, MaxSize) };

    /// <summary>Turned to <paramref name="angle"/>, brought within (-180°, 180°].</summary>
    public LayerPlacement WithAngle(double angle)
    {
        double turned = angle % 360;
        return this with { Angle = turned > 180 ? turned - 360 : turned <= -180 ? turned + 360 : turned };
    }
}

/// <summary>
/// A layer of the Layers global effect: a content laid over the whole grid — a picture, a GIF, a video, or
/// a text drawn into a picture — with the look of its cell effects and its placement. A floating cell:
/// <see cref="Compositor.DrawCell"/> draws it into its box (workfiles/20261029-layers.md).
/// </summary>
/// <param name="Image">Its content; its <see cref="SourceImage.Look"/> is kept equal to <paramref name="Look"/>.</param>
/// <param name="Text">The text and its style for a text layer, drawn into <paramref name="Image"/>; <c>null</c> for any other.</param>
/// <param name="Visible">Unchecked in the layer list, it is neither drawn, played nor heard, its settings kept.</param>
public sealed record Layer(SourceImage Image, ImageLook Look, LayerPlacement Place, string? Text = null, LayerTextStyle? Style = null, bool Visible = true)
{
    /// <summary>The narrowest and widest a text box may be, in lines.</summary>
    public const double MinTextBox = 1;
    public const double MaxTextBox = 400;

    public bool IsText => this.Text is not null;

    /// <summary>
    /// A text layer's size mode: the width of the box its lines wrap in, in lines; <c>null</c> for the automatic
    /// size, the box fitting the text (workfiles/20261029-text-layer-alignment.md § Size Mode). Never part of
    /// the style.
    /// </summary>
    public double? TextBox { get; init; }

    /// <summary>The content's size as shown — cropped, then turned — which the box takes the proportions of.</summary>
    public Size Shown => this.Look.Shown(this.Image.Bitmap.Size);

    /// <summary>What the layer draws now: its frame, its look, its motion at <paramref name="time"/> on the grid's clock.</summary>
    public Frame FrameAt(TimeSpan time) => new(this.Image.DrawnBitmap, this.Image.BandColor, this.Look, time, this.Image.Sharpening, this.Image.Sharpens);

    /// <summary>
    /// A layer's default look: none of the cell effects, the Background off — the content's transparency
    /// kept (workfiles/20261029-layers.md § Cell Effects on a Layer).
    /// </summary>
    public static ImageLook DefaultLook => ImageLook.None.TurnOff(ImageEffect.Background);
}

/// <summary>
/// The Layers global effect: its layers, the first one drawn first, the last one on top. Off at start-up,
/// with none; not persisted. A new list for every change, so a step of the undo history holds the one it saw.
/// </summary>
public sealed record GridLayers(IReadOnlyList<Layer> Items)
{
    public static readonly GridLayers Initial = new([]);

    public int Count => this.Items.Count;

    /// <summary>The layers drawn — the visible ones, in their order —, <c>null</c> when none is.</summary>
    public GridLayers? Shown => this.Items.Any(l => l.Visible) ? new GridLayers([.. this.Items.Where(l => l.Visible)]) : null;

    public GridLayers With(int index, Layer layer)
    {
        var items = this.Items.ToList();
        items[index] = layer;
        return new GridLayers(items);
    }

    public GridLayers Adding(Layer layer) => new([.. this.Items, layer]);

    /// <summary><paramref name="layer"/> put at <paramref name="index"/> in the stacking order, the layers from there one place higher.</summary>
    public GridLayers Inserting(int index, Layer layer)
    {
        var items = this.Items.ToList();
        items.Insert(Math.Clamp(index, 0, items.Count), layer);
        return new GridLayers(items);
    }

    /// <summary>The index of the layer showing <paramref name="image"/>, -1 for none.</summary>
    public int IndexOf(SourceImage image)
    {
        for (int i = 0; i < this.Items.Count; i++)
        {
            if (this.Items[i].Image == image)
            {
                return i;
            }
        }

        return -1;
    }

    public GridLayers Without(int index)
    {
        var items = this.Items.ToList();
        items.RemoveAt(index);
        return new GridLayers(items);
    }

    /// <summary>The layer at <paramref name="index"/> moved to <paramref name="to"/> in the stacking order.</summary>
    public GridLayers Moving(int index, int to)
    {
        var items = this.Items.ToList();
        var layer = items[index];
        items.RemoveAt(index);
        items.Insert(Math.Clamp(to, 0, items.Count), layer);
        return new GridLayers(items);
    }

    /// <summary>The topmost visible layer under <paramref name="point"/> of the canvas, -1 for none.</summary>
    public int At(PointF point, Size canvas)
    {
        for (int i = this.Items.Count - 1; i >= 0; i--)
        {
            var layer = this.Items[i];
            if (layer.Visible && layer.Place.Contains(point, layer.Shown, canvas))
            {
                return i;
            }
        }

        return -1;
    }
}
