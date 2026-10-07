namespace ImageGridFusion.UI;

/// <summary>
/// How the light bulb's arrow to the word the OCR found is drawn — the ⚙ menu's OCR result style,
/// remembered between sessions: <paramref name="Curved"/>, an arc arriving vertically on the word, or
/// straight; <paramref name="Thickness"/>, the width of its colored stroke in logical pixels, the dark
/// halo not counted, 0 drawing no arrow; its <paramref name="Color"/>, the halo staying dark. See
/// workfiles/20261007-ocr-result-style.md.
/// </summary>
internal readonly record struct OcrArrowStyle(bool Curved, int Thickness, Color Color)
{
    public const int MinThickness = 0;
    public const int MaxThickness = 5;
    public const int DefaultThickness = 2;

    /// <summary>The arrow's color until one is chosen: the light bulb's amber.</summary>
    public static readonly Color DefaultColor = Color.FromArgb(214, 150, 0);

    /// <summary>Curved, 2 px, amber: the style until the ⚙ menu changes it.</summary>
    public static OcrArrowStyle Default => new(true, DefaultThickness, DefaultColor);

    /// <summary>The head's length and base width in logical pixels, growing with the stroke: 7 at 2 px, 2 more per pixel.</summary>
    public int Head => 3 + 2 * this.Thickness;
}
