namespace ImageGridFusion.Composition;

/// <summary>
/// The Cascade global effect's settings: the videos and animated GIFs of the grid play one after the
/// other, in cell order, instead of all at once, each followed by a <see cref="Pause"/> (see
/// <see cref="CascadeSchedule"/>). Off at start-up, not persisted (workfiles/20261008-video-cascade.md).
/// </summary>
public sealed record VideoCascade
{
    public static readonly TimeSpan MaxPause = TimeSpan.FromSeconds(3);

    /// <summary>The state the app starts in, and the global Resets and Clear all bring back.</summary>
    public static readonly VideoCascade Initial = new();

    /// <summary>How long every content taking part stands still after each turn, the last one included.</summary>
    public TimeSpan Pause { get; private init; } = TimeSpan.Zero;

    public VideoCascade WithPause(TimeSpan pause) =>
        this with { Pause = pause < TimeSpan.Zero ? TimeSpan.Zero : pause > MaxPause ? MaxPause : pause };
}

/// <summary>
/// When each content of the grid plays its turn while the Cascade is on — the one rule the preview,
/// its sound, the export, its sound and the video length read. A content taking part (a video or an
/// animated GIF that plays) gets a <b>turn</b>: one whole loop of its played part, from its starting
/// point round to it again; the turns follow each other in cell order, each followed by the pause.
/// Before its turn, a content stands on its starting point; after it, on the last frame it played.
/// </summary>
public sealed class CascadeSchedule
{
    private readonly TimeSpan[] _turns;
    private readonly TimeSpan[] _offsets;

    /// <param name="turns">Per cell, the length of its turn: its played part, zero when it takes no part.</param>
    /// <param name="pause">The pause after every turn.</param>
    public CascadeSchedule(IReadOnlyList<TimeSpan> turns, TimeSpan pause)
    {
        this._turns = [.. turns];
        this._offsets = new TimeSpan[this._turns.Length];
        var at = TimeSpan.Zero;
        for (int i = 0; i < this._turns.Length; i++)
        {
            this._offsets[i] = at;
            if (this._turns[i] > TimeSpan.Zero)
            {
                at += this._turns[i] + pause;
            }
        }

        this.Length = at;
    }

    /// <summary>The cascade of <paramref name="images"/>, in cell order, with the pause of <paramref name="cascade"/>.</summary>
    public static CascadeSchedule Of(IEnumerable<SourceImage> images, VideoCascade cascade) =>
        new([.. images.Select(TurnOf)], cascade.Pause);

    /// <summary>Takes part in the cascade: a video or an animated GIF that plays — a frozen one is a still.</summary>
    public static bool TakesPart(SourceImage image) => image.Plays && image.Trims;

    /// <summary>The turn of <paramref name="image"/>: its played part, or zero when it takes no part.</summary>
    public static TimeSpan TurnOf(SourceImage image) => TakesPart(image) ? image.PlayedLength : TimeSpan.Zero;

    /// <summary>One whole cascade: every turn and the pause after it; zero when nothing takes part.</summary>
    public TimeSpan Length { get; }

    /// <summary>Whether the cell at <paramref name="index"/> takes part.</summary>
    public bool Has(int index) => index >= 0 && index < this._turns.Length && this._turns[index] > TimeSpan.Zero;

    /// <summary>Where the turn of the cell at <paramref name="index"/> begins in the cascade.</summary>
    public TimeSpan TurnAt(int index) => this._offsets[index];

    /// <summary>
    /// How far into its turn the cell at <paramref name="index"/> stands at <paramref name="time"/> of
    /// the grid, the cascade starting over every <see cref="Length"/>: zero before its turn — its
    /// starting point —, the end of its turn after it, less one tick — the last frame it played.
    /// </summary>
    public TimeSpan Played(int index, TimeSpan time)
    {
        var at = Animation.LoopTime(time, this.Length) - this._offsets[index];
        var turn = this._turns[index];
        return at <= TimeSpan.Zero ? TimeSpan.Zero : at < turn ? at : turn - TimeSpan.FromTicks(1);
    }

    /// <summary>Whether the cell at <paramref name="index"/> is playing its turn at <paramref name="time"/> of the grid.</summary>
    public bool Plays(int index, TimeSpan time)
    {
        if (!this.Has(index))
        {
            return false;
        }

        var at = Animation.LoopTime(time, this.Length) - this._offsets[index];
        return at >= TimeSpan.Zero && at < this._turns[index];
    }
}
