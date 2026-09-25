using System.Globalization;
using System.Net;
using System.Text;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Reads the text of an HTML fragment — the clipboard's "HTML Format", as browsers and editors
/// copy it — with its character styles: bold, italic, underline, strike, text and background
/// colors, from the tags and their inline <c>style</c>. Blocks break lines, list items get a
/// bullet, table cells are separated by tabs; white space collapses as a browser does, except in
/// <c>&lt;pre&gt;</c> and <c>white-space: pre</c>. Fonts, sizes and images are left out.
/// </summary>
public static class HtmlReader
{
    private static readonly HashSet<string> Blocks =
    [
        "address", "article", "aside", "blockquote", "caption", "center", "dd", "details", "dialog",
        "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3",
        "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section", "summary",
        "table", "tbody", "tfoot", "thead", "tr", "ul",
    ];

    /// <summary>Blocks set apart by a blank line, like the margins a browser gives them.</summary>
    private static readonly HashSet<string> Paragraphs = ["p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "table", "ul", "ol", "dl", "figure"];

    private static readonly HashSet<string> Voids = ["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"];

    /// <summary>Elements whose content is never shown.</summary>
    private static readonly HashSet<string> Hidden = ["head", "script", "style", "template", "title", "noscript", "object", "svg", "math"];

    /// <summary>
    /// Reads the clipboard's "HTML Format": the fragment its header points to, else the markup
    /// between the fragment comments, else the whole string as HTML.
    /// </summary>
    public static StyledText? TryRead(string clipboardHtml) => new Parser(Fragment(clipboardHtml)).Parse();

    private static string Fragment(string html)
    {
        if (html.StartsWith("Version:", StringComparison.Ordinal)
            && Offset(html, "StartFragment:") is { } start && Offset(html, "EndFragment:") is { } end)
        {
            // The offsets count UTF-8 bytes of the whole payload, header included.
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            if (start >= 0 && start <= end && end <= bytes.Length)
            {
                return Encoding.UTF8.GetString(bytes, start, end - start);
            }
        }

        const string Begin = "<!--StartFragment-->", Finish = "<!--EndFragment-->";
        int from = html.IndexOf(Begin, StringComparison.OrdinalIgnoreCase);
        int to = html.IndexOf(Finish, StringComparison.OrdinalIgnoreCase);
        if (from >= 0 && to > from)
        {
            return html[(from + Begin.Length)..to];
        }

        int markup = html.IndexOf('<');
        return markup > 0 && html.StartsWith("Version:", StringComparison.Ordinal) ? html[markup..] : html;
    }

    private static int? Offset(string html, string key)
    {
        int at = html.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        int digits = at + key.Length, stop = digits;
        while (stop < html.Length && char.IsAsciiDigit(html[stop]))
        {
            stop++;
        }

        return int.TryParse(html.AsSpan(digits, stop - digits), out int value) ? value : null;
    }

    /// <summary>What an open element gives its content.</summary>
    private sealed record Element(string Name, TextStyle Style, bool Pre, bool Hide, bool Block, int? ListCounter);

    private sealed class Parser(string html)
    {
        private readonly StyledText.Builder _text = new();
        private readonly List<Element> _open = [];
        private int _pos;
        /// <summary>Style of the white space met since the last word, collapsed into one space; <c>null</c> for none.</summary>
        private TextStyle? _pendingSpace;

        private Element Current => _open.Count > 0 ? _open[^1] : new Element("", TextStyle.Default, false, false, false, null);

        public StyledText Parse()
        {
            while (_pos < html.Length)
            {
                int tag = html.IndexOf('<', _pos);
                if (tag < 0)
                {
                    Text(html[_pos..]);
                    break;
                }

                if (tag > _pos)
                {
                    Text(html[_pos..tag]);
                }

                _pos = tag;
                Markup();
            }

            return _text.Build();
        }

        private void Markup()
        {
            if (Starts("<!--"))
            {
                int end = html.IndexOf("-->", _pos + 4, StringComparison.Ordinal);
                _pos = end < 0 ? html.Length : end + 3;
                return;
            }

            if (Starts("<!") || Starts("<?"))
            {
                int end = html.IndexOf('>', _pos);
                _pos = end < 0 ? html.Length : end + 1;
                return;
            }

            bool closing = _pos + 1 < html.Length && html[_pos + 1] == '/';
            int nameStart = _pos + (closing ? 2 : 1);
            int nameEnd = nameStart;
            while (nameEnd < html.Length && (char.IsAsciiLetterOrDigit(html[nameEnd]) || html[nameEnd] is '-' or ':'))
            {
                nameEnd++;
            }

            if (nameEnd == nameStart)
            {
                // A lone '<': text.
                Text("<");
                _pos++;
                return;
            }

            string name = html[nameStart..nameEnd].ToLowerInvariant();
            var attributes = Attributes(nameEnd, out int after);
            _pos = after;

            if (closing)
            {
                Close(name);
            }
            else
            {
                Open(name, attributes, selfClosing: after >= 2 && html[after - 2] == '/');
            }
        }

        private bool Starts(string text) => string.CompareOrdinal(html, _pos, text, 0, text.Length) == 0;

        private Dictionary<string, string> Attributes(int from, out int after)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = from;
            while (i < html.Length && html[i] != '>')
            {
                if (char.IsWhiteSpace(html[i]) || html[i] == '/')
                {
                    i++;
                    continue;
                }

                int nameStart = i;
                while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] is not ('=' or '>' or '/'))
                {
                    i++;
                }

                string name = html[nameStart..i];
                while (i < html.Length && char.IsWhiteSpace(html[i]))
                {
                    i++;
                }

                string value = "";
                if (i < html.Length && html[i] == '=')
                {
                    i++;
                    while (i < html.Length && char.IsWhiteSpace(html[i]))
                    {
                        i++;
                    }

                    if (i < html.Length && html[i] is '"' or '\'')
                    {
                        char quote = html[i];
                        int close = html.IndexOf(quote, i + 1);
                        close = close < 0 ? html.Length : close;
                        value = html[(i + 1)..close];
                        i = Math.Min(html.Length, close + 1);
                    }
                    else
                    {
                        int valueStart = i;
                        while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                        {
                            i++;
                        }

                        value = html[valueStart..i];
                    }
                }

                if (name.Length > 0)
                {
                    result.TryAdd(name, WebUtility.HtmlDecode(value));
                }
            }

            after = Math.Min(html.Length, i + 1);
            return result;
        }

        private void Open(string name, Dictionary<string, string> attributes, bool selfClosing)
        {
            var parent = Current;
            bool empty = Voids.Contains(name) || selfClosing;
            if (parent.Hide)
            {
                if (!empty)
                {
                    _open.Add(parent with { Name = name });
                }

                return;
            }

            var element = Style(name, attributes, parent);
            if (element.Hide || Hidden.Contains(name))
            {
                if (!empty)
                {
                    _open.Add(element with { Hide = true });
                }

                return;
            }

            switch (name)
            {
                case "br":
                    _text.Append('\n', parent.Style);
                    _pendingSpace = null;
                    return;
                case "td" or "th" when _text.Length > 0 && _text.Last != '\n':
                    _text.Append('\t', parent.Style);
                    _pendingSpace = null;
                    break;
            }

            if (element.Block)
            {
                LineBreak(IsParagraph(name, _open.Count));
            }

            if (name == "li")
            {
                Bullet(element);
            }

            if (!empty)
            {
                _open.Add(element);
            }
        }

        private void Close(string name)
        {
            int at = _open.FindLastIndex(e => e.Name == name);
            if (at < 0)
            {
                return;
            }

            bool block = false, paragraph = false;
            for (int i = _open.Count - 1; i >= at; i--)
            {
                block |= _open[i].Block && !_open[i].Hide;
                paragraph |= IsParagraph(_open[i].Name, i);
            }

            _open.RemoveRange(at, _open.Count - at);
            if (block)
            {
                LineBreak(paragraph);
            }
        }

        /// <summary>Starts a new line, or leaves a blank one before a paragraph; never at the start.</summary>
        private void LineBreak(bool blankLine)
        {
            _pendingSpace = null;
            if (_text.Length == 0)
            {
                return;
            }

            if (_text.Last != '\n')
            {
                _text.Append('\n', TextStyle.Default);
            }

            if (blankLine && !EndsWithBlankLine())
            {
                _text.Append('\n', TextStyle.Default);
            }
        }

        /// <summary>A list nested in a list item is part of it: no blank line around it.</summary>
        private bool IsParagraph(string name, int ancestors) =>
            Paragraphs.Contains(name) && !(name is "ul" or "ol" or "dl" && _open.Take(ancestors).Any(e => e.Name is "li" or "dd"));

        private bool EndsWithBlankLine() => _text.Length >= 2 && _text.Last == '\n' && _text.CharAt(_text.Length - 2) == '\n';

        private void Bullet(Element item)
        {
            int depth = _open.Count(e => e.ListCounter is not null);
            string indent = new(' ', 2 * Math.Max(0, depth - 1));
            int list = _open.FindLastIndex(e => e.ListCounter is not null);
            string mark = "• ";
            if (list >= 0 && _open[list] is { Name: "ol", ListCounter: { } counter })
            {
                _open[list] = _open[list] with { ListCounter = counter + 1 };
                mark = $"{counter + 1}. ";
            }

            _text.Append(indent + mark, item.Style);
        }

        private void Text(string raw)
        {
            var element = Current;
            if (element.Hide)
            {
                return;
            }

            string text = WebUtility.HtmlDecode(raw);
            if (element.Pre)
            {
                if (text.Length > 0)
                {
                    FlushSpace();
                    _text.Append(text, element.Style);
                }

                return;
            }

            var words = new StringBuilder();
            foreach (char c in text)
            {
                if (c is ' ' or '\t' or '\n' or '\r' or '\f')
                {
                    if (words.Length > 0)
                    {
                        FlushSpace();
                        _text.Append(words.ToString(), element.Style);
                        words.Clear();
                    }

                    _pendingSpace ??= element.Style;
                }
                else
                {
                    words.Append(c);
                }
            }

            if (words.Length > 0)
            {
                FlushSpace();
                _text.Append(words.ToString(), element.Style);
            }
        }

        /// <summary>Collapsed white space: one space between words, none at the start of a line.</summary>
        private void FlushSpace()
        {
            if (_pendingSpace is { } space && _text.Length > 0 && _text.Last is not ('\n' or ' ' or '\t'))
            {
                _text.Append(' ', space);
            }

            _pendingSpace = null;
        }

        private static Element Style(string name, Dictionary<string, string> attributes, Element parent)
        {
            var font = parent.Style.Font;
            var ink = parent.Style.Ink;
            var background = parent.Style.Highlight;
            bool pre = parent.Pre, hide = false, block = Blocks.Contains(name);
            int? counter = name is "ul" or "ol" ? 0 : null;

            switch (name)
            {
                case "b" or "strong" or "th" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    font |= FontStyle.Bold;
                    break;
                case "i" or "em" or "cite" or "var" or "dfn" or "address":
                    font |= FontStyle.Italic;
                    break;
                case "u" or "ins":
                    font |= FontStyle.Underline;
                    break;
                case "s" or "strike" or "del":
                    font |= FontStyle.Strikeout;
                    break;
                case "mark":
                    background = Color.Yellow;
                    break;
                case "pre" or "textarea" or "listing" or "xmp" or "plaintext":
                    pre = true;
                    break;
            }

            if (attributes.TryGetValue("color", out string? color) && CssColor.TryParse(color) is { A: > 0 } fontColor)
            {
                ink = fontColor;
            }

            if (attributes.TryGetValue("bgcolor", out string? bgcolor) && CssColor.TryParse(bgcolor) is { A: > 0 } fill)
            {
                background = fill;
            }

            if (attributes.TryGetValue("hidden", out _))
            {
                hide = true;
            }

            if (attributes.TryGetValue("style", out string? css))
            {
                foreach (string declaration in css.Split(';'))
                {
                    int colon = declaration.IndexOf(':');
                    if (colon < 0)
                    {
                        continue;
                    }

                    string property = declaration[..colon].Trim().ToLowerInvariant();
                    string value = declaration[(colon + 1)..].Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim().ToLowerInvariant();
                    switch (property)
                    {
                        case "color" when CssColor.TryParse(value) is { A: > 0 } c:
                            ink = c;
                            break;

                        // Transparent lets the parent's background show: it stays.
                        case "background-color" or "background" when CssColor.FirstIn(value) is { A: > 0 } c:
                            background = c;
                            break;
                        case "font-weight":
                            font = IsBold(value) ? font | FontStyle.Bold : font & ~FontStyle.Bold;
                            break;
                        case "font-style":
                            font = value is "italic" || value.StartsWith("oblique", StringComparison.Ordinal) ? font | FontStyle.Italic : font & ~FontStyle.Italic;
                            break;
                        case "text-decoration" or "text-decoration-line":
                            // A decoration is drawn across the content: a descendant cannot remove it.
                            if (value.Contains("underline"))
                            {
                                font |= FontStyle.Underline;
                            }

                            if (value.Contains("line-through"))
                            {
                                font |= FontStyle.Strikeout;
                            }

                            break;
                        case "white-space":
                            pre = value.StartsWith("pre", StringComparison.Ordinal) || value == "break-spaces";
                            break;
                        case "display":
                            hide |= value == "none";
                            block = value is "block" or "flex" or "grid" or "list-item" or "table" or "table-row" or "table-caption" || (block && value is not ("inline" or "inline-block" or "inline-flex" or "contents"));
                            break;
                    }
                }
            }

            return new Element(name, new TextStyle(font, ink, background), pre, hide, block, counter);
        }

        private static bool IsBold(string weight) =>
            weight is "bold" or "bolder" || (int.TryParse(weight, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 600);
    }

    /// <summary>
    /// CSS colors: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c> /
    /// <c>rgba()</c>, named colors, <c>transparent</c>. A translucent color is laid over white: the
    /// paper it is most often copied from; a fully transparent one keeps an alpha of 0.
    /// </summary>
    private static class CssColor
    {
        public static Color? TryParse(string value)
        {
            value = value.Trim().ToLowerInvariant();
            if (value == "transparent")
            {
                return Color.Transparent;
            }

            if (value.StartsWith('#'))
            {
                return Hex(value[1..]);
            }

            if (value.StartsWith("rgb", StringComparison.Ordinal))
            {
                return Functional(value);
            }

            var named = Color.FromName(value);
            return named.IsKnownColor && !named.IsSystemColor ? Color.FromArgb(named.ToArgb()) : null;
        }

        /// <summary>The first color in a shorthand such as <c>background: rgb(…) none repeat</c>.</summary>
        public static Color? FirstIn(string value)
        {
            if (TryParse(value) is { } whole)
            {
                return whole;
            }

            int rgb = value.IndexOf("rgb", StringComparison.Ordinal);
            if (rgb >= 0)
            {
                int close = value.IndexOf(')', rgb);
                return close > rgb ? TryParse(value[rgb..(close + 1)]) : null;
            }

            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TryParse).FirstOrDefault(c => c is not null);
        }

        private static Color? Hex(string digits)
        {
            if (digits.Length is 3 or 4)
            {
                digits = string.Concat(digits.Select(d => $"{d}{d}"));
            }

            if (digits.Length is not (6 or 8) || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return null;
            }

            return digits.Length == 6
                ? Color.FromArgb((int)(value >> 16) & 255, (int)(value >> 8) & 255, (int)value & 255)
                : OverWhite((int)(value >> 24) & 255, (int)(value >> 16) & 255, (int)(value >> 8) & 255, (int)(value & 255) / 255.0);
        }

        /// <summary><c>rgb(1, 2, 3)</c>, <c>rgba(1, 2, 3, 0.5)</c>, <c>rgb(1 2 3 / 50%)</c>.</summary>
        private static Color? Functional(string value)
        {
            int open = value.IndexOf('('), close = value.IndexOf(')');
            if (open < 0 || close < open)
            {
                return null;
            }

            string[] parts = value[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is not (3 or 4))
            {
                return null;
            }

            var channels = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool percent = part.EndsWith('%');
                if (!double.TryParse(percent ? part[..^1] : part, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    return null;
                }

                channels[i] = i < 3
                    ? Math.Clamp(percent ? number * 2.55 : number, 0, 255)
                    : Math.Clamp(percent ? number / 100 : number, 0, 1);
            }

            return OverWhite((int)Math.Round(channels[0]), (int)Math.Round(channels[1]), (int)Math.Round(channels[2]), parts.Length == 4 ? channels[3] : 1);
        }

        private static Color OverWhite(int r, int g, int b, double alpha)
        {
            if (alpha <= 0)
            {
                return Color.FromArgb(0, r, g, b);
            }

            int Blend(int channel) => (int)Math.Round(channel * alpha + 255 * (1 - alpha));
            return Color.FromArgb(Blend(r), Blend(g), Blend(b));
        }
    }
}
