using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The file explorer's search over the index, from memory only: every word typed must appear in the
/// folded relative path of a file — its name or its subfolders, accents and case ignored — or in its
/// content text, and the best matches come first; <c>*</c> alone lists every file, in the sort
/// drop-down's order (<see cref="FileOrder"/>). See workfiles/20260926-file-explorer.md § Search,
/// workfiles/20260927-file-explorer-show-all.md, workfiles/20260926-ocr-search.md § Search and
/// workfiles/20261009-search-results-sort.md.
/// </summary>
internal static class FileSearch
{
    /// <summary>The query listing every file of the index.</summary>
    public const string Everything = "*";

    /// <summary>
    /// The shortest query word looked for in a content text with its spaces removed — letters the OCR
    /// read apart: a shorter one would match almost anywhere. See
    /// workfiles/20261013-complementary-ocr-passes.md § Spaces-Ignored Match.
    /// </summary>
    public const int JoinedMinLength = 3;

    /// <summary>Whether the query is <see cref="Everything"/> alone, blanks around it ignored.</summary>
    public static bool IsEverything(string query) => query.Trim() == Everything;

    /// <summary>Every entry, in <paramref name="order"/> — the sort drop-down's.</summary>
    public static IReadOnlyList<IndexEntry> All(IReadOnlyList<IndexEntry> entries, FileOrder order)
    {
        var all = entries.ToArray();
        Array.Sort(all, (a, b) => FileOrders.Compare(order, a, b));
        return all;
    }

    /// <summary>Folds a text for matching: the accents dropped, then lower case.</summary>
    public static string Fold(string text)
    {
        // Plain ASCII has no accent to drop: most OCR text, folded without decomposing it.
        if (System.Text.Ascii.IsValid(text))
        {
            return text.ToLowerInvariant();
        }

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
    /// Every entry matching every word, best first — the explorer shows them load by load. A word
    /// missing from the relative path may be found in the entry's content text, how reliably given by
    /// <paramref name="contentOf"/> (null: none, or no content search), the match then made by its
    /// content; with <paramref name="byName"/> false, the path is not read and every word is looked for in
    /// the content text only. Ranked by the entries matched by their path alone first, then by the words
    /// found in the file name itself, then the content matches by their grade — the least reliable of
    /// their words' (<see cref="ContentGrade"/>) — then the position of the first word in the name, then
    /// the shorter name, then the relative path. With an <paramref name="order"/> — the sort drop-down's,
    /// for files — only the relevance tiers are kept (the path matches by the words found in the name,
    /// then the content matches by their grade), each tier in that order: see
    /// workfiles/20261009-search-results-sort.md § Relevance Tiers and
    /// workfiles/20261013-complementary-ocr-passes.md § Ranking.
    /// </summary>
    public static IReadOnlyList<SearchMatch> Search(IReadOnlyList<IndexEntry> entries, string[] words, Func<IndexEntry, string, ContentGrade?>? contentOf = null, bool byName = true, FileOrder? order = null)
    {
        if (words.Length == 0)
        {
            return [];
        }

        var matches = new List<(IndexEntry Entry, Rank Rank)>();
        foreach (var entry in entries)
        {
            if (Rank.Of(entry, words, contentOf, byName) is { } rank)
            {
                matches.Add((entry, rank));
            }
        }

        if (order is { } files)
        {
            matches.Sort((a, b) =>
            {
                int tier = a.Rank.CompareTier(b.Rank);
                return tier != 0 ? tier : FileOrders.Compare(files, a.Entry, b.Entry);
            });
        }
        else
        {
            matches.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }

        return matches.Select(m => new SearchMatch(m.Entry, m.Rank.ContentWord, m.Rank.Tier(words.Length))).ToArray();
    }

    /// <summary>
    /// The effect presets matching every word, by their file's name or their description — the search's
    /// <c>Aa</c> criterion — newest first within a tier, the tiers numbered as <see cref="SearchMatch.Tier"/>
    /// numbers the files': a preset whose file's name holds every word ranks with the files matched by
    /// their name, one found by its description ranks after every path match and before the content
    /// matches. See workfiles/20261026-effect-preset-favorites.md § Search.
    /// </summary>
    public static IReadOnlyList<PresetMatch> SearchPresets(IEnumerable<Favorite> presets, string[] words)
    {
        if (words.Length == 0)
        {
            return [];
        }

        var matches = new List<PresetMatch>();
        foreach (var favorite in presets)
        {
            if (favorite.Preset is not { } preset)
            {
                continue;
            }

            string name = Fold(Path.GetFileName(favorite.FullPath));
            string description = preset.Folded;
            int nameHits = 0;
            string? descriptionWord = null;
            bool matched = true;
            foreach (string word in words)
            {
                if (name.Contains(word, StringComparison.Ordinal))
                {
                    nameHits++;
                }
                else if (description.Contains(word, StringComparison.Ordinal))
                {
                    descriptionWord ??= word;
                }
                else
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                matches.Add(new PresetMatch(favorite, descriptionWord, descriptionWord is null ? words.Length - nameHits : DescriptionTier(words.Length)));
            }
        }

        // Stable: the newest first within a tier, as given.
        return [.. matches.OrderBy(match => match.Tier)];
    }

    // The tiers: the path matches 0 (every word in the name) to words (none in the name), the
    // description matches next, then the content matches by their grade.
    private static int DescriptionTier(int words) => words + 1;

    /// <summary>How well an entry matches; lower compares first.</summary>
    private readonly record struct Rank(string? ContentWord, ContentGrade Grade, int NameHits, int FirstPosition, int NameLength, string Path) : IComparable<Rank>
    {
        /// <summary>Whether a word was found in the content text only.</summary>
        public bool ByContent => this.ContentWord is not null;

        /// <summary>Its relevance tier as a number, lower first, comparable with a preset's (<see cref="SearchPresets"/>).</summary>
        public int Tier(int words) => this.ByContent ? DescriptionTier(words) + 1 + (int)this.Grade : words - this.NameHits;

        /// <summary>
        /// Null when a word is missing from both the folded relative path — not read unless
        /// <paramref name="byName"/> — and the content text.
        /// </summary>
        public static Rank? Of(IndexEntry entry, string[] words, Func<IndexEntry, string, ContentGrade?>? contentOf, bool byName)
        {
            string folded = entry.Folded;
            int nameStart = entry.NameStart;
            int nameHits = 0;
            int first = int.MaxValue;
            string? contentWord = null;
            var grade = ContentGrade.Sure;
            for (int i = 0; i < words.Length; i++)
            {
                int inName = byName ? folded.IndexOf(words[i], nameStart, StringComparison.Ordinal) : -1;
                if (inName >= 0)
                {
                    nameHits++;
                    if (i == 0)
                    {
                        first = inName - nameStart;
                    }
                }
                else if (!byName || folded.IndexOf(words[i], StringComparison.Ordinal) < 0)
                {
                    if (contentOf?.Invoke(entry, words[i]) is not { } found)
                    {
                        return null;
                    }

                    contentWord ??= words[i];
                    grade = (ContentGrade)Math.Max((int)grade, (int)found);
                }
            }

            return new Rank(contentWord, grade, nameHits, first, folded.Length - nameStart, entry.RelativePath);
        }

        /// <summary>
        /// The relevance tier only: the path matches first, the more words in the file name the
        /// earlier, then the content matches, the more reliable grade the earlier.
        /// </summary>
        public int CompareTier(Rank other)
        {
            int order = this.ByContent.CompareTo(other.ByContent);
            return order != 0 ? order : this.ByContent ? this.Grade.CompareTo(other.Grade) : other.NameHits.CompareTo(this.NameHits);
        }

        public int CompareTo(Rank other)
        {
            int order = this.CompareTier(other);
            if (order == 0)
            {
                order = other.NameHits.CompareTo(NameHits);
            }

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

/// <summary>A file of the index: its path relative to the base folder, its creation time, its size, and the folded form the search matches.</summary>
internal sealed class IndexEntry
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <param name="size">The file's size in bytes; <paramref name="stamp"/>'s when not given.</param>
    public IndexEntry(string relativePath, DateTime created, FileStamp? stamp = null, long? size = null)
    {
        RelativePath = relativePath;
        Created = created;
        this.Stamp = stamp;
        this.Size = size ?? stamp?.Size;
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

    /// <summary>
    /// The file's size in bytes; null when unknown — an entry loaded from an index file written before
    /// the sizes were (<c>index 2</c>), until the scan replaces it. See workfiles/20261009-search-results-sort.md.
    /// </summary>
    public long? Size { get; }

    /// <summary>The relative path, folded once for every search.</summary>
    public string Folded { get; }

    /// <summary>Where the file name starts in <see cref="Folded"/>.</summary>
    public int NameStart { get; }

    public string Name => Path.GetFileName(RelativePath);
}

/// <summary>
/// An entry the search found; <paramref name="ContentWord"/>: the first query word found in its content
/// text only — null when its path holds every word. <paramref name="Tier"/>: its relevance tier, lower
/// first — the order a search keeps whatever the sort drop-down says, comparable with a preset's.
/// </summary>
internal readonly record struct SearchMatch(IndexEntry Entry, string? ContentWord, int Tier = 0)
{
    /// <summary>Whether the entry was found thanks to its content text.</summary>
    public bool ByContent => this.ContentWord is not null;
}

/// <summary>
/// An effect preset the search found; <paramref name="DescriptionWord"/>: the first query word found in
/// its description only — null when its file's name holds every word. <paramref name="Tier"/>: as
/// <see cref="SearchMatch.Tier"/>.
/// </summary>
internal readonly record struct PresetMatch(Favorite Favorite, string? DescriptionWord, int Tier);

/// <summary>What tells a file changed since its content text was extracted: its size and last write, in UTC.</summary>
internal readonly record struct FileStamp(long Size, DateTime Written);
