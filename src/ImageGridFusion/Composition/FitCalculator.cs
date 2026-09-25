using System.Drawing.Drawing2D;

namespace ImageGridFusion.Composition;

/// <summary>Part of the source to draw, where to draw it in the canvas, and where the whole image lands.</summary>
public readonly record struct Fit(RectangleF Source, RectangleF Destination, RectangleF Image);

/// <summary>
/// An image as drawn with its fine angle: its unturned <see cref="Fit"/>, turned <see cref="Degrees"/>
/// clockwise around the cell's <see cref="Center"/> and scaled by <see cref="Scale"/>.
/// <see cref="Bounds"/> is the axis-aligned box of the turned image: its stops and its margin apply
/// to it. Without a fine angle, the fit itself.
/// </summary>
public readonly record struct TurnedFit(Fit Fit, RectangleF Bounds, float Scale, int Degrees, PointF Center)
{
    /// <summary>Maps the unturned drawing into the cell; <c>null</c> without a fine angle. The caller owns it.</summary>
    public Matrix? Transform()
    {
        if (Degrees == 0)
        {
            return null;
        }

        // Each call prepends: the image is moved to the center, scaled, turned, then moved back.
        var turn = new Matrix();
        turn.Translate(Center.X, Center.Y);
        turn.Rotate(Degrees);
        turn.Scale(Scale, Scale);
        turn.Translate(-Center.X, -Center.Y);
        return turn;
    }

    /// <summary>Focus that puts the center of <see cref="Bounds"/> at <paramref name="boundsCenter"/>, at this scale.</summary>
    public PointF FocusAt(Rectangle cell, PointF boundsCenter)
    {
        var size = Fit.Image.Size;
        var middle = FitCalculator.Unturn(Center, boundsCenter, Scale, Degrees);
        return FitCalculator.FocusAt(cell, size, new PointF(middle.X - size.Width / 2, middle.Y - size.Height / 2));
    }
}

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

    // A turned image whose center is nearly off its cell would otherwise grow without bound.
    private const double MaxCoverScale = 8;

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
    public static Fit Compute(Rectangle cell, Size image, double zoom, PointF focus) =>
        FitOf(cell, image, Place(cell, DrawnSize(cell, image, zoom), focus));

    /// <summary>
    /// The same image turned by a fine angle of <paramref name="degrees"/> clockwise, around the
    /// center of the cell. Within its stops it is zoomed just enough to keep covering the part of the
    /// cell it covers unturned; pushed beyond, it keeps the zoom it had there, its uncovered corners
    /// showing the bands. The margin it keeps covering applies to its <see cref="TurnedFit.Bounds"/>.
    /// </summary>
    public static TurnedFit ComputeTurned(Rectangle cell, Size image, double zoom, PointF focus, int degrees)
    {
        var center = new PointF(cell.X + cell.Width / 2f, cell.Y + cell.Height / 2f);
        if (degrees == 0)
        {
            var fit = Compute(cell, image, zoom, focus);
            return new TurnedFit(fit, fit.Image, 1, 0, center);
        }

        var drawn = DrawnSize(cell, image, zoom);
        var raw = Placement(cell, drawn, focus);
        var stops = Stops(cell, drawn);
        var within = raw with { X = Math.Clamp(raw.X, stops.Left, stops.Right), Y = Math.Clamp(raw.Y, stops.Top, stops.Bottom) };
        float scale = CoverScale(within, RectangleF.Intersect(within, cell), center, degrees);

        var size = TurnedSize(drawn, scale, degrees);
        var middle = Turn(center, Middle(raw), scale, degrees);
        var bounds = Covering(cell, new RectangleF(middle.X - size.Width / 2, middle.Y - size.Height / 2, size.Width, size.Height));
        var placedMiddle = Unturn(center, Middle(bounds), scale, degrees);
        var placed = new RectangleF(placedMiddle.X - drawn.Width / 2, placedMiddle.Y - drawn.Height / 2, drawn.Width, drawn.Height);
        return new TurnedFit(FitOf(cell, image, placed), bounds, scale, degrees, center);
    }

    /// <summary>The part of the image of <paramref name="image"/> size placed at <paramref name="placed"/> that falls in the cell.</summary>
    private static Fit FitOf(Rectangle cell, Size image, RectangleF placed)
    {
        var destination = RectangleF.Intersect(placed, cell);
        double scale = placed.Width / image.Width;
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
    public static RectangleF Place(Rectangle cell, SizeF drawn, PointF focus) => Covering(cell, Placement(cell, drawn, focus));

    /// <summary>The image of <paramref name="drawn"/> size with <paramref name="focus"/> at the center of the cell.</summary>
    private static RectangleF Placement(Rectangle cell, SizeF drawn, PointF focus) => new(
        cell.X + cell.Width / 2f - focus.X * drawn.Width,
        cell.Y + cell.Height / 2f - focus.Y * drawn.Height,
        drawn.Width,
        drawn.Height);

    /// <summary><paramref name="rect"/>, moved just enough to keep covering <see cref="MinCoveredShare"/> of the cell on each axis.</summary>
    private static RectangleF Covering(Rectangle cell, RectangleF rect)
    {
        float coveredX = Math.Min((float)(cell.Width * MinCoveredShare), rect.Width);
        float coveredY = Math.Min((float)(cell.Height * MinCoveredShare), rect.Height);
        return rect with
        {
            X = Math.Clamp(rect.X, cell.Left + coveredX - rect.Width, cell.Right - coveredX),
            Y = Math.Clamp(rect.Y, cell.Top + coveredY - rect.Height, cell.Bottom - coveredY),
        };
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

    /// <summary>Point <paramref name="point"/> of the unturned drawing, once turned and scaled around <paramref name="center"/>.</summary>
    internal static PointF Turn(PointF center, PointF point, float scale, int degrees)
    {
        var (cos, sin) = CosSin(degrees);
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        return new PointF((float)(center.X + scale * (dx * cos - dy * sin)), (float)(center.Y + scale * (dx * sin + dy * cos)));
    }

    /// <summary>The point of the unturned drawing that <see cref="Turn"/> brings to <paramref name="point"/>.</summary>
    internal static PointF Unturn(PointF center, PointF point, float scale, int degrees)
    {
        var (cos, sin) = CosSin(degrees);
        double dx = (point.X - center.X) / scale;
        double dy = (point.Y - center.Y) / scale;
        return new PointF((float)(center.X + dx * cos + dy * sin), (float)(center.Y - dx * sin + dy * cos));
    }

    /// <summary>Size of the axis-aligned box of an image of <paramref name="drawn"/> size, turned and scaled.</summary>
    private static SizeF TurnedSize(SizeF drawn, float scale, int degrees)
    {
        var (cos, sin) = CosSin(degrees);
        double c = Math.Abs(cos);
        double s = Math.Abs(sin);
        return new SizeF((float)(scale * (drawn.Width * c + drawn.Height * s)), (float)(scale * (drawn.Width * s + drawn.Height * c)));
    }

    private static (double Cos, double Sin) CosSin(int degrees)
    {
        double angle = degrees * Math.PI / 180;
        return (Math.Cos(angle), Math.Sin(angle));
    }

    private static PointF Middle(RectangleF rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>
    /// Smallest scale, 1 or more, for which every corner of the part <paramref name="shown"/>,
    /// brought back into the unturned <paramref name="image"/>, lies within it. A side of the image
    /// the center is beyond cannot be reached by scaling: it is left out, and the scale capped.
    /// </summary>
    private static float CoverScale(RectangleF image, RectangleF shown, PointF center, int degrees)
    {
        var (cos, sin) = CosSin(degrees);
        double scale = 1;
        foreach (var corner in new[] { new PointF(shown.Left, shown.Top), new PointF(shown.Right, shown.Top), new PointF(shown.Left, shown.Bottom), new PointF(shown.Right, shown.Bottom) })
        {
            // The corner, turned back around the center.
            double dx = corner.X - center.X;
            double dy = corner.Y - center.Y;
            scale = Math.Max(scale, Needed(dx * cos + dy * sin, image.Left - center.X, image.Right - center.X));
            scale = Math.Max(scale, Needed(-dx * sin + dy * cos, image.Top - center.Y, image.Bottom - center.Y));
        }

        return (float)Math.Min(scale, MaxCoverScale);

        static double Needed(double offset, double low, double high) =>
            offset > high && high > 0 ? offset / high
            : offset < low && low < 0 ? offset / low
            : 1;
    }

    /// <summary>The focus, moved just enough for the image to lie between its edge stops; see <see cref="Stops"/>.</summary>
    public static PointF WithinStops(Rectangle cell, Size image, double zoom, PointF focus)
    {
        var drawn = DrawnSize(cell, image, zoom);
        var placed = Place(cell, drawn, focus);
        var stops = Stops(cell, drawn);
        return FocusAt(cell, drawn, new PointF(Math.Clamp(placed.X, stops.Left, stops.Right), Math.Clamp(placed.Y, stops.Top, stops.Bottom)));
    }

    /// <summary>The same with a fine angle: the stops are those of the turned image's box; see <see cref="ComputeTurned"/>.</summary>
    public static PointF WithinStops(Rectangle cell, Size image, double zoom, PointF focus, int degrees)
    {
        if (degrees == 0)
        {
            return WithinStops(cell, image, zoom, focus);
        }

        var turned = ComputeTurned(cell, image, zoom, focus, degrees);
        var bounds = turned.Bounds;
        var stops = Stops(cell, bounds.Size);
        float x = Math.Clamp(bounds.X, stops.Left, stops.Right);
        float y = Math.Clamp(bounds.Y, stops.Top, stops.Bottom);
        return turned.FocusAt(cell, new PointF(x + bounds.Width / 2, y + bounds.Height / 2));
    }
}
