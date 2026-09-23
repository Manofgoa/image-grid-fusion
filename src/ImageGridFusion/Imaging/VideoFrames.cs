using ImageGridFusion.Composition;
using Windows.Media.Editing;
using Windows.Storage;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Frames of a video, decoded by Windows (Media Foundation, through <see cref="MediaComposition"/>),
/// at positions a coarse step apart: duration / 100, never under a second. A codec Windows lacks
/// (HEVC without its extension, some mkv / avi) makes the file fall back to its thumbnail, if any.
/// </summary>
public sealed class VideoFrames : PageSource
{
    private const int MaxPositions = 100;

    private static readonly TimeSpan MinStep = TimeSpan.FromSeconds(1);

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm", ".3gp", ".3g2", ".mpg", ".mpeg", ".ts", ".m2ts", ".mts",
    };

    private readonly MediaComposition _composition;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _step;
    private readonly int _width;
    private readonly int _height;

    private VideoFrames(MediaComposition composition, TimeSpan duration, int width, int height)
    {
        _composition = composition;
        _duration = duration;
        _width = width;
        _height = height;
        _step = Step(duration);
        Count = Positions(duration);
    }

    public override int Count { get; }

    /// <summary>The position nearest to 10 % of the duration, past the usual black intro frames.</summary>
    public override int InitialPage => Math.Min(Count - 1, (int)Math.Round(_duration * 0.1 / _step));

    public override string Label(int page) => Time(page).ToString(_duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

    public static TimeSpan Step(TimeSpan duration) => duration / MaxPositions < MinStep ? MinStep : duration / MaxPositions;

    /// <summary>Positions k × step, 0 ≤ k &lt; count: at most 100, and a single one under two steps.</summary>
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
            return new VideoFrames(composition, clip.OriginalDuration, (int)properties.Width, (int)properties.Height);
        }
        catch (Exception)
        {
            // WinRT reports missing codecs and unreadable files with assorted exception types.
            return null;
        }
    }

    public override Bitmap Render(int page)
    {
        using var frame = _composition
            .GetThumbnailAsync(Time(page), _width, _height, VideoFramePrecision.NearestKeyFrame)
            .AsTask().GetAwaiter().GetResult();
        using var image = Image.FromStream(frame.AsStream());
        return ImageLoader.Copy(image);
    }

    /// <summary>A lone position shows the 10 % mark rather than the first frame.</summary>
    private TimeSpan Time(int page) => Count == 1 ? _duration * 0.1 : _step * page;
}
