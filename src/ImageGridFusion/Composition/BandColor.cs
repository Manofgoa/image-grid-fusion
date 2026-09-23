using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// Color of the bands of a cell: the background of the part of the image the cell shows, when at
/// least three of its sides carry one uniform color, otherwise the <see cref="Dominant"/> color of the
/// whole image. Keeps a downsampled copy of the frame it was computed on, so the part shown (cell,
/// zoom, focus) can change without that frame, and an animation keeps its color while playing.
/// </summary>
public sealed class BandColor
{
    private const int SampleSide = 512;

    /// <summary>Depth of a side's band, as a share of the perpendicular dimension of the part shown.</summary>
    private const double BandDepth = 0.02;

    /// <summary>Perceptual distance (CIELAB ΔE76) under which two colors are the same background.</summary>
    private const double Tolerance = 10;

    /// <summary>Share of a band's opaque pixels that must match its color for the side to vote.</summary>
    private const double UniformShare = 0.9;

    private const int VotesNeeded = 3;

    private static readonly double[] Linear = BuildLinearTable();

    private readonly int[] _pixels;
    private readonly int _width;
    private readonly int _height;

    // The preview redraws the same part at every animation tick: the last answer is kept.
    // Swapped as a whole, so an export thread can read it while the preview draws.
    private Cached? _last;

    private BandColor(Color dominant, int[] pixels, int width, int height)
    {
        Dominant = dominant;
        _pixels = pixels;
        _width = width;
        _height = height;
    }

    /// <summary>Most frequent color of the whole image, used when the sides shown do not agree.</summary>
    public Color Dominant { get; }

    /// <summary>Samples <paramref name="image"/>, which can be disposed afterwards.</summary>
    public static BandColor Of(Bitmap image)
    {
        double factor = Math.Min(1, SampleSide / (double)Math.Max(image.Width, image.Height));
        int width = Math.Max(1, (int)(image.Width * factor));
        int height = Math.Max(1, (int)(image.Height * factor));

        using var sample = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sample))
        {
            // Nearest neighbor keeps real pixel colors instead of blending them; source copy keeps alpha.
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, new Rectangle(0, 0, width, height));
        }

        var data = sample.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new int[width * height];
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width, width);
            }
        }
        finally
        {
            sample.UnlockBits(data);
        }

        return new BandColor(DominantColor.Compute(image), pixels, width, height);
    }

    /// <summary>
    /// Band color when the cell shows <paramref name="part"/>, in the pixels of a bitmap of
    /// <paramref name="image"/> — the frame shown may be another frame of the same animation.
    /// </summary>
    public Color For(RectangleF part, Size image)
    {
        var region = Region(part, image);
        if (_last is { } last && last.Region == region)
        {
            return last.Color;
        }

        var color = Background(region) ?? Dominant;
        _last = new Cached(region, color);
        return color;
    }

    private Rectangle Region(RectangleF part, Size image)
    {
        double sx = _width / (double)image.Width;
        double sy = _height / (double)image.Height;
        int left = Math.Clamp((int)Math.Round(part.Left * sx), 0, _width - 1);
        int top = Math.Clamp((int)Math.Round(part.Top * sy), 0, _height - 1);
        int right = Math.Clamp((int)Math.Round(part.Right * sx), left + 1, _width);
        int bottom = Math.Clamp((int)Math.Round(part.Bottom * sy), top + 1, _height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>The color shared by at least three uniform sides of <paramref name="region"/>, if any.</summary>
    private Color? Background(Rectangle region)
    {
        int depthX = Math.Max(1, (int)Math.Round(region.Width * BandDepth));
        int depthY = Math.Max(1, (int)Math.Round(region.Height * BandDepth));
        Rectangle[] bands =
        [
            new(region.Left, region.Top, region.Width, depthY),
            new(region.Left, region.Bottom - depthY, region.Width, depthY),
            new(region.Left, region.Top, depthX, region.Height),
            new(region.Right - depthX, region.Top, depthX, region.Height),
        ];

        var sides = bands.Select(UniformSide).OfType<Side>().ToList();
        int n = sides.Count;
        for (int size = n; size >= VotesNeeded; size--)
        {
            for (int mask = 0; mask < 1 << n; mask++)
            {
                if (BitOperations.PopCount((uint)mask) != size)
                {
                    continue;
                }

                var group = Enumerable.Range(0, n).Where(i => (mask & 1 << i) != 0).Select(i => sides[i]).ToList();
                if (group.All(a => group.All(b => Distance(a.Lab, b.Lab) <= Tolerance)))
                {
                    return Color.FromArgb(
                        (int)Math.Round(group.Average(s => s.Color.R)),
                        (int)Math.Round(group.Average(s => s.Color.G)),
                        (int)Math.Round(group.Average(s => s.Color.B)));
                }
            }
        }

        return null;
    }

    /// <summary>Mean color of the band, when most of it is opaque and nearly all its opaque pixels match that mean.</summary>
    private Side? UniformSide(Rectangle band)
    {
        var labs = new Lab[band.Width * band.Height];
        int opaque = 0;
        long r = 0, g = 0, b = 0;
        for (int y = band.Top; y < band.Bottom; y++)
        {
            for (int x = band.Left; x < band.Right; x++)
            {
                int argb = _pixels[y * _width + x];
                if ((argb >> 24 & 0xFF) < 128)
                {
                    continue;
                }

                int pr = argb >> 16 & 0xFF, pg = argb >> 8 & 0xFF, pb = argb & 0xFF;
                r += pr;
                g += pg;
                b += pb;
                labs[opaque++] = ToLab(pr, pg, pb);
            }
        }

        // A mostly transparent side does not vote.
        if (opaque == 0 || opaque * 2 < labs.Length)
        {
            return null;
        }

        var mean = Color.FromArgb((int)(r / opaque), (int)(g / opaque), (int)(b / opaque));
        var meanLab = ToLab(mean.R, mean.G, mean.B);
        int matching = 0;
        for (int i = 0; i < opaque; i++)
        {
            if (Distance(labs[i], meanLab) <= Tolerance)
            {
                matching++;
            }
        }

        return matching >= UniformShare * opaque ? new Side(mean, meanLab) : null;
    }

    private static Lab ToLab(int r, int g, int b)
    {
        double lr = Linear[r], lg = Linear[g], lb = Linear[b];

        // sRGB to XYZ (D65), each axis divided by the white point.
        double x = (0.4124 * lr + 0.3576 * lg + 0.1805 * lb) / 0.95047;
        double y = 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
        double z = (0.0193 * lr + 0.1192 * lg + 0.9505 * lb) / 1.08883;

        double fx = F(x), fy = F(y), fz = F(z);
        return new Lab(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));

        static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : (24389.0 / 27 * t + 16) / 116;
    }

    private static double Distance(Lab a, Lab b)
    {
        double dl = a.L - b.L, da = a.A - b.A, db = a.B - b.B;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>sRGB channel value to linear light.</summary>
    private static double[] BuildLinearTable() => Enumerable.Range(0, 256).Select(i =>
    {
        double c = i / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }).ToArray();

    private readonly record struct Lab(double L, double A, double B);

    private readonly record struct Side(Color Color, Lab Lab);

    private sealed record Cached(Rectangle Region, Color Color);
}
