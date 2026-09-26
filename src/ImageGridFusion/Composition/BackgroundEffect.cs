namespace ImageGridFusion.Composition;

/// <summary>
/// The background effect of an image: the fill painted behind it over its whole cell — the bands, and
/// the image's transparent pixels, show it — in the automatic band color or in a chosen color, at
/// <see cref="Opacity"/>. On by default; turned off, the cell is transparent behind its image
/// (RULES.md, the Background exception).
/// </summary>
public sealed record BackgroundEffect
{
    /// <summary>The automatic band color, fully opaque: the fill every image had before the effect existed.</summary>
    public static readonly BackgroundEffect Default = new();

    /// <summary>The band color computed from the part of the image shown; else <see cref="Color"/>.</summary>
    public bool Automatic { get; private init; } = true;

    /// <summary>The chosen color, opaque; unused while <see cref="Automatic"/>.</summary>
    public Color Color { get; private init; }

    /// <summary>From 0, no fill, to 1, the fill fully opaque.</summary>
    public double Opacity { get; private init; } = 1;

    /// <summary>
    /// The chosen color, the automatic mode left. Unchecking <i>Automatic color</i> passes the automatic
    /// color of the moment, which it freezes.
    /// </summary>
    public BackgroundEffect WithColor(Color color) => this with { Automatic = false, Color = Color.FromArgb(255, color) };

    /// <summary>Back to the automatic color: the chosen one is dropped, not remembered.</summary>
    public BackgroundEffect WithAutomatic() => this with { Automatic = true, Color = default };

    public BackgroundEffect WithOpacity(double opacity) => this with { Opacity = Math.Clamp(opacity, 0, 1) };

    /// <summary>The fill, given the <paramref name="automatic"/> band color: the color in use at the opacity.</summary>
    public Color Fill(Color automatic) => Color.FromArgb((int)Math.Round(255 * Opacity), Automatic ? automatic : Color);
}
