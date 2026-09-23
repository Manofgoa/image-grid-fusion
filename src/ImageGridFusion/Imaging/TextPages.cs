using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// A text file rendered by the app, in a monospace font, on pages shaped like the cell they fill.
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

    private static readonly Color Paper = Color.White;
    private static readonly Color Ink = Color.FromArgb(34, 34, 34);

    /// <summary>Advance of one character and line spacing, per pixel of font height.</summary>
    private static readonly Lazy<(float Advance, float LineSpacing)> Metrics = new(Measure);

    /// <summary>Logical lines, tabs expanded, and the offset of each in the whole text.</summary>
    private readonly string[] _lines;
    private readonly int[] _lineOffsets;
    private Layout _layout;

    private TextPages(string[] lines, Size pageSize)
    {
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
    /// Returns null unless the file is text: at most 1 MB, not empty, UTF-16 with a byte order mark,
    /// or valid UTF-8 with no NUL byte in its first 8 KB. The extension plays no part.
    /// </summary>
    public static TextPages? TryOpen(string path, Size pageSize)
    {
        string? text = TryReadText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] lines = text.ReplaceLineEndings("\n").TrimEnd().Split('\n').Select(ExpandTabs).ToArray();
        return new TextPages(lines, pageSize);
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
        var (_, lineSpacing) = Metrics.Value;
        var bitmap = new Bitmap(layout.PageSize.Width, layout.PageSize.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Paper);

        // No grid fitting: glyph advances then scale linearly with the size, as measured once.
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        using var font = new Font(FontFamily, layout.FontSize, GraphicsUnit.Pixel);
        using var ink = new SolidBrush(Ink);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

        int first = Math.Clamp(page, 0, layout.PageCount - 1) * layout.LinesPerPage;
        int last = Math.Min(layout.Lines.Length, first + layout.LinesPerPage);
        float height = layout.FontSize * lineSpacing;
        for (int i = first; i < last; i++)
        {
            g.DrawString(layout.Lines[i], font, ink, layout.Margin, layout.Margin + (i - first) * height, format);
        }

        return bitmap;
    }

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

    private static string ExpandTabs(string line)
    {
        if (!line.Contains('\t'))
        {
            return line;
        }

        var builder = new StringBuilder(line.Length + 16);
        foreach (char c in line)
        {
            if (c == '\t')
            {
                builder.Append(' ', TabWidth - builder.Length % TabWidth);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
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
    }
}
