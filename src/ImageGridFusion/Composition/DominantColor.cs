using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// Most frequent color of an image: pixels of a downsampled copy are quantized to 4 bits per
/// channel, the most populated bucket wins, and its pixels are averaged.
/// </summary>
public static class DominantColor
{
    private const int SampleSide = 64;

    public static Color Compute(Bitmap image)
    {
        double factor = Math.Min(1, SampleSide / (double)Math.Max(image.Width, image.Height));
        int width = Math.Max(1, (int)(image.Width * factor));
        int height = Math.Max(1, (int)(image.Height * factor));

        using var sample = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sample))
        {
            // Nearest neighbor keeps real pixel colors instead of blending them.
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, new Rectangle(0, 0, width, height));
        }

        var data = sample.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[data.Stride * height];
        try
        {
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally
        {
            sample.UnlockBits(data);
        }

        var counts = new int[4096];
        var sums = new long[4096, 3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * data.Stride + x * 4;
                byte b = pixels[i], gr = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                if (a == 0)
                {
                    continue;
                }

                int bucket = (r >> 4) << 8 | (gr >> 4) << 4 | b >> 4;
                counts[bucket]++;
                sums[bucket, 0] += r;
                sums[bucket, 1] += gr;
                sums[bucket, 2] += b;
            }
        }

        int best = Array.IndexOf(counts, counts.Max());
        int n = counts[best];
        if (n == 0)
        {
            return Color.Black;
        }

        return Color.FromArgb((int)(sums[best, 0] / n), (int)(sums[best, 1] / n), (int)(sums[best, 2] / n));
    }
}
