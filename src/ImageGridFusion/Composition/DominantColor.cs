using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// Most frequent color of an image: pixels of a downsampled copy are quantized to 4 bits per
/// channel, then the buckets are voted on by <see cref="MostFrequent"/>.
/// </summary>
public static class DominantColor
{
    private const int SampleSide = 64;

    /// <summary>Perceptual distance (CIELAB ΔE76) under which two shades count as one color in the vote.</summary>
    private const double SameColor = 20;

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

        return MostFrequent(counts, sums) ?? Color.Black;
    }

    /// <summary>
    /// Most frequent color of quantized pixels: the populated buckets, most populated first, each join
    /// the first group whose seed is within <see cref="SameColor"/> of their mean, or seed a new one; the
    /// largest group wins with its seed's mean, its most frequent shade. Grouping keeps the many shades
    /// of a varied color from losing to a flat color held by a single bucket. None when no bucket is
    /// populated.
    /// </summary>
    internal static Color? MostFrequent(int[] counts, long[,] sums)
    {
        var groups = new List<(Lab Seed, Color Color, int Count)>();
        foreach (int bucket in Enumerable.Range(0, counts.Length).Where(i => counts[i] > 0).OrderByDescending(i => counts[i]))
        {
            int n = counts[bucket];
            var color = Color.FromArgb((int)(sums[bucket, 0] / n), (int)(sums[bucket, 1] / n), (int)(sums[bucket, 2] / n));
            var lab = Lab.Of(color);
            int group = groups.FindIndex(g => g.Seed.DistanceTo(lab) <= SameColor);
            if (group < 0)
            {
                groups.Add((lab, color, n));
            }
            else
            {
                groups[group] = groups[group] with { Count = groups[group].Count + n };
            }
        }

        return groups.Count == 0 ? null : groups.MaxBy(g => g.Count).Color;
    }
}
