using System.ComponentModel;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Upscale effect in the preview: a cell drawn again once its enlargement is computed in the Upscale window, the
/// <i>Not upscaled</i> badge on a cell whose Upscale is on without one, and a GIF's player moved onto its enlarged
/// frames — or back onto the GIF's own when the effect is turned off. See workfiles/20261029-image-upscale.md
/// § Drawn Bitmap.
/// </summary>
internal sealed partial class GridPreview
{
    private UpscaleService? _upscales;

    // The pages each animated content was last seen playing from: a change reopens its player.
    private readonly Dictionary<SourceImage, PageSource?> _playedPages = [];

    /// <summary>What computes the enlargements, in the Upscale window; the preview draws a cell again once its enlargement is ready.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public UpscaleService? Upscales
    {
        get => this._upscales;
        set
        {
            if (this._upscales is { } previous)
            {
                previous.Completed -= this.OnUpscaleCompleted;
            }

            this._upscales = value;
            if (value is not null)
            {
                value.Completed += this.OnUpscaleCompleted;
            }
        }
    }

    /// <summary>
    /// Gives <paramref name="image"/> — a cell's or a layer's, selected or not — a new look: the Upscale window's
    /// setting applied to an image a Copy or a Save waits for, as the toolbars give the selected one theirs.
    /// </summary>
    public void SetLookOf(SourceImage image, ImageLook look)
    {
        int index = this._images.IndexOf(image);
        if (index >= 0)
        {
            this.SetLook(index, look);
            return;
        }

        var layers = this._layers.Items;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].Image == image && layers[i].Look != look)
            {
                this.SetLayers(this._layers.With(i, layers[i] with { Look = look }));
                this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    /// <summary>
    /// The selected cell's image replaced by <paramref name="image"/>, its look as given — the Crop's Keep: the grid neither
    /// starts over nor shows it anew, one step of the undo history; the former image released, the history keeping it
    /// (workfiles/20261029-image-upscale.md § Crop to a New Image).
    /// </summary>
    public void ReplaceSelectedCell(SourceImage image)
    {
        if (this.HasLayerSelection || this._selected < 0 || this._selected >= this._images.Count)
        {
            image.Dispose();
            return;
        }

        this.EndLayerEdit();
        this.Replace(this._selected, image);
        this.OnImagesChanged();
        this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The Upscale's share of a paint, before the Cutout's — which cuts the enlarged image: every animated content's pages followed.</summary>
    private void PaintUpscale()
    {
        var shown = this._images.ToList();
        if (this.ShownLayers is { } layers)
        {
            shown.AddRange(layers.Items.Select(layer => layer.Image));
        }

        foreach (var image in shown)
        {
            this.FollowDrawnPages(image);
        }

        foreach (var gone in this._playedPages.Keys.Where(image => !shown.Contains(image)).ToList())
        {
            this._playedPages.Remove(gone);
        }
    }

    // A content whose pages changed plays and shows them from now on: its player reopened, its page rendered again.
    private void FollowDrawnPages(SourceImage image)
    {
        if (image.Pages is null || image.IsDisposed)
        {
            return;
        }

        var pages = image.DrawnPages;
        if (this._playedPages.TryGetValue(image, out var known) && !ReferenceEquals(known, pages))
        {
            this._player.Reopen(image);
            this._pageLoader.Request(image, image.Page);
        }

        this._playedPages[image] = pages;
    }

    // What the badge of an image says while its Upscale is on without its enlargement; null for none.
    private static string? UpscaleBadge(SourceImage image) => image.UpscalePending ? "⤢ Not upscaled" : null;

    private void OnUpscaleCompleted(object? sender, SourceImage image)
    {
        if (!this.IsDisposed)
        {
            // Its cell drawn again from the enlargement, then the whole preview: the export's canvas may widen, the readouts with it.
            this.FollowDrawnPages(image);
            this.RedrawCell(image);
            this.Invalidate();
        }
    }
}
