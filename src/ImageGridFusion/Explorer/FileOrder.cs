namespace ImageGridFusion.Explorer;

/// <summary>
/// How the file explorer orders its files, chosen in its sort drop-down: inside each relevance tier of
/// a search, alone for <c>*</c> and a browsed folder; the folders and the favorites keep their own
/// order. Remembered between sessions. See workfiles/20261009-search-results-sort.md § Sort Orders.
/// </summary>
internal enum FileOrder
{
    /// <summary>The most recently created first — the order before the drop-down existed.</summary>
    Newest,

    /// <summary>A→Z on the file name.</summary>
    Name,

    /// <summary>The smallest first, a size not known yet last.</summary>
    Smallest,
}

internal static class FileOrders
{
    /// <summary>
    /// Two files in <paramref name="order"/>; ties broken by the name (A→Z), then the relative path.
    /// </summary>
    public static int Compare(FileOrder order, IndexEntry a, IndexEntry b)
    {
        int result = order switch
        {
            FileOrder.Name => 0,
            FileOrder.Smallest => CompareSizes(a.Size, b.Size),
            _ => b.Created.CompareTo(a.Created),
        };
        if (result == 0)
        {
            result = FolderListing.NameOrder.Compare(a.Name, b.Name);
        }

        return result != 0 ? result : string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The smaller first; an unknown size after every known one.</summary>
    private static int CompareSizes(long? a, long? b) => (a, b) switch
    {
        ({ } left, { } right) => left.CompareTo(right),
        (null, null) => 0,
        (null, _) => 1,
        _ => -1,
    };
}
