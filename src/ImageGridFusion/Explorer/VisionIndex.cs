using System.Collections.Concurrent;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The images' embeddings the Vision pass computed, cached in <see cref="FileName"/> in the indexing
/// folder — a file of its own, never a slot of the content texts, as the faces'. Binary: a header, a
/// version, the base folder, the model the embeddings come from — another one empties it — the key of the
/// vocabulary the tags were last computed with, then per file its relative path, the size and last write
/// (UTC ticks) it was analysed at, and its embedding — none for a file that could not be decoded, so it is
/// not analysed again until it changes; a file absent is not analysed yet. Read on the UI thread while the
/// pass writes it from a worker. See workfiles/20261027-image-content-analysis.md § Vision Index.
/// </summary>
internal sealed class VisionIndex
{
    public const string FileName = "files.vision";

    private const string Magic = "ImageGridFusion vision";
    private const int Version = 1;

    private readonly ConcurrentDictionary<string, VisionFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saving = new();

    public VisionIndex(string baseFolder)
    {
        this.BaseFolder = FileIndex.NormalizeFolder(baseFolder);
    }

    /// <summary>The folder the embeddings belong to, normalized by <see cref="FileIndex.NormalizeFolder"/>.</summary>
    public string BaseFolder { get; }

    /// <summary>The key of the vocabulary the stored tags were computed with (<see cref="VisionTags.Key"/>); "" before any.</summary>
    public string TagsKey { get; set; } = "";

    public static string DefaultPath => Path.Combine(FileIndex.Folder, FileName);

    /// <summary>
    /// Reads the cached embeddings of <paramref name="baseFolder"/>; empty when there are none, they cannot
    /// be read, or they belong to another folder, another model or an unknown version.
    /// </summary>
    public static VisionIndex Load(string path, string baseFolder)
    {
        var index = new VisionIndex(baseFolder);
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            if (reader.ReadString() != Magic || reader.ReadInt32() != Version
                || !string.Equals(Path.TrimEndingDirectorySeparator(reader.ReadString()), index.BaseFolder, StringComparison.OrdinalIgnoreCase)
                || reader.ReadString() != VisionEngineFiles.EmbeddingModelId)
            {
                return index;
            }

            index.TagsKey = reader.ReadString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string relative = reader.ReadString();
                long size = reader.ReadInt64();
                long ticks = reader.ReadInt64();
                var embedding = new float[reader.ReadInt32()];
                for (int v = 0; v < embedding.Length; v++)
                {
                    embedding[v] = reader.ReadSingle();
                }

                if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                {
                    index._files[relative] = new VisionFile(new FileStamp(size, new DateTime(ticks, DateTimeKind.Utc)), embedding);
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is EndOfStreamException or FormatException or OverflowException or ArgumentException)
        {
            // What was read before the damage is kept: those files are not analysed again.
        }

        return index;
    }

    /// <summary>How many files the index holds, with or without an embedding.</summary>
    public int Count => this._files.Count;

    /// <summary>Whether the pass analysed the file at the stamp the scan found it with. False for an entry without a stamp.</summary>
    public bool IsCurrent(IndexEntry entry) =>
        entry.Stamp is { } stamp && this._files.TryGetValue(entry.RelativePath, out var file) && file.Stamp == stamp;

    /// <summary>A file's embedding; null when it has none or was not analysed.</summary>
    public float[]? EmbeddingOf(string relativePath) =>
        this._files.TryGetValue(relativePath, out var file) && file.Embedding.Length > 0 ? file.Embedding : null;

    /// <summary>Every file with an embedding, its stamp and its embedding.</summary>
    public IEnumerable<(string RelativePath, FileStamp Stamp, float[] Embedding)> WithEmbeddings() =>
        this._files.Where(pair => pair.Value.Embedding.Length > 0).Select(pair => (pair.Key, pair.Value.Stamp, pair.Value.Embedding));

    /// <summary>Records a file's embedding at its stamp — empty for a file that could not be read.</summary>
    public void Set(string relativePath, FileStamp stamp, float[] embedding) => this._files[relativePath] = new VisionFile(stamp, embedding);

    /// <summary>Drops the embeddings of the files no longer in the index; true when any was dropped.</summary>
    public bool RetainOnly(IReadOnlyList<IndexEntry> entries)
    {
        var kept = new HashSet<string>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            kept.Add(entry.RelativePath);
        }

        bool dropped = false;
        foreach (string path in this._files.Keys)
        {
            if (!kept.Contains(path))
            {
                dropped |= this._files.TryRemove(path, out _);
            }
        }

        return dropped;
    }

    public void Clear()
    {
        this._files.Clear();
        this.TagsKey = "";
    }

    /// <summary>
    /// Writes the embeddings to a temp file, moved over the previous one: a crash keeps that one. Safe from
    /// any thread, one save at a time. Throws like <see cref="FileIndex.Save"/>.
    /// </summary>
    public void Save(string path)
    {
        lock (this._saving)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            var files = this._files.ToArray();
            using (var writer = new BinaryWriter(File.Create(temp), Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(this.BaseFolder);
                writer.Write(VisionEngineFiles.EmbeddingModelId);
                writer.Write(this.TagsKey);
                writer.Write(files.Length);
                foreach (var (relative, file) in files)
                {
                    writer.Write(relative);
                    writer.Write(file.Stamp.Size);
                    writer.Write(file.Stamp.Written.Ticks);
                    writer.Write(file.Embedding.Length);
                    foreach (float value in file.Embedding)
                    {
                        writer.Write(value);
                    }
                }
            }

            File.Move(temp, path, overwrite: true);
        }
    }

    /// <summary>Deletes a cached file; a missing one is no error. Throws like <see cref="File.Delete"/>.</summary>
    public static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record VisionFile(FileStamp Stamp, float[] Embedding);
}
