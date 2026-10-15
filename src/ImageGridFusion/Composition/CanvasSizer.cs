namespace ImageGridFusion.Composition;

/// <summary>
/// What the canvas sizing reads of one image: its size as drawn (<see cref="ImageLook.Shown"/>) at its
/// unzoomed resolution, its look, and whether it <paramref name="Sharpens"/> — a PDF, a thumbnail or a
/// text renders again at any resolution, so it has none of its own to keep.
/// </summary>
public readonly record struct SizedImage(Size Shown, ImageLook Look, bool Sharpens);

/// <summary>
/// Picks the canvas size at which no image is downscaled, at the output format's ratio, its longer
/// side clamped to [1200, cap]: <see cref="MaxLongSide"/> for a PNG, lower for a PNG max size entry, a
/// JPEG for sharing or a video export (workfiles/20261015-png-native-resolution.md). An image is read as
/// it is drawn — its zoom (at time 0, an Animations motion's starting state), its fine angle, the
/// Borders' gap — so a zoomed-out image widens the canvas and a zoomed-in one does not.
/// </summary>
public static class CanvasSizer
{
    public const int MinLongSide = 1200;

    /// <summary>The PNG's cap.</summary>
    public const int MaxLongSide = 8192;

    /// <summary>The cap of a JPEG for sharing, and the largest Video size choice.</summary>
    public const int MaxSharedLongSide = 4096;

    /// <summary>
    /// The drawn scale is linear in the canvas but for the Borders' rounding: a few passes at the
    /// candidate size absorb it.
    /// </summary>
    private const int RefinePasses = 3;

    /// <param name="ratio">The canvas's width ÷ height: the output format's.</param>
    /// <param name="borders">The Borders shrinking the cells by their gap; <c>null</c> while off.</param>
    /// <param name="maxLongSide">The longer side's maximum: <see cref="MaxLongSide"/>, or a lower cap.</param>
    public static Size Compute(IReadOnlyList<SizedImage> images, GridLayout layout, double ratio, GridBorders? borders, int maxLongSide = MaxLongSide)
    {
        if (layout.Count != images.Count)
        {
            throw new ArgumentException($"Layout {layout.Id} holds {layout.Count} images, not {images.Count}.", nameof(layout));
        }

        int cap = Math.Max(MinLongSide, Math.Min(maxLongSide, MaxLongSide));
        var canvas = Smallest(ratio);
        double longSide = MinLongSide * Need(images, layout, canvas, borders);
        for (int pass = 0; pass < RefinePasses; pass++)
        {
            canvas = At(Clamped(longSide, cap), ratio);
            double next = LongSide(canvas) * Need(images, layout, canvas, borders);
            if (Math.Abs(next - longSide) < 1)
            {
                break;
            }

            longSide = next;
        }

        return At(Clamped(Math.Ceiling(longSide), cap), ratio);
    }

    /// <summary>The smallest canvas at <paramref name="ratio"/>: its longer side <see cref="MinLongSide"/>.</summary>
    public static Size Smallest(double ratio) => At(MinLongSide, ratio);

    /// <summary>
    /// How many times larger <paramref name="canvas"/> must be for no image to be downscaled: the largest
    /// count of source pixels per canvas pixel among the images; 0 with none to keep.
    /// </summary>
    private static double Need(IReadOnlyList<SizedImage> images, GridLayout layout, Size canvas, GridBorders? borders)
    {
        var cells = Compositor.Cells(layout, canvas, borders);
        double need = 0;
        for (int i = 0; i < images.Count; i++)
        {
            double scale = DrawnScale(cells[i], images[i]);
            if (scale > 0)
            {
                need = Math.Max(need, 1 / scale);
            }
        }

        return need;
    }

    /// <summary>
    /// Canvas pixels per source pixel of <paramref name="image"/> in <paramref name="cell"/>; 0 when it
    /// cannot be drawn. A content that sharpens is read at zoom 1, unturned: it never widens the canvas
    /// on its own, rendering sharp again at its cell's size.
    /// </summary>
    private static double DrawnScale(Rectangle cell, SizedImage image)
    {
        var shown = image.Shown;
        if (cell.Width <= 0 || cell.Height <= 0 || shown.Width <= 0 || shown.Height <= 0)
        {
            return 0;
        }

        if (image.Sharpens)
        {
            return FitCalculator.Scale(cell.Width, cell.Height, shown);
        }

        var look = image.Look;
        double zoom = look.ZoomAt(TimeSpan.Zero, cell, shown);
        var turned = FitCalculator.ComputeTurned(cell, shown, zoom, look.FocusAt(TimeSpan.Zero, cell, shown), look.FineAngle);
        return turned.Fit.Image.Width / shown.Width * turned.Scale;
    }

    private static int Clamped(double longSide, int cap) => (int)Math.Round(Math.Clamp(longSide, MinLongSide, cap));

    private static int LongSide(Size canvas) => Math.Max(canvas.Width, canvas.Height);

    private static Size At(int longSide, double ratio) =>
        ratio >= 1 ? new Size(longSide, GridLayout.HeightFor(longSide, ratio)) : new Size(GridLayout.WidthFor(longSide, ratio), longSide);
}
