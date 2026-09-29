using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The file explorer's search over the index, from memory only: every word typed must appear in the
/// folded relative path of a file — its name or its subfolders, accents and case ignored — and the
/// best matches come first. See workfiles/20260926-file-explorer.md § Search.
/// </summary>
internal static class FileSearch
{
    /// <summary>How many results a search returns.</summary>
    public const int Limit = 10;

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
    /// The <see cref="Limit"/> best entries matching every word, best first, and how many matched in
    /// all. Ranked by the words found in the file name itself, then the position of the first word in
    /// the name, then the shorter name, then the relative path.
    /// </summary>
    public static (IReadOnlyList<IndexEntry> Best, int Total) Search(IReadOnlyList<IndexEntry> entries, string[] words)
    {
        if (words.Length == 0)
        {
            return ([], 0);
        }

        // Only the best Limit are kept, sorted: a bounded insertion, so a query matching most of a
        // large index never sorts it whole.
        var best = new List<(IndexEntry Entry, Rank Rank)>(Limit + 1);
        int total = 0;
        foreach (var entry in entries)
        {
            if (Rank.Of(entry, words) is not { } rank)
            {
                continue;
            }

            total++;
            int at = best.Count;
            while (at > 0 && rank.CompareTo(best[at - 1].Rank) < 0)
            {
                at--;
            }

            if (at < Limit)
            {
                best.Insert(at, (entry, rank));
                if (best.Count > Limit)
                {
                    best.RemoveAt(Limit);
                }
            }
        }

        return (best.Select(b => b.Entry).ToArray(), total);
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

    public IndexEntry(string relativePath, DateTime created)
    {
        RelativePath = relativePath;
        Created = created;
        Folded = FileSearch.Fold(relativePath);
        NameStart = Folded.LastIndexOfAny(Separators) + 1;
    }

    public string RelativePath { get; }

    /// <summary>When the file was created — arrived in the folder — in UTC; <see cref="DateTime.MinValue"/> when unknown.</summary>
    public DateTime Created { get; }

    /// <summary>The relative path, folded once for every search.</summary>
    public string Folded { get; }

    /// <summary>Where the file name starts in <see cref="Folded"/>.</summary>
    public int NameStart { get; }

    public string Name => Path.GetFileName(RelativePath);
}
