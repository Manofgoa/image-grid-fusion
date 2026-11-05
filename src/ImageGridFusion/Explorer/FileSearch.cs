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
    /// How <paramref name="word"/> stands in <paramref name="folded"/> from <paramref name="from"/> on — the
    /// best of its occurrences: a whole word, the start of a longer one, or inside one; null when it is not
    /// there. A word is a run of letters, or of digits: anything else, and a change between a letter and a
    /// digit, separates two words. See workfiles/20261105-whole-word-search-ranking.md § Words and Their Boundaries.
    /// </summary>
    public static WordLevel? LevelOf(string folded, string word, int from = 0)
    {
        WordLevel? best = null;
        for (int at = folded.IndexOf(word, from, StringComparison.Ordinal); at >= 0; at = folded.IndexOf(word, at + 1, StringComparison.Ordinal))
        {
            int end = at + word.Length;
            bool left = at == 0 || Splits(folded[at - 1], folded[at]);
            bool right = end == folded.Length || Splits(folded[end - 1], folded[end]);
            var level = left && right ? WordLevel.Whole : left ? WordLevel.Start : WordLevel.Inside;
            if (level == WordLevel.Whole)
            {
                return level;
            }

            if (best is null || level < best)
            {
                best = level;
            }
        }

        return best;
    }

    /// <summary>Whether a word ends between two characters.</summary>
    private static bool Splits(char before, char after) =>
        !char.IsLetterOrDigit(before) || !char.IsLetterOrDigit(after) || char.IsDigit(before) != char.IsDigit(after);

    /// <summary>
    /// Every entry matching every word, best first — the explorer shows them load by load. A word
    /// missing from the relative path may be found in the entry's content text, how reliably and how
    /// given by <paramref name="contentOf"/> (null: none, or no content search), the match then made by its
    /// content; with <paramref name="byName"/> false, the path is not read and every word is looked for in
    /// the content text only. Ranked by the entries matched by their path alone first, then by the words
    /// found in the file name itself, then the content matches by their grade — the least reliable of
    /// their words' (<see cref="ContentGrade"/>) — each of them then by the words found whole, then at a
    /// word's start (<see cref="WordLevel"/>), more first; then the position of the first word in the name,
    /// then the shorter name, then the relative path. With an <paramref name="order"/> — the sort drop-down's,
    /// for files — only the relevance tiers and their levels are kept, each in that order: see
    /// workfiles/20261009-search-results-sort.md § Relevance Tiers,
    /// workfiles/20261013-complementary-ocr-passes.md § Ranking and
    /// workfiles/20261105-whole-word-search-ranking.md.
    /// </summary>
    public static IReadOnlyList<SearchMatch> Search(IReadOnlyList<IndexEntry> entries, string[] words, Func<IndexEntry, string, ContentHit?>? contentOf = null, bool byName = true, FileOrder? order = null)
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

        return matches.Select(m => new SearchMatch(m.Entry, m.Rank.ContentWord ?? m.Rank.AlsoContentWord, m.Rank.Tier(words.Length))).ToArray();
    }

    /// <summary>
    /// The favorites matching every word — a file by its name, an effect preset by its file's name or
    /// its description: the search's <c>Aa</c> criterion — newest first within a tier, the tiers
    /// numbered as <see cref="SearchMatch.Tier"/> numbers the files': one whose name holds every word
    /// ranks with the files matched by their name, a preset found by its description after every path
    /// match and before the content matches. See workfiles/20261026-effect-preset-favorites.md § Search.
    /// </summary>
    public static IReadOnlyList<FavoriteMatch> SearchFavorites(IEnumerable<Favorite> favorites, string[] words)
    {
        if (words.Length == 0)
        {
            return [];
        }

        var matches = new List<FavoriteMatch>();
        foreach (var favorite in favorites)
        {
            string name = Fold(Path.GetFileName(favorite.FullPath));
            string description = favorite.Preset?.Folded ?? "";
            int whole = 0;
            int start = 0;
            string? descriptionWord = null;
            bool matched = true;
            foreach (string word in words)
            {
                if (LevelOf(name, word) is { } level)
                {
                    whole += level == WordLevel.Whole ? 1 : 0;
                    start += level == WordLevel.Start ? 1 : 0;
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
                // Its name holding every word: the first path tier, ranked by its levels as a file's name is.
                matches.Add(new FavoriteMatch(favorite, descriptionWord, descriptionWord is null ? Numbered(0, words.Length, whole, start) : DescriptionTier(words.Length)));
            }
        }

        // Stable: the newest first within a tier, as given.
        return [.. matches.OrderBy(match => match.Tier)];
    }

    /// <summary>
    /// The files of the people whose name holds every word — accents and case ignored — in their own tier,
    /// after the presets found by their description and before the content matches, numbered as
    /// <see cref="SearchMatch.Tier"/> numbers the files'; each file once, in the people's order. See
    /// workfiles/20261027-face-detection.md § Search by Name.
    /// </summary>
    public static IReadOnlyList<(string FullPath, int Tier)> SearchPeople(IEnumerable<(string Name, IEnumerable<string> Files)> people, string[] words)
    {
        if (words.Length == 0)
        {
            return [];
        }

        var found = new List<(string FullPath, int Tier)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, files) in people)
        {
            string folded = Fold(name);
            if (folded.Length > 0 && words.All(word => folded.Contains(word, StringComparison.Ordinal)))
            {
                found.AddRange(files.Where(seen.Add).Select(file => (file, PersonTier(words.Length))));
            }
        }

        return found;
    }

    // The tiers: the files whose name and content both hold every word -1, the path matches 0 (every
    // word in the name) to words (none in the name), the description matches next, the people's files
    // after them, then the content matches by their grade. Each numbered with its levels folded in
    // (Numbered): a tier spans Levels(words) numbers, the more words found whole, then at a word's start,
    // the lower. The description, people and similarity tiers have no levels: their first number.
    private const int NameAndContentTier = -1;

    /// <summary>How many numbers a tier spans: one per count of words found whole and at a word's start.</summary>
    private static int Levels(int words) => (words + 1) * (words + 1);

    /// <summary>
    /// The number of a match in <paramref name="tier"/> with <paramref name="whole"/> of its
    /// <paramref name="words"/> found whole and <paramref name="start"/> at a word's start.
    /// </summary>
    private static int Numbered(int tier, int words, int whole, int start) =>
        First(tier, words) + (words - whole) * (words + 1) + (words - start);

    /// <summary>The first number of <paramref name="tier"/>: a tier without levels.</summary>
    private static int First(int tier, int words) => tier * Levels(words);

    private static int DescriptionBase(int words) => words + 1;

    private static int PersonBase(int words) => DescriptionBase(words) + 1;

    private static int ContentBase(int words, ContentGrade grade) => PersonBase(words) + 1 + (int)grade;

    private static int DescriptionTier(int words) => First(DescriptionBase(words), words);

    /// <summary>
    /// Whether a tier — a file's, a preset's, a person's file's — is one of a match by a name: the file's
    /// name or path, a preset's description, a person's name; the search results' <b>Path</b> group, the
    /// others its <b>Content</b> group. See workfiles/20261103-file-explorer-groups.md § Search Result Groups.
    /// </summary>
    public static bool IsPathTier(int tier, int words) => tier < First(PersonBase(words) + 1, words);

    private static int PersonTier(int words) => First(PersonBase(words), words);

    /// <summary>
    /// The tier of the images found by their similarity to the query — after every content grade, the tags
    /// and the captions included — numbered as <see cref="SearchMatch.Tier"/> numbers the files'. See
    /// workfiles/20261027-image-content-analysis.md § Search Under 💡.
    /// </summary>
    public static int SimilarityTier(int words) => First(ContentBase(words, ContentGrade.SeenInside) + 1, words);

    /// <summary>
    /// At most <paramref name="count"/> entries whose embedding — given by <paramref name="embeddingOf"/>,
    /// null for none — stands out of the query's scores over every entry by <paramref name="cutoff"/> spreads,
    /// those in <paramref name="excluded"/> left out; each in the similarity tier for <paramref name="words"/>
    /// query words. For a text query, <paramref name="spreadOf"/> gives each image's spread over the
    /// vocabulary's concepts: it must also stand out of it by the cutoff plus
    /// <see cref="VisionSimilarity.TextMargin"/>, and that is how far it stands out. The farthest first.
    /// See workfiles/20261029-similar-images-accuracy.md.
    /// </summary>
    public static IReadOnlyList<SearchMatch> SearchSimilar(IReadOnlyList<IndexEntry> entries, Func<IndexEntry, float[]?> embeddingOf, float[] query, int count, int words, float cutoff, IReadOnlySet<IndexEntry>? excluded = null, Func<IndexEntry, float[], VisionSimilarity.Spread>? spreadOf = null)
    {
        var scored = new List<(IndexEntry Entry, float[] Embedding, float Score)>();
        double sum = 0, squares = 0;
        foreach (var entry in entries)
        {
            if (embeddingOf(entry) is { } embedding)
            {
                float score = Embeddings.Dot(embedding, query);
                scored.Add((entry, embedding, score));
                sum += score;
                squares += (double)score * score;
            }
        }

        if (scored.Count == 0)
        {
            return [];
        }

        double mean = sum / scored.Count;
        var spread = new VisionSimilarity.Spread((float)mean, (float)Math.Sqrt(Math.Max(0, squares / scored.Count - mean * mean)));
        var kept = new List<(IndexEntry Entry, float Score, float Standing)>();
        foreach (var (entry, embedding, score) in scored)
        {
            float standing = spread.Of(score);
            if (standing < cutoff || (excluded is not null && excluded.Contains(entry)))
            {
                continue;
            }

            if (spreadOf is not null)
            {
                standing = spreadOf(entry, embedding).Of(score);
                if (standing < cutoff + VisionSimilarity.TextMargin)
                {
                    continue;
                }
            }

            kept.Add((entry, score, standing));
        }

        int tier = SimilarityTier(words);
        return kept.OrderByDescending(s => s.Standing).Take(count).Select(s => new SearchMatch(s.Entry, null, tier, s.Score, s.Standing)).ToArray();
    }

    /// <summary>
    /// How well an entry matches; lower compares first. <paramref name="AlsoContentWord"/>: for a file whose
    /// name holds every word, the first one when its content text holds them all too — the most relevant
    /// match, its light bulb shown (workfiles/20261103-file-explorer-groups.md § Search Result Groups).
    /// <paramref name="Whole"/> and <paramref name="Start"/>: how many query words it holds as a whole word,
    /// and at a word's start — each where it was found: the name, the subfolders, the content text.
    /// </summary>
    private readonly record struct Rank(string? ContentWord, ContentGrade Grade, int NameHits, int FirstPosition, int NameLength, string Path, string? AlsoContentWord = null, int Whole = 0, int Start = 0) : IComparable<Rank>
    {
        /// <summary>Whether a word was found in the content text only.</summary>
        public bool ByContent => this.ContentWord is not null;

        /// <summary>Whether its name and its content text both hold every word.</summary>
        public bool ByNameAndContent => this.AlsoContentWord is not null;

        /// <summary>Its relevance tier as a number, its levels folded in, lower first, comparable with a favorite's (<see cref="SearchFavorites"/>).</summary>
        public int Tier(int words) =>
            Numbered(this.ByNameAndContent ? NameAndContentTier : this.ByContent ? ContentBase(words, this.Grade) : words - this.NameHits, words, this.Whole, this.Start);

        /// <summary>
        /// Null when a word is missing from both the folded relative path — not read unless
        /// <paramref name="byName"/> — and the content text.
        /// </summary>
        public static Rank? Of(IndexEntry entry, string[] words, Func<IndexEntry, string, ContentHit?>? contentOf, bool byName)
        {
            string folded = entry.Folded;
            int nameStart = entry.NameStart;
            int nameHits = 0;
            int first = int.MaxValue;
            int whole = 0;
            int start = 0;
            string? contentWord = null;
            var grade = ContentGrade.Sure;
            for (int i = 0; i < words.Length; i++)
            {
                WordLevel? level;
                int inName = byName ? folded.IndexOf(words[i], nameStart, StringComparison.Ordinal) : -1;
                if (inName >= 0)
                {
                    nameHits++;
                    if (i == 0)
                    {
                        first = inName - nameStart;
                    }

                    level = LevelOf(folded, words[i], nameStart);
                }
                else if (byName && LevelOf(folded, words[i]) is { } inPath)
                {
                    level = inPath;
                }
                else
                {
                    if (contentOf?.Invoke(entry, words[i]) is not { } found)
                    {
                        return null;
                    }

                    contentWord ??= words[i];
                    grade = (ContentGrade)Math.Max((int)grade, (int)found.Grade);
                    level = found.Level;
                }

                whole += level == WordLevel.Whole ? 1 : 0;
                start += level == WordLevel.Start ? 1 : 0;
            }

            // Every word in the name: looked for in the content text too, the match then the most relevant.
            string? alsoContentWord = null;
            if (nameHits == words.Length && contentOf is not null && words.All(word => contentOf(entry, word) is not null))
            {
                alsoContentWord = words[0];
            }

            return new Rank(contentWord, grade, nameHits, first, folded.Length - nameStart, entry.RelativePath, alsoContentWord, whole, start);
        }

        /// <summary>
        /// The relevance tier and its levels only: the files found by their name and their content first,
        /// then the path matches, the more words in the file name the earlier, then the content matches, the
        /// more reliable grade the earlier; in each, the more words found whole, then at a word's start, the
        /// earlier.
        /// </summary>
        public int CompareTier(Rank other)
        {
            int order = other.ByNameAndContent.CompareTo(this.ByNameAndContent);
            if (order == 0)
            {
                order = this.ByContent.CompareTo(other.ByContent);
            }

            if (order == 0)
            {
                order = this.ByContent ? this.Grade.CompareTo(other.Grade) : other.NameHits.CompareTo(this.NameHits);
            }

            if (order == 0)
            {
                order = other.Whole.CompareTo(this.Whole);
            }

            return order != 0 ? order : other.Start.CompareTo(this.Start);
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
/// text only — null when its path holds every word, unless its name and its content text both do, or it
/// was found by its similarity. <paramref name="Tier"/>:
/// its relevance tier, lower first — the order a search keeps whatever the sort drop-down says, comparable
/// with a preset's. <paramref name="Similarity"/>: how close its image is to the query, for an entry found so;
/// <paramref name="Standing"/>: how far that stands out, in spreads (<see cref="VisionSimilarity"/>).
/// </summary>
internal readonly record struct SearchMatch(IndexEntry Entry, string? ContentWord, int Tier = 0, float? Similarity = null, float? Standing = null)
{
    /// <summary>Whether the entry was found thanks to its content text.</summary>
    public bool ByContent => this.ContentWord is not null;
}

/// <summary>
/// A favorite the search found — a file, or an effect preset; <paramref name="DescriptionWord"/>: the
/// first query word a preset holds in its description only — null when its file's name holds every word. <paramref name="Tier"/>: as
/// <see cref="SearchMatch.Tier"/>.
/// </summary>
internal readonly record struct FavoriteMatch(Favorite Favorite, string? DescriptionWord, int Tier);

/// <summary>
/// How a query word stands in a text, best first (<see cref="FileSearch.LevelOf"/>). See
/// workfiles/20261105-whole-word-search-ranking.md.
/// </summary>
internal enum WordLevel
{
    /// <summary>As a whole word: <c>tique</c> in <c>Tique bulle</c>.</summary>
    Whole,

    /// <summary>At the start of a longer word: <c>tique</c> in <c>Tiques</c>.</summary>
    Start,

    /// <summary>Inside a longer word: <c>tique</c> in <c>Soviétique</c>.</summary>
    Inside,
}

/// <summary>What tells a file changed since its content text was extracted: its size and last write, in UTC.</summary>
internal readonly record struct FileStamp(long Size, DateTime Written);
