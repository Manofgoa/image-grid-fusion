using System.Text;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Reads the text of an RTF document (Word, WordPad…) with its character styles: bold, italic,
/// underline, strike, text and highlight colors. Fonts, sizes, pictures and the other
/// destinations (headers, fields' instructions…) are left out.
/// </summary>
public static class RtfReader
{
    /// <summary>Groups whose content is not part of the text.</summary>
    private static readonly HashSet<string> SkippedDestinations =
    [
        "fonttbl", "stylesheet", "info", "pict", "object", "header", "headerl", "headerr", "headerf",
        "footer", "footerl", "footerr", "footerf", "footnote", "listtable", "listoverridetable",
        "rsidtbl", "generator", "xmlnstbl", "themedata", "colorschememapping", "latentstyles",
        "datastore", "fldinst", "filetbl", "revtbl", "pgdsctbl", "mmathPr", "background",
        "nonshppict", "shp", "bkmkstart", "bkmkend", "userprops", "docvar", "pnseclvl",
    ];

    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["par"] = "\n", ["line"] = "\n", ["sect"] = "\n", ["page"] = "\n", ["row"] = "\n",
        ["tab"] = "\t", ["cell"] = "\t", ["nestcell"] = "\t",
        ["emdash"] = "—", ["endash"] = "–", ["bullet"] = "•", ["emspace"] = " ", ["enspace"] = " ",
        ["lquote"] = "‘", ["rquote"] = "’", ["ldblquote"] = "“", ["rdblquote"] = "”",
    };

    public static StyledText? TryRead(string rtf)
    {
        if (!rtf.StartsWith(@"{\rtf", StringComparison.Ordinal))
        {
            return null;
        }

        return new Parser(rtf).Parse();
    }

    private sealed class State
    {
        public bool Bold, Italic, Underline, Strike, Skip, ColorTable;
        public int Ink, Highlight, UnicodeSkip = 1;

        public State Clone() => (State)MemberwiseClone();

        public void Plain()
        {
            Bold = Italic = Underline = Strike = false;
            Ink = Highlight = 0;
        }
    }

    private sealed class Parser(string rtf)
    {
        private readonly StyledText.Builder _text = new();
        private readonly Stack<State> _states = new();
        private readonly List<Color?> _colors = [];
        private readonly List<byte> _bytes = [];
        private State _state = new();
        private Encoding _encoding = CodePage(1252);
        private int _pos;

        /// <summary>Characters still to drop after a <c>\u</c>: its fallback for older readers.</summary>
        private int _toSkip;

        private int _red, _green, _blue;
        private bool _colorSet;

        public StyledText Parse()
        {
            while (_pos < rtf.Length)
            {
                char c = rtf[_pos++];
                switch (c)
                {
                    case '{':
                        Flush();
                        _states.Push(_state);
                        _state = _state.Clone();
                        break;
                    case '}':
                        Flush();
                        if (_states.Count == 0)
                        {
                            return _text.Build();
                        }

                        _state = _states.Pop();
                        break;
                    case '\\':
                        ControlWord();
                        break;
                    case '\r' or '\n':
                        break;
                    default:
                        Flush();
                        Text(c.ToString());
                        break;
                }
            }

            Flush();
            return _text.Build();
        }

        private void ControlWord()
        {
            if (_pos >= rtf.Length)
            {
                return;
            }

            char c = rtf[_pos];
            if (!char.IsAsciiLetter(c))
            {
                _pos++;
                switch (c)
                {
                    case '\'':
                        if (_pos + 2 <= rtf.Length && byte.TryParse(rtf.AsSpan(_pos, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                        {
                            _pos += 2;
                            if (_toSkip > 0)
                            {
                                _toSkip--;
                            }
                            else if (!_state.Skip)
                            {
                                _bytes.Add(b);
                            }
                        }

                        return;
                    case '*':
                        // An optional destination: none of those this reader knows is text.
                        _state.Skip = true;
                        return;
                    case '~':
                        Flush();
                        Text(" ");
                        return;
                    case '_':
                        Flush();
                        Text("‑");
                        return;
                    case '\r' or '\n':
                        Flush();
                        Text("\n");
                        return;
                    case '\\' or '{' or '}':
                        Flush();
                        Text(c.ToString());
                        return;
                    default:
                        // \- (optional hyphen), \: (index subentry), and the like.
                        return;
                }
            }

            int start = _pos;
            while (_pos < rtf.Length && char.IsAsciiLetter(rtf[_pos]))
            {
                _pos++;
            }

            string word = rtf[start.._pos];
            int? param = null;
            int numberStart = _pos;
            if (_pos < rtf.Length && (rtf[_pos] == '-' || char.IsAsciiDigit(rtf[_pos])))
            {
                _pos++;
                while (_pos < rtf.Length && char.IsAsciiDigit(rtf[_pos]))
                {
                    _pos++;
                }

                if (int.TryParse(rtf.AsSpan(numberStart, _pos - numberStart), out int value))
                {
                    param = value;
                }
            }

            if (_pos < rtf.Length && rtf[_pos] == ' ')
            {
                _pos++;
            }

            Flush();
            Apply(word, param);
        }

        private void Apply(string word, int? param)
        {
            bool on = param is not 0;
            switch (word)
            {
                case "bin":
                    _pos = Math.Min(rtf.Length, _pos + Math.Max(0, param ?? 0));
                    return;
                case "u" when param is { } code:
                    if (!_state.Skip)
                    {
                        Text(((char)(code < 0 ? code + 65536 : code)).ToString());
                    }

                    _toSkip = _state.UnicodeSkip;
                    return;
                case "uc":
                    _state.UnicodeSkip = Math.Max(0, param ?? 1);
                    return;
                case "ansicpg" when param is { } page:
                    _encoding = CodePage(page);
                    return;
                case "colortbl":
                    _state.ColorTable = true;
                    _state.Skip = true;
                    return;
                case "red":
                    _red = param ?? 0;
                    _colorSet = true;
                    return;
                case "green":
                    _green = param ?? 0;
                    _colorSet = true;
                    return;
                case "blue":
                    _blue = param ?? 0;
                    _colorSet = true;
                    return;
            }

            if (SkippedDestinations.Contains(word))
            {
                _state.Skip = true;
                return;
            }

            if (_state.Skip)
            {
                return;
            }

            if (Symbols.TryGetValue(word, out string? symbol))
            {
                Text(symbol);
                return;
            }

            switch (word)
            {
                case "plain":
                    _state.Plain();
                    break;
                case "b":
                    _state.Bold = on;
                    break;
                case "i":
                    _state.Italic = on;
                    break;
                case "strike" or "striked":
                    _state.Strike = on;
                    break;
                case "ulnone":
                    _state.Underline = false;
                    break;
                case "cf":
                    _state.Ink = param ?? 0;
                    break;
                case "highlight" or "cb" or "chcbpat":
                    _state.Highlight = param ?? 0;
                    break;
                case not "ulc" when word.StartsWith("ul", StringComparison.Ordinal):
                    // \ul, \uld, \uldb, \ulwave…: every kind of underline.
                    _state.Underline = on;
                    break;
            }
        }

        private void Text(string text)
        {
            if (_state.ColorTable)
            {
                foreach (char c in text)
                {
                    if (c == ';')
                    {
                        _colors.Add(_colorSet ? Color.FromArgb(_red, _green, _blue) : null);
                        _red = _green = _blue = 0;
                        _colorSet = false;
                    }
                }

                return;
            }

            if (_state.Skip)
            {
                return;
            }

            if (_toSkip > 0)
            {
                int dropped = Math.Min(_toSkip, text.Length);
                _toSkip -= dropped;
                text = text[dropped..];
                if (text.Length == 0)
                {
                    return;
                }
            }

            var font = (_state.Bold ? FontStyle.Bold : 0) | (_state.Italic ? FontStyle.Italic : 0)
                | (_state.Underline ? FontStyle.Underline : 0) | (_state.Strike ? FontStyle.Strikeout : 0);
            _text.Append(text, new TextStyle(font, ColorAt(_state.Ink), ColorAt(_state.Highlight)));
        }

        /// <summary>Decodes the bytes given as <c>\'hh</c> together: a multibyte code page needs them all.</summary>
        private void Flush()
        {
            if (_bytes.Count == 0)
            {
                return;
            }

            string text = _encoding.GetString(_bytes.ToArray());
            _bytes.Clear();
            Text(text);
        }

        /// <summary>Entry 0 is the automatic color: the page's default.</summary>
        private Color? ColorAt(int index) => index > 0 && index < _colors.Count ? _colors[index] : null;

        private static Encoding CodePage(int page)
        {
            try
            {
                return CodePagesEncodingProvider.Instance.GetEncoding(page) ?? Encoding.GetEncoding(page);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return Encoding.Latin1;
            }
        }
    }
}
