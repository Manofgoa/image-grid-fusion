using System.Security.Cryptography;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// Where an index keeps its files: the base folder's in the indexing folder itself, each secondary
/// source's in a folder of its own below it (<see cref="SecondarySources.IndexFolderOf"/>) — the four
/// files of an index, each read and written at its path here. See workfiles/20261105-secondary-file-sources.md
/// § The Source's Index.
/// </summary>
internal sealed record IndexFiles(string Folder)
{
    /// <summary>The base folder's: the indexing folder next to the exe.</summary>
    public static IndexFiles Main => new(FileIndex.Folder);

    /// <summary>A secondary source's: its own folder below the indexing folder.</summary>
    public static IndexFiles Of(string sourceFolder) => new(SecondarySources.IndexFolderOf(sourceFolder));

    public string Index => Path.Combine(this.Folder, FileIndex.FileName);

    public string Content => Path.Combine(this.Folder, ContentIndex.FileName);

    public string Faces => Path.Combine(this.Folder, FaceIndex.FileName);

    public string Vision => Path.Combine(this.Folder, VisionIndex.FileName);
}

/// <summary>
/// The secondary sources: folders on disk, anywhere, shown as folders of the base folder's root in the
/// folder view, each indexed apart — never overlapping the base folder nor one another, so a file belongs
/// to one index only. See workfiles/20261105-secondary-file-sources.md.
/// </summary>
internal static class SecondarySources
{
    /// <summary>The folder of the indexing folder holding one folder per source (RULES.md's kebab-case folder names).</summary>
    public const string FolderName = "secondary-sources";

    /// <summary>The folder holding every source's index folder.</summary>
    public static string Folder => Path.Combine(FileIndex.Folder, FolderName);

    /// <summary>A source's index folder: a short hash of its full path, case ignored, so a rename of the source is a new one.</summary>
    public static string IndexFolderOf(string sourceFolder) => Path.Combine(Folder, IdOf(sourceFolder));

    /// <summary>The id of a source: the first 16 hex digits of the SHA-256 of its normalized path, in lower case.</summary>
    public static string IdOf(string sourceFolder)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(FileIndex.NormalizeFolder(sourceFolder).ToLowerInvariant()));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or lies below it, both normalized, case ignored.</summary>
    public static bool Holds(string folder, string path)
    {
        string root = Path.TrimEndingDirectorySeparator(folder);
        return path.Length >= root.Length
            && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && (path.Length == root.Length || path[root.Length] is '\\' or '/' || root.EndsWith('\\'));
    }

    /// <summary>Whether two folders overlap: one of them is the other, or holds it.</summary>
    public static bool Overlap(string a, string b) => Holds(a, b) || Holds(b, a);

    /// <summary>
    /// Why <paramref name="candidate"/> cannot be added as a source — it is the base folder or overlaps it,
    /// or overlaps a source already listed — as the reason to say; null when it can.
    /// </summary>
    public static string? Refusal(string candidate, string? baseFolder, IEnumerable<string> sources)
    {
        string folder = FileIndex.NormalizeFolder(candidate);
        if (baseFolder is not null)
        {
            string root = FileIndex.NormalizeFolder(baseFolder);
            if (string.Equals(folder, root, StringComparison.OrdinalIgnoreCase))
            {
                return Resources.Strings.SourcesRefusedBase;
            }

            if (Holds(root, folder))
            {
                return Resources.Strings.SourcesRefusedInsideBase;
            }

            if (Holds(folder, root))
            {
                return Resources.Strings.SourcesRefusedHoldsBase;
            }
        }

        foreach (string source in sources)
        {
            string other = FileIndex.NormalizeFolder(source);
            if (string.Equals(folder, other, StringComparison.OrdinalIgnoreCase))
            {
                return Resources.Strings.SourcesRefusedListed;
            }

            if (Overlap(folder, other))
            {
                return Resources.Texts.Format(Resources.Strings.SourcesRefusedOverlaps, other);
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes a source's index folder — its four files — once it is removed; the source's folder on disk
    /// is never touched. Throws like the deletion does; nothing to delete is no failure.
    /// </summary>
    public static void DeleteIndex(string sourceFolder)
    {
        string folder = IndexFolderOf(sourceFolder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
