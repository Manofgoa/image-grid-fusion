namespace ImageGridFusion.Composition;

/// <summary>
/// The carousel mode: every second, each image jumps to the next cell clockwise around the grid (see
/// <see cref="GridLayout.ClockwiseLoop"/>), the last cell of the loop feeding the first one. After as
/// many steps as images, the grid is back to its own arrangement, where image i is in cell i.
/// </summary>
public static class Carousel
{
    /// <summary>How long each arrangement stays shown.</summary>
    public static readonly TimeSpan StepDuration = TimeSpan.FromSeconds(1);

    /// <summary>A carousel needs at least two images to move.</summary>
    public static bool CanPlay(int count) => count >= 2;

    /// <summary>One full loop: one step per image, back to the starting arrangement.</summary>
    public static TimeSpan Length(int count) => StepDuration * count;

    /// <summary>
    /// Whole loops covering <paramref name="contents"/>, the longest content playing: it ends before
    /// the video does, which still ends on a full loop, back to the starting arrangement.
    /// </summary>
    public static TimeSpan Length(int count, TimeSpan contents)
    {
        var loop = Length(count);
        long loops = Math.Max(1, (long)Math.Ceiling(contents.Ticks / (double)loop.Ticks));
        return loop * loops;
    }

    /// <summary>Step shown at <paramref name="time"/> from the start, for <paramref name="count"/> images.</summary>
    public static int StepAt(TimeSpan time, int count) => (int)(time.Ticks / StepDuration.Ticks % count);

    /// <summary>Items in cell order at <paramref name="step"/>, item i being in cell i at step 0.</summary>
    public static T[] Arrange<T>(IReadOnlyList<T> items, GridLayout layout, int step)
    {
        var loop = layout.ClockwiseLoop();
        var arranged = new T[loop.Length];
        for (int m = 0; m < loop.Length; m++)
        {
            arranged[loop[m]] = items[loop[Wrap(m - step, loop.Length)]];
        }

        return arranged;
    }

    /// <summary>Cell showing item <paramref name="index"/> at <paramref name="step"/>.</summary>
    public static int CellOf(int index, GridLayout layout, int step)
    {
        var loop = layout.ClockwiseLoop();
        return loop[Wrap(Array.IndexOf(loop, index) + step, loop.Length)];
    }

    /// <summary>
    /// A single canvas for every arrangement: the largest <see cref="CanvasSizer"/> gives over the
    /// steps, so no image is downscaled at any of them.
    /// </summary>
    public static Size CanvasSize(IReadOnlyList<Size> sizes, GridLayout layout)
    {
        var largest = Size.Empty;
        for (int step = 0; step < sizes.Count; step++)
        {
            var canvas = CanvasSizer.Compute(Arrange(sizes, layout, step), layout);
            if (canvas.Width > largest.Width)
            {
                largest = canvas;
            }
        }

        return largest;
    }

    private static int Wrap(int value, int count) => ((value % count) + count) % count;
}
