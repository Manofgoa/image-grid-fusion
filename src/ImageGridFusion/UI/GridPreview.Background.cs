using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Grid background's image in the preview (workfiles/20261104-grid-background-image.md): drawn under the
/// cells from one rendered copy, played beside the cells and the layers, and edited in the background edit view —
/// the layer edit view's mechanism, its target the background.
/// </summary>
internal sealed partial class GridPreview
{
    // The image drawn alone at the canvas's size, so a cell drawn again does not render the whole background again.
    private Bitmap? _backgroundImageCache;
    private (BackgroundImage Image, Bitmap Frame, Size Canvas, TimeSpan Time, bool Fast)? _backgroundImageKey;

    /// <summary>
    /// In the background edit view, the image took the look its cell was just given: the window keeps the Grid
    /// background it now has as its own, for the undo history and the exports.
    /// </summary>
    public event EventHandler<GridBackground>? GridBackgroundEdited;

    /// <summary>Whether the edit view shows the Grid background's image rather than a layer.</summary>
    public bool EditingBackground => this._edit is { EditsBackground: true };

    /// <summary>The Grid background's image drawn now: none while the Grid background is off, or an edit view shows.</summary>
    private BackgroundImage? ShownBackgroundImage => this._edit is null ? this._gridBackground?.Image : null;

    /// <summary>The contents the player plays beside the cells: the shown layers', then the Grid background's image.</summary>
    private IReadOnlyList<SourceImage> PlayedBesideCells =>
        this.ShownBackgroundImage is { } background ? [.. this.ShownLayerImages, background.Image] : this.ShownLayerImages;

    /// <summary>Whether <paramref name="image"/> is the Grid background's image drawn now.</summary>
    private bool IsBackgroundImage(SourceImage image) => this.ShownBackgroundImage?.Image == image;

    /// <summary>
    /// The Grid background's image under what <paramref name="g"/> shows of the canvas, from its rendered copy —
    /// drawn again when its frame, its settings, the canvas or the time of its motion change.
    /// </summary>
    private void PaintBackgroundImage(Graphics g, Size canvas, bool fast)
    {
        if (this.ShownBackgroundImage is not { } background || canvas.Width < 1 || canvas.Height < 1)
        {
            return;
        }

        var time = this._player.GridTime;
        var frame = background.ShownFrameAt(time);
        var key = (background, frame.Frame.Bitmap, canvas, background.Look.Motion is null ? TimeSpan.Zero : time, fast);
        if (this._backgroundImageCache is null || this._backgroundImageKey != key)
        {
            this._backgroundImageCache?.Dispose();
            this._backgroundImageCache = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(this._backgroundImageCache))
            {
                Compositor.DrawBackgroundImage(bg, frame, canvas, fast);
            }

            this._backgroundImageKey = key;
        }

        g.DrawImageUnscaled(this._backgroundImageCache, 0, 0);
    }

    /// <summary>The rendered copy let go: the image changed, or left.</summary>
    private void ForgetBackgroundImage()
    {
        this._backgroundImageCache?.Dispose();
        this._backgroundImageCache = null;
        this._backgroundImageKey = null;
    }

    /// <summary>
    /// A new frame of the Grid background's image: the whole canvas drawn again into the cached preview — every
    /// cell lies over it —, fast while the grid runs.
    /// </summary>
    private void RedrawOverBackground()
    {
        var canvas = this.CanvasBounds();
        if (this._cache is null || this._cache.Size != canvas.Size)
        {
            this.Invalidate();
            return;
        }

        bool fast = this._player.Running || this._eyedropper is not null;
        using (var g = Graphics.FromImage(this._cache))
        {
            g.Clear(Color.Transparent);
            Compositor.Draw(g, this._images, this._layout, canvas.Size, this._borders, this._player.GridTime, this._seams, fast, this.ShownLayers, this._gridBackground);
        }

        this._borders?.CutCorners(this._cache);
        this.Invalidate(canvas);
    }

    /// <summary>The frames of the Grid background's image are decoded at the canvas's size.</summary>
    private void UpdateBackgroundDisplaySize()
    {
        var canvas = this.CanvasBounds();
        if (this.ShownBackgroundImage is { } background && !canvas.IsEmpty)
        {
            this._player.SetDisplaySize(background.Image, FrameDisplaySize(background.Image, new Rectangle(Point.Empty, canvas.Size)));
        }
    }

    /// <summary>
    /// Opens the background edit view (workfiles/20261104-grid-background-image.md § Background Edit View): the grid
    /// set aside — the Grid background with it, so the image is never drawn over itself —, the image alone as the
    /// one cell of a grid at the canvas's ratio, selected, every tool of a cell working on it. Its look changes as
    /// the Grid background's image's.
    /// </summary>
    public void BeginBackgroundEdit()
    {
        if (this._edit is not null || this._gridBackground?.Image is not { } background || this._locked)
        {
            return;
        }

        this.CancelEyedropper();
        this.EndLayerPress();
        this.EndDrag();
        this.EndKeyPan();
        this._edit = new EditState(-1, [.. this._images], this._layout, this._ratio, this.FreeRatio, this._borders, this._seams, this._gridBackground, EditsBackground: true);
        this._images.Clear();
        background.Image.Look = background.Look;
        this._images.Add(background.Image);
        this._layout = GridLayout.Default(1);
        this._borders = null;
        this._seams = null;
        this._gridBackground = null;
        this._selected = 0;
        this.ResetHover();
        this._ratio = this.EditRatio();
        this.EnterOrLeaveEdit();
    }

    /// <summary>In the background edit view, the image takes the look its cell was just given, and the window is told.</summary>
    private void SyncEditedBackground(SourceImage image)
    {
        if (this._edit is { EditsBackground: true, Background: { Image: { } background } grid } edit
            && background.Image == image && background.Look != image.Look)
        {
            var edited = grid.WithImage(background.WithLook(image.Look));
            this._edit = edit with { Background = edited };
            this.GridBackgroundEdited?.Invoke(this, edited);
        }
    }

    /// <summary>
    /// The background edit view's ratio: the canvas's — the grid's set aside — so the fit, the zoom and the position
    /// chosen there are the ones the grid draws; the image's own shape while it is stretched or tiled, shown whole.
    /// </summary>
    private double BackgroundEditRatio(EditState edit)
    {
        if (edit.Background?.Image is not { Placement: not BackgroundPlacement.Cell } background)
        {
            return edit.Ratio;
        }

        var shown = background.Shown;
        return Math.Clamp(shown.Width / (double)Math.Max(1, shown.Height), 0.05, 20);
    }

    /// <summary>The badge of the background edit view while the image is stretched or tiled: what the grid does with what the cell shows.</summary>
    private void PaintBackgroundEditBadge(Graphics g, Rectangle[] cells)
    {
        if (this._edit is { EditsBackground: true, Background.Image: { Placement: not BackgroundPlacement.Cell } background } && cells.Length > 0)
        {
            this.PaintCutoutBadge(g, cells[0], background.Placement == BackgroundPlacement.Tile ? "▦ Tile" : "⇔ Stretch");
        }
    }
}
