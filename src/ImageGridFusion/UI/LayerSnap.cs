namespace ImageGridFusion.UI;

/// <summary>
/// A placement guide of a dragged layer on one axis: its position in the control, and whether another layer
/// gives it — drawn fainter than the grid's and the cells'.
/// </summary>
internal readonly record struct LayerGuide(float At, bool Faint);

/// <summary>
/// The placement guides of a dragged layer along one axis (workfiles/20261029-layer-ergonomics.md): the lines
/// of the grid, the cells and the other layers against the layer's three references — its bounding
/// rectangle's low edge, its center and its high edge, given as <c>low</c> and <c>high</c> to every method.
/// </summary>
internal static class LayerSnap
{
    // Positions within half a pixel are the same line: they come back from fractions of the grid.
    private const float Epsilon = 0.5f;

    /// <summary>One line per position, in order: the grid's or a cell's wins over another layer's at the same place.</summary>
    public static List<LayerGuide> Merge(IEnumerable<LayerGuide> lines)
    {
        var merged = new List<LayerGuide>();
        foreach (var line in lines.OrderBy(l => l.At))
        {
            if (merged.Count > 0 && line.At - merged[^1].At < Epsilon)
            {
                if (merged[^1].Faint && !line.Faint)
                {
                    merged[^1] = line;
                }

                continue;
            }

            merged.Add(line);
        }

        return merged;
    }

    /// <summary>
    /// How far the layer moves to snap: onto the line nearest one of its references, when that one lies within
    /// <paramref name="snap"/> pixels; 0 when none does.
    /// </summary>
    public static float Offset(float low, float high, IReadOnlyList<LayerGuide> lines, float snap) =>
        Nearest(low, high, lines, snap) is { } nearest ? nearest.Line.At - nearest.Reference : 0;

    /// <summary>The lines one of the layer's references lies on: all of them, not only the one it snapped to.</summary>
    public static List<LayerGuide> Resting(float low, float high, IReadOnlyList<LayerGuide> lines)
    {
        float[] references = References(low, high);
        return [.. lines.Where(l => references.Any(r => Math.Abs(l.At - r) < Epsilon))];
    }

    /// <summary>The line nearest one of the layer's references, that reference and the gap between them, when within <paramref name="reach"/> pixels.</summary>
    public static (LayerGuide Line, float Reference, float Gap)? Nearest(float low, float high, IReadOnlyList<LayerGuide> lines, float reach)
    {
        (LayerGuide Line, float Reference, float Gap)? nearest = null;
        foreach (float reference in References(low, high))
        {
            foreach (var line in lines)
            {
                float gap = Math.Abs(line.At - reference);
                if (gap <= reach && (nearest is null || gap < nearest.Value.Gap))
                {
                    nearest = (line, reference, gap);
                }
            }
        }

        return nearest;
    }

    /// <summary>Whether one of the references of a rectangle from <paramref name="low"/> to <paramref name="high"/> lies on the line at <paramref name="at"/>.</summary>
    public static bool Touches(float low, float high, float at) => References(low, high).Any(r => Math.Abs(at - r) < Epsilon);

    private static float[] References(float low, float high) => [low, (low + high) / 2, high];
}
