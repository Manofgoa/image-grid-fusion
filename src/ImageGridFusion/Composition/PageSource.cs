namespace ImageGridFusion.Composition;

/// <summary>
/// Pages of a file previewed as an image — PDF pages, video positions, text pages — rendered on
/// demand. <see cref="Render"/> runs off the UI thread, one call at a time per source; the other
/// members are used on the UI thread.
/// </summary>
public abstract class PageSource : IDisposable
{
    public abstract int Count { get; }

    /// <summary>Page shown when the file is added.</summary>
    public virtual int InitialPage => 0;

    /// <summary>What the slider shows for <paramref name="page"/>: a page number or a time.</summary>
    public abstract string Label(int page);

    /// <summary>
    /// Longest side, in pixels, a page is rendered at to stay sharp under a zoom (<see cref="Sharpen"/>):
    /// past it, the page is magnified again (workfiles/20260926-text-zoom-and-frames.md).
    /// </summary>
    public const int MaxSharpSide = 4096;

    /// <summary>Size the pages are laid out at, when the source takes the shape of its cell; else <c>null</c>.</summary>
    public virtual Size? PageSize => null;

    /// <summary>
    /// The zoom the pages are laid out for, when the source lays its content out again for the zoom —
    /// a text, more of it on a page as the zoom goes down, drawn at 100 % since its pages hold the
    /// zoom; else <c>null</c>.
    /// </summary>
    public virtual double? LayoutZoom => null;

    /// <summary>
    /// Lays the pages out again at <paramref name="pageSize"/>, for <paramref name="zoom"/> (see
    /// <see cref="LayoutZoom"/>), and returns the new page holding the start of <paramref name="page"/>.
    /// Only for a source with a <see cref="PageSize"/>.
    /// </summary>
    public virtual int Resize(Size pageSize, double zoom, int page) => page;

    /// <summary>Whether the pages can be rendered at a higher resolution, so a zoom stays sharp: a PDF, a thumbnail, a text.</summary>
    public virtual bool Sharpens => false;

    /// <summary>
    /// Asks for the pages to be rendered <paramref name="longSide"/> pixels on their longer side, kept
    /// between the source's own resolution and <see cref="MaxSharpSide"/>; returns whether that changes
    /// their resolution — the page shown is then to be rendered again. Only for a source that
    /// <see cref="Sharpens"/>.
    /// </summary>
    public virtual bool Sharpen(int longSide) => false;

    /// <summary>
    /// How many times its unzoomed resolution the last page was rendered at: 1 but while sharpened. The
    /// canvas sizing divides by it, so a sharper page never widens the export on its own.
    /// </summary>
    public virtual double Sharpening => 1;

    public abstract Bitmap Render(int page);

    /// <summary>
    /// Length of one loop of the animation; <see cref="TimeSpan.Zero"/> for a source shown still.
    /// May change when the pages are laid out again (<see cref="Resize"/>).
    /// </summary>
    public virtual TimeSpan LoopDuration => TimeSpan.Zero;

    /// <summary>
    /// Opens a reader that renders the animation frame by frame, independently of <see cref="Render"/>
    /// and of other readers. Only for a source with a <see cref="LoopDuration"/>; runs off the UI thread.
    /// </summary>
    public virtual AnimationReader OpenAnimation() => throw new NotSupportedException("This source is not animated.");

    /// <summary>Page the slider shows at <paramref name="time"/> in the loop.</summary>
    public virtual int PageAt(TimeSpan time) => 0;

    /// <summary>Time in the loop where the animation shows <paramref name="page"/>: where it resumes after browsing.</summary>
    public virtual TimeSpan TimeOf(int page) => TimeSpan.Zero;

    /// <summary>
    /// The pages are the frames of a video or an animated GIF, one each: the Frames effect trims them
    /// and starts on any of them (workfiles/20261008-video-trim.md). False for a PDF's or a text's pages.
    /// </summary>
    public virtual bool HasFrames => false;

    /// <summary>Frames per second of a video, for its minutes : seconds : frame fields; <c>null</c> for a GIF, its frames numbered.</summary>
    public virtual double? FrameRate => null;

    public virtual void Dispose()
    {
    }
}
