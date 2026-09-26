namespace ImageGridFusion.Composition;

/// <summary>
/// The soundtrack global effect: the sound track of an audio or video file, mixed over the sounds of
/// the heard videos at its level, from 0 to <see cref="MaxLevel"/> — in the preview and in the MP4
/// export. It follows the grid's duration: shorter, it loops; longer, it is cut (RULES.md).
/// </summary>
public sealed record Soundtrack(string Path, TimeSpan Duration)
{
    /// <summary>200 %, like the volume effect: amplified, clipped where it goes beyond full scale.</summary>
    public const double MaxLevel = VolumeEffect.MaxLevel;

    /// <summary>Scale of the sound, from 0 (silent) to <see cref="MaxLevel"/>; 1 plays it as it is.</summary>
    public double Level { get; private init; } = 1;

    /// <summary>At <paramref name="level"/>, clamped to 0 – <see cref="MaxLevel"/>.</summary>
    public Soundtrack WithLevel(double level) => this with { Level = Math.Clamp(level, 0, MaxLevel) };

    /// <summary>
    /// Length of the grid's loop the soundtrack follows: the longest playing content, else — a grid of
    /// stills — the soundtrack itself.
    /// </summary>
    public TimeSpan LoopIn(TimeSpan gridLength) => gridLength > TimeSpan.Zero ? gridLength : Duration;
}
