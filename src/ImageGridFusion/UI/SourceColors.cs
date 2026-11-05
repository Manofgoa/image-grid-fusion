using System.Drawing.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// A secondary source's color: its <see cref="Text"/> — its name in the breadcrumb, its tile's file count, the status
/// line while it is open — and its drawn folder's <see cref="Back"/> and <see cref="Front"/>, Windows' folder
/// thumbnail tinted by <see cref="Tint"/>. See workfiles/20261105-secondary-file-sources.md § Source Colors.
/// </summary>
internal sealed class SourceColor
{
    public SourceColor(Color text, Color back, Color front)
    {
        this.Text = text;
        this.Back = back;
        this.Front = front;

        // The thumbnail's brightness, in shades of the front color — scaled so its strongest channel is whole —
        // lifted by a share of the back color, so the manila folder turns a mid shade of it.
        float strongest = Math.Max(1, Math.Max((int)front.R, Math.Max((int)front.G, (int)front.B)));
        float r = front.R / strongest, g = front.G / strongest, b = front.B / strongest;
        this.Tint = new ColorMatrix(
        [
            [0.30f * r, 0.30f * g, 0.30f * b, 0, 0],
            [0.59f * r, 0.59f * g, 0.59f * b, 0, 0],
            [0.11f * r, 0.11f * g, 0.11f * b, 0, 0],
            [0, 0, 0, 1, 0],
            [back.R / 255f * 0.3f, back.G / 255f * 0.3f, back.B / 255f * 0.3f, 0, 1],
        ]);
    }

    public Color Text { get; }

    public Color Back { get; }

    public Color Front { get; }

    public ColorMatrix Tint { get; }
}

/// <summary>
/// The secondary sources' palette, in the order a new source takes its color: blue, violet, teal, green, plum —
/// never a red, an orange or a yellow, which would read as an error or a warning.
/// </summary>
internal static class SourceColors
{
    public static readonly SourceColor[] Palette =
    [
        new(Color.FromArgb(0, 84, 190), Color.FromArgb(46, 108, 196), Color.FromArgb(92, 156, 232)),
        new(Color.FromArgb(106, 61, 184), Color.FromArgb(116, 72, 196), Color.FromArgb(161, 126, 230)),
        new(Color.FromArgb(0, 121, 107), Color.FromArgb(20, 138, 124), Color.FromArgb(77, 182, 168)),
        new(Color.FromArgb(46, 125, 50), Color.FromArgb(61, 139, 63), Color.FromArgb(116, 190, 110)),
        new(Color.FromArgb(142, 58, 138), Color.FromArgb(154, 69, 150), Color.FromArgb(199, 125, 194)),
    ];

    /// <summary>The color at <paramref name="index"/> of the palette, wrapped round.</summary>
    public static SourceColor Of(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];
}
