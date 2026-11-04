namespace ImageGridFusion.Composition;

/// <summary>
/// The Custom output format's two fields and its <see cref="Exact"/> pixels checkbox
/// (workfiles/20261104-custom-output-format.md): <see cref="Width"/> : <see cref="Height"/> a ratio while
/// <see cref="Exact"/> is off — the native size rule unchanged —, the canvas's pixels while it is on, every
/// export then giving exactly that size unless its cap is smaller (<see cref="CanvasSizer.Exactly"/>).
/// Immutable, part of the composed state; not persisted.
/// </summary>
public sealed record CustomFormat
{
    /// <summary>The largest value of a field: the PNG's cap, <see cref="CanvasSizer.MaxLongSide"/>.</summary>
    public const int MaxSide = CanvasSizer.MaxLongSide;

    /// <summary>The smallest value of a field while it is a ratio's term.</summary>
    public const int MinRatioSide = 1;

    /// <summary>The smallest value of a field while it is a number of pixels.</summary>
    public const int MinExactSide = 100;

    /// <summary>How many times longer than the other a side may be, in both modes: the shape stays between 1:10 and 10:1.</summary>
    public const int MaxElongation = 10;

    private CustomFormat(int width, int height, bool exact)
    {
        this.Width = width;
        this.Height = height;
        this.Exact = exact;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Whether <see cref="Width"/> × <see cref="Height"/> are the canvas's pixels rather than a ratio.</summary>
    public bool Exact { get; }

    /// <summary>The canvas's width ÷ height.</summary>
    public double Ratio => this.Width / (double)this.Height;

    /// <summary>The canvas's exact size while <see cref="Exact"/> is on; <c>null</c> for a ratio.</summary>
    public Size? ExactSize => this.Exact ? new Size(this.Width, this.Height) : null;

    /// <summary>The smallest value a field takes in this mode.</summary>
    public int MinSide => MinSideOf(this.Exact);

    /// <summary>The thumbnail's label: <c>3:2</c> — the two fields divided by their greatest common divisor — or <c>1080×1350</c>.</summary>
    public string Label
    {
        get
        {
            if (this.Exact)
            {
                return $"{this.Width}×{this.Height}";
            }

            int divisor = Gcd(this.Width, this.Height);
            return $"{this.Width / divisor}:{this.Height / divisor}";
        }
    }

    /// <summary>A ratio of <paramref name="size"/> — the native size of the format in force before Custom is picked — within the bounds.</summary>
    public static CustomFormat RatioOf(Size size) => Of(size.Width, size.Height, exact: false);

    /// <summary><paramref name="width"/> × <paramref name="height"/> within the bounds of the mode, the longer side brought back to the elongation's limit.</summary>
    public static CustomFormat Of(int width, int height, bool exact)
    {
        int min = MinSideOf(exact);
        width = Math.Clamp(width, min, MaxSide);
        height = Math.Clamp(height, min, MaxSide);
        if (width >= height)
        {
            width = Math.Min(width, height * MaxElongation);
        }
        else
        {
            height = Math.Min(height, width * MaxElongation);
        }

        return new(width, height, exact);
    }

    /// <summary>The width changed to <paramref name="width"/>, brought back within the bounds against the height, which stays.</summary>
    public CustomFormat WithWidth(int width) => new(Fitted(width, this.Height, this.Exact), this.Height, this.Exact);

    /// <summary>The height changed to <paramref name="height"/>, brought back within the bounds against the width, which stays.</summary>
    public CustomFormat WithHeight(int height) => new(this.Width, Fitted(height, this.Width, this.Exact), this.Exact);

    /// <summary>The mode changed: a value under the pixels' minimum brought up to it, the longer side then to the elongation's limit.</summary>
    public CustomFormat WithExact(bool exact) => Of(this.Width, this.Height, exact);

    /// <summary>Whether <paramref name="side"/> may stand against <paramref name="other"/> as it is, without being brought back.</summary>
    public static bool Fits(int side, int other, bool exact) => Fitted(side, other, exact) == side;

    private static int MinSideOf(bool exact) => exact ? MinExactSide : MinRatioSide;

    // A side brought within the mode's bounds, then between other ÷ 10 and other × 10.
    private static int Fitted(int side, int other, bool exact)
    {
        int min = MinSideOf(exact);
        int low = Math.Max(min, (int)Math.Ceiling(other / (double)MaxElongation));
        int high = Math.Min(MaxSide, other * MaxElongation);
        return Math.Clamp(side, low, high);
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return Math.Max(1, a);
    }
}
