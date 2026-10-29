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
    /// says, each a share of the longer side, so a bitmap rendered at another size looks the same — or,
    /// with a background kept, the part left out treated under the part kept.
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

        var pixels = Pixels(source);
        if (edges.Background == CutoutBackground.Transparent)
        {
            for (int i = 0; i < mask.Length; i++)
            {
                pixels[i * 4 + 3] = (byte)((pixels[i * 4 + 3] * mask[i] + 127) / 255);
            }
        }
        else
        {
            Mix(pixels, Treated(pixels, width, height, edges), mask);
        }

        var cut = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var write = cut.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width * 4, write.Scan0 + y * write.Stride, width * 4);
            }
        }
        finally
        {
            cut.UnlockBits(write);
        }

        return cut;
    }

    // The pixels of a bitmap — blue, green, red, alpha — row after row.
    private static byte[] Pixels(Bitmap source)
    {
        int width = source.Width, height = source.Height;
        var pixels = new byte[width * height * 4];
        var read = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(read.Scan0 + y * read.Stride, pixels, y * width * 4, width * 4);
            }
        }
        finally
        {
            source.UnlockBits(read);
        }

        return pixels;
    }

    /// <summary>
    /// The image as the part left out shows it (workfiles/20261029-cutout.md § The Background Kept):
    /// see-through, under a color, blurred or in squares — the blur and the squares averaged with the
    /// colors weighted by their alpha, so a transparent pixel lends no color.
    /// </summary>
    private static byte[] Treated(byte[] pixels, int width, int height, CutoutEffect effect)
    {
        var treated = (byte[])pixels.Clone();
        int longer = Math.Max(width, height);
        switch (effect.Background)
        {
            case CutoutBackground.Opacity:
                for (int i = 3; i < treated.Length; i += 4)
                {
                    treated[i] = (byte)Math.Round(treated[i] * effect.Amount);
                }

                break;
            case CutoutBackground.Color:
                int amount = (int)Math.Round(effect.Amount * 255);
                byte[] color = [effect.Color.B, effect.Color.G, effect.Color.R];
                for (int i = 0; i < treated.Length; i += 4)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        treated[i + c] = (byte)((treated[i + c] * (255 - amount) + color[c] * amount + 127) / 255);
                    }
                }

                break;
            case CutoutBackground.Blur:
                // Three box blurs come close to a gaussian; their radii add up to the strength.
                int radius = (int)Math.Round(effect.Amount * CutoutEffect.MaxBlur * longer / 3);
                if (radius > 0)
                {
                    Premultiply(treated);
                    for (int pass = 0; pass < 3; pass++)
                    {
                        BoxBlurColors(treated, width, height, radius);
                    }

                    Unpremultiply(treated);
                }

                break;
            case CutoutBackground.Pixelate:
                Pixelate(treated, width, height, Math.Max(2, (int)Math.Round(effect.Amount * CutoutEffect.MaxPixel * longer)));
                break;
        }

        return treated;
    }

    /// <summary>The kept part over the treated one, mixed by the mask, their colors weighted by their alpha: <paramref name="pixels"/> becomes the result.</summary>
    private static void Mix(byte[] pixels, byte[] treated, byte[] mask)
    {
        for (int i = 0; i < mask.Length; i++)
        {
            int at = i * 4;
            int keptAlpha = pixels[at + 3] * mask[i];
            int leftAlpha = treated[at + 3] * (255 - mask[i]);
            int alpha = keptAlpha + leftAlpha;
            if (alpha == 0)
            {
                pixels[at + 3] = 0;
                continue;
            }

            for (int c = 0; c < 3; c++)
            {
                pixels[at + c] = (byte)((pixels[at + c] * keptAlpha + treated[at + c] * leftAlpha + alpha / 2) / alpha);
            }

            pixels[at + 3] = (byte)((alpha + 127) / 255);
        }
    }

    private static void Premultiply(byte[] pixels)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            for (int c = 0; c < 3; c++)
            {
                pixels[i + c] = (byte)((pixels[i + c] * alpha + 127) / 255);
            }
        }
    }

    private static void Unpremultiply(byte[] pixels)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            for (int c = 0; c < 3; c++)
            {
                pixels[i + c] = alpha == 0 ? (byte)0 : (byte)Math.Min(255, (pixels[i + c] * 255 + alpha / 2) / alpha);
            }
        }
    }

    // Each channel of every pixel the mean of the square of radius around it, the edges repeated: rows then columns.
    private static void BoxBlurColors(byte[] pixels, int width, int height, int radius)
    {
        int longest = Math.Max(width, height);
        var line = new byte[longest];
        var blurred = new byte[longest];
        for (int c = 0; c < 4; c++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    line[x] = pixels[(y * width + x) * 4 + c];
                }

                BoxBlur1D(line, blurred, width, radius);
                for (int x = 0; x < width; x++)
                {
                    pixels[(y * width + x) * 4 + c] = blurred[x];
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    line[y] = pixels[(y * width + x) * 4 + c];
                }

                BoxBlur1D(line, blurred, height, radius);
                for (int y = 0; y < height; y++)
                {
                    pixels[(y * width + x) * 4 + c] = blurred[y];
                }
            }
        }
    }

    // Squares of side, each the mean of its pixels, the colors weighted by their alpha.
    private static void Pixelate(byte[] pixels, int width, int height, int side)
    {
        var mean = new byte[4];
        for (int top = 0; top < height; top += side)
        {
            for (int left = 0; left < width; left += side)
            {
                int bottom = Math.Min(top + side, height), right = Math.Min(left + side, width);
                long b = 0, g = 0, r = 0, a = 0;
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        int at = (y * width + x) * 4;
                        int alpha = pixels[at + 3];
                        b += pixels[at] * alpha;
                        g += pixels[at + 1] * alpha;
                        r += pixels[at + 2] * alpha;
                        a += alpha;
                    }
                }

                int count = (bottom - top) * (right - left);
                mean[0] = a == 0 ? (byte)0 : (byte)(b / a);
                mean[1] = a == 0 ? (byte)0 : (byte)(g / a);
                mean[2] = a == 0 ? (byte)0 : (byte)(r / a);
                mean[3] = (byte)(a / count);
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        Array.Copy(mean, 0, pixels, (y * width + x) * 4, 4);
                    }
                }
            }
        }
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
