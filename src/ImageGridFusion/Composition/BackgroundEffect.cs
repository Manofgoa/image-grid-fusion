using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>How the background fills the cell around its image: a flat color, or the image's edges extended.</summary>
public enum BackgroundFill
{
    /// <summary>The flat fill over the whole cell.</summary>
    Color,

    /// <summary>The edges extended; each corner the color of the image's corner pixel.</summary>
    CornerPixel,

    /// <summary>
    /// The edges extended; each corner split on its diagonal, each half the flat color of its edge's pixel
    /// next to the image's corner, the corner pixel's color drawn over the diagonal at half opacity.
    /// </summary>
    Miter,

    /// <summary>The edges extended; the corners keep the flat fill.</summary>
    BackgroundCorners,
}

/// <summary>Where the cell Background's first color comes from — its <b>From</b> (workfiles/20261105-cell-background-auto-colors.md).</summary>
public enum FirstColor
{
    /// <summary>The cell's automatic color: the band color of the part of the image shown, live.</summary>
    Auto,

    Black,

    White,

    /// <summary>A color picked with the eyedropper or Color….</summary>
    Chosen,
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

    /// <summary>Where the first color comes from: the automatic band color by default.</summary>
    public FirstColor First { get; private init; } = FirstColor.Auto;

    /// <summary>The first color is the band color computed from the part of the image shown.</summary>
    public bool Automatic => this.First == FirstColor.Auto;

    /// <summary>The chosen color, opaque; read in <see cref="FirstColor.Chosen"/> only.</summary>
    public Color Color { get; private init; }

    /// <summary>
    /// The automatic band color is read: the first color's source, or a gradient's second color
    /// (<see cref="SecondColor.Auto"/>, workfiles/20261105-cell-background-auto-colors.md).
    /// </summary>
    public bool NeedsAutomatic => this.Automatic || this.Style is { IsGradient: true, Second: SecondColor.Auto };

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

    /// <summary>
    /// From 0, the extended edges invisible over the flat fill, to 1, opaque; the flat fill's opacity
    /// untouched. Kept, unused, in the <see cref="BackgroundFill.Color"/> mode.
    /// </summary>
    public double EdgeOpacity { get; private init; } = 1;

    /// <summary>
    /// How the fill spreads its colors over the cell — solid, or a gradient from <see cref="Color"/> (or the
    /// automatic one) to a second color — the shared fill of the Grid background (workfiles/20260926-forced-background.md).
    /// </summary>
    public FillStyle Style { get; private init; } = FillStyle.Cell;

    /// <summary>The image's edges are stretched over the bands.</summary>
    public bool Extends => Mode != BackgroundFill.Color;

    /// <summary>
    /// The chosen color: <see cref="FirstColor.Chosen"/> from now on. From set to Chosen passes the color the
    /// source gave at that moment, which it freezes.
    /// </summary>
    public BackgroundEffect WithColor(Color color) => this with { First = FirstColor.Chosen, Color = Color.FromArgb(255, color) };

    /// <summary>Back to the automatic color: the chosen one is dropped, not remembered.</summary>
    public BackgroundEffect WithAutomatic() => this.WithFirst(FirstColor.Auto);

    /// <summary>
    /// The first color's source: Auto, Black or White drop the chosen color; Chosen keeps it — set it with
    /// <see cref="WithColor"/> to freeze the color of the moment.
    /// </summary>
    public BackgroundEffect WithFirst(FirstColor first) => first == FirstColor.Chosen
        ? this with { First = first }
        : this with { First = first, Color = default };

    public BackgroundEffect WithOpacity(double opacity) => this with { Opacity = Math.Clamp(opacity, 0, 1) };

    public BackgroundEffect WithMode(BackgroundFill mode) => this with { Mode = mode };

    public BackgroundEffect WithBlend(double blend) => this with { Blend = Math.Clamp(blend, 0, 1) };

    public BackgroundEffect WithSoften(bool soften) => this with { Soften = soften };

    public BackgroundEffect WithEdgeOpacity(double opacity) => this with { EdgeOpacity = Math.Clamp(opacity, 0, 1) };

    /// <summary>The fill's style; its <see cref="SecondColor.Auto"/> is the cell's automatic color (workfiles/20261105-cell-background-auto-colors.md).</summary>
    public BackgroundEffect WithStyle(FillStyle style) => this with { Style = style };

    /// <summary>The first color, opaque, given the <paramref name="automatic"/> band color.</summary>
    public Color FirstFor(Color automatic) => this.First switch
    {
        FirstColor.Auto => Color.FromArgb(255, automatic),
        FirstColor.Black => Color.Black,
        FirstColor.White => Color.White,
        _ => this.Color,
    };

    /// <summary>The fill, given the <paramref name="automatic"/> band color: the color in use at the opacity.</summary>
    public Color Fill(Color automatic) => Color.FromArgb((int)Math.Round(255 * Opacity), this.FirstFor(automatic));

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>), the color as <c>#RRGGBB</c>.</summary>
    internal JsonObject ToJson() => new()
    {
        ["first"] = this.First.ToString(),
        ["automatic"] = this.Automatic,
        ["color"] = this.First == FirstColor.Chosen ? $"#{this.Color.R:X2}{this.Color.G:X2}{this.Color.B:X2}" : null,
        ["opacity"] = this.Opacity,
        ["mode"] = this.Mode.ToString(),
        ["blend"] = this.Blend,
        ["soften"] = this.Soften,
        ["edgeOpacity"] = this.EdgeOpacity,
        ["fill"] = this.Style.ToJson(),
    };

    /// <summary>
    /// The settings <see cref="ToJson"/> wrote; an unreadable color gives the automatic one. A preset without
    /// <c>first</c>, saved before it existed, reads its source from <c>automatic</c>.
    /// </summary>
    internal static BackgroundEffect FromJson(JsonObject json)
    {
        var first = json["first"] is not null
            ? PresetJson.Enum(json, "first", Default.First)
            : PresetJson.Bool(json, "automatic", Default.Automatic) ? FirstColor.Auto : FirstColor.Chosen;
        var background = new BackgroundEffect
        {
            Opacity = Math.Clamp(PresetJson.Double(json, "opacity", Default.Opacity), 0, 1),
            Mode = PresetJson.Enum(json, "mode", Default.Mode),
            Blend = Math.Clamp(PresetJson.Double(json, "blend", Default.Blend), 0, 1),
            Soften = PresetJson.Bool(json, "soften", Default.Soften),
            EdgeOpacity = Math.Clamp(PresetJson.Double(json, "edgeOpacity", Default.EdgeOpacity), 0, 1),
        }.WithStyle(json["fill"] is JsonObject fill ? FillStyle.FromJson(fill, FillStyle.Cell) : FillStyle.Cell);
        if (first != FirstColor.Chosen)
        {
            return background.WithFirst(first);
        }

        return json["color"] is JsonValue value && value.TryGetValue(out string? hex)
            && hex.StartsWith('#') && int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb)
                ? background.WithColor(Color.FromArgb(rgb))
                : background;
    }
}
