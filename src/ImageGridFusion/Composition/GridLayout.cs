namespace ImageGridFusion.Composition;

/// <summary>Output ratio and cell rectangles for 1 to 4 images, in reading order.</summary>
public static class GridLayout
{
    public const int RatioWidth = 1200;
    public const int RatioHeight = 628;
    public const int MaxImages = 4;

    public static int HeightFor(int width) => (int)Math.Round(width * (double)RatioHeight / RatioWidth);

    /// <summary>Cells tiling the canvas exactly: the left/top part takes floor(size / 2).</summary>
    public static Rectangle[] Cells(int count, Size canvas)
    {
        int w = canvas.Width, h = canvas.Height;
        int left = w / 2, right = w - left;
        int top = h / 2, bottom = h - top;

        return count switch
        {
            1 => [new(0, 0, w, h)],
            2 => [new(0, 0, left, h), new(left, 0, right, h)],
            3 => [new(0, 0, left, h), new(left, 0, right, top), new(left, top, right, bottom)],
            4 => [new(0, 0, left, top), new(left, 0, right, top), new(0, top, left, bottom), new(left, top, right, bottom)],
            _ => throw new ArgumentOutOfRangeException(nameof(count), count, "Between 1 and 4 images."),
        };
    }

    /// <summary>Cell sizes as fractions of the canvas width and height, same order as <see cref="Cells"/>.</summary>
    public static (double Width, double Height)[] CellFractions(int count) => count switch
    {
        1 => [(1, 1)],
        2 => [(0.5, 1), (0.5, 1)],
        3 => [(0.5, 1), (0.5, 0.5), (0.5, 0.5)],
        4 => [(0.5, 0.5), (0.5, 0.5), (0.5, 0.5), (0.5, 0.5)],
        _ => throw new ArgumentOutOfRangeException(nameof(count), count, "Between 1 and 4 images."),
    };
}
