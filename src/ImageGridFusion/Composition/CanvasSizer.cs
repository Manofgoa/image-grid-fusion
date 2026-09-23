namespace ImageGridFusion.Composition;

/// <summary>
/// Picks the canvas width at which no image is downscaled, clamped to [1200, 4096].
/// Cell sizes are proportional to the canvas width, so each image's applied scale is linear in it.
/// </summary>
public static class CanvasSizer
{
    public const int MinWidth = 1200;
    public const int MaxWidth = 4096;

    public static Size Compute(IReadOnlyList<Size> images, GridLayout layout, double threshold = FitCalculator.DefaultCropThreshold)
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
            double cellHeight = fractions[i].Height * GridLayout.RatioHeight / GridLayout.RatioWidth;
            double scaleAtUnitWidth = FitCalculator.Scale(cellWidth, cellHeight, images[i], threshold);
            widest = Math.Max(widest, 1 / scaleAtUnitWidth);
        }

        int width = (int)Math.Round(Math.Clamp(widest, MinWidth, MaxWidth));
        return new Size(width, GridLayout.HeightFor(width));
    }
}
