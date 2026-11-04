namespace ImageGridFusion.Composition;

/// <summary>
/// How the Grid background's image lies on the canvas (workfiles/20261104-grid-background-image.md § Fitting):
/// <see cref="Cell"/> — the canvas its cell, fitted by its Zoom effect (Fill, Contain, or a free zoom) —, stretched
/// to the canvas, or repeated over it.
/// </summary>
public enum BackgroundPlacement
{
    Cell,
    Stretch,
    Tile,
}

/// <summary>
/// The Grid background's image: a content — a still, a GIF, a video — drawn over the fill, under the cells, with
/// the look of its cell effects, its placement and its opacity. A floating cell as large as the canvas, as a layer
/// is one (workfiles/20261104-grid-background-image.md): <see cref="Compositor.DrawCell"/> draws it.
/// </summary>
/// <param name="Image">Its content; its <see cref="SourceImage.Look"/> is kept equal to <paramref name="Look"/>.</param>
public sealed record BackgroundImage(SourceImage Image, ImageLook Look, BackgroundPlacement Placement = BackgroundPlacement.Cell, double Opacity = 1)
{
    /// <summary>The content's size as shown — cropped, then turned.</summary>
    public Size Shown => this.Look.Shown(this.Image.Bitmap.Size);

    /// <summary>What the exports draw at <paramref name="time"/> on the grid's clock: the content at full resolution.</summary>
    public BackgroundFrame FrameAt(TimeSpan time) =>
        new(new Frame(this.Image.DrawnBitmap, this.Image.BandColor, this.Look, time, this.Image.Sharpening, this.Image.Sharpens), this.Placement, this.Opacity);

    /// <summary>What the preview draws at <paramref name="time"/>: an enlargement reduced (<see cref="SourceImage.ShownBitmap"/>).</summary>
    public BackgroundFrame ShownFrameAt(TimeSpan time) =>
        new(new Frame(this.Image.ShownBitmap, this.Image.BandColor, this.Look, time, this.Image.Sharpening, this.Image.Sharpens), this.Placement, this.Opacity);

    public BackgroundImage WithLook(ImageLook look) => this with { Look = look };

    public BackgroundImage WithPlacement(BackgroundPlacement placement) => this with { Placement = placement };

    public BackgroundImage WithOpacity(double opacity) => this with { Opacity = Math.Clamp(opacity, 0, 1) };

    /// <summary>
    /// The look a new background image arrives with — its default state: no Background, so the fill shows in
    /// Contain's bands and through its transparency; Fill pressed; a video with sound muted, its Volume on at 0 %
    /// (workfiles/20261104-grid-background-image.md § Playback, Q&amp;A 11).
    /// </summary>
    public static ImageLook DefaultLookFor(SourceImage image)
    {
        var look = ImageLook.None.TurnOff(ImageEffect.Background).WithZoomFit(ZoomFit.Fill);
        return image.HasSound ? look.WithVolume(VolumeEffect.Default.WithLevel(0)) : look;
    }

    /// <summary>The background's default state of <paramref name="effect"/>, applied to <paramref name="look"/> just reset: its Background off, its sound muted, Fill pressed.</summary>
    public static ImageLook DefaultOf(ImageEffect effect, ImageLook look, SourceImage image) => effect switch
    {
        ImageEffect.Background => look.TurnOff(ImageEffect.Background),
        ImageEffect.Zoom => look.WithZoomFit(ZoomFit.Fill),
        ImageEffect.Volume when image.HasSound => look.WithVolume(VolumeEffect.Default.WithLevel(0)),
        _ => look,
    };
}

/// <summary>The Grid background's image as drawn: its frame, its placement and its opacity.</summary>
public readonly record struct BackgroundFrame(Frame Frame, BackgroundPlacement Placement, double Opacity);
