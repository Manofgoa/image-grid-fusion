using ImageGridFusion.Composition;
using Windows.Media.Editing;
using Windows.Storage;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Frames of a video, decoded by Windows (Media Foundation, through <see cref="MediaComposition"/>),
/// at 100 positions 1 % of the duration apart. A codec Windows lacks
/// (HEVC without its extension, some mkv / avi) makes the file fall back to its thumbnail, if any.
/// Played frame by frame by a <see cref="VideoReader"/>.
/// </summary>
public sealed class VideoFrames : PageSource, IHasSound
{
    private const int MaxPositions = 100;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm", ".3gp", ".3g2", ".mpg", ".mpeg", ".ts", ".m2ts", ".mts",
    };

    private readonly string _path;
    private readonly MediaComposition _composition;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _step;
    private readonly int _width;
    private readonly int _height;

    private VideoFrames(string path, MediaComposition composition, TimeSpan duration, int width, int height, bool hasSound)
    {
        _path = path;
        _composition = composition;
        _duration = duration;
        _width = width;
        _height = height;
        _step = Step(duration);
        Count = Positions(duration);
        HasSound = hasSound;
    }

    public override int Count { get; }

    public bool HasSound { get; }

    public override TimeSpan LoopDuration => _duration;

    public override AnimationReader OpenAnimation() => VideoReader.Open(_path);

    public override int PageAt(TimeSpan time) => Math.Clamp((int)(time / _step), 0, Count - 1);

    public override TimeSpan TimeOf(int page) => Count == 1 ? TimeSpan.Zero : _step * page;

    /// <summary>The position nearest to 10 % of the duration, past the usual black intro frames.</summary>
    public override int InitialPage => Math.Min(Count - 1, (int)Math.Round(_duration * 0.1 / _step));

    public override string Label(int page) => Time(page).ToString(_duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

    public static TimeSpan Step(TimeSpan duration) => duration / MaxPositions;

    /// <summary>Positions k × step, 0 ≤ k &lt; count: 100, or a single one for a duration too short to split.</summary>
    public static int Positions(TimeSpan duration) => Math.Clamp((int)Math.Floor(duration / Step(duration)), 1, MaxPositions);

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
            return new VideoFrames(path, composition, clip.OriginalDuration, (int)properties.Width, (int)properties.Height, clip.EmbeddedAudioTracks.Count > 0);
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

    /// <summary>A lone position shows the 10 % mark rather than the first frame.</summary>
    private TimeSpan Time(int page) => Count == 1 ? _duration * 0.1 : _step * page;
}
