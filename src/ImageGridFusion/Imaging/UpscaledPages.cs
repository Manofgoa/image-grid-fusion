using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// An animated GIF's pages as the Upscale effect enlarged them: every frame read back from the upscale cache
/// (<see cref="UpscaleCache"/>), at its time and with its delay — the GIF's own pages for everything else. What the
/// preview's player, the page slider and every export read while the effect is on and its frames ready
/// (<see cref="SourceImage.DrawnPages"/>). It owns nothing: the GIF's pages stay the image's.
/// See workfiles/20261029-image-upscale.md § Animated GIFs.
/// </summary>
public sealed class UpscaledPages : PageSource
{
    private readonly PageSource _pages;
    private readonly string _tag;
    private readonly TimeSpan[] _durations;

    public UpscaledPages(PageSource pages, string tag, int factor)
    {
        this._pages = pages;
        this._tag = tag;
        this.Factor = factor;
        this._durations = new TimeSpan[pages.Count];
        for (int page = 0; page < pages.Count; page++)
        {
            var end = page + 1 < pages.Count ? pages.TimeOf(page + 1) : pages.LoopDuration;
            this._durations[page] = end - pages.TimeOf(page);
        }
    }

    /// <summary>How many times larger its frames are than the GIF's.</summary>
    public int Factor { get; }

    /// <summary>The GIF's own pages, enlarged here.</summary>
    public PageSource Source => this._pages;

    public override int Count => this._pages.Count;

    public override string Label(int page) => this._pages.Label(page);

    public override TimeSpan LoopDuration => this._pages.LoopDuration;

    public override int PageAt(TimeSpan time) => this._pages.PageAt(time);

    public override TimeSpan TimeOf(int page) => this._pages.TimeOf(page);

    public override bool HasFrames => this._pages.HasFrames;

    public override double? FrameRate => this._pages.FrameRate;

    /// <summary>The frame enlarged; the GIF's own, should its file have gone from the cache.</summary>
    public override Bitmap Render(int page) =>
        UpscaleCache.Load(this._tag, this.Factor, Math.Clamp(page, 0, this.Count - 1)) ?? this._pages.Render(page);

    public override AnimationReader OpenAnimation() => new StepReader(() => this._durations, this.Render);
}
