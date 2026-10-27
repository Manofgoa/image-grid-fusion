using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The content texts of the indexed files — the words recognised in an image or a PDF's first page,
/// the text of a text or HTML file — cached in <see cref="FileName"/> in the indexing folder, so the
/// search never reads the disk. A version line, the base folder, then one line per file: its relative
/// path, the size and last write (UTC ticks) it was extracted at, then one slot per source of words
/// (<see cref="ContentSlot"/>) — the boxes of the words recognised, <c>-</c> for a file read as text,
/// <c>?</c> for a pass not done yet, and the text — each after a tab. A file with no text is kept with
/// an empty one, so it is not extracted again until it changes. A version-2 file, with one slot, gives
/// the Windows slot, the later passes to be done; a version-1 file, without the boxes, is read too: its
/// recognised files are then stale, to be recognised again. Read by the search on the UI thread while
/// the extraction writes it from a worker. See workfiles/20260926-ocr-search.md § `files.content` and
/// § Arrow to the Word, workfiles/20261013-complementary-ocr-passes.md § Index Format.
/// </summary>
internal sealed class ContentIndex
{
    public const string FileName = "files.content";

    /// <summary>The longest text kept per slot, in characters: its beginning.</summary>
    public const int MaxLength = 32 * 1024;

    /// <summary>The most word boxes kept per slot: the first ones recognised.</summary>
    public const int MaxBoxes = 4000;

    private const string Header = "ImageGridFusion content 3";
    private const string HeaderOneSlot = "ImageGridFusion content 2";
    private const string HeaderWithoutBoxes = "ImageGridFusion content 1";

    // A box: x, y, width, height in fractions of the image, then the word; boxes joined by the unit separator.
    private const char BoxSeparator = '\u001F';
    private const string NoBoxes = "-";
    private const string NotDone = "?";

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
    /// be read, or they belong to another folder or an unknown version.
    /// </summary>
    public static ContentIndex Load(string path, string baseFolder)
    {
        var contents = new ContentIndex(baseFolder);
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            string? header = reader.ReadLine();
            int slots = header == Header ? ContentPasses.SlotCount : header == HeaderOneSlot ? 1 : header == HeaderWithoutBoxes ? 0 : -1;
            if (slots < 0 || reader.ReadLine() is not { } folder
                || !string.Equals(Path.TrimEndingDirectorySeparator(folder), contents.BaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                return contents;
            }

            // Without the boxes, the text alone stands for the Windows slot.
            int count = slots == 0 ? 4 : 3 + 2 * slots;
            while (reader.ReadLine() is { } line)
            {
                string[] columns = line.Split('\t', count);
                if (columns.Length == count && columns[0].Length > 0
                    && long.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size)
                    && long.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                {
                    var read = new SlotText?[ContentPasses.SlotCount];
                    if (slots == 0)
                    {
                        read[0] = new SlotText(columns[3], null);
                    }

                    for (int i = 0; i < slots; i++)
                    {
                        string boxes = columns[3 + 2 * i];
                        read[i] = boxes == NotDone ? null : SlotText.Read(columns[4 + 2 * i], boxes);
                    }

                    contents._texts[columns[0]] = new ContentText(new FileStamp(size, new DateTime(ticks, DateTimeKind.Utc)), read);
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }

        return contents;
    }

    public int Count => this._texts.Count;

    /// <summary>
    /// How reliably a file's content holds <paramref name="foldedWord"/> — accents and case ignored —
    /// the best way any slot holds it (<see cref="ContentGrade"/>); null when no slot does, or the file
    /// has no text yet.
    /// </summary>
    public ContentGrade? GradeOf(string relativePath, string foldedWord) =>
        this._texts.TryGetValue(relativePath, out var text) && Find(text, foldedWord) is var (_, _, grade) ? grade : null;

    /// <summary>
    /// The words of a file's text around the first one holding <paramref name="foldedWord"/> — accents
    /// and case ignored — <paramref name="around"/> on each side, an ellipsis where the text goes on;
    /// null when the text does not hold it.
    /// </summary>
    public string? ExcerptOf(string relativePath, string foldedWord, int around)
    {
        if (this.WordsAround(relativePath, foldedWord) is not var (words, at))
        {
            return null;
        }

        int from = Math.Max(0, at - around);
        int to = Math.Min(words.Length - 1, at + around);
        string excerpt = string.Join(' ', words, from, to - from + 1);
        return $"{(from > 0 ? "… " : "")}{excerpt}{(to < words.Length - 1 ? " …" : "")}";
    }

    /// <summary>
    /// The text of the slot that holds <paramref name="foldedWord"/> best — accents and case ignored —
    /// as words, with the index of the first one holding it, or of the first one of the run of words
    /// whose letters hold it once the spaces are removed; null when no slot holds it. The Details view
    /// fits as many of them around it as its row holds.
    /// </summary>
    public (string[] Words, int At)? WordsAround(string relativePath, string foldedWord)
    {
        if (!this._texts.TryGetValue(relativePath, out var text) || Find(text, foldedWord) is not var (slot, joined, _))
        {
            return null;
        }

        string[] words = slot.Text.Split(' ');
        if (!joined)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (FileSearch.Fold(words[i]).Contains(foldedWord, StringComparison.Ordinal))
                {
                    return (words, i);
                }
            }
        }

        return JoinedAt(words.Select(FileSearch.Fold).ToArray(), foldedWord) is var (first, _) ? (words, first) : null;
    }

    /// <summary>
    /// Where the OCR recognised <paramref name="foldedWord"/> — accents and case ignored — in fractions
    /// of the image, in the slot that holds it best: the box of the first word holding it, or the union
    /// of the boxes of the run of words whose letters hold it once the spaces are removed; null for a
    /// file read as text, or a word it did not place.
    /// </summary>
    public RectangleF? SpotOf(string relativePath, string foldedWord)
    {
        if (!this._texts.TryGetValue(relativePath, out var text) || Find(text, foldedWord) is not var (slot, joined, _) || slot.Boxes is not { } boxes)
        {
            return null;
        }

        if (!joined)
        {
            foreach (var box in boxes)
            {
                if (box.Folded.Contains(foldedWord, StringComparison.Ordinal))
                {
                    return box.Bounds;
                }
            }
        }

        if (JoinedAt(boxes.Select(b => b.Folded).ToArray(), foldedWord) is not var (from, to))
        {
            return null;
        }

        var bounds = boxes[from].Bounds;
        for (int i = from + 1; i <= to; i++)
        {
            bounds = RectangleF.Union(bounds, boxes[i].Bounds);
        }

        return bounds;
    }

    /// <summary>
    /// Whether the pass read the file at the stamp the scan found it with: for the Windows pass, its
    /// text extracted — with the boxes of its words for a file the OCR reads; for a later pass, its slots
    /// filled, or a file it does not read. False for an entry without a stamp.
    /// </summary>
    public bool IsCurrent(IndexEntry entry, ContentPass pass)
    {
        if (entry.Stamp is not { } stamp)
        {
            return false;
        }

        bool recognised = ContentExtractor.KindOf(entry.RelativePath) is ContentKind.Image or ContentKind.Pdf;
        if (pass != ContentPass.Windows && !recognised)
        {
            return true;
        }

        return this._texts.TryGetValue(entry.RelativePath, out var text) && text.Stamp == stamp
            && text.Slots[(int)ContentPasses.SlotsOf(pass)[0]] is { } slot
            && (slot.HasBoxes || !recognised);
    }

    /// <summary>
    /// Records what a pass extracted from a file at its stamp — one result per slot of the pass
    /// (<see cref="ContentPasses.SlotsOf"/>): white space collapsed into single spaces, then cut to
    /// <see cref="MaxLength"/>, "" for a slot with none; the boxes of its words, the first
    /// <see cref="MaxBoxes"/> kept, null for a file read as text. The Windows pass starts the file over,
    /// the later passes' slots dropped; a later pass is ignored when the file changed since the Windows
    /// pass read it.
    /// </summary>
    public void Set(string relativePath, FileStamp stamp, ContentPass pass, IReadOnlyList<ContentResult> results)
    {
        var slots = ContentPasses.SlotsOf(pass);
        ContentText updated;
        if (pass == ContentPass.Windows)
        {
            updated = new ContentText(stamp, new SlotText?[ContentPasses.SlotCount]);
        }
        else if (!this._texts.TryGetValue(relativePath, out var current) || current.Stamp != stamp)
        {
            return;
        }
        else
        {
            updated = current;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            var result = i < results.Count ? results[i] : new ContentResult("", []);
            updated = updated.With(slots[i], new SlotText(Clean(result.Text), result.Boxes?.Take(MaxBoxes).ToArray()));
        }

        // Swapped whole: the search reads the dictionary from the UI thread.
        this._texts[relativePath] = updated;
    }

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
                    foreach (var slot in text.Slots)
                    {
                        writer.Write('\t');
                        writer.Write(slot is null ? NotDone : slot.BoxesColumn);
                        writer.Write('\t');
                        writer.Write(slot?.Text);
                    }

                    writer.WriteLine();
                }
            }

            File.Move(temp, path, overwrite: true);
        }
    }

    /// <summary>
    /// The slot holding <paramref name="foldedWord"/> best, whether it holds it only once its spaces are
    /// removed, and the grade: a sure slot holding it as a word first, then a sure slot once joined —
    /// for a word of <see cref="FileSearch.JoinedMinLength"/> characters or more, in words the OCR
    /// recognised — then a weak slot either way; each in the slots' order.
    /// </summary>
    private static (SlotText Slot, bool Joined, ContentGrade Grade)? Find(ContentText text, string foldedWord)
    {
        bool canJoin = foldedWord.Length >= FileSearch.JoinedMinLength;
        (SlotText, bool, ContentGrade)? joined = null;
        (SlotText, bool, ContentGrade)? weak = null;
        for (int i = 0; i < text.Slots.Length; i++)
        {
            if (text.Slots[i] is not { } slot)
            {
                continue;
            }

            bool inWords = slot.Folded.Contains(foldedWord, StringComparison.Ordinal);
            // Only the OCR reads letters apart: a text read as text is not joined.
            bool inJoined = !inWords && canJoin && slot.HasBoxes && slot.Joined.Contains(foldedWord, StringComparison.Ordinal);
            if (ContentPasses.IsWeak((ContentSlot)i))
            {
                weak ??= inWords || inJoined ? (slot, inJoined, ContentGrade.Weak) : null;
            }
            else if (inWords)
            {
                return (slot, false, ContentGrade.Sure);
            }
            else if (inJoined)
            {
                joined ??= (slot, true, ContentGrade.Joined);
            }
        }

        return joined ?? weak;
    }

    /// <summary>
    /// The first and last of <paramref name="folded"/> — folded words in order — whose letters, joined
    /// without the spaces, hold <paramref name="foldedWord"/>; null when they do not.
    /// </summary>
    private static (int First, int Last)? JoinedAt(string[] folded, string foldedWord)
    {
        var joined = new StringBuilder();
        var starts = new int[folded.Length];
        for (int i = 0; i < folded.Length; i++)
        {
            starts[i] = joined.Length;
            joined.Append(folded[i]);
        }

        int at = joined.ToString().IndexOf(foldedWord, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        int end = at + foldedWord.Length;
        int first = Array.FindLastIndex(starts, s => s <= at);
        int last = Array.FindLastIndex(starts, s => s < end);
        return (Math.Max(0, first), Math.Max(first, last));
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

    /// <summary>A file's slots — null for a pass not done yet — and the stamp they were extracted at. Immutable: a pass done swaps in a new one.</summary>
    private sealed class ContentText(FileStamp stamp, SlotText?[] slots)
    {
        public FileStamp Stamp { get; } = stamp;

        public SlotText?[] Slots { get; } = slots;

        public ContentText With(ContentSlot slot, SlotText text)
        {
            var slots = (SlotText?[])this.Slots.Clone();
            slots[(int)slot] = text;
            return new ContentText(this.Stamp, slots);
        }
    }

    /// <summary>
    /// One slot's text, its folded form computed once, that form without its spaces computed when first
    /// looked into, and its words' boxes — read from the file, they stay as the file writes them until
    /// the arrow first needs them, and are written back so. See
    /// workfiles/20261013-complementary-ocr-passes.md § Index Format.
    /// </summary>
    private sealed class SlotText
    {
        // The boxes as the file holds them, decoded when first needed; or the boxes recognised.
        private readonly string? _column;
        private WordBox[]? _boxes;
        private string? _joined;

        public SlotText(string text, WordBox[]? boxes)
        {
            this.Text = text;
            this.Folded = FileSearch.Fold(text);
            this._boxes = boxes;
            this.HasBoxes = boxes is not null;
        }

        private SlotText(string text, string column)
        {
            this.Text = text;
            this.Folded = FileSearch.Fold(text);
            this._column = column;
            this.HasBoxes = column != NoBoxes;
        }

        public string Text { get; }

        public string Folded { get; }

        // Computed again at worst by two threads at once, to the same value — as the boxes below.
        public string Joined => this._joined ??= this.Folded.Replace(" ", "", StringComparison.Ordinal);

        /// <summary>Whether an OCR read the slot — its boxes, maybe none — rather than a text read as text.</summary>
        public bool HasBoxes { get; }

        public WordBox[]? Boxes => this.HasBoxes ? this._boxes ??= ParseBoxes(this._column!) : null;

        /// <summary>The boxes as the file writes them.</summary>
        public string BoxesColumn => this._column ?? FormatBoxes(this._boxes);

        /// <summary>A slot as the file holds it: its text, and its boxes column left undecoded.</summary>
        public static SlotText Read(string text, string column) => new(text, column);
    }
}

/// <summary>
/// A word the OCR recognised and its box, in fractions of the image as recognised; its folded form
/// computed when the arrow first needs it — never at load time, for the hundred thousand boxes an index holds.
/// </summary>
internal sealed class WordBox(string word, RectangleF bounds)
{
    private string? _folded;

    public string Word { get; } = word;

    public RectangleF Bounds { get; } = bounds;

    // Computed again at worst by two threads at once, to the same value.
    public string Folded => this._folded ??= FileSearch.Fold(this.Word);
}
