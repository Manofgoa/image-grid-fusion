namespace ImageGridFusion.Composition;

/// <summary>
/// The part of the grid's timeline the video plays and exports — a <see cref="Start"/> and an <see cref="End"/> in
/// grid time, the end possibly past the grid's loop, the contents looping meanwhile (workfiles/20261106-video-range.md).
/// Set from the transport; <c>null</c> where it is read stands for the whole loop. At least <see cref="MinLength"/> long.
/// </summary>
public sealed record VideoRange
{
    /// <summary>The shortest range: one frame of the exported video.</summary>
    public static readonly TimeSpan MinLength = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Animation.FramesPerSecond);

    private VideoRange(TimeSpan start, TimeSpan end)
    {
        this.Start = start;
        this.End = end;
    }

    /// <summary>Where the video begins on the grid's timeline, from 0.</summary>
    public TimeSpan Start { get; }

    /// <summary>Where the video ends on the grid's timeline, after the start by <see cref="MinLength"/> at least.</summary>
    public TimeSpan End { get; }

    /// <summary>What the video lasts.</summary>
    public TimeSpan Length => this.End - this.Start;

    /// <summary>From <paramref name="start"/>, not before 0, to <paramref name="end"/>, pushed to <see cref="MinLength"/> after it.</summary>
    public static VideoRange Of(TimeSpan start, TimeSpan end)
    {
        if (start < TimeSpan.Zero)
        {
            start = TimeSpan.Zero;
        }

        return new(start, end < start + MinLength ? start + MinLength : end);
    }

    /// <summary>The whole of a loop of <paramref name="length"/>: from 0 to its end.</summary>
    public static VideoRange Whole(TimeSpan length) => Of(TimeSpan.Zero, length);

    /// <summary>One play of the <paramref name="soundtrack"/>'s part, from the grid's time 0: what <i>Fit to soundtrack</i> sets.</summary>
    public static VideoRange FitTo(Soundtrack soundtrack) => Of(TimeSpan.Zero, soundtrack.PlayedLength);

    /// <summary>The start moved to <paramref name="start"/>, kept before the end by <see cref="MinLength"/>; the end stays.</summary>
    public VideoRange WithStart(TimeSpan start) => Of(start > this.End - MinLength ? this.End - MinLength : start, this.End);

    /// <summary>The end moved to <paramref name="end"/>, kept after the start by <see cref="MinLength"/>; the start stays.</summary>
    public VideoRange WithEnd(TimeSpan end) => Of(this.Start, end);

    /// <summary>Whether it covers exactly a loop of <paramref name="length"/>: the whole loop, as without a range.</summary>
    public bool IsWhole(TimeSpan length) => this.Start == TimeSpan.Zero && this.End == length;

    /// <summary>
    /// What the transport's track spans for a grid whose loop is <paramref name="loop"/> and its <paramref name="range"/>:
    /// the loop, or — the range ending past it — the range's end plus one loop of room, so the end handle can go further.
    /// </summary>
    public static TimeSpan Extent(TimeSpan loop, VideoRange? range) =>
        range is not null && range.End > loop ? range.End + loop : loop;
}
