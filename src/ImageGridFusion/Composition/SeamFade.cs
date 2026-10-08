namespace ImageGridFusion.Composition;

/// <summary>
/// The Seams global effect's settings: the flat fills of neighbour cells fade into each other along the
/// edges they share, over <see cref="Depth"/> on each side (see <see cref="SeamField"/>). Off at
/// start-up, not persisted; it does not apply while the borders leave a gap between the cells.
/// </summary>
public sealed record SeamFade
{
    public const double MinDepth = 0.01;
    public const double MaxDepth = 0.5;

    /// <summary>The state the app starts in, and the global Resets and Clear all bring back.</summary>
    public static readonly SeamFade Initial = new();

    /// <summary>
    /// How far the fade reaches into each cell from a seam, as a share of the smallest cell's shorter
    /// side: resolution-independent, so the preview and an export at another size look the same.
    /// </summary>
    public double Depth { get; private init; } = 0.1;

    public SeamFade WithDepth(double depth) => this with { Depth = Math.Clamp(depth, MinDepth, MaxDepth) };
}
