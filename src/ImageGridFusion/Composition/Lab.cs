namespace ImageGridFusion.Composition;

/// <summary>A color in CIELAB (D65), where the distance between two colors follows the perceived difference (ΔE76).</summary>
internal readonly record struct Lab(double L, double A, double B)
{
    private static readonly double[] Linear = BuildLinearTable();

    public static Lab Of(Color color) => Of(color.R, color.G, color.B);

    public static Lab Of(int r, int g, int b)
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

    public double DistanceTo(Lab other)
    {
        double dl = L - other.L, da = A - other.A, db = B - other.B;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>sRGB channel value to linear light.</summary>
    private static double[] BuildLinearTable() => Enumerable.Range(0, 256).Select(i =>
    {
        double c = i / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }).ToArray();
}
