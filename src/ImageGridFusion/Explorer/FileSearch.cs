using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The file explorer's search over the index, from memory only: every word typed must appear in the
/// folded relative path of a file — its name or its subfolders, accents and case ignored — and the
/// best matches come first; <c>*</c> alone lists every file, the most recently created first. See
/// workfiles/20260926-file-explorer.md § Search and workfiles/20260927-file-explorer-show-all.md.
/// </summary>
internal static class FileSearch
{
    /// <summary>The query listing every file of the index.</summary>
    public const string Everything = "*";

    /// <summary>Whether the query is <see cref="Everything"/> alone, blanks around it ignored.</summary>
    public static bool IsEverything(string query) => query.Trim() == Everything;

    /// <summary>Every entry, the most recently created first, then by relative path.</summary>
    public static IReadOnlyList<IndexEntry> All(IReadOnlyList<IndexEntry> entries)
    {
        var all = entries.ToArray();
        Array.Sort(all, (a, b) =>
        {
            int order = b.Created.CompareTo(a.Created);
            return order != 0 ? order : string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
        });
        return all;
    }

    /// <summary>Folds a text for matching: the accents dropped, then lower case.</summary>
    public static string Fold(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(c);
            }
        }

        return folded.ToString().ToLowerInvariant();
    }

    /// <summary>The words of a query, folded; none when the query is blank.</summary>
    public static string[] Words(string query) => Fold(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Every entry matching every word, best first — the explorer shows them load by load. Ranked by
    /// the words found in the file name itself, then the position of the first word in the name, then
    /// the shorter name, then the relative path.
    /// </summary>
    public static IReadOnlyList<IndexEntry> Search(IReadOnlyList<IndexEntry> entries, string[] words)
    {
        if (words.Length == 0)
        {
            return [];
        }

        var matches = new List<(IndexEntry Entry, Rank Rank)>();
        foreach (var entry in entries)
        {
            if (Rank.Of(entry, words) is { } rank)
            {
                matches.Add((entry, rank));
            }
        }

        matches.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        return matches.Select(m => m.Entry).ToArray();
    }

    /// <summary>How well an entry matches; lower compares first.</summary>
    private readonly record struct Rank(int NameHits, int FirstPosition, int NameLength, string Path) : IComparable<Rank>
    {
        /// <summary>Null when a word is missing from the folded relative path.</summary>
        public static Rank? Of(IndexEntry entry, string[] words)
        {
            string folded = entry.Folded;
            int nameStart = entry.NameStart;
            int nameHits = 0;
            int first = int.MaxValue;
            for (int i = 0; i < words.Length; i++)
            {
                int inName = folded.IndexOf(words[i], nameStart, StringComparison.Ordinal);
                if (inName >= 0)
                {
                    nameHits++;
                    if (i == 0)
                    {
                        first = inName - nameStart;
                    }
                }
                else if (folded.IndexOf(words[i], StringComparison.Ordinal) < 0)
                {
                    return null;
                }
            }

            return new Rank(nameHits, first, folded.Length - nameStart, entry.RelativePath);
        }

        public int CompareTo(Rank other)
        {
            int order = other.NameHits.CompareTo(NameHits);
            if (order == 0)
            {
                order = FirstPosition.CompareTo(other.FirstPosition);
            }

            if (order == 0)
            {
                order = NameLength.CompareTo(other.NameLength);
            }

            return order != 0 ? order : string.Compare(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>A file of the index: its path relative to the base folder, its creation time, and the folded form the search matches.</summary>
internal sealed class IndexEntry
{
    private static readonly char[] Separators = ['\\', '/'];

    public IndexEntry(string relativePath, DateTime created, FileStamp? stamp = null)
    {
        RelativePath = relativePath;
        Created = created;
        this.Stamp = stamp;
        Folded = FileSearch.Fold(relativePath);
        NameStart = Folded.LastIndexOfAny(Separators) + 1;
    }

    public string RelativePath { get; }

    /// <summary>When the file was created — arrived in the folder — in UTC; <see cref="DateTime.MinValue"/> when unknown.</summary>
    public DateTime Created { get; }

    /// <summary>
    /// The file's size and last write as the scan found them; null for an entry loaded from the index
    /// file, which does not hold them — only a scanned index tells which content texts are stale.
    /// </summary>
    public FileStamp? Stamp { get; }

    /// <summary>The relative path, folded once for every search.</summary>
    public string Folded { get; }

    /// <summary>Where the file name starts in <see cref="Folded"/>.</summary>
    public int NameStart { get; }

    public string Name => Path.GetFileName(RelativePath);
}

/// <summary>What tells a file changed since its content text was extracted: its size and last write, in UTC.</summary>
internal readonly record struct FileStamp(long Size, DateTime Written);
