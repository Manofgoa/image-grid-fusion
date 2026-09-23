using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>What a cell shows: a bitmap, and the color of its bands.</summary>
public readonly record struct Frame(Bitmap Bitmap, Color Dominant);

/// <summary>Draws the images into their cells, at full resolution or at any preview size.</summary>
public static class Compositor
{
    /// <summary>Renders the final image at the size given by <see cref="CanvasSizer"/>.</summary>
    public static Bitmap Render(IReadOnlyList<SourceImage> images, GridLayout layout, double threshold = FitCalculator.DefaultCropThreshold) =>
        Render(images.Select(i => new Frame(i.Bitmap, i.Dominant)).ToList(), layout, threshold);

    /// <summary>Renders frames at the size given by <see cref="CanvasSizer"/>; frame i goes into cell i.</summary>
    public static Bitmap Render(IReadOnlyList<Frame> frames, GridLayout layout, double threshold = FitCalculator.DefaultCropThreshold)
    {
        var canvas = CanvasSizer.Compute(frames.Select(f => f.Bitmap.Size).ToList(), layout, threshold);
        var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, frames, layout, canvas, threshold);
        return bitmap;
    }

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; image i goes into cell i of the layout.</summary>
    public static void Draw(Graphics g, IReadOnlyList<SourceImage> images, GridLayout layout, Size canvas, double threshold = FitCalculator.DefaultCropThreshold) =>
        Draw(g, images.Select(i => new Frame(i.Bitmap, i.Dominant)).ToList(), layout, canvas, threshold);

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

        // TileFlipXY avoids the semi-transparent halo GDI+ leaves on the edges of scaled images.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);

        // Bands, and transparent pixels, show the dominant color.
        using (var brush = new SolidBrush(frame.Dominant))
        {
            g.FillRectangle(brush, cell);
        }

        var fit = FitCalculator.Compute(cell, frame.Bitmap.Size, threshold);
        using var clip = g.Clip;
        g.SetClip(cell, CombineMode.Intersect);
        g.DrawImage(
            frame.Bitmap,
            Rectangle.Round(fit.Destination),
            fit.Source.X, fit.Source.Y, fit.Source.Width, fit.Source.Height,
            GraphicsUnit.Pixel,
            attributes);
        g.Clip = clip;
    }
}
