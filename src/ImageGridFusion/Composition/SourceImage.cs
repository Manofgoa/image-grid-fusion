namespace ImageGridFusion.Composition;

/// <summary>
/// An image of the grid: an owned 32bpp copy, the color of its bands and the file it came from, if any.
/// A file with several pages (PDF, video, long text) keeps its <see cref="Pages"/> and shows one of them.
/// </summary>
public sealed class SourceImage : IDisposable
{
    public SourceImage(Bitmap bitmap, string? filePath, PageSource? pages = null, int page = 0)
    {
        Bitmap = bitmap;
        FilePath = filePath;
        Pages = pages;
        Page = page;
        BandColor = BandColor.Of(bitmap);
    }

    public Bitmap Bitmap { get; private set; }

    public string? FilePath { get; }

    public PageSource? Pages { get; }

    /// <summary>Index of the page <see cref="Bitmap"/> shows, in <see cref="Pages"/>.</summary>
    public int Page { get; private set; }

    /// <summary>Computed on the page shown, not on each frame of an animation.</summary>
    public BandColor BandColor { get; private set; }

    /// <summary>Actions on the image, kept whatever cell it moves to and whatever page it shows.</summary>
    public ImageLook Look { get; set; } = ImageLook.None;

    public Size Size => Bitmap.Size;

    public bool IsDisposed { get; private set; }

    /// <summary>Plays when shown: a video, an animated GIF, a PDF of several pages, a text longer than its cell.</summary>
    public bool IsAnimated => Pages is { LoopDuration: var loop } && loop > TimeSpan.Zero;

    /// <summary>An animated image the frames effect holds on one frame: shown, and exported, as a still.</summary>
    public bool IsFrozen => IsAnimated && Look.Frames is { Frozen: true };

    /// <summary>An animated image that plays: in the preview, and as a video when exported.</summary>
    public bool Plays => IsAnimated && !IsFrozen;

    /// <summary>Page the frames effect points at: where the image starts playing, or the one it is frozen on; the first without it.</summary>
    public int StartPage => Pages is { } pages && Look.Frames is { } frames ? frames.PageOf(pages.Count) : 0;

    /// <summary>Time in the loop where the image starts playing.</summary>
    public TimeSpan StartTime => Pages?.TimeOf(StartPage) ?? TimeSpan.Zero;

    /// <summary>Shows another page: takes ownership of <paramref name="bitmap"/> and disposes the previous one.</summary>
    public void ShowPage(int page, Bitmap bitmap)
    {
        ShowFrame(page, bitmap);
        BandColor = BandColor.Of(bitmap);
    }

    /// <summary>
    /// Shows a frame of the animation, which stands at <paramref name="page"/>: takes ownership of
    /// <paramref name="bitmap"/> and keeps the band color, so bands do not flicker while playing.
    /// </summary>
    public void ShowFrame(int page, Bitmap bitmap)
    {
        var previous = Bitmap;
        Bitmap = bitmap;
        Page = page;
        previous.Dispose();
    }

    public void Dispose()
    {
        IsDisposed = true;
        Bitmap.Dispose();
        Pages?.Dispose();
    }
}
