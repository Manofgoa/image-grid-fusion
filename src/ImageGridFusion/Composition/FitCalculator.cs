namespace ImageGridFusion.Composition;

/// <summary>Part of the source to draw, and where to draw it in the canvas.</summary>
public readonly record struct Fit(RectangleF Source, RectangleF Destination);

/// <summary>
/// Scales an image to fill its cell, cropping at most <c>threshold</c> of the source along the
/// overflowing axis (split evenly on both sides). Past the threshold, the image is cropped exactly
/// to it and centered, leaving bands on the other axis.
/// </summary>
public static class FitCalculator
{
    public const double DefaultCropThreshold = 0.15;

    public static double Scale(double cellWidth, double cellHeight, Size image, double threshold = DefaultCropThreshold)
    {
        double sx = cellWidth / image.Width;
        double sy = cellHeight / image.Height;
        double fill = Math.Max(sx, sy);
        double fit = Math.Min(sx, sy);
        return Math.Min(fill, fit / (1 - Math.Clamp(threshold, 0, 0.99)));
    }

    public static Fit Compute(Rectangle cell, Size image, double threshold = DefaultCropThreshold) =>
        Compute(cell, image, threshold, 1, new PointF(0.5f, 0.5f));

    /// <summary>
    /// Same fit, scaled by <paramref name="zoom"/>: the part drawn is centered on <paramref name="focus"/>
    /// (fractions of the image), moved just enough for the cell never to show beyond the image on an
    /// axis where it overflows. An axis where the image does not fill the cell stays centered.
    /// </summary>
    public static Fit Compute(Rectangle cell, Size image, double threshold, double zoom, PointF focus)
    {
        double scale = Scale(cell.Width, cell.Height, image, threshold) * zoom;

        double sourceWidth = Math.Min(image.Width, cell.Width / scale);
        double sourceHeight = Math.Min(image.Height, cell.Height / scale);
        double destinationWidth = sourceWidth * scale;
        double destinationHeight = sourceHeight * scale;

        var source = new RectangleF(
            (float)Math.Clamp(focus.X * image.Width - sourceWidth / 2, 0, image.Width - sourceWidth),
            (float)Math.Clamp(focus.Y * image.Height - sourceHeight / 2, 0, image.Height - sourceHeight),
            (float)sourceWidth,
            (float)sourceHeight);
        var destination = new RectangleF(
            (float)(cell.X + (cell.Width - destinationWidth) / 2),
            (float)(cell.Y + (cell.Height - destinationHeight) / 2),
            (float)destinationWidth,
            (float)destinationHeight);
        return new Fit(source, destination);
    }
}
