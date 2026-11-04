namespace ImageGridFusion.Composition;

/// <summary>
/// The Grid background global effect's settings (workfiles/20260926-forced-background.md): one fill under the
/// whole canvas — the cells, the gap between them, the Twitter corners, a grid of layers only — drawn by the
/// shared painter (<see cref="FillStyle"/>). Off at start-up, not persisted. Until it is first turned on, its
/// color <see cref="FollowsCell">follows</see> the first cell's automatic color; turned on, it takes that color
/// as its own (<see cref="Frozen"/>).
/// </summary>
public sealed record GridBackground
{
    /// <summary>The state the app starts in, and the global Resets and Clear all bring back.</summary>
    public static readonly GridBackground Initial = new();

    /// <summary>
    /// The first color is the first cell's automatic one — the first cell holding an image, white with none —
    /// until the effect is first turned on.
    /// </summary>
    public bool FollowsCell { get; private init; } = true;

    /// <summary>The first color, opaque; read once it no longer follows the first cell.</summary>
    public Color Color { get; private init; } = Color.White;

    /// <summary>The first color's opacity, from 0 to 1.</summary>
    public double Opacity { get; private init; } = 1;

    public FillStyle Style { get; private init; } = FillStyle.Grid;

    /// <summary>
    /// The image drawn over the fill, under the cells; <c>null</c> for none, the fill alone
    /// (workfiles/20261104-grid-background-image.md). Compared by its content's reference and its look's value.
    /// </summary>
    public BackgroundImage? Image { get; private init; }

    /// <summary>The image set, replaced or removed (<c>null</c>), the fill's settings kept.</summary>
    public GridBackground WithImage(BackgroundImage? image) => this with { Image = image };

    /// <summary>The first color chosen: it no longer follows the first cell.</summary>
    public GridBackground WithColor(Color color) => this with { FollowsCell = false, Color = Color.FromArgb(255, color) };

    public GridBackground WithOpacity(double opacity) => this with { Opacity = Math.Clamp(opacity, 0, 1) };

    public GridBackground WithStyle(FillStyle style) => this with { Style = style };

    /// <summary>Turned on: the color followed, <paramref name="automatic"/>, taken as its own.</summary>
    public GridBackground Frozen(Color automatic) => this.FollowsCell ? this.WithColor(automatic) : this;

    /// <summary>The first color in use, opaque: <paramref name="automatic"/> while it follows the first cell, white without one.</summary>
    public Color FirstColor(Color? automatic) => this.FollowsCell ? Color.FromArgb(255, automatic ?? Color.White) : this.Color;

    /// <summary>
    /// Paints the fill over <paramref name="canvas"/>: the first color at its opacity, a gradient going to its
    /// second color — <paramref name="colors"/>.Last standing for Auto.
    /// </summary>
    public void Paint(Graphics g, Rectangle canvas, (Color? First, Color? Last) colors)
    {
        var first = Color.FromArgb((int)Math.Round(255 * this.Opacity), this.FirstColor(colors.First));
        this.Style.Paint(g, canvas, first, colors.Last);
    }
}
