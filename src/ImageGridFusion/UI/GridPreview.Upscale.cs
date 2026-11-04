using System.ComponentModel;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// The Upscale effect in the preview: its enlargements asked for at every paint and their cells drawn again once
/// ready, the <i>Upscaling…</i> badge on a cell waiting for one, and a GIF's player moved onto its enlarged frames
/// once they are all there — or back onto the GIF's own when the effect is turned off. See
/// workfiles/20261029-image-upscale.md § Drawn Bitmap.
/// </summary>
internal sealed partial class GridPreview
{
    private UpscaleService? _upscales;

    // The pages each animated content was last seen playing from: a change reopens its player.
    private readonly Dictionary<SourceImage, PageSource?> _playedPages = [];

    /// <summary>What computes the enlargements; the preview asks it for every image drawn as loaded and draws a cell again once its enlargement is ready.</summary>
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
    /// The Upscale's share of a paint, before the Cutout's — which cuts the enlarged image: every image drawn as loaded
    /// asked for its enlargement, every animated content's pages followed.
    /// </summary>
    private void PaintUpscale()
    {
        var shown = this._images.ToList();
        if (this.ShownLayers is { } layers)
        {
            shown.AddRange(layers.Items.Select(layer => layer.Image));
        }

        foreach (var image in shown)
        {
            this._upscales?.Request(image);
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

    // What the badge of an image says while its enlargement is not ready; null for none.
    private string? UpscaleBadge(SourceImage image)
    {
        if (!image.UpscalePending)
        {
            return null;
        }

        if (this._upscales?.IsWorking(image) == true)
        {
            return this._upscales.ProgressOf(image) is { } done
                ? $"⤢ Upscaling… {done * 100:0} %"
                : $"⤢ Upscaling{new string('.', 1 + (int)(Environment.TickCount64 / CutoutBadgeTick % 3))}";
        }

        return UpscaleEngineFiles.IsAvailable ? null : "⤢ Waiting for the upscale engine";
    }

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
