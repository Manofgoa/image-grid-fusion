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
    /// The plain background of <paramref name="source"/> left out (workfiles/20261029-cutout-enclosed-content.md
    /// § Plain Mode): the pixels within the effect's tolerance of the border's most frequent color, or
    /// transparent, connected to a border through such pixels — the outline's gaps closed first — at the
    /// bitmap's resolution; everything they enclose kept, a kept pixel touching them faded by its distance.
    /// </summary>
    public static CutoutMask Plain(Bitmap source, CutoutEffect effect)
    {
        int width = source.Width, height = source.Height;
        var pixels = Pixels(source);
        var color = BorderColor(pixels, width, height);
        int tolerance = (int)Math.Round(effect.Tolerance * CutoutEffect.MaxTolerance);
        var distance = new int[width * height];
        var near = new bool[distance.Length];
        for (int i = 0; i < distance.Length; i++)
        {
            int at = i * 4;
            distance[i] = Math.Max(Math.Abs(pixels[at] - color.B), Math.Max(Math.Abs(pixels[at + 1] - color.G), Math.Abs(pixels[at + 2] - color.R)));
            near[i] = pixels[at + 3] == 0 || distance[i] <= tolerance;
        }

        var background = Flood(near, width, height, GapRadius(effect.CloseGaps, width, height));

        // The antialiased edge: a kept pixel touching the background fades from the tolerance to twice it.
        int ramp = Math.Max(tolerance, 32);
        var alpha = new byte[distance.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (background[i])
                {
                    continue;
                }

                bool edge = (x > 0 && background[i - 1]) || (x < width - 1 && background[i + 1])
                    || (y > 0 && background[i - width]) || (y < height - 1 && background[i + width]);
                alpha[i] = edge ? (byte)Math.Clamp((distance[i] - tolerance) * 255 / ramp, 0, 255) : (byte)255;
            }
        }

        return new CutoutMask(width, height, alpha);
    }

    /// <summary>
    /// <paramref name="source"/> cut out by the mask: a new bitmap, its alpha multiplied by the mask —
    /// its holes filled at its own resolution, then stretched to the bitmap's size, its edge grown or eaten,
    /// softened and inverted as <paramref name="edges"/> says, each a share of the longer side, so a bitmap
    /// rendered at another size looks the same — or, with a background kept, the part left out treated
    /// under the part kept.
    /// </summary>
    public Bitmap Apply(Bitmap source, CutoutEffect edges)
    {
        int width = source.Width, height = source.Height;
        var filled = edges.FillHoles && edges.Mode != CutoutMode.Plain ? this.Filled(edges.CloseGaps) : this;
        var mask = filled.Stretched(width, height);
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
        if (!edges.KeepsBackground)
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
    /// The image as the part left out shows it (workfiles/20261029-cutout.md § The Background Kept): its
    /// treatments one after the other — blurred, in squares, under a color, see-through — the blur and
    /// the squares averaged with the colors weighted by their alpha, so a transparent pixel lends no color.
    /// </summary>
    private static byte[] Treated(byte[] pixels, int width, int height, CutoutEffect effect)
    {
        var treated = (byte[])pixels.Clone();
        int longer = Math.Max(width, height);
        foreach (var treatment in effect.Treatments)
        {
            double amount = effect.AmountOf(treatment);
            switch (treatment)
            {
                case CutoutBackground.Opacity:
                    for (int i = 3; i < treated.Length; i += 4)
                    {
                        treated[i] = (byte)Math.Round(treated[i] * amount);
                    }

                    break;
                case CutoutBackground.Color:
                    int share = (int)Math.Round(amount * 255);
                    byte[] color = [effect.Color.B, effect.Color.G, effect.Color.R];
                    for (int i = 0; i < treated.Length; i += 4)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            treated[i + c] = (byte)((treated[i + c] * (255 - share) + color[c] * share + 127) / 255);
                        }
                    }

                    break;
                case CutoutBackground.Blur:
                    // Three box blurs come close to a gaussian; their radii add up to the strength.
                    int radius = (int)Math.Round(amount * CutoutEffect.MaxBlur * longer / 3);
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
                    Pixelate(treated, width, height, Math.Max(2, (int)Math.Round(amount * CutoutEffect.MaxPixel * longer)));
                    break;
            }
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

    /// <summary>
    /// The mask with its holes kept (workfiles/20261029-cutout-enclosed-content.md § Fill Holes): every
    /// pixel left out — below 128 — that the part kept encloses, its outline's gaps up to
    /// <paramref name="closeGaps"/> closed, set to 255.
    /// </summary>
    private CutoutMask Filled(double closeGaps)
    {
        var left = new bool[this.Alpha.Length];
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = this.Alpha[i] < 128;
        }

        var background = Flood(left, this.Width, this.Height, GapRadius(closeGaps, this.Width, this.Height));
        var alpha = (byte[])this.Alpha.Clone();
        for (int i = 0; i < alpha.Length; i++)
        {
            if (left[i] && !background[i])
            {
                alpha[i] = 255;
            }
        }

        return new CutoutMask(this.Width, this.Height, alpha);
    }

    // Half the widest gap closed, in pixels of an image of that size: a share of its longer side.
    private static int GapRadius(double closeGaps, int width, int height) =>
        (int)Math.Round(closeGaps * CutoutEffect.MaxGap * Math.Max(width, height) / 2);

    /// <summary>
    /// The <paramref name="open"/> pixels connected to the image's border through open pixels, 4-connected —
    /// with a <paramref name="radius"/>, the closed ones grown by it first, so a gap of up to twice it is
    /// closed, then the region reached grown back by it within the open pixels: the background comes back
    /// up to the outline, and only what the closed outline encloses stays out.
    /// </summary>
    private static bool[] Flood(bool[] open, int width, int height, int radius)
    {
        var passable = open;
        if (radius > 0)
        {
            var eroded = new byte[open.Length];
            for (int i = 0; i < open.Length; i++)
            {
                eroded[i] = open[i] ? (byte)255 : (byte)0;
            }

            Extreme(eroded, width, height, radius, max: false);
            passable = Array.ConvertAll(eroded, value => value == 255);
        }

        var reached = new bool[open.Length];
        var queue = new int[open.Length];
        int head = 0, tail = 0;
        void Seed(int i)
        {
            if (passable[i] && !reached[i])
            {
                reached[i] = true;
                queue[tail++] = i;
            }
        }

        for (int x = 0; x < width; x++)
        {
            Seed(x);
            Seed((height - 1) * width + x);
        }

        for (int y = 0; y < height; y++)
        {
            Seed(y * width);
            Seed(y * width + width - 1);
        }

        while (head < tail)
        {
            int i = queue[head++];
            int x = i % width;
            if (x > 0)
            {
                Seed(i - 1);
            }

            if (x < width - 1)
            {
                Seed(i + 1);
            }

            if (i >= width)
            {
                Seed(i - width);
            }

            if (i < open.Length - width)
            {
                Seed(i + width);
            }
        }

        if (radius == 0)
        {
            return reached;
        }

        var grown = new byte[open.Length];
        for (int i = 0; i < open.Length; i++)
        {
            grown[i] = reached[i] ? (byte)255 : (byte)0;
        }

        Extreme(grown, width, height, radius, max: true);
        for (int i = 0; i < open.Length; i++)
        {
            reached[i] = open[i] && grown[i] == 255;
        }

        return reached;
    }

    /// <summary>
    /// The plain background's color: the most frequent of the border's opaque pixels, counted in buckets of
    /// 32 levels a channel, the mean of the winning bucket taken; white when the whole border is transparent.
    /// </summary>
    private static Color BorderColor(byte[] pixels, int width, int height)
    {
        var buckets = new Dictionary<int, (int Count, long B, long G, long R)>();
        void Count(int i)
        {
            int at = i * 4;
            if (pixels[at + 3] == 0)
            {
                return;
            }

            int key = (pixels[at] >> 5) | ((pixels[at + 1] >> 5) << 3) | ((pixels[at + 2] >> 5) << 6);
            var (count, b, g, r) = buckets.GetValueOrDefault(key);
            buckets[key] = (count + 1, b + pixels[at], g + pixels[at + 1], r + pixels[at + 2]);
        }

        for (int x = 0; x < width; x++)
        {
            Count(x);
            Count((height - 1) * width + x);
        }

        for (int y = 1; y < height - 1; y++)
        {
            Count(y * width);
            Count(y * width + width - 1);
        }

        if (buckets.Count == 0)
        {
            return Color.White;
        }

        var (n, blue, green, red) = buckets.Values.MaxBy(bucket => bucket.Count);
        return Color.FromArgb((int)(red / n), (int)(green / n), (int)(blue / n));
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
