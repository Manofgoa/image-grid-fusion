using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ImageGridFusion.UI;

/// <summary>A section of <c>SHORTCUTS.md</c>: its <c>##</c> heading and the rows of its table.</summary>
internal sealed record ShortcutSection(string Title, IReadOnlyList<ShortcutRow> Rows);

/// <summary>One shortcut: its keys and gestures, then what it does.</summary>
internal sealed record ShortcutRow(IReadOnlyList<ShortcutPart> Keys, string Does);

/// <summary>
/// A piece of a shortcut's cell: a key written <c>&lt;kbd&gt;</c>, drawn as a key cap, or a word written between
/// them; <paramref name="SpaceBefore"/> when a space separates it from the piece before.
/// </summary>
internal readonly record struct ShortcutPart(string Text, bool IsKey, bool SpaceBefore);

/// <summary>
/// <c>SHORTCUTS.md</c>, embedded in the exe, as the shortcuts window shows it: its <c>##</c> sections and, under
/// each, the rows of its <c>| Shortcut | Does |</c> table — the language line, the title and the paragraphs left
/// out. The file's shape is the contract (RULES.md § Keyboard Shortcuts, workfiles/20261029-shortcuts-list.md).
/// </summary>
internal static partial class ShortcutList
{
    private const string ResourceName = "ImageGridFusion.SHORTCUTS.md";

    /// <summary>The sections of the embedded file.</summary>
    public static IReadOnlyList<ShortcutSection> Load()
    {
        using var stream = typeof(ShortcutList).Assembly.GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// The sections of <paramref name="markdown"/>: a <c>##</c> heading opens one, the first row of a table under
    /// it is its header, a row of dashes its separator; a section without rows is left out.
    /// </summary>
    public static IReadOnlyList<ShortcutSection> Parse(string markdown)
    {
        var sections = new List<ShortcutSection>();
        string? title = null;
        var rows = new List<ShortcutRow>();
        bool inTable = false;

        void Close()
        {
            if (title is not null && rows.Count > 0)
            {
                sections.Add(new ShortcutSection(title, [.. rows]));
            }

            rows.Clear();
        }

        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('#'))
            {
                Close();
                title = line.StartsWith("## ") ? Plain(line[3..]) : null;
                inTable = false;
                continue;
            }

            if (title is null || !line.StartsWith('|'))
            {
                inTable = false;
                continue;
            }

            string[] cells = line.Trim('|').Split('|');
            if (cells.Length < 2)
            {
                continue;
            }

            if (!inTable)
            {
                inTable = true;
                continue;
            }

            if (cells.All(cell => cell.Trim().All(c => c is '-' or ':')))
            {
                continue;
            }

            rows.Add(new ShortcutRow(Parts(cells[0]), Plain(string.Join('|', cells[1..]))));
        }

        Close();
        return sections;
    }

    /// <summary>A shortcut's cell cut into its keys and the words between them.</summary>
    private static IReadOnlyList<ShortcutPart> Parts(string cell)
    {
        var parts = new List<ShortcutPart>();
        bool space = false;

        void Words(string text)
        {
            var word = new StringBuilder();
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                {
                    word.Append(c);
                    continue;
                }

                if (word.Length > 0)
                {
                    parts.Add(new ShortcutPart(Plain(word.ToString()), false, space));
                    word.Clear();
                }

                space = true;
            }

            if (word.Length > 0)
            {
                parts.Add(new ShortcutPart(Plain(word.ToString()), false, space));
                space = false;
            }
        }

        string trimmed = cell.Trim();
        int at = 0;
        foreach (Match key in KeyPattern().Matches(trimmed))
        {
            Words(trimmed[at..key.Index]);
            parts.Add(new ShortcutPart(Plain(key.Groups[1].Value), true, space));
            space = false;
            at = key.Index + key.Length;
        }

        Words(trimmed[at..]);
        return parts.Where(part => part.Text.Length > 0).ToList();
    }

    /// <summary>A cell's text without its Markdown emphasis and code marks, its entities decoded.</summary>
    private static string Plain(string text) => WebUtility.HtmlDecode(MarkPattern().Replace(text, string.Empty)).Trim();

    [GeneratedRegex("<kbd>(.*?)</kbd>", RegexOptions.IgnoreCase)]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"\*\*|\*|`")]
    private static partial Regex MarkPattern();
}
