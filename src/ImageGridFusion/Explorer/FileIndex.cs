using System.Globalization;
using System.IO.Enumeration;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The file explorer's index: every file under the base folder and its subfolders, cached in a text
/// file of the <see cref="FolderName"/> folder next to the exe so the search never reads the disk.
/// Three header lines — a version, the base folder, the scan's time — then one line per file: its
/// relative path and, after a tab, its creation time (UTC, ISO 8601); further tab-separated columns
/// are tolerated. See workfiles/20260926-file-explorer.md § Index File,
/// workfiles/20260927-file-explorer-show-all.md § Index File and workfiles/20260926-ocr-search.md
/// § Index Folder.
/// </summary>
internal sealed class FileIndex
{
    public const string FileName = "files.index";

    /// <summary>The folder next to the exe holding every indexing file: this index and the content texts.</summary>
    public const string FolderName = "Index";
    private const string Header = "ImageGridFusion index 2";
    private const int ProgressInterval = 100;

    private readonly List<IndexEntry> _entries;

    private FileIndex(string baseFolder, DateTime scannedAt, List<IndexEntry> entries)
    {
        BaseFolder = baseFolder;
        ScannedAt = scannedAt;
        _entries = entries;
    }

    /// <summary>The folder scanned, normalized by <see cref="NormalizeFolder"/>.</summary>
    public string BaseFolder { get; }

    public DateTime ScannedAt { get; }

    public IReadOnlyList<IndexEntry> Entries => _entries;

    public int Count => _entries.Count;

    /// <summary>The indexing folder of this exe: <see cref="FolderName"/>, next to it.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Where the index of this exe lives: in its indexing folder.</summary>
    public static string DefaultPath => Path.Combine(Folder, FileName);

    /// <summary>
    /// Moves an index left next to the exe by an older version into the indexing folder, as it is, so
    /// the upgrade does not rescan from nothing; nothing happens when the folder already holds one.
    /// Best effort: a failure leaves it where it is, and the scan writes a new one.
    /// </summary>
    public static void MoveLegacy()
    {
        string legacy = Path.Combine(AppContext.BaseDirectory, FileName);
        try
        {
            if (File.Exists(legacy) && !File.Exists(DefaultPath))
            {
                Directory.CreateDirectory(Folder);
                File.Move(legacy, DefaultPath);
            }
        }
        catch (Exception ex) when (IsFileError(ex))
        {
        }
    }

    /// <summary>A base folder as compared and stored: its full path, without a trailing separator.</summary>
    public static string NormalizeFolder(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    public string FullPath(IndexEntry entry) => Path.Combine(BaseFolder, entry.RelativePath);

    /// <summary>
    /// Reads the cached index of <paramref name="baseFolder"/>; null when there is none, it cannot be
    /// read, it was scanned for another folder, or by an older version (without the dates) — the scan
    /// run at every start writes it again.
    /// </summary>
    public static FileIndex? Load(string path, string baseFolder)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return null;
        }

        string root = NormalizeFolder(baseFolder);
        if (lines.Length < 3 || lines[0] != Header || !SameFolder(lines[1], root)
            || !DateTime.TryParse(lines[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scannedAt))
        {
            return null;
        }

        var entries = new List<IndexEntry>(lines.Length - 3);
        for (int i = 3; i < lines.Length; i++)
        {
            string[] columns = lines[i].Split('\t');
            if (columns[0].Length > 0)
            {
                var created = columns.Length > 1
                    && DateTime.TryParse(columns[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                    ? date
                    : DateTime.MinValue;
                entries.Add(new IndexEntry(columns[0], created));
            }
        }

        return new FileIndex(root, scannedAt, entries);
    }

    /// <summary>Writes the index to a temp file, moved over the previous one: a crash keeps that one.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.WriteLine(Header);
            writer.WriteLine(BaseFolder);
            writer.WriteLine(ScannedAt.ToString("o", CultureInfo.InvariantCulture));
            foreach (var entry in _entries)
            {
                writer.Write(entry.RelativePath);
                writer.Write('\t');
                writer.WriteLine(entry.Created.ToString("o", CultureInfo.InvariantCulture));
            }
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Drops the entry of <paramref name="fullPath"/>; false when it has none.</summary>
    public bool Remove(string fullPath)
    {
        string relative = Path.GetRelativePath(BaseFolder, fullPath);
        int at = _entries.FindIndex(e => string.Equals(e.RelativePath, relative, StringComparison.OrdinalIgnoreCase));
        if (at < 0)
        {
            return false;
        }

        _entries.RemoveAt(at);
        return true;
    }

    /// <summary>
    /// Scans <paramref name="baseFolder"/> and its subfolders, on the calling thread: a first pass counts
    /// the files, so the second, which records them, reports an exact ratio. Hidden and system entries
    /// are skipped with their content, inaccessible folders too. Each file's creation time, size and
    /// last write come with the enumeration, without another disk access — the last two kept in
    /// memory only, for the content texts. Throws like the enumeration does when the folder
    /// itself cannot be read.
    /// </summary>
    public static FileIndex Scan(string baseFolder, IProgress<ScanProgress>? progress, CancellationToken cancellation)
    {
        string root = NormalizeFolder(baseFolder);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        long reported = long.MinValue;
        void Report(ScanPhase phase, int done, int total, bool force = false)
        {
            long now = Environment.TickCount64;
            if (force || now - reported >= ProgressInterval)
            {
                reported = now;
                progress?.Report(new ScanProgress(phase, done, total));
            }
        }

        int count = 0;
        foreach (var _ in Directory.EnumerateFiles(root, "*", options))
        {
            cancellation.ThrowIfCancellationRequested();
            count++;
            Report(ScanPhase.Counting, count, 0);
        }

        // The enumeration prefixes each path with the root as given; a drive root already ends with its separator.
        int prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root.Length : root.Length + 1;
        var entries = new List<IndexEntry>(count);
        var files = new FileSystemEnumerable<(string Path, DateTime Created, long Size, DateTime Written)>(
            root,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.CreationTimeUtc.UtcDateTime, entry.Length, entry.LastWriteTimeUtc.UtcDateTime),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
        };
        foreach (var (file, created, size, written) in files)
        {
            cancellation.ThrowIfCancellationRequested();
            string relative = file.Length > prefix && file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? file[prefix..]
                : Path.GetRelativePath(root, file);
            entries.Add(new IndexEntry(relative, created, new FileStamp(size, written)));
            Report(ScanPhase.Indexing, entries.Count, count, force: entries.Count == count);
        }

        return new FileIndex(root, DateTime.Now, entries);
    }

    public static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private static bool SameFolder(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(a), b, StringComparison.OrdinalIgnoreCase);
}

internal enum ScanPhase
{
    Counting,
    Indexing,
}

/// <summary>Where a scan stands: the files counted so far, or the files recorded out of the count.</summary>
internal readonly record struct ScanProgress(ScanPhase Phase, int Done, int Total);
