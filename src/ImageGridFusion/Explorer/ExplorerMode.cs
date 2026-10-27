namespace ImageGridFusion.Explorer;

/// <summary>
/// What the file explorer lists, picked in its views drop-down: the favorites and the search over every
/// file, the base folder folder by folder, or the people its faces were grouped into. Saved by name
/// (<c>AppSettings.ExplorerMode</c>). See workfiles/20261027-face-detection.md § People View.
/// </summary>
internal enum ExplorerMode
{
    Favorites,
    Folders,
    People,
}
