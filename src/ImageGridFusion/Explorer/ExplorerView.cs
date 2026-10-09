namespace ImageGridFusion.Explorer;

/// <summary>
/// How the file explorer shows its lists, chosen by its two view buttons next to the sort drop-down;
/// the same in the search view, the favorites and the folder view. Remembered between sessions. See
/// workfiles/20261009-explorer-details-view.md.
/// </summary>
internal enum ExplorerView
{
    /// <summary>Tiles: as many per row as fit at the tile size, the name below each — the default.</summary>
    Thumbnails,

    /// <summary>
    /// One row per file: its thumbnail, a quarter of the tile size, then its name and size, and the
    /// excerpt of its content a search found it by; a folder's row shows its first files' thumbnails.
    /// </summary>
    Details,
}
