namespace ImageGridFusion.Composition;

/// <summary>
/// The layout strip's Auto buttons: the separators of one orientation moved so that every cell is
/// shaped as close as possible to its image — the fewest bands once each image is shown whole. Side by
/// side, the widths follow the images' ratios and the images get the same height; stacked, the heights
/// follow them and the images get the same width.
/// </summary>
public static class LayoutAutoSize
{
    // Evenly spaced positions tried along a separator's range before the finer search around the best one.
    private const int Samples = 64;

    private const int RefineSteps = 40;

    // Rounds over the separators: moving one changes the best place of the others it shares a cell with.
    private const int MaxRounds = 12;

    // A move that gains less is not made, so a separator only texts depend on stays where it is.
    private const double MinGain = 1e-9;

    /// <summary>
    /// <paramref name="layout"/> with its vertical (<paramref name="vertical"/>) or horizontal separators
    /// moved, each within its <see cref="GridLayout.Range"/>, so the cells as <paramref name="cellsOf"/>
    /// draws them are shaped like the images of <paramref name="shown"/> — one per cell, as seen; <c>null</c>
    /// for a content left out (a text, which takes its cell's shape). The layout itself when nothing moves.
    /// </summary>
    public static GridLayout Fitted(GridLayout layout, bool vertical, IReadOnlyList<Size?> shown, Func<GridLayout, Rectangle[]> cellsOf)
    {
        double Cost(GridLayout candidate) => Mismatch(cellsOf(candidate), shown);

        var keys = layout.Separators().Where(s => s.Vertical == vertical).Select(s => (s.Before, s.After)).ToList();
        double cost = Cost(layout);
        for (int round = 0; round < MaxRounds; round++)
        {
            bool moved = false;
            foreach (var (before, after) in keys)
            {
                // Found again by its cells: its position, and so the record, changes as it moves.
                if (Find(layout, vertical, before, after) is not { } separator)
                {
                    continue;
                }

                var (min, max) = layout.Range(separator);
                double best = Best(position => Cost(layout.WithSeparator(separator, position)), min, max);
                var candidate = layout.WithSeparator(separator, best);
                double candidateCost = Cost(candidate);
                if (candidateCost < cost - MinGain)
                {
                    layout = candidate;
                    cost = candidateCost;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return layout;
    }

    /// <summary>
    /// How far the cells' shapes are from their images': the variance, over the images taken into account,
    /// of the log of the cell's ratio divided by its image's. Zero when every cell is the same multiple of
    /// its image's shape — side by side, widths proportional to the images' ratios, so equal heights shown
    /// whole — and when every cell matches its image exactly. The canvas's own shape, which no separator
    /// changes, is the common multiple left over.
    /// </summary>
    private static double Mismatch(Rectangle[] cells, IReadOnlyList<Size?> shown)
    {
        var logs = new List<double>(cells.Length);
        for (int i = 0; i < cells.Length && i < shown.Count; i++)
        {
            if (shown[i] is not { Width: > 0, Height: > 0 } image || cells[i].Width <= 0 || cells[i].Height <= 0)
            {
                continue;
            }

            logs.Add(Math.Log((double)cells[i].Width / cells[i].Height) - Math.Log((double)image.Width / image.Height));
        }

        if (logs.Count < 2)
        {
            return 0;
        }

        double mean = logs.Average();
        return logs.Sum(l => (l - mean) * (l - mean)) / logs.Count;
    }

    /// <summary>The position in [<paramref name="min"/>, <paramref name="max"/>] where <paramref name="cost"/> is lowest: sampled, then narrowed down around the best sample.</summary>
    private static double Best(Func<double, double> cost, double min, double max)
    {
        if (max <= min)
        {
            return min;
        }

        double step = (max - min) / Samples;
        double best = min, bestCost = double.MaxValue;
        for (int k = 0; k <= Samples; k++)
        {
            double position = min + (k * step);
            double value = cost(position);
            if (value < bestCost)
            {
                best = position;
                bestCost = value;
            }
        }

        // Golden-section search over the samples on both sides of the best one.
        double lo = Math.Max(min, best - step), hi = Math.Min(max, best + step);
        double ratio = (Math.Sqrt(5) - 1) / 2;
        double a = hi - (ratio * (hi - lo)), b = lo + (ratio * (hi - lo));
        double costA = cost(a), costB = cost(b);
        for (int k = 0; k < RefineSteps; k++)
        {
            if (costA <= costB)
            {
                hi = b;
                b = a;
                costB = costA;
                a = hi - (ratio * (hi - lo));
                costA = cost(a);
            }
            else
            {
                lo = a;
                a = b;
                costA = costB;
                b = lo + (ratio * (hi - lo));
                costB = cost(b);
            }
        }

        double refined = (lo + hi) / 2;
        return cost(refined) < bestCost ? refined : best;
    }

    private static Separator? Find(GridLayout layout, bool vertical, int[] before, int[] after) =>
        layout.Separators().FirstOrDefault(s => s.Vertical == vertical && s.Before.SequenceEqual(before) && s.After.SequenceEqual(after));
}
