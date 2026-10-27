using System.Collections.Concurrent;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The faces the face pass found in the indexed images and PDFs, cached in <see cref="FileName"/> in the
/// indexing folder — a file of its own, never a slot of the content texts: the embeddings would weigh on
/// a text every search reads. Binary: a header, a version, the base folder, then per file its relative
/// path, the size and last write (UTC ticks) it was analysed at, and its faces — box in fractions of the
/// image, score, embedding. A file analysed without a face is kept with none, so it is not analysed again
/// until it changes; a file absent is not analysed yet. Read on the UI thread while the pass writes it
/// from a worker. See workfiles/20261027-face-detection.md § Face Index.
/// </summary>
internal sealed class FaceIndex
{
    public const string FileName = "files.faces";

    private const string Magic = "ImageGridFusion faces";
    private const int Version = 1;

    private readonly ConcurrentDictionary<string, FaceFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saving = new();
    private int _changes;

    public FaceIndex(string baseFolder)
    {
        this.BaseFolder = FileIndex.NormalizeFolder(baseFolder);
    }

    /// <summary>The folder the faces belong to, normalized by <see cref="FileIndex.NormalizeFolder"/>.</summary>
    public string BaseFolder { get; }

    public static string DefaultPath => Path.Combine(FileIndex.Folder, FileName);

    /// <summary>A number that changes with every change: the grouping is computed again when it moved.</summary>
    public int Changes => Volatile.Read(ref this._changes);

    /// <summary>
    /// Reads the cached faces of <paramref name="baseFolder"/>; empty when there are none, they cannot be
    /// read, or they belong to another folder or an unknown version.
    /// </summary>
    public static FaceIndex Load(string path, string baseFolder)
    {
        var faces = new FaceIndex(baseFolder);
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            if (reader.ReadString() != Magic || reader.ReadInt32() != Version
                || !string.Equals(Path.TrimEndingDirectorySeparator(reader.ReadString()), faces.BaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                return faces;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string relative = reader.ReadString();
                long size = reader.ReadInt64();
                long ticks = reader.ReadInt64();
                var boxes = new FaceBox[reader.ReadInt32()];
                for (int f = 0; f < boxes.Length; f++)
                {
                    var bounds = new RectangleF(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float score = reader.ReadSingle();
                    var embedding = new float[reader.ReadInt32()];
                    for (int v = 0; v < embedding.Length; v++)
                    {
                        embedding[v] = reader.ReadSingle();
                    }

                    boxes[f] = new FaceBox(bounds, score, embedding);
                }

                if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                {
                    faces._files[relative] = new FaceFile(new FileStamp(size, new DateTime(ticks, DateTimeKind.Utc)), boxes);
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is EndOfStreamException or FormatException or OverflowException or ArgumentException)
        {
            // What was read before the damage is kept: those files are not analysed again.
        }

        return faces;
    }

    /// <summary>How many files the index holds, with or without a face.</summary>
    public int Count => this._files.Count;

    /// <summary>Whether the pass analysed the file at the stamp the scan found it with. False for an entry without a stamp.</summary>
    public bool IsCurrent(IndexEntry entry) =>
        entry.Stamp is { } stamp && this._files.TryGetValue(entry.RelativePath, out var file) && file.Stamp == stamp;

    /// <summary>The faces of a file, none when it has none or was not analysed.</summary>
    public IReadOnlyList<FaceBox> FacesOf(string relativePath) => this._files.TryGetValue(relativePath, out var file) ? file.Faces : [];

    /// <summary>Every file with at least one face, and its faces.</summary>
    public IEnumerable<(string RelativePath, FaceBox[] Faces)> WithFaces() =>
        this._files.Where(pair => pair.Value.Faces.Length > 0).Select(pair => (pair.Key, pair.Value.Faces));

    /// <summary>Records the faces found in a file at its stamp, none included.</summary>
    public void Set(string relativePath, FileStamp stamp, FaceBox[] faces)
    {
        this._files[relativePath] = new FaceFile(stamp, faces);
        Interlocked.Increment(ref this._changes);
    }

    public void Remove(string relativePath)
    {
        if (this._files.TryRemove(relativePath, out _))
        {
            Interlocked.Increment(ref this._changes);
        }
    }

    /// <summary>Drops the faces of the files no longer in the index; true when any was dropped.</summary>
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

        if (dropped)
        {
            Interlocked.Increment(ref this._changes);
        }

        return dropped;
    }

    public void Clear()
    {
        this._files.Clear();
        Interlocked.Increment(ref this._changes);
    }

    /// <summary>
    /// Writes the faces to a temp file, moved over the previous one: a crash keeps that one. Safe from
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
                writer.Write(files.Length);
                foreach (var (relative, file) in files)
                {
                    writer.Write(relative);
                    writer.Write(file.Stamp.Size);
                    writer.Write(file.Stamp.Written.Ticks);
                    writer.Write(file.Faces.Length);
                    foreach (var face in file.Faces)
                    {
                        writer.Write(face.Bounds.X);
                        writer.Write(face.Bounds.Y);
                        writer.Write(face.Bounds.Width);
                        writer.Write(face.Bounds.Height);
                        writer.Write(face.Score);
                        writer.Write(face.Embedding.Length);
                        foreach (float value in face.Embedding)
                        {
                            writer.Write(value);
                        }
                    }
                }
            }

            File.Move(temp, path, overwrite: true);
        }
    }

    /// <summary>Deletes the cached faces' file; a missing one is no error. Throws like <see cref="File.Delete"/>.</summary>
    public static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record FaceFile(FileStamp Stamp, FaceBox[] Faces);
}
