using System.IO.Enumeration;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The folders of the index, derived from its files' paths — the index holds files only: every
/// folder holding a file somewhere below it, and how many files are below each. What the folder view
/// searches and counts from; the open folder itself is listed from the disk (<see cref="FolderListing"/>).
/// See workfiles/20260930-file-explorer-folder-view.md.
/// </summary>
internal sealed class FolderTree
{
    private readonly Dictionary<string, int> _counts;

    private FolderTree(Dictionary<string, int> counts, IReadOnlyList<IndexEntry> folders)
    {
        _counts = counts;
        Folders = folders;
    }

    /// <summary>Every folder holding a file below it, relative to the base folder, the base folder itself aside.</summary>
    public IReadOnlyList<IndexEntry> Folders { get; }

    public static FolderTree Of(IReadOnlyList<IndexEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            string path = entry.RelativePath;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] is '\\' or '/')
                {
                    string folder = path[..i];
                    counts[folder] = counts.GetValueOrDefault(folder) + 1;
                }
            }
        }

        var folders = counts.Keys.Select(folder => new IndexEntry(folder, DateTime.MinValue)).ToArray();
        return new FolderTree(counts, folders);
    }

    /// <summary>The files below a folder, its subfolders' included; the whole index for the base folder ("").</summary>
    public int CountBelow(string relativeFolder, int total) =>
        relativeFolder.Length == 0 ? total : _counts.GetValueOrDefault(relativeFolder);

    /// <summary>Whether a relative path lies below a relative folder — strictly: the folder itself is not below itself. Everything lies below the base folder ("").</summary>
    public static bool IsUnder(string relativePath, string relativeFolder) =>
        relativeFolder.Length == 0
            ? relativePath.Length > 0
            : relativePath.Length > relativeFolder.Length + 1
                && relativePath[relativeFolder.Length] is '\\' or '/'
                && relativePath.StartsWith(relativeFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Two relative folders A→Z, folder by folder, so a folder's subfolders follow it before its next sibling.</summary>
    public static int ComparePaths(string a, string b)
    {
        string[] left = a.Split(['\\', '/']);
        string[] right = b.Split(['\\', '/']);
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int order = FolderListing.NameOrder.Compare(left[i], right[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    /// <summary>The parent of a relative folder; the base folder ("") for a top-level one.</summary>
    public static string Parent(string relativeFolder) => Path.GetDirectoryName(relativeFolder) ?? "";
}

/// <summary>
/// One folder as the disk holds it now: its subfolders and its files, hidden and system entries
/// skipped as the scan skips them, each file's creation time and size from the enumeration.
/// </summary>
internal static class FolderListing
{
    /// <summary>The order of folder names and paths: A→Z, as the user reads them.</summary>
    public static readonly StringComparer NameOrder = StringComparer.CurrentCultureIgnoreCase;

    /// <summary>
    /// The folder <paramref name="relative"/> of <paramref name="root"/>: its folders A→Z, as full paths,
    /// then its files in <paramref name="order"/> — the sort drop-down's — as entries relative to
    /// <paramref name="root"/>, like the index's. Throws like the enumeration does when the folder cannot
    /// be read.
    /// </summary>
    public static (IReadOnlyList<string> Folders, IReadOnlyList<IndexEntry> Files) Read(string root, string relative, FileOrder order)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var folders = new List<string>();
        var files = new List<IndexEntry>();
        var entries = new FileSystemEnumerable<(string Path, bool IsFolder, DateTime Created, long Size)>(
            Path.Combine(root, relative),
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory, entry.CreationTimeUtc.UtcDateTime, entry.Length),
            options);
        foreach (var (path, isFolder, created, size) in entries)
        {
            if (isFolder)
            {
                folders.Add(path);
            }
            else
            {
                files.Add(new IndexEntry(Path.Combine(relative, Path.GetFileName(path)), created, size: size));
            }
        }

        folders.Sort((a, b) => NameOrder.Compare(Path.GetFileName(a), Path.GetFileName(b)));
        files.Sort((a, b) => FileOrders.Compare(order, a, b));
        return (folders, files);
    }
}
