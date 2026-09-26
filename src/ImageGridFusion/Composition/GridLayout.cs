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
/// image 1; the other cells follow in reading order. Its cells can be resized by moving the
/// separators between them: the edges of each cell are kept as fractions of the canvas, starting on
/// the units. Also holds the output ratio and the catalog of layouts per image count.
/// </summary>
public sealed class GridLayout
{
    public const int RatioWidth = 1200;
    public const int RatioHeight = 628;
    public const int MaxImages = 4;

    /// <summary>Smallest share of the canvas width (or height) a separator leaves to each cell it moves.</summary>
    public const double MinCellFraction = 0.1;

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

    // The edges of the cells on the units, and as resized: neighbour cells hold the very same value
    // for the boundary they share, so they meet exactly.
    private readonly Edges[] _defaults;
    private readonly Edges[] _edges;

    private GridLayout(string id, string name, int columns, int rows, MirrorAxis mirrorAxis, Rectangle[] units, bool isMirrored = false, Edges[]? edges = null)
    {
        Id = id;
        Name = name;
        _columns = columns;
        _rows = rows;
        MirrorAxis = mirrorAxis;
        _units = units;
        IsMirrored = isMirrored;
        _defaults = units.Select(u => new Edges((double)u.Left / columns, (double)u.Top / rows, (double)u.Right / columns, (double)u.Bottom / rows)).ToArray();
        _edges = edges ?? _defaults;
    }

    public string Id { get; }

    public string Name { get; }

    public MirrorAxis MirrorAxis { get; }

    public bool IsMirrored { get; }

    public int Count => _units.Length;

    /// <summary>Whether a separator was moved away from the layout's own proportions.</summary>
    public bool IsResized => !_edges.SequenceEqual(_defaults);

    public static int HeightFor(int width) => (int)Math.Round(width * (double)RatioHeight / RatioWidth);

    /// <summary>Layouts available for <paramref name="count"/> images, the default one first.</summary>
    public static IReadOnlyList<GridLayout> For(int count) =>
        count is >= 1 and <= MaxImages
            ? Catalog[count - 1]
            : throw new ArgumentOutOfRangeException(nameof(count), count, "Between 1 and 4 images.");

    public static GridLayout Default(int count) => For(count)[0];

    /// <summary>
    /// Pixel at which a boundary at <paramref name="fraction"/> of <paramref name="size"/> falls:
    /// <c>floor(size × fraction)</c>, with a margin for the rounding of the fraction itself.
    /// </summary>
    public static int Boundary(int size, double fraction) => (int)Math.Floor((size * fraction) + 1e-9);

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

        // The sizes flip with the layout; unresized, it starts on its own units again.
        Edges[]? edges = !IsResized ? null
            : MirrorAxis == MirrorAxis.Horizontal ? _edges.Select(e => e with { Left = 1 - e.Right, Right = 1 - e.Left }).ToArray()
            : _edges.Select(e => e with { Top = 1 - e.Bottom, Bottom = 1 - e.Top }).ToArray();
        return new GridLayout(Id, Name, _columns, _rows, MirrorAxis, units, !IsMirrored, edges);
    }

    /// <summary>The same layout back on its own proportions, mirror kept.</summary>
    public GridLayout WithDefaultSizes() =>
        IsResized ? new GridLayout(Id, Name, _columns, _rows, MirrorAxis, _units, IsMirrored) : this;

    /// <summary>
    /// Cells tiling the canvas exactly: a boundary at a fraction of the canvas falls at
    /// <see cref="Boundary"/>, so neighbour cells share it and no pixel is lost.
    /// </summary>
    public Rectangle[] Cells(Size canvas) => _edges.Select(e =>
    {
        int left = Boundary(canvas.Width, e.Left), right = Boundary(canvas.Width, e.Right);
        int top = Boundary(canvas.Height, e.Top), bottom = Boundary(canvas.Height, e.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }).ToArray();

    /// <summary>Cell sizes as fractions of the canvas width and height, same order as <see cref="Cells"/>.</summary>
    public (double Width, double Height)[] CellFractions() =>
        _edges.Select(e => (e.Right - e.Left, e.Bottom - e.Top)).ToArray();

    /// <summary>
    /// The separators between the cells, vertical ones first. Along one boundary line, cells facing
    /// each other over a stretch of it form one separator; cells meeting at a single point do not, so
    /// the arms of a cross move on their own while both of its lines are straight, and a broken line
    /// makes the other one whole.
    /// </summary>
    public IReadOnlyList<Separator> Separators()
    {
        var separators = new List<Separator>();
        foreach (bool vertical in new[] { true, false })
        {
            foreach (double position in _edges.Select(e => vertical ? e.Right : e.Bottom).Where(p => p < 1).Distinct())
            {
                var before = Enumerable.Range(0, Count).Where(i => (vertical ? _edges[i].Right : _edges[i].Bottom) == position).ToList();
                var after = Enumerable.Range(0, Count).Where(i => (vertical ? _edges[i].Left : _edges[i].Top) == position).ToList();
                separators.AddRange(Facing(vertical, position, before, after));
            }
        }

        return separators;
    }

    /// <summary>Where a separator can go: every cell it moves keeps <see cref="MinCellFraction"/> of the canvas side.</summary>
    public (double Min, double Max) Range(Separator separator)
    {
        double min = separator.Before.Max(i => separator.Vertical ? _edges[i].Left : _edges[i].Top) + MinCellFraction;
        double max = separator.After.Min(i => separator.Vertical ? _edges[i].Right : _edges[i].Bottom) - MinCellFraction;
        return (min, Math.Max(min, max));
    }

    /// <summary>Where a separator lies in the layout's own proportions.</summary>
    public double DefaultPosition(Separator separator) =>
        separator.Vertical ? _defaults[separator.Before[0]].Right : _defaults[separator.Before[0]].Bottom;

    /// <summary>
    /// The layout with <paramref name="separator"/> moved to <paramref name="position"/>, kept within
    /// its <see cref="Range"/>: the cells on both of its sides follow it, the others stay.
    /// </summary>
    public GridLayout WithSeparator(Separator separator, double position)
    {
        var (min, max) = Range(separator);
        position = Math.Clamp(position, min, max);
        var edges = (Edges[])_edges.Clone();
        foreach (int i in separator.Before)
        {
            edges[i] = separator.Vertical ? edges[i] with { Right = position } : edges[i] with { Bottom = position };
        }

        foreach (int i in separator.After)
        {
            edges[i] = separator.Vertical ? edges[i] with { Left = position } : edges[i] with { Top = position };
        }

        return edges.SequenceEqual(_edges) ? this
            : new GridLayout(Id, Name, _columns, _rows, MirrorAxis, _units, IsMirrored, edges.SequenceEqual(_defaults) ? null : edges);
    }

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
    /// The separators along one boundary line: each cell before or after it, grouped with the cells
    /// it faces across the line over a stretch of it — meeting at a mere point does not count.
    /// </summary>
    private IEnumerable<Separator> Facing(bool vertical, double position, List<int> before, List<int> after)
    {
        (double Start, double End) Span(int i) => vertical ? (_edges[i].Top, _edges[i].Bottom) : (_edges[i].Left, _edges[i].Right);
        bool Face(int a, int b) => Math.Min(Span(a).End, Span(b).End) > Math.Max(Span(a).Start, Span(b).Start);

        var pending = before.Concat(after).ToList();
        while (pending.Count > 0)
        {
            // Grows a group from one cell, through the cells facing it across the line.
            var group = new List<int> { pending[0] };
            pending.RemoveAt(0);
            for (int k = 0; k < group.Count; k++)
            {
                int cell = group[k];
                var facing = pending.Where(o => before.Contains(o) != before.Contains(cell) && Face(cell, o)).ToList();
                group.AddRange(facing);
                pending.RemoveAll(facing.Contains);
            }

            int[] sideBefore = [.. group.Where(before.Contains).Order()];
            int[] sideAfter = [.. group.Where(after.Contains).Order()];
            if (sideBefore.Length > 0 && sideAfter.Length > 0)
            {
                yield return new Separator(vertical, position, group.Min(i => Span(i).Start), group.Max(i => Span(i).End), sideBefore, sideAfter);
            }
        }
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

    /// <summary>Edges of a cell as fractions of the canvas width and height.</summary>
    private readonly record struct Edges(double Left, double Top, double Right, double Bottom);
}
