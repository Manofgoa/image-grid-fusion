using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// Draws the blur effect of a cell: the cell is drawn again into a small bitmap, then stretched back
/// over the outer bands around the rectangle that stays sharp. No loop over the full-size pixels, so
/// a playing video keeps up.
/// </summary>
internal static class BlurRenderer
{
    // Size of a block, as a share of the cell's shorter side: resolution-independent, so the preview
    // and an export at another size look the same.
    private const double LightestBlock = 0.01;
    private const double StrongestBlock = 0.1;

    // Box blurs on the small bitmap: three of them come close to a gaussian.
    private const int GaussianPasses = 3;

    public static void Draw(Graphics g, Frame frame, Rectangle cell, double threshold, bool fast)
    {
        if (frame.Look is not { Blur: { } blur } look)
        {
            return;
        }

        // Every bar snapped to its edge: nothing is left to blur.
        var sharp = Rectangle.Intersect(blur.Area(cell), cell);
        if (sharp == cell || cell.Width < 1 || cell.Height < 1)
        {
            return;
        }

        double block = Math.Max(1, (LightestBlock + (StrongestBlock - LightestBlock) * blur.Intensity) * Math.Min(cell.Width, cell.Height));
        int width = Math.Max(1, (int)Math.Ceiling(cell.Width / block));
        int height = Math.Max(1, (int)Math.Ceiling(cell.Height / block));

        using var small = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var sg = Graphics.FromImage(small))
        {
            // Each call prepends: the cell is moved to the origin, then scaled down.
            sg.ScaleTransform(width / (float)cell.Width, height / (float)cell.Height);
            sg.TranslateTransform(-cell.X, -cell.Y);
            Compositor.DrawCell(sg, frame with { Look = look.WithoutEffects() }, cell, threshold, fast);
        }

        bool pixelate = blur.Kind == BlurKind.Pixelate;
        if (!pixelate)
        {
            for (int i = 0; i < GaussianPasses; i++)
            {
                BoxBlur(small);
            }
        }

        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        g.SetClip(sharp, CombineMode.Exclude);
        g.InterpolationMode = pixelate ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = pixelate ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;

        // TileFlipXY keeps the edges of the stretched bitmap opaque, as for the image itself.
        using (var attributes = new ImageAttributes())
        {
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(small, cell, 0, 0, width, height, GraphicsUnit.Pixel, attributes);
        }

        g.Restore(state);
    }

    /// <summary>A 3 × 3 box blur, in two passes, the edges repeating their last pixel.</summary>
    private static void BoxBlur(Bitmap bitmap)
    {
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var pixels = new byte[stride * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var blurred = new byte[pixels.Length];
            Pass(pixels, blurred, bitmap.Width, bitmap.Height, stride, horizontal: true);
            Pass(blurred, pixels, bitmap.Width, bitmap.Height, stride, horizontal: false);
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void Pass(byte[] source, byte[] target, int width, int height, int stride, bool horizontal)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int before = horizontal ? Offset(Math.Max(0, x - 1), y) : Offset(x, Math.Max(0, y - 1));
                int after = horizontal ? Offset(Math.Min(width - 1, x + 1), y) : Offset(x, Math.Min(height - 1, y + 1));
                int at = Offset(x, y);
                for (int c = 0; c < 4; c++)
                {
                    target[at + c] = (byte)((source[before + c] + source[at + c] + source[after + c] + 1) / 3);
                }
            }
        }

        int Offset(int x, int y) => y * stride + x * 4;
    }
}
