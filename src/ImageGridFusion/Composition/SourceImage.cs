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

    public bool IsDisposed { get; private set; }

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
        Pages?.Dispose();
    }
}

/// <summary>A text as it arrived — its RTF, its HTML as a document, or its plain text — and the extension of that form.</summary>
public sealed record TextOrigin(string Content, string Extension);
