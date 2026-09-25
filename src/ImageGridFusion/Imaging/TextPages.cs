using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// A text rendered by the app — a text file, or a text pasted or dropped — in a monospace font, on
/// pages shaped like the cell they fill, each character in its own style (bold, colors…).
/// The font is the largest size in [<see cref="MinFontSize"/>, <see cref="MaxFontSize"/>] at which
/// the whole text fits one page; when even the smallest does not fit, the text is paginated at it.
/// </summary>
/// <remarks>
/// Readability holds in the export because the canvas is never narrower than the width at which no
/// image is downscaled: a page drawn at its cell size on a 1200 px canvas keeps its font height in
/// pixels, or grows.
/// </remarks>
public sealed class TextPages : PageSource
{
    /// <summary>Font height in pixels of the page, hence at least in the export.</summary>
    public const int MinFontSize = 24;
    public const int MaxFontSize = 96;

    private const long MaxFileSize = 1024 * 1024;
    private const int SniffedBytes = 8 * 1024;
    private const int TabWidth = 4;
    private const string FontFamily = "Consolas";

    private static readonly Color WhitePaper = Color.White;
    private static readonly Color DarkInk = Color.FromArgb(34, 34, 34);
    private static readonly Color LightInk = Color.FromArgb(221, 221, 221);

    /// <summary>Advance of one character and line spacing, per pixel of font height.</summary>
    private static readonly Lazy<(float Advance, float LineSpacing)> Metrics = new(Measure);

    /// <summary>The whole text, normalized: the styles of the lines, by offset.</summary>
    private readonly StyledText _text;

    /// <summary>Logical lines, tabs expanded, and the offset of each in the whole text.</summary>
    private readonly string[] _lines;
    private readonly int[] _lineOffsets;
    private readonly Color _paper;

    /// <summary>Color of the text that has none of its own: dark on a light paper, light on a dark one.</summary>
    private readonly Color _ink;
    private Layout _layout;

    private TextPages(StyledText text, Size pageSize)
    {
        _text = text;
        _paper = text.Paper ?? WhitePaper;
        _ink = IsDark(_paper) ? LightInk : DarkInk;
        string[] lines = text.Text.Split('\n');
        _lines = lines;
        _lineOffsets = new int[lines.Length];
        for (int i = 1; i < lines.Length; i++)
        {
            _lineOffsets[i] = _lineOffsets[i - 1] + lines[i - 1].Length + 1;
        }

        _layout = LayOut(pageSize);
    }

    public override int Count => _layout.PageCount;

    public override Size? PageSize => _layout.PageSize;

    public override string Label(int page) => $"{page + 1} / {Count}";

    /// <summary>
    /// A text longer than its page scrolls: every <see cref="Animation.StepDuration"/>, the view moves
    /// down half a page, keeping the lower half of the previous view on top, until the end shows.
    /// </summary>
    public override TimeSpan LoopDuration
    {
        get
        {
            var layout = Volatile.Read(ref _layout);
            return layout.PageCount > 1 ? Animation.StepDuration * layout.ViewCount : TimeSpan.Zero;
        }
    }

    public override AnimationReader OpenAnimation() => new StepReader(
        () => Views(Volatile.Read(ref _layout)),
        view =>
        {
            var layout = Volatile.Read(ref _layout);
            return RenderFrom(layout, layout.ViewStart(view));
        });

    public override int PageAt(TimeSpan time)
    {
        var layout = Volatile.Read(ref _layout);
        return layout.ViewStart(StepReader.StepAt(Views(layout), time)) / layout.LinesPerPage;
    }

    public override TimeSpan TimeOf(int page)
    {
        var layout = Volatile.Read(ref _layout);
        int line = Math.Clamp(page, 0, layout.PageCount - 1) * layout.LinesPerPage;
        return StepReader.StartOf(Views(layout), (line + layout.HalfPage - 1) / layout.HalfPage);
    }

    /// <summary>
    /// Returns null unless the file is text: at most 1 MB, not empty, UTF-16 with a byte order mark,
    /// or valid UTF-8 with no NUL byte in its first 8 KB. The extension plays no part.
    /// </summary>
    public static TextPages? TryOpen(string path, Size pageSize) =>
        TryReadText(path) is { } text ? TryCreate(StyledText.Plain(text), pageSize) : null;

    /// <summary>Returns null when the text is empty or white space only.</summary>
    public static TextPages? TryCreate(StyledText text, Size pageSize)
    {
        var normalized = text.Normalize(TabWidth);
        return normalized.IsBlank ? null : new TextPages(normalized, pageSize);
    }

    public override int Resize(Size pageSize, int page)
    {
        var previous = _layout;
        int offset = previous.LineStarts[Math.Clamp(page, 0, previous.PageCount - 1) * previous.LinesPerPage];
        var layout = LayOut(pageSize);
        Volatile.Write(ref _layout, layout);

        int line = Array.FindLastIndex(layout.LineStarts, start => start <= offset);
        return Math.Max(0, line) / layout.LinesPerPage;
    }

    public override Bitmap Render(int page)
    {
        var layout = Volatile.Read(ref _layout);
        return RenderFrom(layout, Math.Clamp(page, 0, layout.PageCount - 1) * layout.LinesPerPage);
    }

    private static TimeSpan[] Views(Layout layout) => Enumerable.Repeat(Animation.StepDuration, layout.ViewCount).ToArray();

    /// <summary>Renders a page's worth of lines, from line <paramref name="first"/>.</summary>
    private Bitmap RenderFrom(Layout layout, int first)
    {
        var (advance, lineSpacing) = Metrics.Value;
        var bitmap = new Bitmap(layout.PageSize.Width, layout.PageSize.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(_paper);

        // No grid fitting: glyph advances then scale linearly with the size, as measured once.
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        var fonts = new Dictionary<FontStyle, Font>();
        using var ink = new SolidBrush(_ink);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

        try
        {
            int last = Math.Min(layout.Lines.Length, first + layout.LinesPerPage);
            float height = layout.FontSize * lineSpacing;
            float width = layout.FontSize * advance;
            for (int i = first; i < last; i++)
            {
                float y = layout.Margin + (i - first) * height;
                string line = layout.Lines[i];
                if (!_text.HasStyles)
                {
                    g.DrawString(line, FontOf(FontStyle.Regular), ink, layout.Margin, y, format);
                    continue;
                }

                // Runs of one style, each at its column: the font is monospace, bold and italic too.
                int start = layout.LineStarts[i];
                for (int run = 0; run < line.Length;)
                {
                    ushort index = _text.StyleIndexAt(start + run);
                    int end = run + 1;
                    while (end < line.Length && _text.StyleIndexAt(start + end) == index)
                    {
                        end++;
                    }

                    var style = _text.StyleOf(index);
                    float x = layout.Margin + run * width;
                    if (style.Highlight is { } highlight)
                    {
                        using var fill = new SolidBrush(highlight);
                        g.FillRectangle(fill, x, y, (end - run) * width, height);
                    }

                    if (style.Ink is { } color)
                    {
                        using var own = new SolidBrush(color);
                        g.DrawString(line[run..end], FontOf(style.Font), own, x, y, format);
                    }
                    else
                    {
                        g.DrawString(line[run..end], FontOf(style.Font), ink, x, y, format);
                    }

                    run = end;
                }
            }
        }
        finally
        {
            foreach (var font in fonts.Values)
            {
                font.Dispose();
            }
        }

        return bitmap;

        Font FontOf(FontStyle style)
        {
            if (!fonts.TryGetValue(style, out var font))
            {
                font = new Font(FontFamily, layout.FontSize, style, GraphicsUnit.Pixel);
                fonts[style] = font;
            }

            return font;
        }
    }

    /// <summary>Perceived lightness below the middle: a dark theme's background.</summary>
    private static bool IsDark(Color color) => 0.299 * color.R + 0.587 * color.G + 0.114 * color.B < 128;

    /// <summary>Largest font at which the text fits one page, else pages at the smallest font.</summary>
    private Layout LayOut(Size pageSize)
    {
        int margin = Math.Max(8, (int)(Math.Min(pageSize.Width, pageSize.Height) * 0.04));
        var content = new Size(Math.Max(1, pageSize.Width - 2 * margin), Math.Max(1, pageSize.Height - 2 * margin));

        int low = MinFontSize, high = MaxFontSize, best = -1;
        while (low <= high)
        {
            int size = (low + high) / 2;
            var (columns, rows) = Grid(content, size);
            if (Wrap(columns, rows).Count <= rows)
            {
                best = size;
                low = size + 1;
            }
            else
            {
                high = size - 1;
            }
        }

        int fontSize = best > 0 ? best : MinFontSize;
        var (cols, linesPerPage) = Grid(content, fontSize);
        var wrapped = Wrap(cols, int.MaxValue);
        return new Layout(pageSize, fontSize, margin, [.. wrapped.Select(l => l.Text)], [.. wrapped.Select(l => l.Start)], linesPerPage);
    }

    private static (int Columns, int Rows) Grid(Size content, int fontSize)
    {
        var (advance, lineSpacing) = Metrics.Value;
        return (
            Math.Max(1, (int)(content.Width / (fontSize * advance))),
            Math.Max(1, (int)(content.Height / (fontSize * lineSpacing))));
    }

    /// <summary>
    /// Word-wraps the lines to <paramref name="columns"/>, breaking inside a word only when it is
    /// wider than a line. Stops once past <paramref name="maxLines"/>: enough to know it overflows.
    /// </summary>
    private List<(string Text, int Start)> Wrap(int columns, int maxLines)
    {
        var result = new List<(string, int)>();
        for (int i = 0; i < _lines.Length && result.Count <= maxLines; i++)
        {
            string line = _lines[i];
            int start = 0;
            while (line.Length - start > columns && result.Count <= maxLines)
            {
                int space = line.LastIndexOf(' ', start + columns, columns);
                int end = space > start ? space : start + columns;
                result.Add((line[start..end], _lineOffsets[i] + start));
                start = space > start ? end + 1 : end;
            }

            result.Add((line[start..], _lineOffsets[i] + start));
        }

        return result;
    }

    private static string? TryReadText(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length is 0 or > MaxFileSize)
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes is [0xFF, 0xFE, ..])
            {
                return Strict(new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true), bytes, 2);
            }

            if (bytes is [0xFE, 0xFF, ..])
            {
                return Strict(new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true), bytes, 2);
            }

            if (bytes.AsSpan(0, Math.Min(bytes.Length, SniffedBytes)).Contains((byte)0))
            {
                return null;
            }

            int bom = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
            return Strict(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), bytes, bom);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Strict(Encoding encoding, byte[] bytes, int skip)
    {
        try
        {
            return encoding.GetString(bytes, skip, bytes.Length - skip);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static (float Advance, float LineSpacing) Measure()
    {
        const int Size = 100, Sample = 100;
        using var font = new Font(FontFamily, Size, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        float width = g.MeasureString(new string('M', Sample), font, PointF.Empty, StringFormat.GenericTypographic).Width;
        return (width / Sample / Size, font.GetHeight(g) / Size);
    }

    /// <summary>Wrapped lines, the offset each starts at in the whole text, and how many a page holds.</summary>
    private sealed record Layout(Size PageSize, int FontSize, int Margin, string[] Lines, int[] LineStarts, int LinesPerPage)
    {
        public int PageCount => Math.Max(1, (Lines.Length + LinesPerPage - 1) / LinesPerPage);

        /// <summary>How far the animation moves down at each step.</summary>
        public int HalfPage => Math.Max(1, LinesPerPage / 2);

        /// <summary>Views of the animation: half a page apart, the last one ending on the last line.</summary>
        public int ViewCount => LastStart == 0 ? 1 : (LastStart + HalfPage - 1) / HalfPage + 1;

        public int ViewStart(int view) => Math.Min(Math.Clamp(view, 0, ViewCount - 1) * HalfPage, LastStart);

        /// <summary>First line of the view that ends on the last line.</summary>
        private int LastStart => Math.Max(0, Lines.Length - LinesPerPage);
    }
}
