namespace ImageGridFusion.Composition;

/// <summary>
/// The soundtrack global effect: the sound track of an audio or video file, mixed over the sounds of
/// the heard videos at its level, from 0 to <see cref="MaxLevel"/> — in the preview and in the MP4
/// export. It plays its <b>part</b>, from <see cref="From"/> to <see cref="To"/> in the file (the whole file by
/// default), on the grid's clock: looped (<see cref="Loops"/>, the default) or once, silence following
/// (workfiles/20261106-video-range.md § Soundtrack Trim). It gives a grid of stills its length (RULES.md).
/// </summary>
public sealed record Soundtrack(string Path, TimeSpan Duration)
{
    /// <summary>200 %, like the volume effect: amplified, clipped where it goes beyond full scale.</summary>
    public const double MaxLevel = VolumeEffect.MaxLevel;

    /// <summary>The shortest part: one frame of the exported video.</summary>
    public static readonly TimeSpan MinPart = VideoRange.MinLength;

    /// <summary>Scale of the sound, from 0 (silent) to <see cref="MaxLevel"/>; 1 plays it as it is.</summary>
    public double Level { get; private init; } = 1;

    /// <summary>Where the part played begins in the file.</summary>
    public TimeSpan From { get; private init; }

    /// <summary>Where the part played ends in the file: its end by default.</summary>
    public TimeSpan To { get; private init; } = Duration;

    /// <summary>Whether the part repeats for as long as the video lasts; else it plays once from the video's start.</summary>
    public bool Loops { get; private init; } = true;

    /// <summary>What the part lasts.</summary>
    public TimeSpan PlayedLength => this.To - this.From;

    /// <summary>Whether a part of the file only is played.</summary>
    public bool IsTrimmed => this.From > TimeSpan.Zero || this.To < this.Duration;

    /// <summary>At <paramref name="level"/>, clamped to 0 – <see cref="MaxLevel"/>.</summary>
    public Soundtrack WithLevel(double level) => this with { Level = Math.Clamp(level, 0, MaxLevel) };

    /// <summary>
    /// The part from <paramref name="from"/> to <paramref name="to"/>, both within the file, the part at least
    /// <see cref="MinPart"/> long — the end pushed after the start, the start pulled before the file's end.
    /// </summary>
    public Soundtrack WithTrim(TimeSpan from, TimeSpan to)
    {
        var lastStart = this.Duration > MinPart ? this.Duration - MinPart : TimeSpan.Zero;
        from = from < TimeSpan.Zero ? TimeSpan.Zero : from > lastStart ? lastStart : from;
        to = to > this.Duration ? this.Duration : to < from + MinPart ? from + MinPart : to;
        return this with { From = from, To = to > this.Duration ? this.Duration : to };
    }

    /// <summary>Looped or played once.</summary>
    public Soundtrack WithLoops(bool loops) => this with { Loops = loops };

    /// <summary>
    /// Length of the grid's loop the soundtrack follows: the longest playing content, else — a grid of
    /// stills — the soundtrack's part.
    /// </summary>
    public TimeSpan LoopIn(TimeSpan gridLength) => gridLength > TimeSpan.Zero ? gridLength : this.PlayedLength;

    /// <summary>
    /// Where the file stands at <paramref name="time"/> of the grid's clock: within its part, looped — or, played
    /// once, <c>null</c> past the part, nothing heard.
    /// </summary>
    public TimeSpan? FileTimeAt(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return this.Loops ? this.From + Animation.LoopTime(time, this.PlayedLength)
            : time < this.PlayedLength ? this.From + time
            : null;
    }
}
