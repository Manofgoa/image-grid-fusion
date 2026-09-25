namespace ImageGridFusion.Composition;

/// <summary>
/// The volume effect of a video with sound: its level in the grid's mix, from 0 to
/// <see cref="MaxLevel"/>, and a mute that keeps the level, so unmuting brings it back — in the
/// preview and in the exports.
/// </summary>
public sealed record VolumeEffect
{
    /// <summary>200 %: the sound amplified, clipped where it goes beyond full scale.</summary>
    public const double MaxLevel = 2;

    /// <summary>At 100 %, heard.</summary>
    public static readonly VolumeEffect Default = new();

    /// <summary>Silenced, the slider kept at 100 %: a video that arrives while another one is heard (RULES.md).</summary>
    public static readonly VolumeEffect Muted = new() { IsMuted = true };

    /// <summary>Scale of the sound, from 0 (silent) to <see cref="MaxLevel"/>; 1 plays it as it is.</summary>
    public double Level { get; private init; } = 1;

    /// <summary>Silenced, whatever the <see cref="Level"/>, which is kept for unmuting.</summary>
    public bool IsMuted { get; private init; }

    /// <summary>Scale the sound is mixed at: 0 while muted.</summary>
    public double Gain => IsMuted ? 0 : Level;

    /// <summary>At <paramref name="level"/>: reaching 0 mutes, any level above 0 unmutes.</summary>
    public VolumeEffect WithLevel(double level)
    {
        double clamped = Math.Clamp(level, 0, MaxLevel);
        return this with { Level = clamped, IsMuted = clamped == 0 };
    }

    /// <summary>Muted or not, the level kept; unmuting a level of 0 brings it back to 100 %.</summary>
    public VolumeEffect WithMuted(bool muted) => muted
        ? this with { IsMuted = true }
        : this with { IsMuted = false, Level = Level == 0 ? 1 : Level };
}
