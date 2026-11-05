namespace ImageGridFusion.Explorer;

/// <summary>
/// What the file explorer lists, picked in its views drop-down — its entries in this order, Ctrl+F cycling
/// them: the base folder folder by folder, the people its faces were grouped into, the favorites and the
/// search over every file, or GIPHY, the first online library. Saved by name (<c>AppSettings.ExplorerMode</c>),
/// so the order can change. See workfiles/20261027-face-detection.md § People View,
/// workfiles/20261029-ctrl-f-view-cycle.md, workfiles/20261105-online-library-giphy.md.
/// </summary>
internal enum ExplorerMode
{
    Folders,
    People,
    Favorites,
    Giphy,
}
