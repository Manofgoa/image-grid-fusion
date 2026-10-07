using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The content texts of the indexed files — the words recognised in an image or a PDF's first page,
/// the text of a text or HTML file — cached in <see cref="FileName"/> in the indexing folder, so the
/// search never reads the disk. A version line, the base folder, then one line per file: its relative
/// path, the size and last write (UTC ticks) it was extracted at, and its text, each after a tab. A
/// file with no text is kept with an empty one, so it is not extracted again until it changes. Read
/// by the search on the UI thread while the extraction writes it from a worker. See
/// workfiles/20260926-ocr-search.md § `files.content`.
/// </summary>
internal sealed class ContentIndex
{
    public const string FileName = "files.content";

    /// <summary>The longest text kept per file, in characters: its beginning.</summary>
    public const int MaxLength = 32 * 1024;

    private const string Header = "ImageGridFusion content 1";

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
            if (reader.ReadLine() != Header || reader.ReadLine() is not { } folder
                || !string.Equals(Path.TrimEndingDirectorySeparator(folder), contents.BaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                return contents;
            }

            while (reader.ReadLine() is { } line)
            {
                string[] columns = line.Split('\t', 4);
                if (columns.Length == 4 && columns[0].Length > 0
                    && long.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size)
                    && long.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                {
                    contents._texts[columns[0]] = new ContentText(new FileStamp(size, new DateTime(ticks, DateTimeKind.Utc)), columns[3]);
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

    /// <summary>Whether the file's text was extracted at the stamp the scan found it with; false for an entry without one.</summary>
    public bool IsCurrent(IndexEntry entry) =>
        entry.Stamp is { } stamp && this._texts.TryGetValue(entry.RelativePath, out var text) && text.Stamp == stamp;

    /// <summary>
    /// Records a file's text at its stamp: white space collapsed into single spaces, then cut to
    /// <see cref="MaxLength"/>; "" for a file with none.
    /// </summary>
    public void Set(string relativePath, FileStamp stamp, string text) => this._texts[relativePath] = new ContentText(stamp, Clean(text));

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

    /// <summary>A file's text, the stamp it was extracted at, and its folded form, computed once.</summary>
    private sealed class ContentText(FileStamp stamp, string text)
    {
        public FileStamp Stamp { get; } = stamp;

        public string Text { get; } = text;

        public string Folded { get; } = FileSearch.Fold(text);
    }
}
