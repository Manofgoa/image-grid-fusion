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

    /// <summary>Share of a loop of <paramref name="loop"/> played at <paramref name="time"/>, from 0 up to 1 excluded; 0 without a loop.</summary>
    public static double Progress(TimeSpan time, TimeSpan loop) =>
        loop <= TimeSpan.Zero ? 0 : LoopTime(time, loop).Ticks / (double)loop.Ticks;

    /// <summary>
    /// Length of the grid's loop: the longest loop an image plays (<see cref="LoopOf(SourceImage)"/>) —
    /// its content's, a frozen one playing nothing, or the cycle of its Animations effect.
    /// </summary>
    public static TimeSpan GridLength(IEnumerable<SourceImage> images) =>
        images.Select(LoopOf).DefaultIfEmpty(TimeSpan.Zero).Max();

    /// <summary>
    /// The loop <paramref name="image"/> plays: its content's while it plays — the trimmed part
    /// (<see cref="SourceImage.PlayedLength"/>) — or the cycle of its motion when longer.
    /// </summary>
    public static TimeSpan LoopOf(SourceImage image) =>
        LoopOf(image.Plays ? image.PlayedLength : TimeSpan.Zero, image.Look);

    /// <summary>
    /// The loop of a cell whose content plays a loop of <paramref name="content"/> (zero for a still):
    /// the cycle of the Animations effect of <paramref name="look"/> when it is on and longer — a moving
    /// still plays a loop like a video (RULES.md § Video Length).
    /// </summary>
    public static TimeSpan LoopOf(TimeSpan content, ImageLook look) =>
        look.Motion is { Cycle: var cycle } && cycle > content ? cycle : content;

    /// <summary>
    /// Length of the exported video — the one rule the export, the preview's soundtrack loop and the
    /// bottom bar's length readout read (RULES.md § Video Length): the grid's loop of
    /// <paramref name="gridLength"/>, the shorter contents starting over until it ends; for a grid of
    /// stills with a <paramref name="soundtrack"/>, the soundtrack's length; <see cref="TimeSpan.Zero"/>
    /// without either, the export being a still.
    /// </summary>
    public static TimeSpan VideoLength(TimeSpan gridLength, Soundtrack? soundtrack) =>
        soundtrack?.LoopIn(gridLength) ?? gridLength;

    /// <summary>The <see cref="VideoLength(TimeSpan, Soundtrack?)"/> of <paramref name="images"/> as they stand.</summary>
    public static TimeSpan VideoLength(IEnumerable<SourceImage> images, Soundtrack? soundtrack) =>
        VideoLength(GridLength(images), soundtrack);

    /// <summary>Number of frames of a video of <paramref name="length"/>, the last one possibly shown shorter.</summary>
    public static int FrameCount(TimeSpan length) => Math.Max(1, (int)Math.Ceiling(length.TotalSeconds * FramesPerSecond - 1e-6));

    /// <summary>Time of frame <paramref name="frame"/> of the video.</summary>
    public static TimeSpan FrameTime(int frame) => TimeSpan.FromTicks(frame * TimeSpan.TicksPerSecond / FramesPerSecond);

    /// <summary>
    /// Images whose sounds are mixed into the video, and in the preview, each at the gain of its volume
    /// effect: the videos with sound that play and are not muted. A frozen video has no sound.
    /// </summary>
    public static IReadOnlyList<SourceImage> Heard(IEnumerable<SourceImage> images) => images.Where(i => i.IsHeard).ToList();

    /// <summary>H.264 needs even dimensions: rounded down, never under 2.</summary>
    public static Size EvenSize(Size size) => new(Math.Max(2, size.Width & ~1), Math.Max(2, size.Height & ~1));
}

/// <summary>A source that may carry a sound track, played with it.</summary>
public interface IHasSound
{
    bool HasSound { get; }
}
