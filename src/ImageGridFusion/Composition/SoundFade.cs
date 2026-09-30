namespace ImageGridFusion.Composition;

/// <summary>Shape of the <see cref="SoundFade"/>'s ramps.</summary>
public enum FadeCurve
{
    /// <summary>The gain follows the square of the ramp's progress, heard as a steady rise.</summary>
    Squared,

    /// <summary>The gain follows the ramp's progress.</summary>
    Linear,
}

/// <summary>
/// The fade global effect: the grid's sound mix rising from silence over <see cref="Duration"/> at the
/// start of the video, and falling back to silence over as long before its end — in the preview on
/// every loop of the grid, and in the MP4 export (RULES.md § Global Effects).
/// </summary>
public sealed record SoundFade(TimeSpan Duration, FadeCurve Curve)
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(0.1);

    public static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(5);

    /// <summary>The initial settings: 1 s, squared.</summary>
    public static readonly SoundFade Initial = new(TimeSpan.FromSeconds(1), FadeCurve.Squared);

    /// <summary>At <paramref name="duration"/>, clamped to <see cref="MinDuration"/> – <see cref="MaxDuration"/>.</summary>
    public SoundFade WithDuration(TimeSpan duration) =>
        this with { Duration = TimeSpan.FromTicks(Math.Clamp(duration.Ticks, MinDuration.Ticks, MaxDuration.Ticks)) };

    public SoundFade WithCurve(FadeCurve curve) => this with { Curve = curve };

    /// <summary>
    /// Gain of the mix at <paramref name="time"/> of a video lasting <paramref name="length"/>: 0 at both
    /// ends, 1 between the ramps. A video shorter than two ramps gets ramps of half its length, so the
    /// sound rises then falls straight away; no length, no fade.
    /// </summary>
    public double GainAt(TimeSpan time, TimeSpan length) => GainAt(time.TotalSeconds, length.TotalSeconds);

    /// <inheritdoc cref="GainAt(TimeSpan, TimeSpan)"/>
    public double GainAt(double seconds, double lengthSeconds)
    {
        if (lengthSeconds <= 0)
        {
            return 1;
        }

        double ramp = Math.Min(Duration.TotalSeconds, lengthSeconds / 2);
        if (ramp <= 0)
        {
            return 1;
        }

        double progress = Math.Clamp(Math.Min(seconds, lengthSeconds - seconds) / ramp, 0, 1);
        return Curve == FadeCurve.Squared ? progress * progress : progress;
    }
}
