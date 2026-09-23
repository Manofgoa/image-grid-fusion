namespace ImageGridFusion.Composition;

/// <summary>
/// Renders the frames of an animated source, asked for by time within one loop. Meant to be read
/// forward — the live preview and the video export move on in small steps — but any time may be
/// asked for. One call at a time, off the UI thread, except <see cref="Reset"/>.
/// </summary>
public abstract class AnimationReader : IDisposable
{
    private volatile bool _stale;

    /// <summary>
    /// Returns the frame shown at <paramref name="time"/>, owned by the caller, or <c>null</c> when it
    /// is still the one returned last time.
    /// </summary>
    public Bitmap? FrameAt(TimeSpan time)
    {
        if (_stale)
        {
            _stale = false;
            Forget();
        }

        return Read(time);
    }

    /// <summary>
    /// Makes the next <see cref="FrameAt"/> render its frame even if unchanged: the source was laid out
    /// again. Safe from any thread.
    /// </summary>
    public void Reset() => _stale = true;

    /// <summary>
    /// Times worth trying, in order, to find a frame that is not empty (see <see cref="EmptyFrame"/>),
    /// for a loop of <paramref name="loop"/>.
    /// </summary>
    public abstract IEnumerable<TimeSpan> ProbeTimes(TimeSpan loop);

    public virtual void Dispose()
    {
    }

    protected abstract Bitmap? Read(TimeSpan time);

    /// <summary>Forgets which frame was returned last.</summary>
    protected abstract void Forget();
}

/// <summary>
/// An animation made of steps, each shown for its own duration: PDF pages, text views, GIF frames.
/// </summary>
public sealed class StepReader : AnimationReader
{
    private readonly Func<IReadOnlyList<TimeSpan>> _durations;
    private readonly Func<int, Bitmap> _render;
    private int _shown = -1;

    /// <param name="durations">Current step durations; read at each frame, as they may change with the layout.</param>
    /// <param name="render">Renders a step.</param>
    public StepReader(Func<IReadOnlyList<TimeSpan>> durations, Func<int, Bitmap> render)
    {
        _durations = durations;
        _render = render;
    }

    /// <summary>Step shown at <paramref name="time"/>, looping over the sum of <paramref name="durations"/>.</summary>
    public static int StepAt(IReadOnlyList<TimeSpan> durations, TimeSpan time)
    {
        var loop = durations.Aggregate(TimeSpan.Zero, (sum, d) => sum + d);
        if (durations.Count == 0 || loop <= TimeSpan.Zero)
        {
            return 0;
        }

        time = Animation.LoopTime(time, loop);
        for (int step = 0; step < durations.Count; step++)
        {
            if (time < durations[step])
            {
                return step;
            }

            time -= durations[step];
        }

        return durations.Count - 1;
    }

    /// <summary>Time at which <paramref name="step"/> starts.</summary>
    public static TimeSpan StartOf(IReadOnlyList<TimeSpan> durations, int step) =>
        durations.Take(Math.Clamp(step, 0, durations.Count)).Aggregate(TimeSpan.Zero, (sum, d) => sum + d);

    public override IEnumerable<TimeSpan> ProbeTimes(TimeSpan loop)
    {
        var durations = _durations();
        for (int step = 0; step < durations.Count; step++)
        {
            yield return StartOf(durations, step);
        }
    }

    protected override Bitmap? Read(TimeSpan time)
    {
        int step = StepAt(_durations(), time);
        if (step == _shown)
        {
            return null;
        }

        var bitmap = _render(step);
        _shown = step;
        return bitmap;
    }

    protected override void Forget() => _shown = -1;
}
