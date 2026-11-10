namespace ImageGridFusion.Explorer;

/// <summary>
/// The view a press of Ctrl+F (step 1) or Ctrl+Shift+F (step -1) gives, the search box focused: the folder view
/// when it starts a cycle, whatever the view shown — the folder view itself included —, else the view
/// <c>step</c> away in <see cref="ExplorerMode"/>'s order, round the end, the views not available skipped. See
/// workfiles/20261029-ctrl-f-view-cycle.md, workfiles/20261110-ctrl-f-cycle-starts-at-folders.md.
/// </summary>
internal static class ViewCycle
{
    public static ExplorerMode Next(ExplorerMode current, int step, bool cycling, Func<ExplorerMode, bool> available)
    {
        if (!cycling)
        {
            return ExplorerMode.Folders;
        }

        int count = Enum.GetValues<ExplorerMode>().Length;
        var mode = current;
        for (int i = 0; i < count; i++)
        {
            mode = (ExplorerMode)((((int)mode + step) % count + count) % count);
            if (available(mode))
            {
                return mode;
            }
        }

        return current;
    }
}
