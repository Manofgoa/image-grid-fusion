using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Frames of an animated GIF, decoded by GDI+, each shown for the delay the file gives it. A GIF with
/// a single frame is left to the plain image decoder. The file is read into memory, so it does not
/// stay locked.
/// </summary>
public sealed class GifFrames : PageSource
{
    private const int FrameDelayTag = 0x5100;

    /// <summary>Browsers stretch delays of 0 or 10 ms to 100 ms: many GIFs rely on it.</summary>
    private static readonly TimeSpan ShortestDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan StretchedDelay = TimeSpan.FromMilliseconds(100);

    private readonly MemoryStream _stream;
    private readonly Image _image;
    private readonly TimeSpan[] _delays;

    private GifFrames(MemoryStream stream, Image image, TimeSpan[] delays)
    {
        _stream = stream;
        _image = image;
        _delays = delays;
    }

    public override int Count => _delays.Length;

    public override string Label(int page) => $"{page + 1} / {Count}";

    public override TimeSpan LoopDuration => _delays.Aggregate(TimeSpan.Zero, (sum, d) => sum + d);

    public override AnimationReader OpenAnimation() => new StepReader(() => _delays, Render);

    public override int PageAt(TimeSpan time) => StepReader.StepAt(_delays, time);

    public override TimeSpan TimeOf(int page) => StepReader.StartOf(_delays, page);

    /// <summary>Returns null unless the file is a GIF GDI+ can decode with at least two frames.</summary>
    public static GifFrames? TryOpen(string path)
    {
        MemoryStream? stream = null;
        Image? image = null;
        try
        {
            using (var file = File.OpenRead(path))
            {
                var head = new byte[6];
                if (file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) < head.Length || !head.AsSpan().StartsWith("GIF8"u8))
                {
                    return null;
                }
            }

            stream = new MemoryStream(File.ReadAllBytes(path));
            image = Image.FromStream(stream);
            int count = image.GetFrameCount(FrameDimension.Time);
            if (count < 2)
            {
                image.Dispose();
                stream.Dispose();
                return null;
            }

            return new GifFrames(stream, image, Delays(image, count));
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException or ExternalException)
        {
            image?.Dispose();
            stream?.Dispose();
            return null;
        }
    }

    /// <summary>Serialized: the GIF's active frame is shared by the slider and the animation readers.</summary>
    public override Bitmap Render(int page)
    {
        lock (_image)
        {
            _image.SelectActiveFrame(FrameDimension.Time, Math.Clamp(page, 0, Count - 1));
            return ImageLoader.Copy(_image);
        }
    }

    public override void Dispose()
    {
        _image.Dispose();
        _stream.Dispose();
    }

    /// <summary>Delays of the frames, stored in hundredths of a second, one 32-bit value per frame.</summary>
    private static TimeSpan[] Delays(Image image, int count)
    {
        byte[]? values = image.PropertyIdList.Contains(FrameDelayTag) ? image.GetPropertyItem(FrameDelayTag)?.Value : null;
        var delays = new TimeSpan[count];
        for (int i = 0; i < count; i++)
        {
            var delay = values is not null && values.Length >= (i + 1) * 4
                ? TimeSpan.FromMilliseconds(BitConverter.ToInt32(values, i * 4) * 10)
                : StretchedDelay;
            delays[i] = delay < ShortestDelay ? StretchedDelay : delay;
        }

        return delays;
    }
}
