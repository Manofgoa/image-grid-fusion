using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Plays the animated images of the grid live, on one clock so their steps change together, each
/// from the starting point of its frames effect. Frames are decoded off the UI thread and shown on
/// it; a frozen image stands on its frame; forced still, every image stops where it stands and
/// resumes from there, or from the page it was moved to meanwhile. Also mixes the sounds of its videos,
/// each in step with its frames, and the soundtrack, looping on the grid's duration while the grid
/// holds an image, the whole mix faded in and out on every loop of the grid while the fade is on. With
/// the Cascade on, the videos and animated GIFs play their turns one after the other on the grid's
/// clock (<see cref="CascadeSchedule"/>), standing still and silent between them. The grid can be
/// started over: every image from its starting point at once, the soundtrack from its beginning. It can
/// be paused — by the user, or held while a Copy or an export runs — and moved to any time of the
/// grid's loop, wrapping at its end as the exported video does (workfiles/20261009-playback-controls.md).
/// Used from the UI thread only.
/// </summary>
internal sealed class AnimationPlayer : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    /// <summary>A loop never waits less: a continuation posted at once would starve the paint messages.</summary>
    private static readonly TimeSpan MinimumWait = TimeSpan.FromMilliseconds(1);

    // A scrub snippet: its gain ramps in and out in steps of this timer, and holds this many ticks between.
    private const int SnippetHoldTicks = 6;
    private const double SnippetGainStep = 0.34;

    private readonly PlaybackClock _clock = new();
    private readonly Dictionary<SourceImage, Playback> _playbacks = [];
    private readonly PreviewSound _sound = new();
    private readonly System.Windows.Forms.Timer _soundtrackTimer = new() { Interval = 100 };

    // Short enough for the fade's gain to ramp without audible steps.
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 30 };

    // Brings the clock back to the start of the grid's loop once it reaches its end.
    private readonly System.Windows.Forms.Timer _loopTimer = new() { Interval = 30 };
    private readonly System.Windows.Forms.Timer _snippetTimer = new() { Interval = 15 };

    // Paused by the user; held by a Copy, an export or a drag of the timeline, each released once.
    private bool _paused;
    private int _holds;

    // Held with its readers closed, by a Copy or an export (Suspend): the playbacks stand stopped until Resume.
    private bool _suspended;

    // While a scrub snippet sounds: the ticks left before its gain ramps out.
    private bool _snippet;
    private int _snippetTicks;

    // The images last synced, empty once stopped; the soundtrack and the clock time it started at,
    // null while it does not play.
    private IReadOnlyList<SourceImage> _images = [];

    // The contents of the layers over the grid: they play beside the cells, never taking a turn in the cascade.
    private IReadOnlyList<SourceImage> _layers = [];
    private Soundtrack? _soundtrack;
    private TimeSpan? _soundtrackStart;
    private SoundFade? _fade;
    private VideoCascade? _cascade;

    public AnimationPlayer()
    {
        _soundtrackTimer.Tick += (_, _) => SyncSoundtrack();
        _fadeTimer.Tick += (_, _) => SyncFade();
        this._loopTimer.Tick += (_, _) => this.WrapLoop();
        this._snippetTimer.Tick += (_, _) => this.OnSnippetTick();
        this._loopTimer.Start();
    }

    /// <summary>Raised on the UI thread once an image shows a new frame.</summary>
    public event EventHandler<SourceImage>? FrameShown;

    /// <summary>Raised on the UI thread when the grid is paused, resumes, is held or released, or its time jumps.</summary>
    public event EventHandler? PlaybackChanged;

    /// <summary>Paused by the user: the clock stands still, the sound is silent, until unpaused.</summary>
    public bool Paused
    {
        get => this._paused;
        set
        {
            if (this._paused != value)
            {
                this._paused = value;
                this.ApplyRunning();
            }
        }
    }

    /// <summary>Whether the grid plays: neither paused by the user nor held.</summary>
    public bool Running => !this._paused && this._holds == 0;

    /// <summary>
    /// The grid's loop — the video length (RULES.md § Video Length), the Cascade and the soundtrack
    /// included — or zero without one: the length of the timeline.
    /// </summary>
    public TimeSpan LoopLength => this._images.Count == 0 ? TimeSpan.Zero : Animation.VideoLength(this._images, this._soundtrack, this._cascade, this._layers);

    /// <summary>Where the grid stands in its loop: the time the exported video shows the same instant at.</summary>
    public TimeSpan LoopPosition => Animation.LoopTime(this.GridTime, this.LoopLength);

    /// <summary>Stands the grid still until <see cref="Release"/>, whatever the user's pause; holds add up.</summary>
    public void Hold()
    {
        this._holds++;
        if (this._holds == 1)
        {
            this.ApplyRunning();
        }
    }

    /// <summary>Ends one <see cref="Hold"/>: the last one gives the grid back to the user's play or pause.</summary>
    public void Release()
    {
        if (this._holds == 0)
        {
            return;
        }

        this._holds--;
        if (this._holds == 0)
        {
            this.ApplyRunning();
        }
    }

    /// <summary>
    /// Holds the grid and closes its readers: every playback loop is cancelled, so its reader is released
    /// where a decode may be in progress, the images keeping their shown frame and their position; the
    /// sound's voices are released too. What a Copy or an export does, so its own readers are the only
    /// ones open (workfiles/20261010-optimisation-lack-of-memory.md § 2.1). Ended by <see cref="Resume"/>.
    /// </summary>
    public void Suspend()
    {
        this.Hold();
        if (this._suspended)
        {
            return;
        }

        this._suspended = true;
        foreach (var playback in this._playbacks.Values)
        {
            playback.Stop();
        }

        this._sound.Follow([]);
        this._sound.FollowSoundtrack(null);
    }

    /// <summary>
    /// Ends a <see cref="Suspend"/>: every playback starts again where it stood — its reader opened anew,
    /// the frame there decoded once — the sound's voices back; then the hold is released.
    /// </summary>
    public void Resume()
    {
        if (this._suspended)
        {
            this._suspended = false;
            foreach (var (image, stopped) in this._playbacks.ToList())
            {
                stopped.Stop();
                var playback = new Playback(image, stopped.Offset) { PausedAt = stopped.PausedAt, DisplaySize = stopped.DisplaySize };
                this._playbacks[image] = playback;
                _ = this.RunAsync(playback);
            }

            this._sound.Follow([.. this._images, .. this._layers]);
            this.UpdateSoundtrack();
        }

        this.Release();
    }

    /// <summary>
    /// Moves the grid to <paramref name="time"/> of its loop, as the exported video shows it: every
    /// playing image from its starting point at the clock's origin, the soundtrack from its beginning;
    /// paused, the frames there are shown once. A frozen image stays where it stands.
    /// </summary>
    public void Seek(TimeSpan time)
    {
        this._clock.Set(time < TimeSpan.Zero ? TimeSpan.Zero : time);
        foreach (var playback in this._playbacks.Values)
        {
            if (playback.PausedAt is null)
            {
                playback.Offset = -playback.Image.StartTime;
            }

            playback.Pending = true;
        }

        if (this._soundtrackStart is not null)
        {
            this._soundtrackStart = TimeSpan.Zero;
            this.SyncSoundtrack();
        }

        this.SyncFade();
        this.PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the grid to <paramref name="time"/> while the timeline is dragged, the grid held: the mix
    /// heard there sounds for a short snippet, faded in and out, the next scrub step starting another.
    /// </summary>
    public void Scrub(TimeSpan time)
    {
        this.Seek(time);
        if (!this._snippet)
        {
            this._snippet = true;
            this._sound.ScrubGain = 0;
        }

        this._snippetTicks = SnippetHoldTicks;
        this._snippetTimer.Start();
        foreach (var playback in this._playbacks.Values)
        {
            this.SyncSound(playback, playback.Image.ContentTime(this.Position(playback)));
        }

        this.SyncSoundtrack();
    }

    /// <summary>
    /// Plays the animated images not playing yet — the cells' <paramref name="images"/> and the contents of the
    /// <paramref name="layers"/> —, stops the ones gone or shown still, and mixes the sounds of the videos.
    /// </summary>
    public void Sync(IReadOnlyList<SourceImage> images, IReadOnlyList<SourceImage>? layers = null)
    {
        this._layers = layers ?? [];
        var all = images.Concat(this._layers).ToList();
        foreach (var (image, playback) in _playbacks.ToList())
        {
            if (!all.Contains(image) || !image.IsAnimated)
            {
                playback.Stop();
                _playbacks.Remove(image);
            }
        }

        foreach (var image in all)
        {
            if (image.IsAnimated && !_playbacks.ContainsKey(image))
            {
                // Started on a whole second of the clock, so steps change together in every cell.
                var start = TimeSpan.FromSeconds(Math.Floor(_clock.Elapsed.TotalSeconds)) - image.StartTime;
                var playback = new Playback(image, start);
                _playbacks[image] = playback;
                if (image.IsFrozen)
                {
                    StandAtStart(playback);
                }

                // Suspended, its loop starts with the others, on Resume.
                if (!this._suspended)
                {
                    _ = RunAsync(playback);
                }
            }
        }

        _sound.Follow(all);
        _images = images;
        UpdateSoundtrack();
        UpdateFade();
    }

    /// <summary>Fades the whole mix in and out on every loop of the grid with <paramref name="fade"/>, or no more when <c>null</c>.</summary>
    public void SetFade(SoundFade? fade)
    {
        _fade = fade;
        UpdateFade();
    }

    /// <summary>
    /// Plays the videos and animated GIFs one after the other with <paramref name="cascade"/>, or all at
    /// once when <c>null</c>; the caller starts the grid over, so the first one plays at once.
    /// </summary>
    public void SetCascade(VideoCascade? cascade)
    {
        this._cascade = cascade;
        this.SyncSoundtrack();
        this.SyncFade();
    }

    /// <summary>Whether the Cascade is on with a content taking part: turns follow each other, pauses included.</summary>
    public bool Cascading => this._cascade is not null && this._images.Any(CascadeSchedule.TakesPart);

    /// <summary>
    /// Mixes <paramref name="soundtrack"/> over the videos, or stops it when <c>null</c>; another file
    /// plays from its start, a new level applies where it plays.
    /// </summary>
    public void SetSoundtrack(Soundtrack? soundtrack)
    {
        if (soundtrack?.Path != _soundtrack?.Path)
        {
            _soundtrackStart = null;
        }

        _soundtrack = soundtrack;
        UpdateSoundtrack();
        SyncFade();
    }

    /// <summary>
    /// The frames effect of <paramref name="image"/> changed: frozen, it stands on the frame the
    /// effect points at; else it plays again from that starting point.
    /// </summary>
    public void Update(SourceImage image)
    {
        if (_playbacks.TryGetValue(image, out var playback))
        {
            if (image.IsFrozen)
            {
                StandAtStart(playback);
            }
            else
            {
                playback.PausedAt = null;
                playback.Offset = _clock.Elapsed - image.StartTime;
                playback.Reader?.Reset();
            }

            playback.Pending = true;
        }
    }

    /// <summary>
    /// Plays every animated image again from its starting point, all at this instant, and the
    /// soundtrack from its beginning (RULES.md). The clock starts over, so its origin stays a whole
    /// second for the images <see cref="Sync"/> starts later; a frozen image stays where it stands, and
    /// the sounds follow their frames by themselves. Paused, the grid stays paused, at its start.
    /// </summary>
    public void Restart() => this.Seek(TimeSpan.Zero);

    /// <summary>
    /// The clock of the grid, from its last start over: the time the motions of the Animations effects
    /// are drawn at, as the export draws them from its first frame.
    /// </summary>
    public TimeSpan GridTime => this._clock.Elapsed;

    /// <summary>
    /// Share of its loop <paramref name="image"/> has played, from 0 up to 1 excluded, read from the
    /// clock at the moment of the call — the preview's progress line samples it at each paint. A still,
    /// or a frozen image, moved by its Animations effect plays the motion's cycle on the grid's clock.
    /// <c>null</c> while the image does not play nor move, or the player is stopped.
    /// </summary>
    public double? ProgressOf(SourceImage image)
    {
        if (this._playbacks.TryGetValue(image, out var playback) && playback.PausedAt is null && image.Pages is not null)
        {
            // In the cascade, the line shows the turn being played only.
            if (this.TurnOf(image) is var (schedule, index))
            {
                return schedule.Plays(index, this.GridTime)
                    ? Animation.Progress(schedule.Played(index, this.GridTime), image.PlayedLength)
                    : null;
            }

            return Animation.Progress(this.Position(playback), image.PlayedLength);
        }

        return image.Look.Motion is { } motion && (this._images.Contains(image) || this._layers.Contains(image))
            ? Animation.Progress(this.GridTime, motion.Cycle)
            : null;
    }

    /// <summary>Size the frames of <paramref name="image"/> are shown at: larger frames are scaled down off the UI thread.</summary>
    public void SetDisplaySize(SourceImage image, Size size)
    {
        if (_playbacks.TryGetValue(image, out var playback))
        {
            if (playback.DisplaySize != size)
            {
                playback.Pending = true;
            }

            playback.DisplaySize = size;
        }
    }

    /// <summary>The pages were laid out again: the next frame is rendered even if at the same step.</summary>
    public void Refresh(SourceImage image)
    {
        if (_playbacks.TryGetValue(image, out var playback))
        {
            playback.Reader?.Reset();
            playback.Pending = true;
        }
    }

    /// <summary>
    /// Stops every image and the sound; each cell keeps the frame it shows. The next <see cref="Sync"/>
    /// plays them again from the start.
    /// </summary>
    public void Stop()
    {
        foreach (var playback in _playbacks.Values)
        {
            playback.Stop();
        }

        _playbacks.Clear();
        _sound.Follow([]);
        _images = [];
        this._layers = [];
        UpdateSoundtrack();
        UpdateFade();
    }

    public void Dispose()
    {
        Stop();
        _soundtrackTimer.Dispose();
        _fadeTimer.Dispose();
        this._loopTimer.Dispose();
        this._snippetTimer.Dispose();
        _sound.Dispose();
    }

    /// <summary>The clock runs while the grid plays, the sound follows, and the transport is told.</summary>
    private void ApplyRunning()
    {
        if (this.Running)
        {
            this.EndSnippet();
            this._clock.Resume();
        }
        else
        {
            this._clock.Pause();
        }

        foreach (var playback in this._playbacks.Values)
        {
            this.SyncSound(playback, playback.Image.ContentTime(this.Position(playback)));
        }

        this.SyncSoundtrack();
        this.PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Once the clock reaches the end of the grid's loop, the grid starts its loop again, as the exported video loops.</summary>
    private void WrapLoop()
    {
        var loop = this.LoopLength;
        if (this.Running && loop > TimeSpan.Zero && this.GridTime >= loop)
        {
            this.Seek(Animation.LoopTime(this.GridTime, loop));
        }
    }

    /// <summary>A scrub snippet ramps in, holds, then ramps out and stops its sounds.</summary>
    private void OnSnippetTick()
    {
        if (this._snippetTicks > 0)
        {
            this._snippetTicks--;
            this._sound.ScrubGain += SnippetGainStep;
            return;
        }

        this._sound.ScrubGain -= SnippetGainStep;
        if (this._sound.ScrubGain <= 0)
        {
            this.EndSnippet();
        }
    }

    /// <summary>Stops a scrub snippet: its sounds go silent as the grid stands, the gain back to full.</summary>
    private void EndSnippet()
    {
        if (!this._snippet)
        {
            return;
        }

        this._snippet = false;
        this._snippetTimer.Stop();
        foreach (var playback in this._playbacks.Values)
        {
            this.SyncSound(playback, playback.Image.ContentTime(this.Position(playback)));
        }

        this.SyncSoundtrack();
        this._sound.ScrubGain = 1;
    }

    /// <summary>
    /// The sound of <paramref name="playback"/> at <paramref name="time"/> of its content: heard while
    /// it plays — not frozen, the grid running or a scrub snippet sounding — and, in the cascade, in its turn.
    /// </summary>
    private void SyncSound(Playback playback, TimeSpan time) =>
        this._sound.Sync(playback.Image, time, playback.PausedAt is null && (this.Running || this._snippet) && this.InTurn(playback.Image));

    /// <summary>The soundtrack plays while the grid holds an image; stopped, it starts again on a whole second of the clock.</summary>
    private void UpdateSoundtrack()
    {
        bool plays = _soundtrack is not null && _images.Count > 0;
        _sound.FollowSoundtrack(plays ? _soundtrack!.Path : null);
        if (!plays)
        {
            _soundtrackStart = null;
            _soundtrackTimer.Stop();
            return;
        }

        _soundtrackStart ??= TimeSpan.FromSeconds(Math.Floor(_clock.Elapsed.TotalSeconds));
        _soundtrackTimer.Start();
        SyncSoundtrack();
    }

    /// <summary>
    /// Keeps the soundtrack on the grid's loop — the longest playing content, else its own length —
    /// looping within it when shorter, cut at its end when longer.
    /// </summary>
    private void SyncSoundtrack()
    {
        if (_soundtrack is not { } soundtrack || _soundtrackStart is not { } start)
        {
            return;
        }

        var loop = Animation.VideoLength(_images, soundtrack, this._cascade, this._layers);
        var time = Animation.LoopTime(Animation.LoopTime(_clock.Elapsed - start, loop), soundtrack.Duration);
        _sound.SyncSoundtrack(time, soundtrack.Level, this.Running || this._snippet);
    }

    /// <summary>The fade follows the grid while it holds an image; without it, the mix plays as it is.</summary>
    private void UpdateFade()
    {
        if (_fade is null || _images.Count == 0)
        {
            _fadeTimer.Stop();
            _sound.MasterGain = 1;
            return;
        }

        _fadeTimer.Start();
        SyncFade();
    }

    /// <summary>
    /// The fade's gain at the grid's time within its loop — the video length (RULES.md § Video Length),
    /// so the preview sounds like the export: timed from the clock's origin, where the grid started
    /// over, or from the soundtrack's start over a grid of stills, the soundtrack alone giving it a length.
    /// </summary>
    private void SyncFade()
    {
        if (_fade is not { } fade || _images.Count == 0)
        {
            return;
        }

        var loop = Animation.VideoLength(_images, _soundtrack, this._cascade, this._layers);
        var origin = Animation.GridLength(_images, this._cascade, this._layers) > TimeSpan.Zero ? TimeSpan.Zero : _soundtrackStart ?? TimeSpan.Zero;
        _sound.MasterGain = fade.GainAt(Animation.LoopTime(_clock.Elapsed - origin, loop), loop);
    }

    /// <summary>
    /// Where <paramref name="playback"/> stands in its loop, its starting point included: where it was
    /// paused, else where its turn of the cascade has brought it, else where the clock has.
    /// </summary>
    private TimeSpan Position(Playback playback)
    {
        if (playback.PausedAt is { } paused)
        {
            return paused;
        }

        return this.TurnOf(playback.Image) is var (schedule, index)
            ? playback.Image.StartTime + schedule.Played(index, this.GridTime)
            : this._clock.Elapsed - playback.Offset;
    }

    /// <summary>The cascade <paramref name="image"/> takes part in and its cell, or <c>null</c> while it plays on its own.</summary>
    private (CascadeSchedule Schedule, int Index)? TurnOf(SourceImage image)
    {
        if (this._cascade is not { } cascade)
        {
            return null;
        }

        for (int i = 0; i < this._images.Count; i++)
        {
            if (this._images[i] == image)
            {
                var schedule = CascadeSchedule.Of(this._images, cascade);
                return schedule.Has(i) ? (schedule, i) : null;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="image"/> is heard now: always on its own, during its turn only in the cascade.</summary>
    private bool InTurn(SourceImage image) =>
        this.TurnOf(image) is not var (schedule, index) || schedule.Plays(index, this.GridTime);

    private static void StandAtStart(Playback playback)
    {
        playback.PausedAt = playback.Image.StartTime;
    }

    private async Task RunAsync(Playback playback)
    {
        var image = playback.Image;
        var pages = image.Pages!;
        var cancellation = playback.Cancellation.Token;
        try
        {
            playback.Reader = await Task.Run(pages.OpenAnimation, cancellation);
            var pace = Stopwatch.StartNew();
            var next = Interval;
            while (!cancellation.IsCancellationRequested && !image.IsDisposed)
            {
                // Content time, within the part the frames effect leaves to play.
                var loop = image.PlayedLength;
                var time = image.ContentTime(this.Position(playback));
                bool playing = playback.PausedAt is null;
                this._sound.Span(image, image.PlayedFrom, image.PlayedFrom + loop);
                this.SyncSound(playback, time);

                // The grid paused, a frame is decoded only where it was moved or laid out again.
                if (playing && loop > TimeSpan.Zero && (this.Running || playback.Pending))
                {
                    playback.Pending = false;
                    var reader = playback.Reader;
                    var size = playback.DisplaySize;
                    var frame = await Task.Run(() => ScaleDown(reader.FrameAt(time), size), cancellation);
                    if (frame is not null)
                    {
                        if (cancellation.IsCancellationRequested || image.IsDisposed || playback.PausedAt is not null)
                        {
                            frame.Dispose();
                        }
                        else
                        {
                            image.ShowFrame(pages.PageAt(time), frame);
                            FrameShown?.Invoke(this, image);
                        }
                    }
                }

                // Paced on a deadline, so the decode, the scaling and the redraw do not add to the interval:
                // about 30 frames a second when the work fits in it. Late by more than an interval, the
                // deadline starts over from now — a slow source drops frames rather than bursting
                // (workfiles/20261010-optimisation-lack-of-memory.md § 4.1).
                next += Interval;
                var wait = next - pace.Elapsed;
                if (wait < -Interval)
                {
                    next = pace.Elapsed;
                    wait = TimeSpan.Zero;
                }

                await Task.Delay(wait < MinimumWait ? MinimumWait : wait, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // A source that fails to decode stops playing and keeps the frame it shows.
        }
        finally
        {
            _sound.Sync(image, TimeSpan.Zero, playing: false);

            // Released on the thread pool, where Media Foundation objects live.
            if (playback.Reader is { } reader)
            {
                _ = Task.Run(reader.Dispose);
            }
        }
    }

    /// <summary>
    /// Scales a frame down to just cover <paramref name="size"/> (the cell it fills), so the UI thread
    /// only draws it about 1:1; smaller frames are kept as they are.
    /// </summary>
    private static Bitmap? ScaleDown(Bitmap? frame, Size size)
    {
        if (frame is null || size.Width <= 0 || size.Height <= 0)
        {
            return frame;
        }

        double scale = Math.Max(size.Width / (double)frame.Width, size.Height / (double)frame.Height);
        if (scale >= 0.75)
        {
            return frame;
        }

        using (frame)
        {
            // The frame is disposed whatever happens: a copy refused for want of memory leaves nothing behind.
            // Premultiplied, as GDI+ blends: drawn at every frame, a straight-alpha bitmap would be converted each time.
            var scaled = new Bitmap(Math.Max(1, (int)Math.Ceiling(frame.Width * scale)), Math.Max(1, (int)Math.Ceiling(frame.Height * scale)), PixelFormat.Format32bppPArgb);
            try
            {
                using var g = Graphics.FromImage(scaled);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(frame, new Rectangle(Point.Empty, scaled.Size));
                return scaled;
            }
            catch
            {
                scaled.Dispose();
                throw;
            }
        }
    }

    private sealed class Playback(SourceImage image, TimeSpan offset)
    {
        public SourceImage Image { get; } = image;

        /// <summary>Clock time at which the loop started.</summary>
        public TimeSpan Offset { get; set; } = offset;

        public TimeSpan? PausedAt { get; set; }

        /// <summary>A frame to show though the grid is paused: just started, moved, or laid out again.</summary>
        public bool Pending { get; set; } = true;

        public Size DisplaySize { get; set; }

        public AnimationReader? Reader { get; set; }

        public CancellationTokenSource Cancellation { get; } = new();

        public void Stop() => Cancellation.Cancel();
    }
}
