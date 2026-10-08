using System.Diagnostics;
using System.Drawing.Drawing2D;
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
/// started over: every image from its starting point at once, the soundtrack from its beginning. Used
/// from the UI thread only.
/// </summary>
internal sealed class AnimationPlayer : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<SourceImage, Playback> _playbacks = [];
    private readonly PreviewSound _sound = new();
    private readonly System.Windows.Forms.Timer _soundtrackTimer = new() { Interval = 100 };

    // Short enough for the fade's gain to ramp without audible steps.
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 30 };

    // The images last synced, empty once stopped; the soundtrack and the clock time it started at,
    // null while it does not play.
    private IReadOnlyList<SourceImage> _images = [];
    private Soundtrack? _soundtrack;
    private TimeSpan? _soundtrackStart;
    private SoundFade? _fade;
    private VideoCascade? _cascade;

    public AnimationPlayer()
    {
        _soundtrackTimer.Tick += (_, _) => SyncSoundtrack();
        _fadeTimer.Tick += (_, _) => SyncFade();
    }

    /// <summary>Raised on the UI thread once an image shows a new frame.</summary>
    public event EventHandler<SourceImage>? FrameShown;

    /// <summary>Plays the animated images not playing yet, stops the ones gone or shown still, and mixes the sounds of the videos.</summary>
    public void Sync(IReadOnlyList<SourceImage> images)
    {
        foreach (var (image, playback) in _playbacks.ToList())
        {
            if (!images.Contains(image) || !image.IsAnimated)
            {
                playback.Stop();
                _playbacks.Remove(image);
            }
        }

        foreach (var image in images)
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

                _ = RunAsync(playback);
            }
        }

        _sound.Follow(images);
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
        }
    }

    /// <summary>
    /// Plays every animated image again from its starting point, all at this instant, and the
    /// soundtrack from its beginning (RULES.md). The clock starts over, so its origin stays a whole
    /// second for the images <see cref="Sync"/> starts later; a frozen image stays where it stands, and
    /// the sounds follow their frames by themselves.
    /// </summary>
    public void Restart()
    {
        _clock.Restart();
        foreach (var playback in _playbacks.Values)
        {
            if (playback.PausedAt is null)
            {
                playback.Offset = -playback.Image.StartTime;
            }
        }

        if (_soundtrackStart is not null)
        {
            _soundtrackStart = TimeSpan.Zero;
            SyncSoundtrack();
        }

        SyncFade();
    }

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

        return image.Look.Motion is { } motion && this._images.Contains(image)
            ? Animation.Progress(this.GridTime, motion.Cycle)
            : null;
    }

    /// <summary>Size the frames of <paramref name="image"/> are shown at: larger frames are scaled down off the UI thread.</summary>
    public void SetDisplaySize(SourceImage image, Size size)
    {
        if (_playbacks.TryGetValue(image, out var playback))
        {
            playback.DisplaySize = size;
        }
    }

    /// <summary>The pages were laid out again: the next frame is rendered even if at the same step.</summary>
    public void Refresh(SourceImage image)
    {
        if (_playbacks.TryGetValue(image, out var playback))
        {
            playback.Reader?.Reset();
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
        UpdateSoundtrack();
        UpdateFade();
    }

    public void Dispose()
    {
        Stop();
        _soundtrackTimer.Dispose();
        _fadeTimer.Dispose();
        _sound.Dispose();
    }

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

        var loop = Animation.VideoLength(_images, soundtrack, this._cascade);
        var time = Animation.LoopTime(Animation.LoopTime(_clock.Elapsed - start, loop), soundtrack.Duration);
        _sound.SyncSoundtrack(time, soundtrack.Level);
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

        var loop = Animation.VideoLength(_images, _soundtrack, this._cascade);
        var origin = Animation.GridLength(_images, this._cascade) > TimeSpan.Zero ? TimeSpan.Zero : _soundtrackStart ?? TimeSpan.Zero;
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
            while (!cancellation.IsCancellationRequested && !image.IsDisposed)
            {
                // Content time, within the part the frames effect leaves to play.
                var loop = image.PlayedLength;
                var time = image.ContentTime(this.Position(playback));
                bool playing = playback.PausedAt is null;
                this._sound.Span(image, image.PlayedFrom, image.PlayedFrom + loop);
                _sound.Sync(image, time, playing && this.InTurn(image));

                if (playing && loop > TimeSpan.Zero)
                {
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

                await Task.Delay(Interval, cancellation);
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

        var scaled = new Bitmap(Math.Max(1, (int)Math.Ceiling(frame.Width * scale)), Math.Max(1, (int)Math.Ceiling(frame.Height * scale)));
        using (frame)
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(frame, new Rectangle(Point.Empty, scaled.Size));
        }

        return scaled;
    }

    private sealed class Playback(SourceImage image, TimeSpan offset)
    {
        public SourceImage Image { get; } = image;

        /// <summary>Clock time at which the loop started.</summary>
        public TimeSpan Offset { get; set; } = offset;

        public TimeSpan? PausedAt { get; set; }

        public Size DisplaySize { get; set; }

        public AnimationReader? Reader { get; set; }

        public CancellationTokenSource Cancellation { get; } = new();

        public void Stop() => Cancellation.Cancel();
    }
}
