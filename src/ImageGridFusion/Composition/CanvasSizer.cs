namespace ImageGridFusion.Composition;

/// <summary>
/// Picks the canvas size at which no image is downscaled, at the output format's ratio, its longer
/// side clamped to [1200, 4096]. Cell sizes are proportional to the canvas width, so each image's
/// applied scale is linear in it.
/// </summary>
public static class CanvasSizer
{
    public const int MinLongSide = 1200;
    public const int MaxLongSide = 4096;

    /// <param name="ratio">The canvas's width ÷ height: the output format's.</param>
    public static Size Compute(IReadOnlyList<Size> images, GridLayout layout, double ratio)
    {
        if (layout.Count != images.Count)
        {
            throw new ArgumentException($"Layout {layout.Id} holds {layout.Count} images, not {images.Count}.", nameof(layout));
        }

        var fractions = layout.CellFractions();
        double widest = 0;
        for (int i = 0; i < images.Count; i++)
        {
            // Scale of the image on a canvas 1 px wide: drawing it 1:1 needs a canvas 1 / scale wide.
            double cellWidth = fractions[i].Width;
            double cellHeight = fractions[i].Height / ratio;
            double scaleAtUnitWidth = FitCalculator.Scale(cellWidth, cellHeight, images[i]);
            widest = Math.Max(widest, 1 / scaleAtUnitWidth);
        }

        double longSide = ratio >= 1 ? widest : widest / ratio;
        int side = (int)Math.Round(Math.Clamp(longSide, MinLongSide, MaxLongSide));
        return ratio >= 1 ? new Size(side, GridLayout.HeightFor(side, ratio)) : new Size(GridLayout.WidthFor(side, ratio), side);
    }

    /// <summary>The smallest canvas at <paramref name="ratio"/>: its longer side <see cref="MinLongSide"/>.</summary>
    public static Size Smallest(double ratio) =>
        ratio >= 1 ? new Size(MinLongSide, GridLayout.HeightFor(MinLongSide, ratio)) : new Size(GridLayout.WidthFor(MinLongSide, ratio), MinLongSide);
}
