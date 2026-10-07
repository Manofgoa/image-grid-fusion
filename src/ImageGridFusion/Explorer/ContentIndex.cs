using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The content texts of the indexed files — the words recognised in an image or a PDF's first page,
/// the text of a text or HTML file — cached in <see cref="FileName"/> in the indexing folder, so the
/// search never reads the disk. A version line, the base folder, then one line per file: its relative
/// path, the size and last write (UTC ticks) it was extracted at, the boxes of the words the OCR
/// recognised — <c>-</c> for a file read as text — and its text, each after a tab. A file with no
/// text is kept with an empty one, so it is not extracted again until it changes. A version-1 file,
/// without the boxes, is read too: its recognised files are then stale, to be recognised again. Read
/// by the search on the UI thread while the extraction writes it from a worker. See
/// workfiles/20260926-ocr-search.md § `files.content` and § Arrow to the Word.
/// </summary>
internal sealed class ContentIndex
{
    public const string FileName = "files.content";

    /// <summary>The longest text kept per file, in characters: its beginning.</summary>
    public const int MaxLength = 32 * 1024;

    /// <summary>The most word boxes kept per file: the first ones recognised.</summary>
    public const int MaxBoxes = 4000;

    private const string Header = "ImageGridFusion content 2";
    private const string HeaderWithoutBoxes = "ImageGridFusion content 1";

    // A box: x, y, width, height in fractions of the image, then the word; boxes joined by the unit separator.
    private const char BoxSeparator = '\u001F';
    private const string NoBoxes = "-";

    private readonly ConcurrentDictionary<string, ContentText> _texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saving = new();

    public ContentIndex(string baseFolder)
    {
        this.BaseFolder = FileIndex.NormalizeFolder(baseFolder);
    }

    /// <summary>The folder the texts belong to, normalized by <see cref="FileIndex.NormalizeFolder"/>.</summary>
    public string BaseFolder { get; }

    public static string DefaultPath => Path.Combine(FileIndex.Folder, FileName);

    /// <summary>
    /// Reads the cached texts of <paramref name="baseFolder"/>; empty when there are none, they cannot
    /// be read, or they belong to another folder or another version.
    /// </summary>
    public static ContentIndex Load(string path, string baseFolder)
    {
        var contents = new ContentIndex(baseFolder);
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            string? header = reader.ReadLine();
            bool withBoxes = header == Header;
            if ((!withBoxes && header != HeaderWithoutBoxes) || reader.ReadLine() is not { } folder
                || !string.Equals(Path.TrimEndingDirectorySeparator(folder), contents.BaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                return contents;
            }

            int count = withBoxes ? 5 : 4;
            while (reader.ReadLine() is { } line)
            {
                string[] columns = line.Split('\t', count);
                if (columns.Length == count && columns[0].Length > 0
                    && long.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size)
                    && long.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                {
                    var boxes = withBoxes ? ParseBoxes(columns[3]) : null;
                    contents._texts[columns[0]] = new ContentText(new FileStamp(size, new DateTime(ticks, DateTimeKind.Utc)), columns[^1], boxes);
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }

        return contents;
    }

    public int Count => this._texts.Count;

    /// <summary>The folded text of a file, for the search; null when it has none yet.</summary>
    public string? FoldedOf(string relativePath) => this._texts.TryGetValue(relativePath, out var text) ? text.Folded : null;

    /// <summary>
    /// The words of a file's text around the first one holding <paramref name="foldedWord"/> — accents
    /// and case ignored — <paramref name="around"/> on each side, an ellipsis where the text goes on;
    /// null when the text does not hold it.
    /// </summary>
    public string? ExcerptOf(string relativePath, string foldedWord, int around)
    {
        if (!this._texts.TryGetValue(relativePath, out var text) || text.Text.Length == 0)
        {
            return null;
        }

        string[] words = text.Text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (FileSearch.Fold(words[i]).Contains(foldedWord, StringComparison.Ordinal))
            {
                int from = Math.Max(0, i - around);
                int to = Math.Min(words.Length - 1, i + around);
                string excerpt = string.Join(' ', words, from, to - from + 1);
                return $"{(from > 0 ? "… " : "")}{excerpt}{(to < words.Length - 1 ? " …" : "")}";
            }
        }

        return null;
    }

    /// <summary>
    /// Where the OCR recognised the first word holding <paramref name="foldedWord"/> — accents and case
    /// ignored — in fractions of the image; null for a file read as text, or a word it did not place.
    /// </summary>
    public RectangleF? SpotOf(string relativePath, string foldedWord)
    {
        if (!this._texts.TryGetValue(relativePath, out var text) || text.Boxes is null)
        {
            return null;
        }

        foreach (var box in text.Boxes)
        {
            if (box.Folded.Contains(foldedWord, StringComparison.Ordinal))
            {
                return box.Bounds;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the file's text was extracted at the stamp the scan found it with — and, for a file the
    /// OCR reads, with the boxes of its words; false for an entry without a stamp.
    /// </summary>
    public bool IsCurrent(IndexEntry entry) =>
        entry.Stamp is { } stamp && this._texts.TryGetValue(entry.RelativePath, out var text) && text.Stamp == stamp
        && (text.Boxes is not null || ContentExtractor.KindOf(entry.RelativePath) is not (ContentKind.Image or ContentKind.Pdf));

    /// <summary>
    /// Records a file's text at its stamp: white space collapsed into single spaces, then cut to
    /// <see cref="MaxLength"/>; "" for a file with none. <paramref name="boxes"/>: where the OCR
    /// recognised its words, the first <see cref="MaxBoxes"/> kept; null for a file read as text.
    /// </summary>
    public void Set(string relativePath, FileStamp stamp, string text, IReadOnlyList<WordBox>? boxes) =>
        this._texts[relativePath] = new ContentText(stamp, Clean(text), boxes is null ? null : boxes.Take(MaxBoxes).ToArray());

    public void Remove(string relativePath) => this._texts.TryRemove(relativePath, out _);

    /// <summary>Drops the texts of the files no longer in the index; true when any was dropped.</summary>
    public bool RetainOnly(IReadOnlyList<IndexEntry> entries)
    {
        var kept = new HashSet<string>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            kept.Add(entry.RelativePath);
        }

        bool dropped = false;
        foreach (string path in this._texts.Keys)
        {
            if (!kept.Contains(path))
            {
                dropped |= this._texts.TryRemove(path, out _);
            }
        }

        return dropped;
    }

    public void Clear() => this._texts.Clear();

    /// <summary>
    /// Writes the texts to a temp file, moved over the previous one: a crash keeps that one. Safe from
    /// any thread, one save at a time. Throws like <see cref="FileIndex.Save"/>.
    /// </summary>
    public void Save(string path)
    {
        lock (this._saving)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.WriteLine(Header);
                writer.WriteLine(this.BaseFolder);
                foreach (var (relativePath, text) in this._texts)
                {
                    writer.Write(relativePath);
                    writer.Write('\t');
                    writer.Write(text.Stamp.Size.ToString(CultureInfo.InvariantCulture));
                    writer.Write('\t');
                    writer.Write(text.Stamp.Written.Ticks.ToString(CultureInfo.InvariantCulture));
                    writer.Write('\t');
                    writer.Write(FormatBoxes(text.Boxes));
                    writer.Write('\t');
                    writer.WriteLine(text.Text);
                }
            }

            File.Move(temp, path, overwrite: true);
        }
    }

    /// <summary>White space — tabs and line breaks included — collapsed into single spaces, trimmed, cut to <see cref="MaxLength"/>.</summary>
    private static string Clean(string text)
    {
        var clean = new StringBuilder(Math.Min(text.Length, MaxLength));
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = clean.Length > 0;
                continue;
            }

            if (space)
            {
                if (clean.Length + 1 >= MaxLength)
                {
                    break;
                }

                clean.Append(' ');
                space = false;
            }

            if (clean.Length >= MaxLength)
            {
                break;
            }

            clean.Append(c);
        }

        return clean.ToString();
    }

    private static string FormatBoxes(WordBox[]? boxes)
    {
        if (boxes is null)
        {
            return NoBoxes;
        }

        var text = new StringBuilder();
        foreach (var box in boxes)
        {
            if (text.Length > 0)
            {
                text.Append(BoxSeparator);
            }

            var b = box.Bounds;
            text.Append(CultureInfo.InvariantCulture, $"{b.X:0.#####};{b.Y:0.#####};{b.Width:0.#####};{b.Height:0.#####};{box.Word}");
        }

        return text.ToString();
    }

    private static WordBox[]? ParseBoxes(string column)
    {
        if (column == NoBoxes)
        {
            return null;
        }

        var boxes = new List<WordBox>();
        foreach (string item in column.Split(BoxSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split(';', 5);
            if (parts.Length == 5
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float width)
                && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
            {
                boxes.Add(new WordBox(parts[4], new RectangleF(x, y, width, height)));
            }
        }

        return boxes.ToArray();
    }

    /// <summary>A file's text, the stamp it was extracted at, its folded form computed once, and its words' boxes.</summary>
    private sealed class ContentText(FileStamp stamp, string text, WordBox[]? boxes)
    {
        public FileStamp Stamp { get; } = stamp;

        public string Text { get; } = text;

        public string Folded { get; } = FileSearch.Fold(text);

        public WordBox[]? Boxes { get; } = boxes;
    }
}

/// <summary>A word the OCR recognised and its box, in fractions of the image as recognised; its folded form computed once.</summary>
internal sealed class WordBox(string word, RectangleF bounds)
{
    public string Word { get; } = word;

    public RectangleF Bounds { get; } = bounds;

    public string Folded { get; } = FileSearch.Fold(word);
}
