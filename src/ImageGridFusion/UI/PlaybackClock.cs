using System.Diagnostics;

namespace ImageGridFusion.UI;

/// <summary>
/// The preview's clock: a stopwatch that can be paused, resumed and set to any time — the one clock
/// the frames, the motions, the Cascade, the soundtrack and the fade all read, so a pause or a seek
/// moves them together (workfiles/20261009-playback-controls.md).
/// </summary>
internal sealed class PlaybackClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // The time the clock stood at when it was last set; the stopwatch counts from there.
    private TimeSpan _origin;

    /// <summary>The time on the clock: frozen while paused.</summary>
    public TimeSpan Elapsed => this._origin + this._stopwatch.Elapsed;

    /// <summary>Whether the clock is counting.</summary>
    public bool IsRunning => this._stopwatch.IsRunning;

    /// <summary>Stops counting; <see cref="Elapsed"/> stays where it is.</summary>
    public void Pause() => this._stopwatch.Stop();

    /// <summary>Counts again from where it stands.</summary>
    public void Resume() => this._stopwatch.Start();

    /// <summary>Puts the clock on <paramref name="time"/>, running or paused as it was.</summary>
    public void Set(TimeSpan time)
    {
        bool running = this._stopwatch.IsRunning;
        this._origin = time;
        this._stopwatch.Reset();
        if (running)
        {
            this._stopwatch.Start();
        }
    }
}
