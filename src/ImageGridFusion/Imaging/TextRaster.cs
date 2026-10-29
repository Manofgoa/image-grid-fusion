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

    /// <summary>The text as drawn: in capitals with the current culture under <see cref="Caps"/>, the emojis untouched.</summary>
    public string Drawn(string text) => this.Caps ? text.ToUpper(System.Globalization.CultureInfo.CurrentCulture) : text;

    /// <summary>The style under named keys, each setting by hand (RULES.md § Effect Presets): an older file reads a later one.</summary>
    public JsonObject ToJson() => new()
    {
        ["font"] = this.Font,
        ["bold"] = this.Bold,
        ["italic"] = this.Italic,
        ["caps"] = this.Caps,
        ["alignment"] = this.Alignment.ToString(),
        ["color"] = Hex(this.Color),
        ["outline"] = this.Outline,
        ["outlineColor"] = Hex(this.OutlineColor),
        ["outlineWidth"] = this.OutlineWidth,
        ["shadow"] = this.Shadow,
        ["shadowKind"] = this.ShadowKind.ToString(),
        ["shadowColor"] = Hex(this.ShadowColor),
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
            Italic = PresetJson.Bool(json, "italic", Default.Italic),
            Caps = PresetJson.Bool(json, "caps", Default.Caps),
            Alignment = PresetJson.Enum(json, "alignment", Default.Alignment),
            Shadow = PresetJson.Bool(json, "shadow", Default.Shadow),
            ShadowKind = PresetJson.Enum(json, "shadowKind", Default.ShadowKind),
            ShadowColor = HexColor(json, "shadowColor", Default.ShadowColor),
            ShadowSize = Math.Clamp(PresetJson.Double(json, "shadowSize", Default.ShadowSize), MinShadowSize, MaxShadowSize),
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
    private static readonly Guid PremultipliedBgra = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    /// <summary>
    /// The text drawn in <paramref name="style"/>, on a transparent background, and the height of one of its
    /// lines in the bitmap's pixels — what a text layer's size measures. An empty text gives an empty box
    /// half a line wide.
    /// </summary>
    public static (Bitmap Bitmap, double LinePixels) Render(string text, LayerTextStyle style)
    {
        var factories = Factories.Get();
        text = style.Drawn(text);
        float em = EmSize;
        for (int attempt = 0; ; attempt++)
        {
            var layout = CreateLayout(factories.Write, text.Length == 0 ? " " : text, style, em);
            try
            {
                var metrics = Metrics(layout);
                double line = metrics.Height / Math.Max(1, metrics.LineCount);
                float width = text.Length == 0 ? (float)line / 2 : metrics.WidthIncludingTrailingWhitespace;
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

                var bitmap = Draw(factories, layout, text.Length == 0, style, new Size(Math.Max(1, w), Math.Max(1, h)), (float)pad, width, metrics.Height, line);
                return (bitmap, line);
            }
            finally
            {
                Marshal.Release(layout);
            }
        }
    }

    /// <summary>The families of the fonts installed, A→Z, for the font drop-down.</summary>
    public static IReadOnlyList<string> FontFamilies()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        return [.. fonts.Families.Select(f => f.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static IntPtr CreateLayout(IntPtr write, string text, LayerTextStyle style, float em)
    {
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

            // SetWordWrapping(NO_WRAP), then the lines aligned within the widest once it is known — SetTextAlignment's
            // LEADING, TRAILING or CENTER; justified, LEADING with the lines stretched by the app.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 5))(layout, 1));
            var metrics = Metrics(layout);
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
    /// Stretches the lines of a paragraph to its widest by widening their spaces between words, its last line
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
    /// The text drawn from the bottom up: the box, the shadow, the outline, the letters. Without a shadow, in
    /// one pass; with one, its mask is drawn and blurred apart, then the three put together.
    /// </summary>
    private static Bitmap Draw(Factories factories, IntPtr layout, bool empty, LayerTextStyle style, Size size, float pad, float width, float height, double line)
    {
        byte[] pixels;
        if (!style.Shadow || empty)
        {
            pixels = Paint(factories, size, target =>
            {
                PaintBox(target, style, pad, width, height, line);
                if (!empty)
                {
                    PaintText(target, layout, style, pad, line);
                }
            });
        }
        else
        {
            double reach = style.ShadowSize * line;
            bool halo = style.ShadowKind == ShadowKind.Halo;
            float offset = halo ? 0 : (float)(reach / Math.Sqrt(2));
            double spread = (style.Outline ? style.OutlineWidth * line : 0) + (halo ? reach / 2 : 0);
            pixels = style.Background ? Paint(factories, size, target => PaintBox(target, style, pad, width, height, line)) : new byte[size.Width * size.Height * 4];
            var mask = Paint(factories, size, target => PaintMask(target, layout, pad + offset, spread));
            Over(pixels, Shadow(mask, size, reach / 2, style.ShadowColor));
            Over(pixels, Paint(factories, size, target => PaintText(target, layout, style, pad, line)));
        }

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

    /// <summary>The outline — copies around the text in its color, its emojis as plain shapes of that color — then the letters.</summary>
    private static void PaintText(IntPtr target, IntPtr layout, LayerTextStyle style, float pad, double line)
    {
        if (style.Outline)
        {
            IntPtr outline = Brush(target, style.OutlineColor);
            try
            {
                PaintRings(target, layout, outline, pad, style.OutlineWidth * line);
            }
            finally
            {
                Marshal.Release(outline);
            }
        }

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
    private static void PaintMask(IntPtr target, IntPtr layout, float at, double spread)
    {
        IntPtr brush = Brush(target, Color.Black);
        try
        {
            PaintRings(target, layout, brush, at, spread);
            DrawText(target, layout, brush, at, at, colorFont: false);
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>Copies of the text in rings around <paramref name="at"/>, out to <paramref name="radius"/>: the text grown by it.</summary>
    private static void PaintRings(IntPtr target, IntPtr layout, IntPtr brush, float at, double radius)
    {
        if (radius <= 0)
        {
            return;
        }

        for (int ring = 0; ring < OutlineRings.Length; ring++)
        {
            double r = radius * (OutlineRings.Length - ring) / OutlineRings.Length;
            int copies = OutlineRings[ring];
            for (int k = 0; k < copies; k++)
            {
                double angle = 2 * Math.PI * k / copies;
                DrawText(target, layout, brush, at + (float)(r * Math.Cos(angle)), at + (float)(r * Math.Sin(angle)), colorFont: false);
            }
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

    /// <summary>The three factories, made once per thread: the Direct2D one is single-threaded.</summary>
    private sealed class Factories
    {
        [ThreadStatic]
        private static Factories? _current;

        private Factories()
        {
            Check(D2D1CreateFactory(0, FactoryId, IntPtr.Zero, out var direct2D));
            Check(DWriteCreateFactory(0, WriteFactoryId, out var write));
            Check(CoCreateInstance(ImagingFactoryClass, IntPtr.Zero, 1, ImagingFactoryId, out var imaging));
            this.Direct2D = direct2D;
            this.Write = write;
            this.Imaging = imaging;
        }

        public IntPtr Direct2D { get; }

        public IntPtr Write { get; }

        public IntPtr Imaging { get; }

        public static Factories Get() => _current ??= new Factories();
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
