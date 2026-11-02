using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// How a text is drawn into a bitmap by <see cref="TextRaster"/>: its font, weight, slant and color, in
/// capitals or as typed, its lines' alignment, an outline around its letters, a shadow under them, a box behind it. The
/// outline's width, the shadow's size and the box's margin are fractions of a line.
/// </summary>
public sealed record LayerTextStyle(string Font, bool Bold, Color Color, bool Outline, Color OutlineColor, double OutlineWidth, bool Background, Color BackgroundColor, double BackgroundOpacity)
{
    public const double MinOutlineWidth = 0.02;
    public const double MaxOutlineWidth = 0.2;
    public const double MinShadowSize = 0.02;
    public const double MaxShadowSize = 0.3;

    /// <summary>The box's margin around the text, in lines.</summary>
    public const double BackgroundMargin = 0.15;

    /// <summary>Segoe UI bold, white, outlined black over 8 % of a line, no shadow, no box (workfiles/20261029-layers.md).</summary>
    public static readonly LayerTextStyle Default = new("Segoe UI", true, Color.White, true, Color.Black, 0.08, false, Color.Black, 0.5);

    /// <summary>The italic face, DirectWrite's oblique when the family has none (workfiles/20261029-text-layer-style-presets.md).</summary>
    public bool Italic { get; init; }

    /// <summary>The text drawn in capitals, the text typed kept as it is.</summary>
    public bool Caps { get; init; }

    /// <summary>How the lines stand within the widest one: centered by default (workfiles/20261029-text-layer-alignment.md).</summary>
    public LayerTextAlignment Alignment { get; init; } = LayerTextAlignment.Center;

    /// <summary>A blur of the letters — and of their outline — in <see cref="ShadowColor"/>, under them.</summary>
    public bool Shadow { get; init; }

    public ShadowKind ShadowKind { get; init; }

    public Color ShadowColor { get; init; } = Color.Black;

    /// <summary>The halo's reach, or the drop's offset, in lines.</summary>
    public double ShadowSize { get; init; } = 0.08;

    /// <summary>
    /// The letters' opacity, their emojis' included, 0 to 1: the outline is cut out under them, so a translucent text
    /// reads as one color (workfiles/20261029-eyedropper-ergonomics.md § Color Opacity).
    /// </summary>
    public double Opacity { get; init; } = 1;

    /// <summary>The outline's opacity, 0 to 1.</summary>
    public double OutlineOpacity { get; init; } = 1;

    /// <summary>The shadow's opacity, 0 to 1.</summary>
    public double ShadowOpacity { get; init; } = 1;

    /// <summary>
    /// The font the emojis are drawn in, the letters staying in <see cref="Font"/>: Windows' Segoe UI Emoji by default, else
    /// an emoji font installed or shipped (workfiles/20261102-emoji-fonts.md); a font missing draws as the default.
    /// </summary>
    public string EmojiFont { get; init; } = EmojiFonts.Default;

    /// <summary>The text as drawn: in capitals with the current culture under <see cref="Caps"/>, the emojis untouched.</summary>
    public string Drawn(string text) => this.Caps ? text.ToUpper(System.Globalization.CultureInfo.CurrentCulture) : text;

    /// <summary>The style under named keys, each setting by hand (RULES.md § Effect Presets): an older file reads a later one.</summary>
    public JsonObject ToJson() => new()
    {
        ["font"] = this.Font,
        ["emojiFont"] = this.EmojiFont,
        ["bold"] = this.Bold,
        ["italic"] = this.Italic,
        ["caps"] = this.Caps,
        ["alignment"] = this.Alignment.ToString(),
        ["color"] = Hex(this.Color),
        ["opacity"] = this.Opacity,
        ["outline"] = this.Outline,
        ["outlineColor"] = Hex(this.OutlineColor),
        ["outlineOpacity"] = this.OutlineOpacity,
        ["outlineWidth"] = this.OutlineWidth,
        ["shadow"] = this.Shadow,
        ["shadowKind"] = this.ShadowKind.ToString(),
        ["shadowColor"] = Hex(this.ShadowColor),
        ["shadowOpacity"] = this.ShadowOpacity,
        ["shadowSize"] = this.ShadowSize,
        ["background"] = this.Background,
        ["backgroundColor"] = Hex(this.BackgroundColor),
        ["backgroundOpacity"] = this.BackgroundOpacity,
    };

    /// <summary>The style <see cref="ToJson"/> wrote; a missing or foreign value gives <see cref="Default"/>'s.</summary>
    public static LayerTextStyle FromJson(JsonObject json) =>
        new(
            json["font"] is JsonValue font && font.TryGetValue(out string? family) && family.Trim().Length > 0 ? family.Trim() : Default.Font,
            PresetJson.Bool(json, "bold", Default.Bold),
            HexColor(json, "color", Default.Color),
            PresetJson.Bool(json, "outline", Default.Outline),
            HexColor(json, "outlineColor", Default.OutlineColor),
            Math.Clamp(PresetJson.Double(json, "outlineWidth", Default.OutlineWidth), MinOutlineWidth, MaxOutlineWidth),
            PresetJson.Bool(json, "background", Default.Background),
            HexColor(json, "backgroundColor", Default.BackgroundColor),
            Math.Clamp(PresetJson.Double(json, "backgroundOpacity", Default.BackgroundOpacity), 0, 1))
        {
            EmojiFont = json["emojiFont"] is JsonValue emoji && emoji.TryGetValue(out string? emojiFamily) && emojiFamily.Trim().Length > 0 ? emojiFamily.Trim() : Default.EmojiFont,
            Italic = PresetJson.Bool(json, "italic", Default.Italic),
            Caps = PresetJson.Bool(json, "caps", Default.Caps),
            Alignment = PresetJson.Enum(json, "alignment", Default.Alignment),
            Shadow = PresetJson.Bool(json, "shadow", Default.Shadow),
            ShadowKind = PresetJson.Enum(json, "shadowKind", Default.ShadowKind),
            ShadowColor = HexColor(json, "shadowColor", Default.ShadowColor),
            ShadowSize = Math.Clamp(PresetJson.Double(json, "shadowSize", Default.ShadowSize), MinShadowSize, MaxShadowSize),
            Opacity = Math.Clamp(PresetJson.Double(json, "opacity", Default.Opacity), 0, 1),
            OutlineOpacity = Math.Clamp(PresetJson.Double(json, "outlineOpacity", Default.OutlineOpacity), 0, 1),
            ShadowOpacity = Math.Clamp(PresetJson.Double(json, "shadowOpacity", Default.ShadowOpacity), 0, 1),
        };

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color HexColor(JsonObject json, string key, Color fallback) =>
        json[key] is JsonValue value && value.TryGetValue(out string? hex) && hex.Length == 7 && hex.StartsWith('#')
            && int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb)
                ? Color.FromArgb(255, Color.FromArgb(rgb))
                : fallback;
}

/// <summary>
/// How a text layer's lines stand within the widest one. Justified stretches every line of a paragraph to the
/// widest by its spaces, but the paragraph's last and a line without a space, left-aligned; a blank line ends
/// a paragraph.
/// </summary>
public enum LayerTextAlignment
{
    Left,
    Center,
    Right,
    Justified,
}

/// <summary>A text shadow's shape: a soft halo all around the letters, or dropped down to the right.</summary>
public enum ShadowKind
{
    Halo,
    Drop,
}

/// <summary>
/// Draws a text into a bitmap with DirectWrite through Direct2D — the emojis in their own colors, which
/// GDI and GDI+ cannot draw (workfiles/20261029-layers.md § Rendering). Windows' own components, called
/// through their COM tables here rather than through a library; used on the UI thread.
/// </summary>
internal static unsafe class TextRaster
{
    /// <summary>The font size the text is drawn at, in pixels: large enough for a 4K export, drawn once per change.</summary>
    public const float EmSize = 192;

    // The longer side of the bitmap: a longer text is drawn smaller.
    private const int MaxSide = 8192;

    // Rings of copies drawn around the text, in its outline's color, under it.
    private static readonly int[] OutlineRings = [40, 24, 12];

    // How far an italic's last letter may lean past the text's width, in lines.
    private const double ItalicOverhang = 0.2;

    // The box blurs a shadow's mask goes through, approaching a Gaussian.
    private const int ShadowBlurPasses = 3;

    private static readonly Guid FactoryId = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid WriteFactoryId = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
    private static readonly Guid ImagingFactoryClass = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    private static readonly Guid ImagingFactoryId = new("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
    private static readonly Guid TextLayout1Id = new("9064d822-80a7-465c-a986-df65f78b8feb");
    private static readonly Guid WriteFactory5Id = new("958db99a-be2a-4f09-af7d-65189803d1d3");
    private static readonly Guid Font2Id = new("29748ed6-8c9c-4a6a-be0b-d912e8538944");
    private static readonly Guid PremultipliedBgra = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    // The flag font: Twemoji Mozilla cut down to its flags (workfiles/20261102-text-flag-emojis.md § The Flag Font).
    private const string FlagFontResource = "ImageGridFusion.Imaging.TwemojiFlags.ttf";
    private const string FlagFamily = "Twemoji Mozilla";

    // An emoji alone, for a text cell: Segoe UI, the other emojis falling back to Segoe UI Emoji.
    private static readonly LayerTextStyle Glyphs = LayerTextStyle.Default with { Bold = false, Color = Color.Black, Outline = false, Alignment = LayerTextAlignment.Left };

    /// <summary>
    /// The text drawn in <paramref name="style"/>, on a transparent background, and the height of one of its
    /// lines in the bitmap's pixels — what a text layer's size measures. An empty text gives an empty box
    /// half a line wide. In a <paramref name="box"/> — its width in lines — the lines are wrapped within it,
    /// and the bitmap is as wide as it (workfiles/20261029-text-layer-alignment.md § Size Mode).
    /// </summary>
    public static (Bitmap Bitmap, double LinePixels) Render(string text, LayerTextStyle style, double? box = null)
    {
        var factories = Factories.Get();
        text = style.Drawn(text);
        bool colorShapes = EmojiFontOf(factories, style.EmojiFont) is not null;
        float em = EmSize;
        for (int attempt = 0; ; attempt++)
        {
            var layout = CreateLayout(factories, text.Length == 0 ? " " : text, style, em, box);
            try
            {
                var metrics = Metrics(layout);
                double line = metrics.Height / Math.Max(1, metrics.LineCount);
                float width = box is not null ? metrics.LayoutWidth : text.Length == 0 ? (float)line / 2 : metrics.WidthIncludingTrailingWhitespace;
                double outline = style.Outline ? style.OutlineWidth * line : 0;
                double shadow = style.Shadow && text.Length > 0 ? outline + ShadowReach(style, line) : 0;
                double pad = Math.Max(Math.Max(outline, shadow), style.Background ? LayerTextStyle.BackgroundMargin * line : 0)
                    + (style.Italic ? ItalicOverhang * line : 0) + 2;
                int w = (int)Math.Ceiling(width + 2 * pad), h = (int)Math.Ceiling(metrics.Height + 2 * pad);
                if (Math.Max(w, h) > MaxSide && attempt == 0)
                {
                    em *= MaxSide / (float)Math.Max(w, h) * 0.98f;
                    continue;
                }

                var bitmap = Draw(factories, layout, text.Length == 0, style, new Size(Math.Max(1, w), Math.Max(1, h)), (float)pad, width, metrics.Height, line, colorShapes);
                return (bitmap, line);
            }
            finally
            {
                Marshal.Release(layout);
            }
        }
    }

    /// <summary>
    /// The emojis of <paramref name="text"/> in their own colors, at a font size of <paramref name="em"/> pixels, on a
    /// transparent background as wide and as tall as the line DirectWrite lays them out on: an emoji of a text cell, which
    /// GDI+ draws in one color (workfiles/20261102-text-flag-emojis.md § Text Cells).
    /// </summary>
    public static Bitmap RenderGlyphs(string text, float em)
    {
        var factories = Factories.Get();
        var layout = CreateLayout(factories, text, Glyphs, Math.Max(1, em), null);
        try
        {
            var metrics = Metrics(layout);
            var size = new Size(Math.Max(1, (int)Math.Ceiling(metrics.WidthIncludingTrailingWhitespace)), Math.Max(1, (int)Math.Ceiling(metrics.Height)));
            return ToBitmap(Paint(factories, size, target => PaintLetters(target, layout, Glyphs, 0)), size);
        }
        finally
        {
            Marshal.Release(layout);
        }
    }

    /// <summary>The families of the fonts installed, A→Z, for the font drop-down: the emoji fonts left out, they have their own.</summary>
    public static IReadOnlyList<string> FontFamilies()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        var emoji = InstalledEmojiFamilies().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. fonts.Families.Select(f => f.Name).Where(n => !emoji.Contains(n)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// The emoji fonts, for the Emoji drop-down: <see cref="EmojiFonts.Default"/> first, then the ones installed and the
    /// ones shipped there, A→Z, a family once (workfiles/20261102-emoji-fonts.md § User Interface).
    /// </summary>
    public static IReadOnlyList<string> EmojiFamilies() =>
    [
        EmojiFonts.Default,
        .. InstalledEmojiFamilies().Concat(EmojiFonts.Present.Select(f => f.Family))
            .Where(n => !string.Equals(n, EmojiFonts.Default, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>
    /// The installed families that are emoji fonts — color fonts holding 😀 (U+1F600), whatever their name — read once
    /// from the system's collection.
    /// </summary>
    public static IReadOnlyList<string> InstalledEmojiFamilies() => _installedEmoji ??= ReadInstalledEmojiFamilies(Factories.Get());

    private static IReadOnlyList<string>? _installedEmoji;

    private static IntPtr CreateLayout(Factories factories, string text, LayerTextStyle style, float em, double? box)
    {
        IntPtr write = factories.Write;
        if (factories.Flags != IntPtr.Zero)
        {
            text = Emojis.Joined(text);
        }

        IntPtr format;
        fixed (char* family = style.Font, locale = "en-us")
        {
            // CreateTextFormat: the family, the system's collection, the weight, the style (normal or italic), normal stretch, the size, the locale.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, int, int, int, float, char*, IntPtr*, int>)Slot(write, 15))(
                write, family, IntPtr.Zero, style.Bold ? 700 : 400, style.Italic ? 2 : 0, 5, em, locale, &format));
        }

        try
        {
            IntPtr layout;
            fixed (char* chars = text)
            {
                // CreateTextLayout, wide enough that only the line breaks of the text end a line.
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, int, IntPtr, float, float, IntPtr*, int>)Slot(write, 18))(
                    write, chars, text.Length, format, 1e6f, 1e6f, &layout));
            }

            // The emoji font on each emoji, then the flag font on the flags it does not draw itself.
            var emoji = EmojiFontOf(factories, style.EmojiFont);
            if (emoji is { } font)
            {
                SetEmojiFont(layout, font.Collection, font.Family, text);
            }

            SetFlagFont(layout, factories.Flags, text, joinedOnly: emoji?.HasFlags == true);

            // SetWordWrapping(NO_WRAP): the lines measured as typed, a line's height read from them.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 5))(layout, 1));
            var metrics = Metrics(layout);
            if (box is { } lines)
            {
                // In a box: SetWordWrapping(EMERGENCY_BREAK) within its width — a word longer than it cut —, then
                // SetTextAlignment's LEADING, TRAILING, CENTER or JUSTIFIED, DirectWrite's own: each typed line
                // break ends a paragraph, its last line left-aligned.
                float line = metrics.Height / Math.Max(1, metrics.LineCount);
                Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 5))(layout, 2));
                Check(((delegate* unmanaged[Stdcall]<IntPtr, float, int>)Slot(layout, 28))(layout, Math.Max(1, (float)lines * line)));
                Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 3))(layout, style.Alignment switch
                {
                    LayerTextAlignment.Right => 1,
                    LayerTextAlignment.Center => 2,
                    LayerTextAlignment.Justified => 3,
                    _ => 0,
                }));
                return layout;
            }

            // Automatic: the lines aligned within the widest once it is known — SetTextAlignment's LEADING,
            // TRAILING or CENTER; justified, LEADING with the lines stretched by the app.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, float, int>)Slot(layout, 28))(layout, Math.Max(1, metrics.WidthIncludingTrailingWhitespace)));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 3))(layout, style.Alignment switch
            {
                LayerTextAlignment.Right => 1,
                LayerTextAlignment.Center => 2,
                _ => 0,
            }));
            if (style.Alignment == LayerTextAlignment.Justified)
            {
                Justify(layout, text.Length);
            }

            return layout;
        }
        finally
        {
            Marshal.Release(format);
        }
    }

    /// <summary>
    /// Stretches the lines of a paragraph to the text's widest line by widening their spaces between words, its last line
    /// and a line without such a space left as they are; a blank line ends a paragraph. DirectWrite's own
    /// JUSTIFIED never stretches a line ended by a line break — every line of a text layer — so the spaces are
    /// widened here, by IDWriteTextLayout1's character spacing.
    /// </summary>
    private static void Justify(IntPtr layout, int length)
    {
        // GetClusterMetrics: at most one cluster per character, in the text's order.
        var clusters = new ClusterMetrics[Math.Max(1, length)];
        uint count;
        fixed (ClusterMetrics* buffer = clusters)
        {
            Check(((delegate* unmanaged[Stdcall]<IntPtr, ClusterMetrics*, uint, uint*, int>)Slot(layout, 62))(layout, buffer, (uint)clusters.Length, &count));
        }

        // Each line: its width up to its last visible cluster (-1 for a blank one), the spaces between its words.
        var lines = new List<(float Width, List<int> Spaces)>();
        for (int first = 0; first < count;)
        {
            int end = first;
            while (end < count && !clusters[end].IsNewline)
            {
                end++;
            }

            int start = first, last = end - 1;
            while (start < end && clusters[start].IsWhitespace)
            {
                start++;
            }

            while (last >= start && clusters[last].IsWhitespace)
            {
                last--;
            }

            float width = 0;
            var spaces = new List<int>();
            for (int i = first; i <= last; i++)
            {
                width += clusters[i].Width;
                if (i > start && clusters[i].IsWhitespace)
                {
                    spaces.Add(i);
                }
            }

            lines.Add((start <= last ? width : -1, spaces));
            first = end + 1;
        }

        float widest = lines.Count == 0 ? 0 : lines.Max(l => l.Width);
        if (widest <= 0 || Marshal.QueryInterface(layout, in TextLayout1Id, out var layout1) < 0)
        {
            return;
        }

        try
        {
            var positions = new uint[count + 1];
            for (int i = 0; i < count; i++)
            {
                positions[i + 1] = positions[i] + clusters[i].Length;
            }

            for (int l = 0; l < lines.Count; l++)
            {
                var (width, spaces) = lines[l];
                bool lastOfParagraph = l == lines.Count - 1 || lines[l + 1].Width < 0;
                if (lastOfParagraph || spaces.Count == 0 || width >= widest)
                {
                    continue;
                }

                // SetCharacterSpacing(leading, trailing, minimum advance, range): each space between the words widened alike.
                float extra = (widest - width) / spaces.Count;
                foreach (int space in spaces)
                {
                    var range = new TextRange { StartPosition = positions[space], Length = clusters[space].Length };
                    Check(((delegate* unmanaged[Stdcall]<IntPtr, float, float, float, TextRange, int>)Slot(layout1, 69))(layout1, 0, extra, 0, range));
                }
            }
        }
        finally
        {
            Marshal.Release(layout1);
        }
    }

    private static TextMetrics Metrics(IntPtr layout)
    {
        TextMetrics metrics;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, TextMetrics*, int>)Slot(layout, 60))(layout, &metrics));
        return metrics;
    }

    /// <summary>
    /// The text drawn from the bottom up: the box, the shadow, the outline, the letters. Without a shadow nor a
    /// translucent outline or letters, in one pass; else the shadow's mask is drawn and blurred apart, the text
    /// made translucent apart (<see cref="Text"/>), then put together.
    /// </summary>
    private static Bitmap Draw(Factories factories, IntPtr layout, bool empty, LayerTextStyle style, Size size, float pad, float width, float height, double line, bool colorShapes)
    {
        bool translucent = style.Opacity < 1 || (style.Outline && style.OutlineOpacity < 1);
        bool shaped = !empty && (style.Shadow || style.Outline);
        var shape = new Shape(layout, colorShapes && shaped ? Paint(factories, size, target => PaintColorShape(target, layout, pad)) : null, size, pad);
        byte[] pixels;
        if (empty || (!style.Shadow && !translucent))
        {
            pixels = Paint(factories, size, target =>
            {
                PaintBox(target, style, pad, width, height, line);
                if (!empty)
                {
                    PaintText(target, shape, style, pad, line);
                }
            });
        }
        else
        {
            pixels = style.Background ? Paint(factories, size, target => PaintBox(target, style, pad, width, height, line)) : new byte[size.Width * size.Height * 4];
            if (style.Shadow)
            {
                double reach = style.ShadowSize * line;
                bool halo = style.ShadowKind == ShadowKind.Halo;
                float offset = halo ? 0 : (float)(reach / Math.Sqrt(2));
                double spread = (style.Outline ? style.OutlineWidth * line : 0) + (halo ? reach / 2 : 0);
                var mask = Paint(factories, size, target => PaintMask(target, shape, pad + offset, spread));
                var shadow = Shadow(mask, size, reach / 2, style.ShadowColor);
                Fade(shadow, style.ShadowOpacity);
                Over(pixels, shadow);
            }

            Over(pixels, Text(factories, shape, style, size, pad, line));
        }

        return ToBitmap(pixels, size);
    }

    /// <summary>Premultiplied BGRA pixels, row by row, as a bitmap.</summary>
    private static Bitmap ToBitmap(byte[] pixels, Size size)
    {
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < size.Height; y++)
            {
                Marshal.Copy(pixels, y * size.Width * 4, data.Scan0 + y * data.Stride, size.Width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>A transparent bitmap of <paramref name="size"/> that <paramref name="paint"/> draws into, its premultiplied BGRA pixels row by row.</summary>
    private static byte[] Paint(Factories factories, Size size, Action<IntPtr> paint)
    {
        IntPtr wic = IntPtr.Zero, target = IntPtr.Zero;
        try
        {
            var format = PremultipliedBgra;

            // IWICImagingFactory.CreateBitmap, cached on load.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int, Guid*, int, IntPtr*, int>)Slot(factories.Imaging, 17))(
                factories.Imaging, size.Width, size.Height, &format, 2, &wic));

            // ID2D1Factory.CreateWicBitmapRenderTarget: BGRA premultiplied, at 96 dpi.
            var properties = new RenderTargetProperties { Format = 87, AlphaMode = 1, DpiX = 96, DpiY = 96 };
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, RenderTargetProperties*, IntPtr*, int>)Slot(factories.Direct2D, 13))(
                factories.Direct2D, wic, &properties, &target));

            ((delegate* unmanaged[Stdcall]<IntPtr, void>)Slot(target, 48))(target);
            var clear = default(D2DColor);
            ((delegate* unmanaged[Stdcall]<IntPtr, D2DColor*, void>)Slot(target, 47))(target, &clear);

            // Grayscale antialiasing: ClearType needs an opaque background.
            ((delegate* unmanaged[Stdcall]<IntPtr, int, void>)Slot(target, 34))(target, 2);
            paint(target);
            Check(((delegate* unmanaged[Stdcall]<IntPtr, ulong*, ulong*, int>)Slot(target, 49))(target, null, null));

            // IWICBitmapSource.CopyPixels: the whole bitmap, row by row.
            var pixels = new byte[size.Width * size.Height * 4];
            fixed (byte* bytes = pixels)
            {
                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, int, IntPtr, int>)Slot(wic, 7))(
                    wic, IntPtr.Zero, size.Width * 4, pixels.Length, (IntPtr)bytes));
            }

            return pixels;
        }
        finally
        {
            if (target != IntPtr.Zero)
            {
                Marshal.Release(target);
            }

            if (wic != IntPtr.Zero)
            {
                Marshal.Release(wic);
            }
        }
    }

    private static void PaintBox(IntPtr target, LayerTextStyle style, float pad, float width, float height, double line)
    {
        if (style.Background)
        {
            float margin = (float)(LayerTextStyle.BackgroundMargin * line);
            var box = new D2DRect { Left = pad - margin, Top = pad - margin, Right = pad + width + margin, Bottom = pad + height + margin };
            Fill(target, box, Color.FromArgb((int)Math.Round(Math.Clamp(style.BackgroundOpacity, 0, 1) * 255), style.BackgroundColor));
        }
    }

    /// <summary>
    /// The outline and the letters, each at its opacity, the outline cut out under the letters: from the two drawn
    /// together (premultiplied) and the letters alone, the outline is what the letters leave of it, so the
    /// antialiased edges come out as they do opaque.
    /// </summary>
    private static byte[] Text(Factories factories, Shape shape, LayerTextStyle style, Size size, float pad, double line)
    {
        var text = Paint(factories, size, target => PaintText(target, shape, style, pad, line));
        double letters = Math.Clamp(style.Opacity, 0, 1);
        double outline = style.Outline ? Math.Clamp(style.OutlineOpacity, 0, 1) : 1;
        if (letters >= 1 && outline >= 1)
        {
            return text;
        }

        var alone = Paint(factories, size, target => PaintLetters(target, shape.Layout, style, pad));
        for (int i = 0; i < text.Length; i++)
        {
            text[i] = (byte)Math.Clamp((int)Math.Round((text[i] - alone[i]) * outline + alone[i] * letters), 0, 255);
        }

        return text;
    }

    /// <summary>A premultiplied layer made <paramref name="opacity"/> times as opaque.</summary>
    private static void Fade(byte[] pixels, double opacity)
    {
        opacity = Math.Clamp(opacity, 0, 1);
        if (opacity >= 1)
        {
            return;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)Math.Round(pixels[i] * opacity);
        }
    }

    /// <summary>The outline — copies around the text in its color, its emojis as plain shapes of that color — then the letters.</summary>
    private static void PaintText(IntPtr target, Shape shape, LayerTextStyle style, float pad, double line)
    {
        if (style.Outline)
        {
            IntPtr outline = Brush(target, style.OutlineColor);
            try
            {
                DrawShapes(target, shape, outline, Rings(pad, style.OutlineWidth * line));
            }
            finally
            {
                Marshal.Release(outline);
            }
        }

        PaintLetters(target, shape.Layout, style, pad);
    }

    /// <summary>The letters in their color, the emojis in theirs.</summary>
    private static void PaintLetters(IntPtr target, IntPtr layout, LayerTextStyle style, float pad)
    {
        IntPtr brush = Brush(target, style.Color);
        try
        {
            DrawText(target, layout, brush, pad, pad, colorFont: true);
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>The shape casting the shadow: the letters, their emojis as plain shapes, grown by <paramref name="spread"/>.</summary>
    private static void PaintMask(IntPtr target, Shape shape, float at, double spread)
    {
        IntPtr brush = Brush(target, Color.Black);
        try
        {
            DrawShapes(target, shape, brush, Rings(at, spread).Append((at, at)));
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>Where the copies of the text go in rings around <paramref name="at"/>, out to <paramref name="radius"/>: the text grown by it.</summary>
    private static IEnumerable<(float X, float Y)> Rings(float at, double radius)
    {
        if (radius <= 0)
        {
            yield break;
        }

        for (int ring = 0; ring < OutlineRings.Length; ring++)
        {
            double r = radius * (OutlineRings.Length - ring) / OutlineRings.Length;
            int copies = OutlineRings[ring];
            for (int k = 0; k < copies; k++)
            {
                double angle = 2 * Math.PI * k / copies;
                yield return (at + (float)(r * Math.Cos(angle)), at + (float)(r * Math.Sin(angle)));
            }
        }
    }

    /// <summary>
    /// The text's shape in <paramref name="brush"/> at each of <paramref name="places"/> — where the text drawn at the
    /// pad's offset would be: its plain shapes, or the alpha of the text drawn in color
    /// (<see cref="Shape.Alpha"/>) as an opacity mask.
    /// </summary>
    private static void DrawShapes(IntPtr target, Shape shape, IntPtr brush, IEnumerable<(float X, float Y)> places)
    {
        if (shape.Alpha is null)
        {
            foreach (var (x, y) in places)
            {
                DrawText(target, shape.Layout, brush, x, y, colorFont: false);
            }

            return;
        }

        // ID2D1RenderTarget.CreateBitmap: the alpha as a premultiplied BGRA bitmap of the target.
        IntPtr mask;
        var properties = new BitmapProperties { Format = 87, AlphaMode = 1, DpiX = 96, DpiY = 96 };
        fixed (byte* pixels = shape.Alpha)
        {
            Check(((delegate* unmanaged[Stdcall]<IntPtr, SizeU, byte*, uint, BitmapProperties*, IntPtr*, int>)Slot(target, 4))(
                target, new SizeU { Width = (uint)shape.Size.Width, Height = (uint)shape.Size.Height }, pixels, (uint)shape.Size.Width * 4, &properties, &mask));
        }

        try
        {
            // SetAntialiasMode(ALIASED): FillOpacityMask's requirement; FillOpacityMask(mask, brush, GRAPHICS, the place, the whole mask).
            ((delegate* unmanaged[Stdcall]<IntPtr, int, void>)Slot(target, 32))(target, 1);
            foreach (var (x, y) in places)
            {
                var rect = new D2DRect { Left = x - shape.Pad, Top = y - shape.Pad, Right = x - shape.Pad + shape.Size.Width, Bottom = y - shape.Pad + shape.Size.Height };
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int, D2DRect*, D2DRect*, void>)Slot(target, 25))(target, mask, brush, 0, &rect, null);
            }
        }
        finally
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, int, void>)Slot(target, 32))(target, 0);
            Marshal.Release(mask);
        }
    }

    /// <summary>The text drawn in color, opaque, at the pad's offset: the shape of an emoji font's emojis, which have no plain one.</summary>
    private static void PaintColorShape(IntPtr target, IntPtr layout, float pad)
    {
        IntPtr brush = Brush(target, Color.Black);
        try
        {
            DrawText(target, layout, brush, pad, pad, colorFont: true);
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>How far a shadow reaches past the letters and their outline, in pixels: a halo's spread and blur, a drop's offset and blur.</summary>
    private static double ShadowReach(LayerTextStyle style, double line)
    {
        double reach = style.ShadowSize * line;
        return style.ShadowKind == ShadowKind.Halo ? reach : reach / Math.Sqrt(2) + reach / 2;
    }

    /// <summary>The shadow's pixels: <paramref name="mask"/>'s alpha blurred over <paramref name="blur"/> pixels, in <paramref name="color"/>.</summary>
    private static byte[] Shadow(byte[] mask, Size size, double blur, Color color)
    {
        var alpha = new float[size.Width * size.Height];
        for (int i = 0; i < alpha.Length; i++)
        {
            alpha[i] = mask[i * 4 + 3];
        }

        int radius = Math.Max(1, (int)Math.Round(blur / ShadowBlurPasses));
        var scratch = new float[Math.Max(size.Width, size.Height)];
        for (int pass = 0; pass < ShadowBlurPasses; pass++)
        {
            BoxBlur(alpha, size.Width, size.Height, 1, size.Width, radius, scratch);
            BoxBlur(alpha, size.Height, size.Width, size.Width, 1, radius, scratch);
        }

        var shadow = new byte[mask.Length];
        for (int i = 0; i < alpha.Length; i++)
        {
            float a = Math.Clamp(alpha[i], 0, 255) * color.A / 255f;
            shadow[i * 4] = (byte)Math.Round(color.B * a / 255);
            shadow[i * 4 + 1] = (byte)Math.Round(color.G * a / 255);
            shadow[i * 4 + 2] = (byte)Math.Round(color.R * a / 255);
            shadow[i * 4 + 3] = (byte)Math.Round(a);
        }

        return shadow;
    }

    /// <summary>
    /// A box blur of <paramref name="radius"/> along each of <paramref name="lines"/> lines of
    /// <paramref name="length"/> values — <paramref name="step"/> apart within a line, <paramref name="lineStep"/>
    /// from one line to the next — the values past the ends taken as 0.
    /// </summary>
    private static void BoxBlur(float[] values, int length, int lines, int step, int lineStep, int radius, float[] scratch)
    {
        float scale = 1f / (2 * radius + 1);
        for (int l = 0; l < lines; l++)
        {
            int start = l * lineStep;
            float sum = 0;
            for (int i = 0; i < Math.Min(radius, length); i++)
            {
                sum += values[start + i * step];
            }

            for (int i = 0; i < length; i++)
            {
                if (i + radius < length)
                {
                    sum += values[start + (i + radius) * step];
                }

                scratch[i] = sum * scale;
                if (i - radius >= 0)
                {
                    sum -= values[start + (i - radius) * step];
                }
            }

            for (int i = 0; i < length; i++)
            {
                values[start + i * step] = scratch[i];
            }
        }
    }

    /// <summary><paramref name="top"/> laid over <paramref name="pixels"/>, both premultiplied BGRA.</summary>
    private static void Over(byte[] pixels, byte[] top)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int keep = 255 - top[i + 3];
            for (int c = 0; c < 4; c++)
            {
                pixels[i + c] = (byte)Math.Min(255, top[i + c] + (pixels[i + c] * keep + 127) / 255);
            }
        }
    }

    /// <summary>ID2D1RenderTarget.DrawTextLayout, unsnapped so the outline's copies land between pixels; the color glyphs on demand.</summary>
    private static void DrawText(IntPtr target, IntPtr layout, IntPtr brush, float x, float y, bool colorFont) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, D2DPoint, IntPtr, IntPtr, int, void>)Slot(target, 28))(
            target, new D2DPoint { X = x, Y = y }, layout, brush, colorFont ? 0x5 : 0x1);

    private static void Fill(IntPtr target, D2DRect rect, Color color)
    {
        IntPtr brush = Brush(target, color);
        try
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, D2DRect*, IntPtr, void>)Slot(target, 17))(target, &rect, brush);
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>ID2D1RenderTarget.CreateSolidColorBrush: the caller's to release.</summary>
    private static IntPtr Brush(IntPtr target, Color color)
    {
        var d2d = new D2DColor { R = color.R / 255f, G = color.G / 255f, B = color.B / 255f, A = color.A / 255f };
        IntPtr brush;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, D2DColor*, IntPtr, IntPtr*, int>)Slot(target, 8))(target, &d2d, IntPtr.Zero, &brush));
        return brush;
    }

    private static void* Slot(IntPtr com, int index) => (*(void***)com)[index];

    private static void Check(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    [DllImport("d2d1.dll", ExactSpelling = true)]
    private static extern int D2D1CreateFactory(int type, in Guid id, IntPtr options, out IntPtr factory);

    [DllImport("dwrite.dll", ExactSpelling = true)]
    private static extern int DWriteCreateFactory(int type, in Guid id, out IntPtr factory);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid classId, IntPtr outer, int context, in Guid id, out IntPtr instance);

    /// <summary>
    /// The flag font in a collection of its own, loaded from the exe's resources: what <see cref="SetFlagFont"/> gives the
    /// flags of a layout (workfiles/20261102-text-flag-emojis.md § Text Layers). Zero where it cannot be made — before
    /// Windows 10 1703 —: the flags are then drawn as Windows draws them, as letters.
    /// </summary>
    private static IntPtr CreateFlagCollection(IntPtr write)
    {
        byte[] font;
        using (var stream = typeof(TextRaster).Assembly.GetManifestResourceStream(FlagFontResource)!)
        using (var copy = new MemoryStream())
        {
            stream.CopyTo(copy);
            font = copy.ToArray();
        }

        return CreateMemoryCollection(write, font);
    }

    /// <summary>
    /// A font given as its bytes, in a collection of its own: the flag font, an emoji font of <see cref="EmojiFonts.Folder"/>
    /// — read into memory, so its file is never held open and an update can replace it. Zero where it cannot be made —
    /// before Windows 10 1703 —, or the bytes are no font.
    /// </summary>
    private static IntPtr CreateMemoryCollection(IntPtr write, byte[] font)
    {
        IntPtr factory5 = IntPtr.Zero, loader = IntPtr.Zero, file = IntPtr.Zero, builder = IntPtr.Zero, set = IntPtr.Zero;
        try
        {
            // IDWriteFactory5: the in-memory font files and the font set builder.
            if (Marshal.QueryInterface(write, in WriteFactory5Id, out factory5) < 0)
            {
                return IntPtr.Zero;
            }

            // IDWriteFactory5.CreateInMemoryFontFileLoader, registered with the factory (RegisterFontFileLoader) before it
            // makes a file; IDWriteInMemoryFontFileLoader.CreateInMemoryFontFileReference, without an owner: DirectWrite
            // keeps its own copy of the font.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(factory5, 44))(factory5, &loader));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)Slot(write, 13))(write, loader));
            fixed (byte* data = font)
            {
                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, byte*, uint, IntPtr, IntPtr*, int>)Slot(loader, 4))(
                    loader, write, data, (uint)font.Length, IntPtr.Zero, &file));
            }

            // IDWriteFactory5.CreateFontSetBuilder, IDWriteFontSetBuilder1.AddFontFile and CreateFontSet, then
            // IDWriteFactory3.CreateFontCollectionFromFontSet.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(factory5, 43))(factory5, &builder));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)Slot(builder, 7))(builder, file));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(builder, 6))(builder, &set));
            IntPtr collection;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)Slot(factory5, 37))(factory5, set, &collection));
            return collection;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return IntPtr.Zero;
        }
        finally
        {
            foreach (var com in new[] { set, builder, file, loader, factory5 })
            {
                if (com != IntPtr.Zero)
                {
                    Marshal.Release(com);
                }
            }
        }
    }

    /// <summary>
    /// The flag font on every flag sequence of the layout (<see cref="Emojis.Flags"/>), whatever the text's font: set on
    /// the whole sequence, its invisible characters — tags, joiner — included, so it is shaped into one flag. A font
    /// fallback cannot do it: it leaves those characters to the text's font, and the flag falls apart. With
    /// <paramref name="joinedOnly"/> — an emoji font drawing the flags itself — on England, Scotland, Wales and the pirate
    /// flag only, which DirectWrite joins in no other font (workfiles/20261102-emoji-fonts.md § Rendering).
    /// </summary>
    private static void SetFlagFont(IntPtr layout, IntPtr flags, string text, bool joinedOnly)
    {
        if (flags == IntPtr.Zero)
        {
            return;
        }

        fixed (char* family = FlagFamily)
        {
            // Emojis.Flags gives the joined flags alone as one character.
            foreach (var (start, length) in Emojis.Flags(text).Where(f => !joinedOnly || f.Length == 1))
            {
                // IDWriteTextLayout.SetFontCollection and SetFontFamilyName on the sequence's range.
                var range = new TextRange { StartPosition = (uint)start, Length = (uint)length };
                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, TextRange, int>)Slot(layout, 30))(layout, flags, range));
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, TextRange, int>)Slot(layout, 31))(layout, family, range));
            }
        }
    }

    /// <summary>
    /// The emoji font on every emoji of the layout (<see cref="Emojis.IsEmoji"/>), whatever the text's font — as the flag
    /// font is set on the flags, and for the same reason: a fallback would leave a sequence's joiner, VS16 or skin tone to
    /// the text's font (workfiles/20261102-emoji-fonts.md § Rendering).
    /// </summary>
    private static void SetEmojiFont(IntPtr layout, IntPtr collection, string emojiFamily, string text)
    {
        fixed (char* family = emojiFamily)
        {
            foreach (var (start, length) in Emojis.Elements(text).Where(e => Emojis.IsEmoji(text, e.Start, e.Length)))
            {
                // IDWriteTextLayout.SetFontCollection and SetFontFamilyName on the emoji's range.
                var range = new TextRange { StartPosition = (uint)start, Length = (uint)length };
                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, TextRange, int>)Slot(layout, 30))(layout, collection, range));
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, TextRange, int>)Slot(layout, 31))(layout, family, range));
            }
        }
    }

    /// <summary>
    /// The emoji font named <paramref name="family"/>: a shipped one, loaded from <see cref="EmojiFonts.Folder"/>, else an
    /// installed one; <c>null</c> for <see cref="EmojiFonts.Default"/> — the system's fallback draws it — and for a font
    /// found nowhere, drawn as the default. Kept per thread until the shipped fonts change.
    /// </summary>
    private static EmojiFont? EmojiFontOf(Factories factories, string family)
    {
        if (string.Equals(family, EmojiFonts.Default, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        factories.Refresh();
        if (factories.EmojiFonts.TryGetValue(family, out var known))
        {
            return known;
        }

        EmojiFont? found = null;
        var (_, path) = EmojiFonts.Present.FirstOrDefault(f => string.Equals(f.Family, family, StringComparison.OrdinalIgnoreCase));
        if (path is not null)
        {
            try
            {
                var collection = CreateMemoryCollection(factories.Write, File.ReadAllBytes(path));
                if (collection != IntPtr.Zero)
                {
                    // Its one family, by the name DirectWrite gives it: the WWS family — OpenMoji's is "OpenMoji Color".
                    factories.Own(collection);
                    found = FamilyName(collection, 0) is { } name ? Find(collection, name) : null;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unreadable: looked for among the installed fonts, else drawn as the default.
            }
        }

        found ??= Find(factories.System, family);
        factories.EmojiFonts[family] = found;
        return found;
    }

    /// <summary><paramref name="family"/> in <paramref name="collection"/>, and whether it holds the flags; <c>null</c> when it is not there.</summary>
    private static EmojiFont? Find(IntPtr collection, string family)
    {
        uint index;
        int exists;
        fixed (char* name = family)
        {
            // IDWriteFontCollection.FindFamilyName.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, uint*, int*, int>)Slot(collection, 5))(collection, name, &index, &exists));
        }

        if (exists == 0)
        {
            return null;
        }

        IntPtr font = FirstFont(collection, index);
        try
        {
            return new EmojiFont(collection, family, font != IntPtr.Zero && HasCharacter(font, 0x1F1E6));
        }
        finally
        {
            if (font != IntPtr.Zero)
            {
                Marshal.Release(font);
            }
        }
    }

    /// <summary>The installed families that are color fonts holding 😀: <see cref="InstalledEmojiFamilies"/>.</summary>
    private static IReadOnlyList<string> ReadInstalledEmojiFamilies(Factories factories)
    {
        var families = new List<string>();
        IntPtr collection = factories.System;

        // IDWriteFontCollection.GetFontFamilyCount.
        uint count = ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(collection, 3))(collection);
        for (uint i = 0; i < count; i++)
        {
            IntPtr font = FirstFont(collection, i), font2 = IntPtr.Zero;
            try
            {
                // IDWriteFont2.IsColorFont, then IDWriteFont.HasCharacter.
                if (font == IntPtr.Zero || Marshal.QueryInterface(font, in Font2Id, out font2) < 0
                    || ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(font2, 18))(font2) == 0 || !HasCharacter(font, 0x1F600))
                {
                    continue;
                }

                if (FamilyName(collection, i) is { } name)
                {
                    families.Add(name);
                }
            }
            finally
            {
                if (font2 != IntPtr.Zero)
                {
                    Marshal.Release(font2);
                }

                if (font != IntPtr.Zero)
                {
                    Marshal.Release(font);
                }
            }
        }

        return families;
    }

    /// <summary>The regular font of the collection's family at <paramref name="index"/> — the caller's to release —, zero when it cannot be read.</summary>
    private static IntPtr FirstFont(IntPtr collection, uint index)
    {
        IntPtr family = IntPtr.Zero;
        try
        {
            // IDWriteFontCollection.GetFontFamily, then IDWriteFontFamily.GetFirstMatchingFont: normal weight, stretch and style.
            if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Slot(collection, 4))(collection, index, &family) < 0)
            {
                return IntPtr.Zero;
            }

            IntPtr font;
            return ((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, IntPtr*, int>)Slot(family, 7))(family, 400, 5, 0, &font) < 0 ? IntPtr.Zero : font;
        }
        finally
        {
            if (family != IntPtr.Zero)
            {
                Marshal.Release(family);
            }
        }
    }

    /// <summary>IDWriteFont.HasCharacter.</summary>
    private static bool HasCharacter(IntPtr font, int code)
    {
        int exists;
        return ((delegate* unmanaged[Stdcall]<IntPtr, uint, int*, int>)Slot(font, 12))(font, (uint)code, &exists) >= 0 && exists != 0;
    }

    /// <summary>The name of the collection's family at <paramref name="index"/>, in English when it has one, else its first; <c>null</c> when it cannot be read.</summary>
    private static string? FamilyName(IntPtr collection, uint index)
    {
        IntPtr family = IntPtr.Zero, names = IntPtr.Zero;
        try
        {
            // IDWriteFontCollection.GetFontFamily, IDWriteFontFamily.GetFamilyNames.
            if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Slot(collection, 4))(collection, index, &family) < 0
                || ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(family, 6))(family, &names) < 0)
            {
                return null;
            }

            // IDWriteLocalizedStrings.FindLocaleName, GetStringLength, GetString.
            uint at;
            int exists;
            fixed (char* locale = "en-us")
            {
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, uint*, int*, int>)Slot(names, 4))(names, locale, &at, &exists));
            }

            if (exists == 0)
            {
                at = 0;
            }

            uint length;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint*, int>)Slot(names, 7))(names, at, &length));
            var buffer = new char[length + 1];
            fixed (char* chars = buffer)
            {
                Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, char*, uint, int>)Slot(names, 8))(names, at, chars, length + 1));
            }

            return new string(buffer, 0, (int)length);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (names != IntPtr.Zero)
            {
                Marshal.Release(names);
            }

            if (family != IntPtr.Zero)
            {
                Marshal.Release(family);
            }
        }
    }

    /// <summary>
    /// The three factories, the flag font and the emoji fonts in use, made once per thread: the Direct2D factory is
    /// single-threaded.
    /// </summary>
    private sealed class Factories
    {
        [ThreadStatic]
        private static Factories? _current;

        // The shipped emoji fonts' collections this thread loaded, released when the fonts change.
        private readonly List<IntPtr> _owned = [];
        private IntPtr _system;
        private int _generation;

        private Factories()
        {
            Check(D2D1CreateFactory(0, FactoryId, IntPtr.Zero, out var direct2D));
            Check(DWriteCreateFactory(0, WriteFactoryId, out var write));
            Check(CoCreateInstance(ImagingFactoryClass, IntPtr.Zero, 1, ImagingFactoryId, out var imaging));
            this.Direct2D = direct2D;
            this.Write = write;
            this.Imaging = imaging;
            this.Flags = CreateFlagCollection(write);
            this._generation = global::ImageGridFusion.Imaging.EmojiFonts.Generation;
        }

        public IntPtr Direct2D { get; }

        public IntPtr Write { get; }

        public IntPtr Imaging { get; }

        /// <summary>The flag font's collection, zero without one (<see cref="CreateFlagCollection"/>).</summary>
        public IntPtr Flags { get; }

        /// <summary>The system's font collection, read once (IDWriteFactory.GetSystemFontCollection).</summary>
        public IntPtr System
        {
            get
            {
                if (this._system == IntPtr.Zero)
                {
                    IntPtr collection;
                    Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int, int>)Slot(this.Write, 3))(this.Write, &collection, 0));
                    this._system = collection;
                }

                return this._system;
            }
        }

        /// <summary>The emoji fonts looked for, by family: <see cref="EmojiFontOf"/>'s.</summary>
        public Dictionary<string, EmojiFont?> EmojiFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static Factories Get() => _current ??= new Factories();

        /// <summary>Keeps <paramref name="collection"/> until the shipped fonts change.</summary>
        public void Own(IntPtr collection) => this._owned.Add(collection);

        /// <summary>Forgets the emoji fonts looked for once the shipped ones changed, their collections released.</summary>
        public void Refresh()
        {
            int generation = global::ImageGridFusion.Imaging.EmojiFonts.Generation;
            if (generation == this._generation)
            {
                return;
            }

            this._generation = generation;
            this.EmojiFonts.Clear();
            foreach (var collection in this._owned)
            {
                Marshal.Release(collection);
            }

            this._owned.Clear();
        }
    }

    /// <summary>An emoji font found: the collection holding it, its family there, and whether it draws the flags.</summary>
    private readonly record struct EmojiFont(IntPtr Collection, string Family, bool HasFlags);

    /// <summary>
    /// What the outline's rings and the shadow's mask are made of: the layout's plain shapes, or — <see cref="Alpha"/> set —
    /// the text drawn in color at the pad's offset, for an emoji font whose emojis have no plain shape
    /// (workfiles/20261102-emoji-fonts.md § Rendering).
    /// </summary>
    private sealed record Shape(IntPtr Layout, byte[]? Alpha, Size Size, float Pad);

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeU
    {
        public uint Width, Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapProperties
    {
        public int Format, AlphaMode;
        public float DpiX, DpiY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DColor
    {
        public float R, G, B, A;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DPoint
    {
        public float X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DRect
    {
        public float Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetProperties
    {
        public int Type, Format, AlphaMode;
        public float DpiX, DpiY;
        public int Usage, MinLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClusterMetrics
    {
        public float Width;
        public ushort Length;
        public ushort Flags;

        public readonly bool IsWhitespace => (this.Flags & 2) != 0;

        public readonly bool IsNewline => (this.Flags & 4) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextRange
    {
        public uint StartPosition, Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextMetrics
    {
        public float Left, Top, Width, WidthIncludingTrailingWhitespace, Height, LayoutWidth, LayoutHeight;
        public uint MaxBidiReorderingDepth, LineCount;
    }
}
