namespace ImageGridFusion.Composition;

/// <summary>Part of the source to draw, where to draw it in the canvas, and where the whole image lands.</summary>
public readonly record struct Fit(RectangleF Source, RectangleF Destination, RectangleF Image);

/// <summary>
/// Scales an image to fill its cell, cropping at most <see cref="CropThreshold"/> of the source along
/// the overflowing axis (split evenly on both sides). Past the threshold, the image is cropped exactly
/// to it and centered, leaving bands on the other axis.
/// </summary>
public static class FitCalculator
{
    /// <summary>Fixed: zooming and moving the image past its cell's edges crop more, or less.</summary>
    public const double CropThreshold = 0.15;

    /// <summary>Share of the cell's width and height a moved image always keeps covering, so it can be grabbed back.</summary>
    public const double MinCoveredShare = 0.1;

    public static double Scale(double cellWidth, double cellHeight, Size image)
    {
        double sx = cellWidth / image.Width;
        double sy = cellHeight / image.Height;
        double fill = Math.Max(sx, sy);
        double fit = Math.Min(sx, sy);
        return Math.Min(fill, fit / (1 - CropThreshold));
    }

    public static Fit Compute(Rectangle cell, Size image) => Compute(cell, image, 1, new PointF(0.5f, 0.5f));

    /// <summary>
    /// Same fit, scaled by <paramref name="zoom"/>: the image is placed with <paramref name="focus"/>
    /// (fractions of the image) at the center of the cell — see <see cref="Place"/> — and the part
    /// drawn is what of it falls in the cell; the rest of the cell shows the bands.
    /// </summary>
    public static Fit Compute(Rectangle cell, Size image, double zoom, PointF focus)
    {
        var drawn = DrawnSize(cell, image, zoom);
        var placed = Place(cell, drawn, focus);
        var destination = RectangleF.Intersect(placed, cell);
        double scale = drawn.Width / image.Width;
        var source = new RectangleF(
            (float)((destination.X - placed.X) / scale),
            (float)((destination.Y - placed.Y) / scale),
            (float)(destination.Width / scale),
            (float)(destination.Height / scale));
        return new Fit(source, destination, placed);
    }

    /// <summary>Size of the whole image once drawn in <paramref name="cell"/> at <paramref name="zoom"/>.</summary>
    public static SizeF DrawnSize(Rectangle cell, Size image, double zoom)
    {
        double scale = Scale(cell.Width, cell.Height, image) * zoom;
        return new SizeF((float)(image.Width * scale), (float)(image.Height * scale));
    }

    /// <summary>
    /// Where the whole image of <paramref name="drawn"/> size lands: <paramref name="focus"/> at the
    /// center of the cell — beyond 0…1 when the image is moved past the cell's edges — then moved
    /// just enough to keep covering <see cref="MinCoveredShare"/> of the cell on each axis.
    /// </summary>
    public static RectangleF Place(Rectangle cell, SizeF drawn, PointF focus)
    {
        float coveredX = Math.Min((float)(cell.Width * MinCoveredShare), drawn.Width);
        float coveredY = Math.Min((float)(cell.Height * MinCoveredShare), drawn.Height);
        float x = cell.X + cell.Width / 2f - focus.X * drawn.Width;
        float y = cell.Y + cell.Height / 2f - focus.Y * drawn.Height;
        return new RectangleF(
            Math.Clamp(x, cell.Left + coveredX - drawn.Width, cell.Right - coveredX),
            Math.Clamp(y, cell.Top + coveredY - drawn.Height, cell.Bottom - coveredY),
            drawn.Width,
            drawn.Height);
    }

    /// <summary>
    /// Positions of the image's top-left corner between its edge stops, as a rectangle: where the
    /// image covers the cell on an axis it overflows, and lies inside it on an axis it does not fill.
    /// </summary>
    public static RectangleF Stops(Rectangle cell, SizeF drawn)
    {
        float right = cell.Right - drawn.Width;
        float bottom = cell.Bottom - drawn.Height;
        return RectangleF.FromLTRB(
            Math.Min(cell.Left, right), Math.Min(cell.Top, bottom),
            Math.Max(cell.Left, right), Math.Max(cell.Top, bottom));
    }

    /// <summary>Focus that draws the image of <paramref name="drawn"/> size with its top-left corner at <paramref name="origin"/>.</summary>
    public static PointF FocusAt(Rectangle cell, SizeF drawn, PointF origin) => new(
        (cell.X + cell.Width / 2f - origin.X) / drawn.Width,
        (cell.Y + cell.Height / 2f - origin.Y) / drawn.Height);

    /// <summary>The focus, moved just enough for the image to lie between its edge stops; see <see cref="Stops"/>.</summary>
    public static PointF WithinStops(Rectangle cell, Size image, double zoom, PointF focus)
    {
        var drawn = DrawnSize(cell, image, zoom);
        var placed = Place(cell, drawn, focus);
        var stops = Stops(cell, drawn);
        return FocusAt(cell, drawn, new PointF(Math.Clamp(placed.X, stops.Left, stops.Right), Math.Clamp(placed.Y, stops.Top, stops.Bottom)));
    }
}
