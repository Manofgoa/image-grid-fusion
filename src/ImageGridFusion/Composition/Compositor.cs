using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>Draws the images into their cells, at full resolution or at any preview size.</summary>
public static class Compositor
{
    /// <summary>Renders the final image at the size given by <see cref="CanvasSizer"/>.</summary>
    public static Bitmap Render(IReadOnlyList<SourceImage> images, double threshold = FitCalculator.DefaultCropThreshold)
    {
        var canvas = CanvasSizer.Compute(images.Select(i => i.Size).ToList(), threshold);
        var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, images, canvas, threshold);
        return bitmap;
    }

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>.</summary>
    public static void Draw(Graphics g, IReadOnlyList<SourceImage> images, Size canvas, double threshold = FitCalculator.DefaultCropThreshold)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        // TileFlipXY avoids the semi-transparent halo GDI+ leaves on the edges of scaled images.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);

        var cells = GridLayout.Cells(images.Count, canvas);
        using var clip = g.Clip;
        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            var cell = cells[i];

            // Bands, and transparent pixels, show the dominant color.
            using (var brush = new SolidBrush(image.Dominant))
            {
                g.FillRectangle(brush, cell);
            }

            var fit = FitCalculator.Compute(cell, image.Size, threshold);
            g.SetClip(cell, CombineMode.Intersect);
            g.DrawImage(
                image.Bitmap,
                Rectangle.Round(fit.Destination),
                fit.Source.X, fit.Source.Y, fit.Source.Width, fit.Source.Height,
                GraphicsUnit.Pixel,
                attributes);
            g.Clip = clip;
        }
    }
}
