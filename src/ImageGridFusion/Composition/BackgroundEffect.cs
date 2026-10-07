namespace ImageGridFusion.Composition;

/// <summary>How the background fills the cell around its image: a flat color, or the image's edges extended.</summary>
public enum BackgroundFill
{
    /// <summary>The flat fill over the whole cell.</summary>
    Color,

    /// <summary>The edges extended; each corner the color of the image's corner pixel.</summary>
    CornerPixel,

    /// <summary>The edges extended; each corner split on its diagonal, each half its band mirrored past the image's corner.</summary>
    Miter,

    /// <summary>The edges extended; the corners keep the flat fill.</summary>
    BackgroundCorners,
}

/// <summary>
/// The background effect of an image: the fill painted behind it over its whole cell — the bands, and
/// the image's transparent pixels, show it — in the automatic band color or in a chosen color, at
/// <see cref="Opacity"/>; in an extending <see cref="Mode"/>, the image's edge pixels are stretched over
/// the bands. On by default; turned off, the cell is transparent behind its image (RULES.md, the
/// Background exception).
/// </summary>
public sealed record BackgroundEffect
{
    /// <summary>The automatic band color, fully opaque: the fill every image had before the effect existed.</summary>
    public static readonly BackgroundEffect Default = new();

    /// <summary>The band color computed from the part of the image shown; else <see cref="Color"/>.</summary>
    public bool Automatic { get; private init; } = true;

    /// <summary>The chosen color, opaque; unused while <see cref="Automatic"/>.</summary>
    public Color Color { get; private init; }

    /// <summary>From 0, no fill, to 1, the fill fully opaque — the extended edges included.</summary>
    public double Opacity { get; private init; } = 1;

    /// <summary>The flat color, or the image's edges extended with one of the corner fills.</summary>
    public BackgroundFill Mode { get; private init; } = BackgroundFill.Color;

    /// <summary>
    /// From 0, the extended edges as they are, to 1, the extended edges blended into the flat fill by the
    /// cell's edge, linearly with the distance from the image. Kept, unused, in the <see cref="BackgroundFill.Color"/> mode.
    /// </summary>
    public double Blend { get; private init; }

    /// <summary>The extended edges blurred, more with the distance from the image. Kept, unused, in the <see cref="BackgroundFill.Color"/> mode.</summary>
    public bool Soften { get; private init; }

    /// <summary>The image's edges are stretched over the bands.</summary>
    public bool Extends => Mode != BackgroundFill.Color;

    /// <summary>
    /// The chosen color, the automatic mode left. Unchecking <i>Automatic color</i> passes the automatic
    /// color of the moment, which it freezes.
    /// </summary>
    public BackgroundEffect WithColor(Color color) => this with { Automatic = false, Color = Color.FromArgb(255, color) };

    /// <summary>Back to the automatic color: the chosen one is dropped, not remembered.</summary>
    public BackgroundEffect WithAutomatic() => this with { Automatic = true, Color = default };

    public BackgroundEffect WithOpacity(double opacity) => this with { Opacity = Math.Clamp(opacity, 0, 1) };

    public BackgroundEffect WithMode(BackgroundFill mode) => this with { Mode = mode };

    public BackgroundEffect WithBlend(double blend) => this with { Blend = Math.Clamp(blend, 0, 1) };

    public BackgroundEffect WithSoften(bool soften) => this with { Soften = soften };

    /// <summary>The fill, given the <paramref name="automatic"/> band color: the color in use at the opacity.</summary>
    public Color Fill(Color automatic) => Color.FromArgb((int)Math.Round(255 * Opacity), Automatic ? automatic : Color);
}
