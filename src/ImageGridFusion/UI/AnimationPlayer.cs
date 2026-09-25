using System.Diagnostics;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Plays the animated images of the grid live, on one clock so their steps change together, each
/// from the starting point of its frames effect. Frames are decoded off the UI thread and shown on
/// it; a frozen image stands on its frame; forced still, every image stops where it stands and
/// resumes from there, or from the page it was moved to meanwhile. Also mixes the sounds of its videos,
/// each in step with its frames. Used from the UI thread only.
/// </summary>
internal sealed class AnimationPlayer : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<SourceImage, Playback> _playbacks = [];
    private readonly PreviewSound _sound = new();

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
    }

    public void Dispose()
    {
        Stop();
        _sound.Dispose();
    }

    private TimeSpan Position(Playback playback) => playback.PausedAt ?? _clock.Elapsed - playback.Offset;

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
                var loop = pages.LoopDuration;
                var time = Animation.LoopTime(Position(playback), loop);
                bool playing = playback.PausedAt is null;
                _sound.Sync(image, time, playing);

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
