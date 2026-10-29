namespace ImageGridFusion.Composition;

/// <summary>
/// An image of the grid: an owned 32bpp copy, the color of its bands and the file it came from, if any.
/// A file with several pages (PDF, video, long text) keeps its <see cref="Pages"/> and shows one of them.
/// </summary>
public sealed class SourceImage : IDisposable
{
    // The masks kept for an undo or a redo between two picks to be instant (workfiles/20261029-cutout.md § The Mask).
    private const int KeptMasks = 6;

    // The Cutout's masks computed so far, the latest first, each for a page and a mask key (CutoutEffect.MaskKey).
    private readonly List<(int Page, string Key, CutoutMask Mask)> _masks = [];

    // The image cut out as its look's Cutout effect says, for a page; drawn while they still match.
    private (int Page, CutoutEffect Effect, Bitmap Bitmap)? _cut;

    public SourceImage(Bitmap bitmap, string? filePath, PageSource? pages = null, int page = 0)
    {
        Bitmap = bitmap;
        FilePath = filePath;
        Pages = pages;
        Page = page;
        BandColor = BandColor.Of(bitmap);
    }

    public Bitmap Bitmap { get; private set; }

    public string? FilePath { get; private set; }

    /// <summary>A text pasted or dropped: the form it was read from, saved as is when it becomes a favorite.</summary>
    public TextOrigin? TextOrigin { get; set; }

    /// <summary>An image without a file that was dropped (a text dragged from another app), not pasted: named so in the preview.</summary>
    public bool Dropped { get; set; }

    public PageSource? Pages { get; }

    /// <summary>Index of the page <see cref="Bitmap"/> shows, in <see cref="Pages"/>.</summary>
    public int Page { get; private set; }

    /// <summary>Computed on the page shown, not on each frame of an animation.</summary>
    public BandColor BandColor { get; private set; }

    /// <summary>Actions on the image, kept whatever cell it moves to and whatever page it shows.</summary>
    public ImageLook Look { get; set; } = ImageLook.None;

    public Size Size => Bitmap.Size;

    /// <summary>Whether the Cutout effect applies to it: a still — a video, a GIF or a content of several pages plays.</summary>
    public bool CutsOut => !this.IsAnimated;

    /// <summary>
    /// The bitmap the image is drawn from — by the preview, the exports and the layers: cut out while
    /// its Cutout effect is on and its cut-out ready, else <see cref="Bitmap"/>. Same size as it.
    /// </summary>
    public Bitmap DrawnBitmap =>
        this.Look.Cutout is { } effect && this.CutsOut && this._cut is { } cut && cut.Page == this.Page && cut.Effect.Equals(effect) && cut.Bitmap.Size == this.Bitmap.Size
            ? cut.Bitmap
            : this.Bitmap;

    /// <summary>The Cutout on, its cut-out not ready yet: the image is drawn uncut meanwhile.</summary>
    public bool CutoutPending => this.Look.Cutout is not null && this.CutsOut && ReferenceEquals(this.DrawnBitmap, this.Bitmap);

    /// <summary>The image the Cutout's model last read, while a pick reads it again (<see cref="PickEmbedding"/>).</summary>
    public PickEmbedding? PickEmbedding { get; set; }

    /// <summary>The mask computed for the page shown and <paramref name="key"/> (<see cref="CutoutEffect.MaskKey"/>); <c>null</c> when none is kept.</summary>
    public CutoutMask? MaskFor(string key) =>
        this._masks.FirstOrDefault(m => m.Page == this.Page && m.Key == key).Mask;

    /// <summary>Keeps a mask computed for <paramref name="page"/>, the oldest ones dropped past a few.</summary>
    public void KeepMask(int page, string key, CutoutMask mask)
    {
        this._masks.RemoveAll(m => m.Page == page && m.Key == key);
        this._masks.Insert(0, (page, key, mask));
        if (this._masks.Count > KeptMasks)
        {
            this._masks.RemoveRange(KeptMasks, this._masks.Count - KeptMasks);
        }
    }

    /// <summary>Takes <paramref name="bitmap"/>, the image cut out as <paramref name="effect"/> says on <paramref name="page"/>, disposing the previous one.</summary>
    public void SetCut(int page, CutoutEffect effect, Bitmap bitmap)
    {
        var previous = this._cut?.Bitmap;
        this._cut = (page, effect, bitmap);
        previous?.Dispose();
    }

    /// <summary>
    /// A text laying its content out for its zoom: its pages hold the zoom, so it is drawn at 100 %,
    /// filling its cell. Not while cropped — a cropped text keeps its layout and zooms like an image
    /// (workfiles/20260926-text-zoom-and-frames.md).
    /// </summary>
    public bool LaysOutZoom => this.Pages is { LayoutZoom: not null } && this.Look.Crop is null;

    /// <summary>
    /// The look the image is drawn with — by the preview, the exports and the gestures' geometry: its
    /// own, but centered and at 100 % of the zoom its pages hold for a text laying its zoom out
    /// (<see cref="LaysOutZoom"/>).
    /// </summary>
    public ImageLook DrawnLook => this.LaysOutZoom ? this.Look.WithZoomLaidOut(this.Pages!.LayoutZoom!.Value) : this.Look;

    /// <summary>How many times its unzoomed resolution the page shown is rendered at (<see cref="PageSource.Sharpening"/>).</summary>
    public double Sharpening => this.Pages?.Sharpening ?? 1;

    /// <summary>Whether its pages render again at any resolution (<see cref="PageSource.Sharpens"/>): a PDF, a thumbnail, a text.</summary>
    public bool Sharpens => this.Pages?.Sharpens ?? false;

    /// <summary>What the canvas sizing reads: the image as drawn, at its unzoomed resolution, with the look it is drawn with.</summary>
    public SizedImage Sizing => new(Frame.Unsharpened(this.Look.Shown(this.Bitmap.Size), this.Sharpening), this.DrawnLook, this.Sharpens);

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// What the image holds in memory, in bytes: its bitmap's pixels, and what its pages keep — what the
    /// undo history weighs the images it parks by (workfiles/20261010-optimisation-lack-of-memory.md § 2.4).
    /// </summary>
    public long MemoryBytes => this.IsDisposed ? 0 : (long)this.Bitmap.Width * this.Bitmap.Height * 4 + (this.Pages?.MemoryBytes ?? 0)
        + (this._cut is { } cut ? (long)cut.Bitmap.Width * cut.Bitmap.Height * 4 : 0)
        + this._masks.Sum(m => m.Mask.MemoryBytes) + (this.PickEmbedding?.MemoryBytes ?? 0);

    /// <summary>Plays when shown: a video, an animated GIF, a PDF of several pages, a text longer than its cell.</summary>
    public bool IsAnimated => Pages is { LoopDuration: var loop } && loop > TimeSpan.Zero;

    /// <summary>An animated image the frames effect holds on one frame: shown, and exported, as a still.</summary>
    public bool IsFrozen => IsAnimated && Look.Frames is { Frozen: true };

    /// <summary>An animated image that plays: in the preview, and as a video when exported.</summary>
    public bool Plays => IsAnimated && !IsFrozen;

    /// <summary>Changes over time, so it makes a video when exported: it plays, or its Animations effect is on.</summary>
    public bool Moves => this.Plays || this.Look.Motion is not null;

    /// <summary>A video with a sound track, read again from its file to be heard.</summary>
    public bool HasSound => Pages is IHasSound { HasSound: true } && FilePath is not null;

    /// <summary>Its sound is in the grid's mix: a video with sound that plays, not silenced by its volume effect.</summary>
    public bool IsHeard => HasSound && Plays && Look.SoundGain > 0;

    /// <summary>
    /// Page the frames effect points at: where the image starts playing, or the one it is frozen on —
    /// inside the trim; the first played without it.
    /// </summary>
    public int StartPage => this.Pages is { } pages && this.Look.Frames is { } frames
        ? (this.Trims ? Math.Clamp(frames.PageOf(pages.Count), this.FirstPage, this.LastPage) : frames.PageOf(pages.Count))
        : this.FirstPage;

    /// <summary>Time in the loop — the trimmed part — where the image starts playing.</summary>
    public TimeSpan StartTime => this.Pages is { } pages ? pages.TimeOf(this.StartPage) - this.PlayedFrom : TimeSpan.Zero;

    /// <summary>
    /// A video or an animated GIF: its frames effect may trim it. The one definition of the part that
    /// plays is <see cref="PlayedFrom"/> and <see cref="PlayedLength"/>, read by the loop, the preview,
    /// the sound and the exports (workfiles/20261008-video-trim.md).
    /// </summary>
    public bool Trims => this.IsAnimated && this.Pages!.HasFrames;

    /// <summary>First frame played: the trim's, the first without it.</summary>
    public int FirstPage => this.Trims && this.Look.Frames is { } frames ? frames.FirstOf(this.Pages!.Count) : 0;

    /// <summary>Last frame played: the trim's, the last without it.</summary>
    public int LastPage => this.Pages is not { } pages ? 0
        : this.Trims && this.Look.Frames is { } frames ? frames.LastOf(pages.Count)
        : Math.Max(0, pages.Count - 1);

    /// <summary>Content time where the played part begins.</summary>
    public TimeSpan PlayedFrom => this.Trims ? this.Pages!.TimeOf(this.FirstPage) : TimeSpan.Zero;

    /// <summary>
    /// Length of the played part, its last frame shown for its whole duration: the content's loop
    /// untrimmed; <see cref="TimeSpan.Zero"/> for a source shown still.
    /// </summary>
    public TimeSpan PlayedLength
    {
        get
        {
            if (this.Pages is not { } pages)
            {
                return TimeSpan.Zero;
            }

            if (!this.Trims)
            {
                return pages.LoopDuration;
            }

            int last = this.LastPage;
            var end = last >= pages.Count - 1 ? pages.LoopDuration : pages.TimeOf(last + 1);
            return end - this.PlayedFrom;
        }
    }

    /// <summary>Content time shown at <paramref name="position"/> of the loop: within the played part, starting over at its end.</summary>
    public TimeSpan ContentTime(TimeSpan position) => this.PlayedFrom + Animation.LoopTime(position, this.PlayedLength);

    /// <summary>
    /// An image without a file, now saved into <paramref name="path"/>: it names that file from now
    /// on, as if loaded from it; nothing is reloaded, its look kept.
    /// </summary>
    public void AdoptFile(string path) => FilePath = path;

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

    // Once only: the grid and the undo history may both let go of the same image.
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Bitmap.Dispose();
        this._cut?.Bitmap.Dispose();
        Pages?.Dispose();
    }
}

/// <summary>A text as it arrived — its RTF, its HTML as a document, or its plain text — and the extension of that form.</summary>
public sealed record TextOrigin(string Content, string Extension);
