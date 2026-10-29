using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// A mask a model gave for an image: an opacity, 0 to 255, over the whole image as loaded — before any
/// rotation, flip or crop — at its own resolution, stretched over the image when applied. Immutable, so
/// an export thread may read it. See workfiles/20261029-cutout.md § The Mask.
/// </summary>
public sealed class CutoutMask
{
    public CutoutMask(int width, int height, byte[] alpha)
    {
        this.Width = width;
        this.Height = height;
        this.Alpha = alpha;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>One opacity per pixel, row after row.</summary>
    public byte[] Alpha { get; }

    public long MemoryBytes => this.Alpha.LongLength;

    /// <summary>
    /// <paramref name="source"/> cut out by the mask: a new bitmap, its alpha multiplied by the mask —
    /// stretched to its size, its edge grown or eaten, softened and inverted as <paramref name="edges"/>
    /// says, each a share of the longer side, so a bitmap rendered at another size looks the same.
    /// </summary>
    public Bitmap Apply(Bitmap source, CutoutEffect edges)
    {
        int width = source.Width, height = source.Height;
        var mask = this.Stretched(width, height);
        int longer = Math.Max(width, height);
        int grow = (int)Math.Round(Math.Abs(edges.Grow) * CutoutEffect.MaxEdge * longer);
        if (grow > 0)
        {
            Extreme(mask, width, height, grow, max: edges.Grow > 0);
        }

        // Three box blurs come close to a gaussian; their radii add up to the width softened.
        int soften = (int)Math.Round(edges.Soften * CutoutEffect.MaxEdge * longer / 3);
        if (soften > 0)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(mask, width, height, soften);
            }
        }

        if (edges.Invert)
        {
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = (byte)(255 - mask[i]);
            }
        }

        var cut = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var area = new Rectangle(0, 0, width, height);
        var read = source.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var write = cut.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[width * 4];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(read.Scan0 + y * read.Stride, row, 0, row.Length);
                    int at = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int alpha = x * 4 + 3;
                        row[alpha] = (byte)((row[alpha] * mask[at + x] + 127) / 255);
                    }

                    Marshal.Copy(row, 0, write.Scan0 + y * write.Stride, row.Length);
                }
            }
            finally
            {
                cut.UnlockBits(write);
            }
        }
        catch
        {
            cut.Dispose();
            throw;
        }
        finally
        {
            source.UnlockBits(read);
        }

        return cut;
    }

    /// <summary>The mask stretched to <paramref name="width"/> × <paramref name="height"/>, bilinear.</summary>
    private byte[] Stretched(int width, int height)
    {
        var result = new byte[width * height];
        var (x0, x1, wx) = Samples(width, this.Width);
        var (y0, y1, wy) = Samples(height, this.Height);
        for (int y = 0; y < height; y++)
        {
            int top = y0[y] * this.Width, bottom = y1[y] * this.Width;
            float fy = wy[y];
            int at = y * width;
            for (int x = 0; x < width; x++)
            {
                float fx = wx[x];
                float upper = this.Alpha[top + x0[x]] + (this.Alpha[top + x1[x]] - this.Alpha[top + x0[x]]) * fx;
                float lower = this.Alpha[bottom + x0[x]] + (this.Alpha[bottom + x1[x]] - this.Alpha[bottom + x0[x]]) * fx;
                result[at + x] = (byte)(upper + (lower - upper) * fy + 0.5f);
            }
        }

        return result;
    }

    // For each of the target's n pixels, the two source pixels it lies between and its weight toward the second.
    private static (int[] First, int[] Second, float[] Weight) Samples(int n, int source)
    {
        var first = new int[n];
        var second = new int[n];
        var weight = new float[n];
        float scale = (float)source / n;
        for (int i = 0; i < n; i++)
        {
            float at = Math.Clamp((i + 0.5f) * scale - 0.5f, 0, source - 1);
            first[i] = (int)at;
            second[i] = Math.Min(first[i] + 1, source - 1);
            weight[i] = at - first[i];
        }

        return (first, second, weight);
    }

    /// <summary>
    /// The largest (<paramref name="max"/>) or smallest value within <paramref name="radius"/> of each
    /// pixel, a square around it: the mask grown or eaten. Rows then columns, each in linear time
    /// (van Herk / Gil-Werman), whatever the radius.
    /// </summary>
    private static void Extreme(byte[] mask, int width, int height, int radius, bool max)
    {
        int longest = Math.Max(width, height);
        var line = new byte[longest];
        var filtered = new byte[longest];
        var padded = new byte[longest + 2 * radius];
        var forward = new byte[padded.Length];
        var backward = new byte[padded.Length];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(mask, y * width, line, 0, width);
            Extreme1D(line, filtered, width, radius, max, padded, forward, backward);
            Array.Copy(filtered, 0, mask, y * width, width);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                line[y] = mask[y * width + x];
            }

            Extreme1D(line, filtered, height, radius, max, padded, forward, backward);
            for (int y = 0; y < height; y++)
            {
                mask[y * width + x] = filtered[y];
            }
        }
    }

    private static void Extreme1D(byte[] source, byte[] target, int n, int radius, bool max, byte[] padded, byte[] forward, byte[] backward)
    {
        // Past the ends, the value the operation ignores.
        byte none = max ? (byte)0 : (byte)255;
        int window = 2 * radius + 1;
        int length = n + 2 * radius;
        for (int i = 0; i < length; i++)
        {
            padded[i] = i < radius || i >= n + radius ? none : source[i - radius];
        }

        for (int i = 0; i < length; i++)
        {
            forward[i] = i % window == 0 ? padded[i] : Pick(forward[i - 1], padded[i], max);
        }

        for (int i = length - 1; i >= 0; i--)
        {
            backward[i] = i == length - 1 || (i + 1) % window == 0 ? padded[i] : Pick(backward[i + 1], padded[i], max);
        }

        for (int i = 0; i < n; i++)
        {
            target[i] = Pick(backward[i], forward[i + window - 1], max);
        }
    }

    private static byte Pick(byte a, byte b, bool max) => max ? Math.Max(a, b) : Math.Min(a, b);

    /// <summary>Each pixel the mean of the square of <paramref name="radius"/> around it, the edges repeated: rows then columns, a running sum.</summary>
    private static void BoxBlur(byte[] mask, int width, int height, int radius)
    {
        int longest = Math.Max(width, height);
        var line = new byte[longest];
        var blurred = new byte[longest];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(mask, y * width, line, 0, width);
            BoxBlur1D(line, blurred, width, radius);
            Array.Copy(blurred, 0, mask, y * width, width);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                line[y] = mask[y * width + x];
            }

            BoxBlur1D(line, blurred, height, radius);
            for (int y = 0; y < height; y++)
            {
                mask[y * width + x] = blurred[y];
            }
        }
    }

    private static void BoxBlur1D(byte[] source, byte[] target, int n, int radius)
    {
        int window = 2 * radius + 1;
        int sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            sum += source[Math.Clamp(i, 0, n - 1)];
        }

        for (int i = 0; i < n; i++)
        {
            target[i] = (byte)((sum + window / 2) / window);
            sum += source[Math.Min(i + radius + 1, n - 1)] - source[Math.Max(i - radius, 0)];
        }
    }
}

/// <summary>
/// What the pick's model read of an image's page — its embedding, once for every click — and the size
/// the image was read at, its longer side the model's: the points are given in it.
/// </summary>
public sealed record PickEmbedding(int Page, float[] Values, Size Read)
{
    public long MemoryBytes => this.Values.LongLength * sizeof(float);
}
