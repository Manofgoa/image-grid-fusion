using ImageGridFusion.Composition;
using Windows.Media.Editing;
using Windows.Storage;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Frames of a video, decoded by Windows (Media Foundation, through <see cref="MediaComposition"/>),
/// one page per frame at the file's frame rate, so the Frames effect can stand on any of them
/// (workfiles/20261008-video-trim.md). A codec Windows lacks
/// (HEVC without its extension, some mkv / avi) makes the file fall back to its thumbnail, if any.
/// Played frame by frame by a <see cref="VideoReader"/>.
/// </summary>
public sealed class VideoFrames : PageSource, IHasSound
{
    /// <summary>A file giving no frame rate, or an absurd one, is counted at the export's.</summary>
    private const double DefaultFrameRate = Animation.FramesPerSecond;

    /// <summary>A frame's time read back lands on that frame despite the ticks' rounding.</summary>
    private const double FrameTolerance = 1e-4;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm", ".3gp", ".3g2", ".mpg", ".mpeg", ".ts", ".m2ts", ".mts",
    };

    private readonly string _path;
    private readonly MediaComposition _composition;
    private readonly TimeSpan _duration;
    private readonly double _frameRate;
    private readonly int _width;
    private readonly int _height;

    private VideoFrames(string path, MediaComposition composition, TimeSpan duration, double frameRate, int width, int height, bool hasSound)
    {
        _path = path;
        _composition = composition;
        _duration = duration;
        _frameRate = frameRate;
        _width = width;
        _height = height;
        Count = Frames(duration, frameRate);
        HasSound = hasSound;
    }

    public override int Count { get; }

    public bool HasSound { get; }

    public override TimeSpan LoopDuration => _duration;

    public override bool HasFrames => true;

    public override double? FrameRate => _frameRate;

    public override AnimationReader OpenAnimation() => VideoReader.Open(_path);

    public override int PageAt(TimeSpan time) => Math.Clamp((int)Math.Floor(time.TotalSeconds * _frameRate + FrameTolerance), 0, Count - 1);

    public override TimeSpan TimeOf(int page) => Count == 1 ? TimeSpan.Zero : FrameTime(page);

    /// <summary>The frame nearest to 10 % of the duration, past the usual black intro frames.</summary>
    public override int InitialPage => Math.Clamp((int)Math.Round(_duration.TotalSeconds * 0.1 * _frameRate), 0, Count - 1);

    public override string Label(int page) => Time(page).ToString(_duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

    /// <summary>Frames k / rate, 0 ≤ k &lt; count, starting within the duration; a single one for a duration too short.</summary>
    public static int Frames(TimeSpan duration, double frameRate) => Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * frameRate - FrameTolerance));

    /// <summary>The rate a file gives, or the export's when it gives none or an absurd one.</summary>
    private static double RateOf(uint numerator, uint denominator) =>
        numerator == 0 || denominator == 0 || numerator / (double)denominator is < 1 or > 1000
            ? DefaultFrameRate
            : numerator / (double)denominator;

    /// <summary>Returns null when the file is not a video Windows can decode.</summary>
    public static VideoFrames? TryOpen(string path)
    {
        if (!Extensions.Contains(Path.GetExtension(path)))
        {
            return null;
        }

        try
        {
            var file = StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask().GetAwaiter().GetResult();
            var clip = MediaClip.CreateFromFileAsync(file).AsTask().GetAwaiter().GetResult();
            var properties = clip.GetVideoEncodingProperties();
            if (clip.OriginalDuration <= TimeSpan.Zero || properties.Width == 0 || properties.Height == 0)
            {
                return null;
            }

            var composition = new MediaComposition();
            composition.Clips.Add(clip);
            var rate = RateOf(properties.FrameRate.Numerator, properties.FrameRate.Denominator);
            return new VideoFrames(path, composition, clip.OriginalDuration, rate, (int)properties.Width, (int)properties.Height, clip.EmbeddedAudioTracks.Count > 0);
        }
        catch (Exception)
        {
            // WinRT reports missing codecs and unreadable files with assorted exception types.
            return null;
        }
    }

    /// <summary>The exact frame: at the nearest key frame, neighbour positions would repeat the same image.</summary>
    public override Bitmap Render(int page)
    {
        using var frame = _composition
            .GetThumbnailAsync(Time(page), _width, _height, VideoFramePrecision.NearestFrame)
            .AsTask().GetAwaiter().GetResult();
        using var image = Image.FromStream(frame.AsStream());
        return ImageLoader.Copy(image);
    }

    /// <summary>A lone frame shows the 10 % mark rather than the first one.</summary>
    private TimeSpan Time(int page) => Count == 1 ? _duration * 0.1 : FrameTime(page);

    private TimeSpan FrameTime(int page) => TimeSpan.FromTicks((long)Math.Round(page * TimeSpan.TicksPerSecond / _frameRate));
}
