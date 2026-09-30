namespace ImageGridFusion.Composition;

/// <summary>
/// The crop effect of an image: the part of it kept, which then becomes the image — fitted into its
/// cell by the fitting rule, its background computed on it, the canvas sized on it (RULES.md). The
/// sides are fractions of the image as loaded, before any rotation or flip, so the crop keeps the same
/// content when the image is turned or flipped; <see cref="Seen"/> and <see cref="WithSeen"/> give and
/// take them as the image is seen.
/// </summary>
public sealed record CropEffect
{
    /// <summary>A tenth cut off each edge, no ratio kept.</summary>
    public static readonly CropEffect Default = new();

    /// <summary>The ratios of the options toolbar's buttons, width : height as the image is seen, <see cref="Free"/> aside.</summary>
    public static readonly IReadOnlyList<double> Ratios = [1, 4 / 3.0, 16 / 9.0, 9 / 16.0];

    // Two ratios closer than this are the same button.
    private const double RatioTolerance = 0.001;

    /// <summary>From 0, the left edge of the image as loaded, to 1, its right edge.</summary>
    public double Left { get; private init; } = 0.1;

    /// <summary>From 0, the top edge of the image as loaded, to 1, its bottom edge.</summary>
    public double Top { get; private init; } = 0.1;

    public double Right { get; private init; } = 0.9;

    public double Bottom { get; private init; } = 0.9;

    /// <summary>Width : height kept, in pixels of the image as loaded; <c>null</c> while free.</summary>
    public double? Ratio { get; private init; }

    /// <summary>The kept part in pixels of a bitmap of <paramref name="size"/>, at least one pixel each way.</summary>
    public Rectangle Pixels(Size size)
    {
        int left = Math.Clamp((int)Math.Round(Left * size.Width), 0, size.Width - 1);
        int top = Math.Clamp((int)Math.Round(Top * size.Height), 0, size.Height - 1);
        int right = Math.Clamp((int)Math.Round(Right * size.Width), left + 1, size.Width);
        int bottom = Math.Clamp((int)Math.Round(Bottom * size.Height), top + 1, size.Height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>The kept part in fractions of the image as <paramref name="look"/> shows it: rotated, then flipped.</summary>
    public RectangleF Seen(ImageLook look)
    {
        var a = ToSeen(new PointF((float)Left, (float)Top), look);
        var b = ToSeen(new PointF((float)Right, (float)Bottom), look);
        return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    /// <summary>The same crop, its kept part <paramref name="seen"/> given in fractions of the image as <paramref name="look"/> shows it.</summary>
    public CropEffect WithSeen(RectangleF seen, ImageLook look)
    {
        var a = FromSeen(new PointF(seen.Left, seen.Top), look);
        var b = FromSeen(new PointF(seen.Right, seen.Bottom), look);
        return this with
        {
            Left = Math.Clamp(Math.Min(a.X, b.X), 0, 1),
            Top = Math.Clamp(Math.Min(a.Y, b.Y), 0, 1),
            Right = Math.Clamp(Math.Max(a.X, b.X), 0, 1),
            Bottom = Math.Clamp(Math.Max(a.Y, b.Y), 0, 1),
        };
    }

    /// <summary>The ratio kept as <paramref name="look"/> shows the image: inverted by a quarter turn; <c>null</c> while free.</summary>
    public double? SeenRatio(ImageLook look) => Ratio is { } ratio ? (look.SwapsAxes ? 1 / ratio : ratio) : null;

    /// <summary>
    /// Keeps <paramref name="seenRatio"/> (<c>null</c>: free), as the image of <paramref name="size"/>
    /// is seen through <paramref name="look"/>: the kept part becomes the largest rectangle at that
    /// ratio within it, around its center.
    /// </summary>
    public CropEffect WithRatio(double? seenRatio, ImageLook look, Size size)
    {
        if (seenRatio is not { } ratio)
        {
            return this with { Ratio = null };
        }

        var kept = this with { Ratio = look.SwapsAxes ? 1 / ratio : ratio };
        var pixels = look.Oriented(size);
        var seen = Seen(look);
        double width = seen.Width * pixels.Width;
        double height = seen.Height * pixels.Height;
        if (width / height > ratio)
        {
            width = height * ratio;
        }
        else
        {
            height = width / ratio;
        }

        float w = (float)(width / pixels.Width);
        float h = (float)(height / pixels.Height);
        float cx = seen.X + seen.Width / 2;
        float cy = seen.Y + seen.Height / 2;
        return kept.WithSeen(new RectangleF(cx - w / 2, cy - h / 2, w, h), look);
    }

    /// <summary>
    /// Moves one side, seen through <paramref name="look"/>, to <paramref name="value"/> (a fraction of
    /// the image as seen), kept within the image and at least <paramref name="minGap"/> away from the
    /// opposite side. While a ratio is kept, the two sides across follow around the kept part's center,
    /// and the side stops where they would leave the image.
    /// </summary>
    public CropEffect WithSeenSide(BarSide side, double value, double minGap, ImageLook look, Size size)
    {
        var seen = Seen(look);
        double left = seen.Left, top = seen.Top, right = seen.Right, bottom = seen.Bottom;
        switch (side)
        {
            case BarSide.Left:
                left = Math.Max(0, Math.Min(value, right - minGap));
                break;
            case BarSide.Top:
                top = Math.Max(0, Math.Min(value, bottom - minGap));
                break;
            case BarSide.Right:
                right = Math.Min(1, Math.Max(value, left + minGap));
                break;
            default:
                bottom = Math.Min(1, Math.Max(value, top + minGap));
                break;
        }

        if (SeenRatio(look) is { } ratio)
        {
            // In fractions, the ratio is scaled by the image's own shape.
            var pixels = look.Oriented(size);
            double shape = ratio * pixels.Height / pixels.Width;
            bool vertical = side is BarSide.Left or BarSide.Right;
            if (vertical)
            {
                double center = (seen.Top + seen.Bottom) / 2;
                double half = Math.Min((right - left) / shape / 2, Math.Min(center, 1 - center));
                double width = half * 2 * shape;
                if (side == BarSide.Left)
                {
                    left = right - width;
                }
                else
                {
                    right = left + width;
                }

                top = center - half;
                bottom = center + half;
            }
            else
            {
                double center = (seen.Left + seen.Right) / 2;
                double half = Math.Min((bottom - top) * shape / 2, Math.Min(center, 1 - center));
                double height = half * 2 / shape;
                if (side == BarSide.Top)
                {
                    top = bottom - height;
                }
                else
                {
                    bottom = top + height;
                }

                left = center - half;
                right = center + half;
            }
        }

        return WithSeen(RectangleF.FromLTRB((float)left, (float)top, (float)right, (float)bottom), look);
    }

    /// <summary>
    /// The crop once its image is turned a quarter turn: it turns along, being in the image's own frame,
    /// and so does its ratio as seen — freed when no button offers it turned (4:3 would be 3:4).
    /// </summary>
    public CropEffect QuarterTurned() => Ratio is { } ratio && !(IsListed(ratio) && IsListed(1 / ratio)) ? this with { Ratio = null } : this;

    /// <summary>Whether <paramref name="ratio"/> is the one of a button (or free).</summary>
    public static bool IsListed(double? ratio) => ratio is not { } r || Ratios.Any(listed => Same(listed, r));

    public static bool Same(double a, double b) => Math.Abs(a - b) < RatioTolerance;

    /// <summary>A point of the image as loaded, where <paramref name="look"/> shows it: turned clockwise, then flipped.</summary>
    private static PointF ToSeen(PointF point, ImageLook look)
    {
        for (int i = 0; i < look.Rotation / 90; i++)
        {
            point = new PointF(1 - point.Y, point.X);
        }

        return new PointF(look.FlipX ? 1 - point.X : point.X, look.FlipY ? 1 - point.Y : point.Y);
    }

    private static PointF FromSeen(PointF point, ImageLook look)
    {
        point = new PointF(look.FlipX ? 1 - point.X : point.X, look.FlipY ? 1 - point.Y : point.Y);
        for (int i = 0; i < look.Rotation / 90; i++)
        {
            point = new PointF(point.Y, 1 - point.X);
        }

        return point;
    }
}
