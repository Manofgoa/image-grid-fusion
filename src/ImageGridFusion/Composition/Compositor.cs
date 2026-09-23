using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>What a cell shows: a bitmap, what colors its bands, and the actions on the image, if any.</summary>
public readonly record struct Frame(Bitmap Bitmap, BandColor BandColor, ImageLook? Look = null)
{
    /// <summary>Size of the image as drawn: once rotated by its look.</summary>
    public Size Size => (Look ?? ImageLook.None).Oriented(Bitmap.Size);
}

/// <summary>Draws the images into their cells, at full resolution or at any preview size.</summary>
public static class Compositor
{
    /// <summary>Luminance weights of the black &amp; white action.</summary>
    private static readonly ColorMatrix GrayscaleMatrix = new(
    [
        [0.299f, 0.299f, 0.299f, 0, 0],
        [0.587f, 0.587f, 0.587f, 0, 0],
        [0.114f, 0.114f, 0.114f, 0, 0],
        [0, 0, 0, 1, 0],
        [0, 0, 0, 0, 1],
    ]);

    /// <summary>Renders the final image at the size given by <see cref="CanvasSizer"/>.</summary>
    public static Bitmap Render(IReadOnlyList<SourceImage> images, GridLayout layout, double threshold = FitCalculator.DefaultCropThreshold) =>
        Render(images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look)).ToList(), layout, threshold);

    /// <summary>Renders frames at the size given by <see cref="CanvasSizer"/>; frame i goes into cell i.</summary>
    public static Bitmap Render(IReadOnlyList<Frame> frames, GridLayout layout, double threshold = FitCalculator.DefaultCropThreshold)
    {
        var canvas = CanvasSizer.Compute(frames.Select(f => f.Size).ToList(), layout, threshold);
        var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, frames, layout, canvas, threshold);
        return bitmap;
    }

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; image i goes into cell i of the layout.</summary>
    public static void Draw(Graphics g, IReadOnlyList<SourceImage> images, GridLayout layout, Size canvas, double threshold = FitCalculator.DefaultCropThreshold) =>
        Draw(g, images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look)).ToList(), layout, canvas, threshold);

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; frame i goes into cell i of the layout.</summary>
    public static void Draw(Graphics g, IReadOnlyList<Frame> frames, GridLayout layout, Size canvas, double threshold = FitCalculator.DefaultCropThreshold)
    {
        var cells = layout.Cells(canvas);
        for (int i = 0; i < frames.Count; i++)
        {
            DrawCell(g, frames[i], cells[i], threshold);
        }
    }

    /// <summary>Draws one frame into its cell, leaving the rest of <paramref name="g"/> untouched.</summary>
    public static void DrawCell(Graphics g, Frame frame, Rectangle cell, double threshold = FitCalculator.DefaultCropThreshold)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        var look = frame.Look ?? ImageLook.None;

        // TileFlipXY avoids the semi-transparent halo GDI+ leaves on the edges of scaled images.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        if (look.Grayscale)
        {
            attributes.SetColorMatrix(GrayscaleMatrix);
        }

        var fit = FitCalculator.Compute(cell, frame.Size, threshold, look.Zoom, look.Focus);
        bool oriented = look is not { Rotation: 0, FlipX: false, FlipY: false };
        var bitmapPart = oriented ? BitmapPart(frame.Bitmap.Size, look, fit.Source) : fit.Source;

        // Bands, and transparent pixels, show the background of the part shown.
        var bands = frame.BandColor.For(bitmapPart, frame.Bitmap.Size);
        using (var brush = new SolidBrush(look.Grayscale ? Gray(bands) : bands))
        {
            g.FillRectangle(brush, cell);
        }

        var destination = Rectangle.Round(fit.Destination);
        using var clip = g.Clip;
        g.SetClip(cell, CombineMode.Intersect);
        if (!oriented)
        {
            g.DrawImage(
                frame.Bitmap,
                destination,
                fit.Source.X, fit.Source.Y, fit.Source.Width, fit.Source.Height,
                GraphicsUnit.Pixel,
                attributes);
        }
        else
        {
            DrawOriented(g, frame.Bitmap, look, fit.Source, bitmapPart, destination, attributes);
        }

        g.Clip = clip;
    }

    /// <summary>Rectangle of a bitmap of <paramref name="size"/> that the part <paramref name="source"/> of the oriented image comes from.</summary>
    private static RectangleF BitmapPart(Size size, ImageLook look, RectangleF source)
    {
        using var inverse = look.Orientation(size);
        inverse.Invert();

        PointF[] corners = [new(source.Left, source.Top), new(source.Right, source.Top), new(source.Left, source.Bottom), new(source.Right, source.Bottom)];
        inverse.TransformPoints(corners);
        return RectangleF.FromLTRB(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
    }

    /// <summary>
    /// Draws the part <paramref name="source"/> of the oriented image, which comes from
    /// <paramref name="bitmapPart"/>, into <paramref name="destination"/>: the bitmap part goes to a
    /// parallelogram whose corners follow the rotation and flips.
    /// </summary>
    private static void DrawOriented(Graphics g, Bitmap bitmap, ImageLook look, RectangleF source, RectangleF bitmapPart, Rectangle destination, ImageAttributes attributes)
    {
        using var orientation = look.Orientation(bitmap.Size);

        // Upper-left, upper-right and lower-left corners of the bitmap part, as the cell shows them.
        PointF[] points = [new(bitmapPart.Left, bitmapPart.Top), new(bitmapPart.Right, bitmapPart.Top), new(bitmapPart.Left, bitmapPart.Bottom)];
        orientation.TransformPoints(points);
        float scaleX = destination.Width / source.Width;
        float scaleY = destination.Height / source.Height;
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new PointF(destination.X + (points[i].X - source.X) * scaleX, destination.Y + (points[i].Y - source.Y) * scaleY);
        }

        g.DrawImage(bitmap, points, bitmapPart, GraphicsUnit.Pixel, attributes);
    }

    private static Color Gray(Color color)
    {
        int luminance = (int)Math.Round(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
        return Color.FromArgb(color.A, luminance, luminance, luminance);
    }
}
