namespace ImageGridFusion.Composition;

/// <summary>
/// The aspect ratio of the final canvas — the preview and every export — in the order of the Format
/// tab's thumbnails (see RULES.md § Output Format). Not persisted: <see cref="Twitter"/> at start-up.
/// </summary>
public enum OutputFormat
{
    /// <summary>The ratio that loses the least of the images; see <see cref="OutputFormats.FreeRatio"/>.</summary>
    Free,
    Twitter,
    Square,
    Portrait,
    Story,
    Landscape,
}

/// <summary>The ratios (width ÷ height) and names of the output formats, and the computation of the free one.</summary>
public static class OutputFormats
{
    /// <summary>Twitter / X's in-feed ratio, 1200:628: the default format, and the free one with nothing to weigh.</summary>
    public const double TwitterRatio = 1200.0 / 628;

    /// <summary>Narrowest ratio the free format takes: 9:16.</summary>
    public const double MinFreeRatio = 9.0 / 16;

    /// <summary>Widest ratio the free format takes: 21:9.</summary>
    public const double MaxFreeRatio = 21.0 / 9;

    // Ratios tried between the bounds, evenly spread on a log scale, besides each cell's own.
    private const int FreeSteps = 400;

    // Image ratios are rounded to this many decimals, so a video's frames, scaled down to their cell,
    // do not move the free ratio with their rounding.
    private const int RatioDecimals = 3;

    public static string Name(OutputFormat format) => format switch
    {
        OutputFormat.Free => "Free",
        OutputFormat.Twitter => "Twitter",
        OutputFormat.Square => "Square 1:1",
        OutputFormat.Portrait => "Portrait 4:5",
        OutputFormat.Story => "Story 9:16",
        _ => "Landscape 16:9",
    };

    /// <summary>The fixed ratio of <paramref name="format"/>; <c>null</c> for <see cref="OutputFormat.Free"/>, computed from the content.</summary>
    public static double? Ratio(OutputFormat format) => format switch
    {
        OutputFormat.Free => null,
        OutputFormat.Twitter => TwitterRatio,
        OutputFormat.Square => 1,
        OutputFormat.Portrait => 4.0 / 5,
        OutputFormat.Story => 9.0 / 16,
        _ => 16.0 / 9,
    };

    /// <summary>
    /// The free ratio: among the ratios between <see cref="MinFreeRatio"/> and <see cref="MaxFreeRatio"/>,
    /// the one where the fitting rule loses the least of the images over the cells of
    /// <paramref name="layout"/> — what it crops off plus the bands it leaves, each cell's loss weighted
    /// by its area. Image i, of <paramref name="images"/>, goes into cell i; a <c>null</c> one (a text or
    /// page preview, which takes its cell's shape) does not weigh. With nothing to weigh,
    /// <see cref="TwitterRatio"/>. Each cell's own ratio is tried too, so a single image gets its exact one.
    /// </summary>
    public static double FreeRatio(IReadOnlyList<Size?> images, GridLayout layout)
    {
        var fractions = layout.CellFractions();
        var weighed = Enumerable.Range(0, Math.Min(images.Count, fractions.Length))
            .Where(i => images[i] is { Width: > 0, Height: > 0 })
            .Select(i => (Fraction: fractions[i], Aspect: Math.Round(images[i]!.Value.Width / (double)images[i]!.Value.Height, RatioDecimals)))
            .ToList();
        if (weighed.Count == 0)
        {
            return TwitterRatio;
        }

        // On a canvas 1 wide and 1 / ratio high, a cell is fraction.Width × fraction.Height / ratio: its
        // shape matches its image at ratio = aspect × fraction.Height / fraction.Width.
        double step = Math.Log(MaxFreeRatio / MinFreeRatio) / FreeSteps;
        var candidates = Enumerable.Range(0, FreeSteps + 1).Select(k => MinFreeRatio * Math.Exp(k * step))
            .Concat(weighed.Select(w => Math.Clamp(w.Aspect * w.Fraction.Height / w.Fraction.Width, MinFreeRatio, MaxFreeRatio)));

        double best = TwitterRatio;
        double least = double.MaxValue;
        foreach (double ratio in candidates)
        {
            double loss = weighed.Sum(w => Loss(w.Fraction.Width, w.Fraction.Height / ratio, w.Aspect) * ratio);
            if (loss < least - 1e-12)
            {
                least = loss;
                best = ratio;
            }
        }

        return best;
    }

    /// <summary>
    /// Area the fitting rule loses in a cell of <paramref name="width"/> × <paramref name="height"/> with
    /// an image of <paramref name="aspect"/>: the part of the image cropped off plus the bands of the cell.
    /// </summary>
    private static double Loss(double width, double height, double aspect)
    {
        // The image at 1 px high, scaled as FitCalculator scales it.
        double scale = FitCalculator.Scale(width, height, new Size((int)Math.Round(aspect * 1_000_000), 1_000_000)) * 1_000_000;
        double drawnWidth = aspect * scale;
        double drawnHeight = scale;
        double covered = Math.Min(drawnWidth, width) * Math.Min(drawnHeight, height);
        return (drawnWidth * drawnHeight - covered) + (width * height - covered);
    }
}
