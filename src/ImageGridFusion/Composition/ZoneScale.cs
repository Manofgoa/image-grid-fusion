namespace ImageGridFusion.Composition;

/// <summary>
/// The wheel's scaling of a resizable zone — a rectangle set by four bars, in fractions of its bounds:
/// the crop's kept part in the image as seen, the blur's sharp rectangle in the cell (RULES.md § On-Cell
/// Handles). Both sides scale alike, so the zone keeps its ratio; growing slides it back inside its
/// bounds, then stops at the largest rectangle at that ratio within them; shrinking stops at a minimum.
/// </summary>
public static class ZoneScale
{
    /// <summary>What one notch of the wheel scales the zone's sides by: up grows it, down shrinks it.</summary>
    public const double NotchFactor = 1.05;

    /// <summary>
    /// <paramref name="zone"/> scaled by <paramref name="notches"/> of the wheel (up when positive) around
    /// <paramref name="anchor"/> — its own center when <c>null</c>, else a point of the bounds, clamped to
    /// them — no larger than its bounds, no side below <paramref name="minWidth"/> / <paramref name="minHeight"/>,
    /// and shifted back inside where it crosses an edge. All in fractions of the bounds.
    /// </summary>
    public static RectangleF Scaled(RectangleF zone, int notches, PointF? anchor, double minWidth, double minHeight)
    {
        if (notches == 0 || zone.Width <= 0 || zone.Height <= 0)
        {
            return zone;
        }

        double width = zone.Width;
        double height = zone.Height;
        double factor = Math.Pow(NotchFactor, notches);
        if (notches > 0)
        {
            // The largest rectangle at that ratio within the bounds: reached exactly, never passed.
            factor = Math.Min(factor, Math.Max(1, Math.Min(1 / width, 1 / height)));
        }
        else
        {
            // A zone already below the minimum is not grown by a notch down.
            factor = Math.Max(factor, Math.Min(1, Math.Max(minWidth / width, minHeight / height)));
        }

        double x = Math.Clamp(anchor?.X ?? zone.X + width / 2, 0, 1);
        double y = Math.Clamp(anchor?.Y ?? zone.Y + height / 2, 0, 1);
        double scaledWidth = Math.Min(1, width * factor);
        double scaledHeight = Math.Min(1, height * factor);
        double left = Math.Clamp(x + (zone.X - x) * factor, 0, 1 - scaledWidth);
        double top = Math.Clamp(y + (zone.Y - y) * factor, 0, 1 - scaledHeight);
        return new RectangleF((float)left, (float)top, (float)scaledWidth, (float)scaledHeight);
    }
}
