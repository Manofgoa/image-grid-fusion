using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Files dropped onto the Global toolbar (workfiles/20261107-soundtrack-drop-from-url-zone.md): every control of its
/// two rows a drop target, the effect under the cursor deciding at each move — the Soundtrack, the Layers or the Grid
/// background, its tab or its options while selected — the forbidden cursor elsewhere and while exporting.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// Makes <paramref name="control"/> and every control it holds, now or later, a drop target of the Global toolbar.
    /// A text field is left to its parent, which takes its drops, so its own text editing is untouched.
    /// </summary>
    private void WireGlobalDrops(Control control)
    {
        if (control is TextBoxBase)
        {
            return;
        }

        control.AllowDrop = true;
        control.DragEnter += this.OnGlobalDragOver;
        control.DragOver += this.OnGlobalDragOver;
        control.DragDrop += this.OnGlobalDragDrop;
        control.ControlAdded += (_, e) =>
        {
            if (e.Control is { } added)
            {
                this.WireGlobalDrops(added);
            }
        };
        foreach (Control child in control.Controls)
        {
            this.WireGlobalDrops(child);
        }
    }

    /// <summary>Copy where the drop would act, the forbidden cursor elsewhere — decided at each move.</summary>
    private void OnGlobalDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DroppedPaths(e) is { } paths && GlobalDrop.Accepts(this.GlobalDropTargetAt(new Point(e.X, e.Y)), paths)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnGlobalDragDrop(object? sender, DragEventArgs e)
    {
        if (DroppedPaths(e) is not { } paths)
        {
            return;
        }

        var target = this.GlobalDropTargetAt(new Point(e.X, e.Y));
        if (!GlobalDrop.Accepts(target, paths))
        {
            return;
        }

        switch (target)
        {
            case GlobalDropTarget.Soundtrack:
                // The first file that may carry sound; one Windows reads none from is refused with a status message.
                this.SelectGlobalEffect(GlobalEffect.Soundtrack);
                await this.LoadSoundtrackAsync(GlobalDrop.SoundtrackFileOf(paths)!);
                break;
            case GlobalDropTarget.Layers:
                // Laid over the grid (workfiles/20261029-layers.md).
                await this.DropLayerFilesAsync(paths);
                break;
            case GlobalDropTarget.GridBackground:
                // Its image (workfiles/20261104-grid-background-image.md § Drop and Browse).
                this.SelectGlobalEffect(GlobalEffect.GridBackground);
                await this.SetBackgroundImageFromFilesAsync(paths, FileExplorerPanel.PresetLookOf(e.Data));
                break;
        }
    }

    /// <summary>What a drop at <paramref name="screen"/> goes to: the tab under it, else the options of the selected tab.</summary>
    private GlobalDropTarget GlobalDropTargetAt(Point screen)
    {
        var onTabs = this._globalTabs.PointToClient(screen);
        GlobalEffect? tab = this._globalTabs.ClientRectangle.Contains(onTabs) ? this._globalTabs.EffectAt(onTabs) : null;
        bool overOptions = this._globalOptionsHost.RectangleToScreen(this._globalOptionsHost.ClientRectangle).Contains(screen);
        return GlobalDrop.TargetOf(tab, overOptions, this._selectedGlobalEffect, this.IsExporting);
    }

    private static string[]? DroppedPaths(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths ? paths : null;
}
