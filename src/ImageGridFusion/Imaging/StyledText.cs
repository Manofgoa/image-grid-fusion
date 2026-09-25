using System.Text;

namespace ImageGridFusion.Imaging;

/// <summary>How a run of text is drawn: the font's style, and colors that default to the page's.</summary>
/// <param name="Ink">Text color; <c>null</c> for the page's default ink.</param>
/// <param name="Highlight">Background behind the text; <c>null</c> for none.</param>
public readonly record struct TextStyle(FontStyle Font, Color? Ink, Color? Highlight)
{
    public static readonly TextStyle Default = new(FontStyle.Regular, null, null);
}

/// <summary>
/// A text with a style per character, and the paper it sits on: plain text read from a file, or
/// rich text (RTF, HTML) pasted or dropped. Font families and sizes are not kept: the text is
/// drawn in the monospace font of <see cref="TextPages"/>.
/// </summary>
public sealed class StyledText
{
    /// <summary>Longest text accepted, in characters — the size limit of a text file.</summary>
    public const int MaxLength = 1024 * 1024;

    private readonly ushort[]? _styleAt;
    private readonly TextStyle[] _styles;

    private StyledText(string text, ushort[]? styleAt, TextStyle[] styles, Color? paper)
    {
        Text = text;
        _styleAt = styleAt;
        _styles = styles;
        Paper = paper;
    }

    public string Text { get; }

    /// <summary>Background of the whole page; <c>null</c> for white paper.</summary>
    public Color? Paper { get; }

    public bool IsBlank => string.IsNullOrWhiteSpace(Text);

    /// <summary>False when every character has the default style: the text draws line by line.</summary>
    public bool HasStyles => _styleAt is not null;

    public static StyledText Plain(string text) => new(text, null, [TextStyle.Default], null);

    /// <summary>Index of the style of character <paramref name="index"/>, in <see cref="StyleOf"/>.</summary>
    public ushort StyleIndexAt(int index) => _styleAt?[index] ?? 0;

    public TextStyle StyleOf(ushort styleIndex) => _styles[styleIndex];

    /// <summary>
    /// The same text with line endings made <c>\n</c>, tabs expanded to <paramref name="tabWidth"/>
    /// columns and trailing white space removed, each character keeping its style.
    /// </summary>
    public StyledText Normalize(int tabWidth)
    {
        string source = Text;
        var text = new StringBuilder(source.Length);
        var styleAt = _styleAt is null ? null : new List<ushort>(source.Length);
        int column = 0;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            ushort style = StyleIndexAt(i);

            // The line endings string.ReplaceLineEndings recognizes.
            if (c is '\r' or '\n' or '\f' or '\u0085' or '\u2028' or '\u2029')
            {
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                {
                    i++;
                }

                Add('\n', 1);
                column = 0;
            }
            else if (c == '\t')
            {
                int spaces = tabWidth - column % tabWidth;
                Add(' ', spaces);
                column += spaces;
            }
            else
            {
                Add(c, 1);
                column++;
            }

            void Add(char added, int count)
            {
                text.Append(added, count);
                for (int k = 0; k < count; k++)
                {
                    styleAt?.Add(style);
                }
            }
        }

        int end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        text.Length = end;
        styleAt?.RemoveRange(end, styleAt.Count - end);
        return new StyledText(text.ToString(), styleAt?.ToArray(), _styles, Paper);
    }

    /// <summary>
    /// Builds a styled text run by run. When every visible character sits on a background, the text
    /// was on a colored surface (a dark theme, a colored page): the most frequent background becomes
    /// the paper, and stops being a highlight.
    /// </summary>
    public sealed class Builder
    {
        private readonly StringBuilder _text = new();
        private readonly List<ushort> _styleAt = [];
        private readonly List<TextStyle> _styles = [TextStyle.Default];
        private readonly Dictionary<TextStyle, ushort> _indexes = new() { [TextStyle.Default] = 0 };

        public int Length => _text.Length;

        /// <summary>Last character appended, or <c>'\0'</c> when empty.</summary>
        public char Last => _text.Length > 0 ? _text[^1] : '\0';

        public void Append(string text, TextStyle style)
        {
            ushort index = IndexOf(style);
            _text.Append(text);
            for (int i = 0; i < text.Length; i++)
            {
                _styleAt.Add(index);
            }
        }

        public void Append(char c, TextStyle style) => Append(c.ToString(), style);

        public StyledText Build()
        {
            string text = _text.ToString();
            var styleAt = _styleAt.ToArray();
            var styles = _styles.ToArray();
            Color? paper = CommonBackground(text, styleAt, styles);
            if (paper is { } color)
            {
                // The paper shows through: a highlight of its color draws nothing more.
                for (int i = 0; i < styles.Length; i++)
                {
                    if (styles[i].Highlight is { } highlight && highlight.ToArgb() == color.ToArgb())
                    {
                        styles[i] = styles[i] with { Highlight = null };
                    }
                }
            }

            bool styled = styleAt.Any(s => styles[s] != TextStyle.Default);
            return new StyledText(text, styled ? styleAt : null, styled ? styles : [TextStyle.Default], paper);
        }

        private ushort IndexOf(TextStyle style)
        {
            if (_indexes.TryGetValue(style, out ushort index))
            {
                return index;
            }

            if (_styles.Count > ushort.MaxValue)
            {
                return 0;
            }

            index = (ushort)_styles.Count;
            _styles.Add(style);
            _indexes[style] = index;
            return index;
        }

        private static Color? CommonBackground(string text, ushort[] styleAt, TextStyle[] styles)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    continue;
                }

                if (styles[styleAt[i]].Highlight is not { } background)
                {
                    return null;
                }

                counts[background.ToArgb()] = counts.GetValueOrDefault(background.ToArgb()) + 1;
            }

            return counts.Count == 0 ? null : Color.FromArgb(counts.MaxBy(c => c.Value).Key);
        }
    }
}
