namespace ImageGridFusion.Composition;

/// <summary>Timing rules shared by the live preview and the video export.</summary>
public static class Animation
{
    /// <summary>Frame rate of the exported video.</summary>
    public const int FramesPerSecond = 30;

    /// <summary>How long a PDF page, or a text view, stays shown.</summary>
    public static readonly TimeSpan StepDuration = TimeSpan.FromSeconds(1);

    /// <summary>Time within a loop of <paramref name="loop"/>: a shorter source starts over from its beginning.</summary>
    public static TimeSpan LoopTime(TimeSpan time, TimeSpan loop) =>
        loop <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(((time.Ticks % loop.Ticks) + loop.Ticks) % loop.Ticks);

    /// <summary>Length of the video: the longest loop; the others loop until it ends.</summary>
    public static TimeSpan VideoLength(IEnumerable<SourceImage> images) =>
        images.Where(i => i.IsAnimated).Select(i => i.Pages!.LoopDuration).DefaultIfEmpty(TimeSpan.Zero).Max();

    /// <summary>Number of frames of a video of <paramref name="length"/>, the last one possibly shown shorter.</summary>
    public static int FrameCount(TimeSpan length) => Math.Max(1, (int)Math.Ceiling(length.TotalSeconds * FramesPerSecond - 1e-6));

    /// <summary>Time of frame <paramref name="frame"/> of the video.</summary>
    public static TimeSpan FrameTime(int frame) => TimeSpan.FromTicks(frame * TimeSpan.TicksPerSecond / FramesPerSecond);

    /// <summary>
    /// Image whose sound goes into the video, and plays in the preview: image 1 when it is a video with
    /// sound, else the first video with sound in grid order; <c>null</c> when none has any. A frozen
    /// video has no sound.
    /// </summary>
    public static SourceImage? SoundSource(IReadOnlyList<SourceImage> images) =>
        images.FirstOrDefault(i => i.Plays && i.Pages is IHasSound { HasSound: true } && i.FilePath is not null);

    /// <summary>
    /// Images whose sounds are mixed into the video, and in the preview, each at the gain of its volume
    /// effect: the videos with sound that play and are not muted. A frozen video has no sound.
    /// </summary>
    public static IReadOnlyList<SourceImage> Heard(IEnumerable<SourceImage> images) => images.Where(i => i.IsHeard).ToList();

    /// <summary>
    /// The look of <paramref name="image"/> as it arrives in the grid, or has its volume reset: a video
    /// with sound is heard at 100 % when no other image is heard, else muted (RULES.md). The look is
    /// returned as it is for any other image.
    /// </summary>
    public static ImageLook SoundOnArrival(SourceImage image, ImageLook look, IEnumerable<SourceImage> grid) =>
        image.HasSound && grid.Any(i => i != image && i.IsHeard)
            ? look.WithVolume(VolumeEffect.Muted)
            : look;

    /// <summary>H.264 needs even dimensions: rounded down, never under 2.</summary>
    public static Size EvenSize(Size size) => new(Math.Max(2, size.Width & ~1), Math.Max(2, size.Height & ~1));
}

/// <summary>A source that may carry a sound track, played with it.</summary>
public interface IHasSound
{
    bool HasSound { get; }
}
