using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>What a file dropped onto the Global toolbar goes to (workfiles/20261107-soundtrack-drop-from-url-zone.md).</summary>
internal enum GlobalDropTarget
{
    /// <summary>Refused: the forbidden cursor, nothing done.</summary>
    None,
    Soundtrack,
    Layers,
    GridBackground,
}

/// <summary>
/// One decision for every drop onto the Global toolbar (see RULES.md § Global Toolbar): the effect under the
/// cursor — its tab, or the options while it is selected — when it takes files, nothing elsewhere or while exporting.
/// </summary>
internal static class GlobalDrop
{
    /// <summary>
    /// The target of a drop over the tab <paramref name="tab"/> (<c>null</c> off the tabs), or over the options
    /// (<paramref name="overOptions"/>) while <paramref name="selected"/> is the selected tab.
    /// </summary>
    public static GlobalDropTarget TargetOf(GlobalEffect? tab, bool overOptions, GlobalEffect? selected, bool exporting)
    {
        if (exporting)
        {
            return GlobalDropTarget.None;
        }

        if (tab is { } under)
        {
            return Taking(under);
        }

        return overOptions && selected is { } shown ? Taking(shown) : GlobalDropTarget.None;
    }

    /// <summary>The file of <paramref name="paths"/> the Soundtrack loads: the first that may carry sound, <c>null</c> for none.</summary>
    public static string? SoundtrackFileOf(IEnumerable<string> paths) => paths.FirstOrDefault(SoundtrackFile.MayCarrySound);

    /// <summary>Whether a drop of <paramref name="paths"/> onto <paramref name="target"/> is accepted at the hover.</summary>
    public static bool Accepts(GlobalDropTarget target, IReadOnlyCollection<string> paths) => target switch
    {
        GlobalDropTarget.None => false,
        GlobalDropTarget.Soundtrack => SoundtrackFileOf(paths) is not null,
        _ => paths.Count > 0,
    };

    private static GlobalDropTarget Taking(GlobalEffect effect) => effect switch
    {
        GlobalEffect.Soundtrack => GlobalDropTarget.Soundtrack,
        GlobalEffect.Layers => GlobalDropTarget.Layers,
        GlobalEffect.GridBackground => GlobalDropTarget.GridBackground,
        _ => GlobalDropTarget.None,
    };
}
