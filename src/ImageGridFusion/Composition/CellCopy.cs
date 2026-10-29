using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>
/// One cell's image, or one layer, rendered alone (workfiles/20261029-copy-single-cell.md): drawn by
/// <see cref="Compositor.DrawCell"/> with every effect of its look but the Background, at its native size,
/// cut to the image as drawn — clipped to its cell, turned with a layer — and transparent around it.
/// </summary>
public static class CellCopy
{
    /// <summary>
    /// Renders <paramref name="frame"/> into a cell of <paramref name="cellShape"/>'s ratio, turned by
    /// <paramref name="angle"/> degrees (a layer's; 0 for a cell), scaled so that the image is not downscaled
    /// as drawn, the result's longer side at most <paramref name="maxLongSide"/>; <c>null</c> when nothing
    /// would be drawn.
    /// </summary>
    public static Bitmap? Render(Frame frame, SizeF cellShape, double angle = 0, int maxLongSide = CanvasSizer.MaxLongSide)
    {
        if (cellShape.Width <= 0 || cellShape.Height <= 0)
        {
            return null;
        }

        frame = frame with { Look = (frame.Look ?? ImageLook.None).WithBackground(null) };
        var reference = Centered(cellShape, 1);
        double scale = CanvasSizer.DrawnScale(reference, frame.Sizing);
        if (scale <= 0)
        {
            return null;
        }

        // Native: as many times larger as the image is downscaled in the reference cell; then capped.
        double factor = 1 / scale;
        var cell = Centered(cellShape, factor);
        var crop = Cut(frame, cell, angle);
        int longer = Math.Max(crop.Width, crop.Height);
        if (longer > maxLongSide)
        {
            factor *= (double)maxLongSide / longer;
            cell = Centered(cellShape, factor);
            crop = Cut(frame, cell, angle);
        }

        if (crop.Width < 1 || crop.Height < 1)
        {
            return null;
        }

        var bitmap = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.TranslateTransform(-crop.X, -crop.Y);
        g.RotateTransform((float)angle);
        Compositor.DrawCell(g, frame, cell);
        return bitmap;
    }

    /// <summary><paramref name="shape"/> times <paramref name="factor"/>, centered on the origin, as a layer's box is.</summary>
    private static Rectangle Centered(SizeF shape, double factor)
    {
        int width = Math.Max(1, (int)Math.Round(shape.Width * factor));
        int height = Math.Max(1, (int)Math.Round(shape.Height * factor));
        return new Rectangle(-width / 2, -height / 2, width, height);
    }

    /// <summary>
    /// The part of the output the image covers: the box it is drawn in — its fine angle included — clipped
    /// to <paramref name="cell"/>, turned by <paramref name="angle"/>, in whole pixels.
    /// </summary>
    private static Rectangle Cut(Frame frame, Rectangle cell, double angle)
    {
        var look = frame.Look ?? ImageLook.None;
        var turned = FitCalculator.ComputeTurned(cell, frame.Size, look.ZoomAt(frame.Time, cell, frame.Size), look.FocusAt(frame.Time, cell, frame.Size), look.FineAngle);
        var drawn = RectangleF.Intersect(turned.Bounds, cell);
        if (drawn.Width <= 0 || drawn.Height <= 0)
        {
            return Rectangle.Empty;
        }

        if (angle == 0)
        {
            return Rectangle.Round(drawn);
        }

        PointF[] corners = [new(drawn.Left, drawn.Top), new(drawn.Right, drawn.Top), new(drawn.Right, drawn.Bottom), new(drawn.Left, drawn.Bottom)];
        using (var turn = new Matrix())
        {
            turn.Rotate((float)angle);
            turn.TransformPoints(corners);
        }

        float left = corners.Min(p => p.X), top = corners.Min(p => p.Y);
        float right = corners.Max(p => p.X), bottom = corners.Max(p => p.Y);
        return Rectangle.FromLTRB((int)Math.Floor(left), (int)Math.Floor(top), (int)Math.Ceiling(right), (int)Math.Ceiling(bottom));
    }
}
