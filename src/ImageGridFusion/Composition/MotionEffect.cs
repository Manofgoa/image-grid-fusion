namespace ImageGridFusion.Composition;

/// <summary>The kinds of motion the Animations effect offers.</summary>
public enum MotionKind
{
    /// <summary>A continuous back-and-forth zoom.</summary>
    Zoom,
}

/// <summary>
/// The Animations effect of an image: a motion played over time on one clock — the grid's in the
/// preview, the video's in the animated exports — its <see cref="Cycle"/> a loop of the grid like a
/// video's (RULES.md § Video Length). At time 0, the starting state: the image as the other effects
/// place it; still exports show that state.
/// </summary>
public sealed record MotionEffect
{
    /// <summary>The zoom's fixed amplitude: from the starting state to +20 %, and back.</summary>
    public const double ZoomAmplitude = 0.2;

    public static readonly TimeSpan MinCycle = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaxCycle = TimeSpan.FromSeconds(30);

    /// <summary>A zoom, one back-and-forth every 6 s.</summary>
    public static readonly MotionEffect Default = new();

    public MotionKind Kind { get; private init; } = MotionKind.Zoom;

    /// <summary>Duration of one back-and-forth, within <see cref="MinCycle"/>…<see cref="MaxCycle"/>.</summary>
    public TimeSpan Cycle { get; private init; } = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Scale the motion adds to the zoom at <paramref name="time"/>: 1 at the start of each cycle, up to
    /// 1 + <see cref="ZoomAmplitude"/> halfway, on a sine, so it slows down at both ends.
    /// </summary>
    public double ZoomAt(TimeSpan time)
    {
        double wave = (1 - Math.Cos(2 * Math.PI * Animation.Progress(time, this.Cycle))) / 2;
        return 1 + ZoomAmplitude * wave;
    }

    public MotionEffect WithKind(MotionKind kind) => this with { Kind = kind };

    public MotionEffect WithCycle(TimeSpan cycle) => this with { Cycle = cycle < MinCycle ? MinCycle : cycle > MaxCycle ? MaxCycle : cycle };
}
