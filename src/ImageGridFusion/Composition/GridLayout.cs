namespace ImageGridFusion.Composition;

/// <summary>Axis along which a layout can be mirrored; <see cref="None"/> for a symmetric layout.</summary>
public enum MirrorAxis
{
    None,
    Horizontal,
    Vertical,
}

/// <summary>
/// A layout of 1 to 4 cells, described on a grid of units. Cell 0 is the featured cell and takes
/// image 1; the other cells follow in reading order. Also holds the output ratio and the catalog of
/// layouts per image count.
/// </summary>
public sealed class GridLayout
{
    public const int RatioWidth = 1200;
    public const int RatioHeight = 628;
    public const int MaxImages = 4;

    // Per image count, the default layout first.
    private static readonly GridLayout[][] Catalog =
    [
        [
            new("1-single", "Single", 1, 1, MirrorAxis.None, [new(0, 0, 1, 1)]),
        ],
        [
            new("2-columns", "Two columns", 2, 1, MirrorAxis.None, [new(0, 0, 1, 1), new(1, 0, 1, 1)]),
            new("2-rows", "Two rows", 1, 2, MirrorAxis.None, [new(0, 0, 1, 1), new(0, 1, 1, 1)]),
            new("2-split", "Two thirds + one third", 3, 1, MirrorAxis.Horizontal, [new(0, 0, 2, 1), new(2, 0, 1, 1)]),
        ],
        [
            new("3-big-left", "Big left", 2, 2, MirrorAxis.Horizontal, [new(0, 0, 1, 2), new(1, 0, 1, 1), new(1, 1, 1, 1)]),
            new("3-columns", "Three columns", 3, 1, MirrorAxis.None, [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1)]),
            new("3-featured", "Featured", 3, 2, MirrorAxis.Horizontal, [new(0, 0, 2, 2), new(2, 0, 1, 1), new(2, 1, 1, 1)]),
            new("3-big-top", "Big top", 2, 2, MirrorAxis.Vertical, [new(0, 0, 2, 1), new(0, 1, 1, 1), new(1, 1, 1, 1)]),
        ],
        [
            new("4-grid", "Grid", 2, 2, MirrorAxis.None, [new(0, 0, 1, 1), new(1, 0, 1, 1), new(0, 1, 1, 1), new(1, 1, 1, 1)]),
            new("4-columns", "Four columns", 4, 1, MirrorAxis.None, [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1), new(3, 0, 1, 1)]),
            new("4-featured", "Featured", 3, 3, MirrorAxis.Horizontal, [new(0, 0, 2, 3), new(2, 0, 1, 1), new(2, 1, 1, 1), new(2, 2, 1, 1)]),
            new("4-big-left", "Big left", 2, 3, MirrorAxis.Horizontal, [new(0, 0, 1, 3), new(1, 0, 1, 1), new(1, 1, 1, 1), new(1, 2, 1, 1)]),
            new("4-big-top", "Big top", 3, 2, MirrorAxis.Vertical, [new(0, 0, 3, 1), new(0, 1, 1, 1), new(1, 1, 1, 1), new(2, 1, 1, 1)]),
        ],
    ];

    private readonly int _columns;
    private readonly int _rows;
    private readonly Rectangle[] _units;

    private GridLayout(string id, string name, int columns, int rows, MirrorAxis mirrorAxis, Rectangle[] units, bool isMirrored = false)
    {
        Id = id;
        Name = name;
        _columns = columns;
        _rows = rows;
        MirrorAxis = mirrorAxis;
        _units = units;
        IsMirrored = isMirrored;
    }

    public string Id { get; }

    public string Name { get; }

    public MirrorAxis MirrorAxis { get; }

    public bool IsMirrored { get; }

    public int Count => _units.Length;

    public static int HeightFor(int width) => (int)Math.Round(width * (double)RatioHeight / RatioWidth);

    /// <summary>Layouts available for <paramref name="count"/> images, the default one first.</summary>
    public static IReadOnlyList<GridLayout> For(int count) =>
        count is >= 1 and <= MaxImages
            ? Catalog[count - 1]
            : throw new ArgumentOutOfRangeException(nameof(count), count, "Between 1 and 4 images.");

    public static GridLayout Default(int count) => For(count)[0];

    /// <summary>
    /// The same layout flipped along its asymmetric axis, or back unflipped. Flipping along one
    /// axis keeps the order along the other, so the non-featured cells stay in reading order.
    /// </summary>
    public GridLayout Mirrored()
    {
        var units = MirrorAxis switch
        {
            MirrorAxis.Horizontal => _units.Select(u => u with { X = _columns - u.Right }).ToArray(),
            MirrorAxis.Vertical => _units.Select(u => u with { Y = _rows - u.Bottom }).ToArray(),
            _ => throw new InvalidOperationException($"Layout {Id} is symmetric."),
        };
        return new GridLayout(Id, Name, _columns, _rows, MirrorAxis, units, !IsMirrored);
    }

    /// <summary>
    /// Cells tiling the canvas exactly: a boundary at <c>u</c> units of <c>n</c> falls at
    /// <c>floor(size × u / n)</c>, so neighbour cells share it and no pixel is lost.
    /// </summary>
    public Rectangle[] Cells(Size canvas) => _units.Select(u =>
    {
        int left = canvas.Width * u.Left / _columns, right = canvas.Width * u.Right / _columns;
        int top = canvas.Height * u.Top / _rows, bottom = canvas.Height * u.Bottom / _rows;
        return new Rectangle(left, top, right - left, bottom - top);
    }).ToArray();

    /// <summary>Cell sizes as fractions of the canvas width and height, same order as <see cref="Cells"/>.</summary>
    public (double Width, double Height)[] CellFractions() =>
        _units.Select(u => ((double)u.Width / _columns, (double)u.Height / _rows)).ToArray();

    /// <summary>
    /// Cell indices clockwise around the grid, from the featured cell. A cell is placed where it first
    /// meets the border, walked clockwise from the top-left corner; cells in a single row go left to
    /// right. Follows the geometry, so a mirrored layout reverses the order of its cells.
    /// </summary>
    public int[] ClockwiseLoop()
    {
        var order = Enumerable.Range(0, Count).OrderBy(i => BorderPosition(_units[i])).ToArray();
        int start = Array.IndexOf(order, 0);
        return [.. order.Skip(start), .. order.Take(start)];
    }

    /// <summary>
    /// Earliest point of the border a cell touches, walking it clockwise in units: the top edge left to
    /// right, the right edge downward, the bottom edge right to left, the left edge upward. Every cell
    /// of the catalog touches the border; one that would not goes last.
    /// </summary>
    private double BorderPosition(Rectangle unit)
    {
        var positions = new List<double>(4);
        if (unit.Top == 0)
        {
            positions.Add(unit.Left);
        }

        if (unit.Right == _columns)
        {
            positions.Add(_columns + unit.Top);
        }

        if (unit.Bottom == _rows)
        {
            positions.Add(_columns + _rows + (_columns - unit.Right));
        }

        if (unit.Left == 0)
        {
            positions.Add((2 * _columns) + _rows + (_rows - unit.Bottom));
        }

        return positions.Count > 0 ? positions.Min() : double.MaxValue;
    }
}
