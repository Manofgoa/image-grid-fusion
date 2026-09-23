using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Decodes a video frame after frame with the Media Foundation Source Reader, converted to 32-bit RGB:
/// fast when read forward, as the live preview and the video export do. Going back (a loop starting
/// over) or far ahead seeks to the key frame before the time asked for.
/// </summary>
internal sealed class VideoReader : AnimationReader
{
    /// <summary>Beyond this jump forward, seeking beats decoding every frame in between.</summary>
    private static readonly TimeSpan SeekAhead = TimeSpan.FromSeconds(2);

    private readonly IMFSourceReader _reader;
    private Format _format;

    /// <summary>Sample read ahead: the first one past the time last asked for.</summary>
    private IMFSample? _ahead;
    private long _aheadTime;
    private bool _ended;

    /// <summary>Time of the frame returned last; <c>null</c> when none since the last seek.</summary>
    private long? _shownTime;

    private VideoReader(IMFSourceReader reader)
    {
        _reader = reader;
        _format = ReadFormat();
    }

    public static VideoReader Open(string path)
    {
        var attributes = MediaFoundation.CreateAttributes();
        IMFSourceReader? reader = null;
        try
        {
            attributes.SetUINT32(MediaFoundation.Keys.EnableVideoProcessing, 1);
            reader = MediaFoundation.CreateSourceReader(path, attributes);
            reader.SetStreamSelection(MediaFoundation.AllStreams, false);
            reader.SetStreamSelection(MediaFoundation.FirstVideoStream, true);

            var type = MediaFoundation.CreateMediaType();
            try
            {
                type.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Video);
                type.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.Rgb32);
                Marshal.ThrowExceptionForHR(reader.SetCurrentMediaType(MediaFoundation.FirstVideoStream, IntPtr.Zero, type));
            }
            finally
            {
                MediaFoundation.Release(type);
            }

            return new VideoReader(reader);
        }
        catch
        {
            MediaFoundation.Release(reader);
            throw;
        }
        finally
        {
            MediaFoundation.Release(attributes);
        }
    }

    public override void Dispose()
    {
        MediaFoundation.Release(_ahead);
        _ahead = null;
        MediaFoundation.Release(_reader);
    }

    protected override Bitmap? Read(TimeSpan time)
    {
        long target = time.Ticks;
        if (_shownTime is { } shown && (target < shown || target - shown > SeekAhead.Ticks))
        {
            Seek(target);
        }

        // The frame shown at the target is the last one starting at or before it.
        IMFSample? candidate = null;
        long candidateTime = 0;
        try
        {
            while (true)
            {
                if (_ahead is null && !ReadAhead())
                {
                    break;
                }

                if (_aheadTime > target && (_shownTime is not null || candidate is not null))
                {
                    break;
                }

                MediaFoundation.Release(candidate);
                candidate = _ahead;
                candidateTime = _aheadTime;
                _ahead = null;
            }

            if (candidate is null)
            {
                return null;
            }

            _shownTime = candidateTime;
            return ToBitmap(candidate);
        }
        finally
        {
            MediaFoundation.Release(candidate);
        }
    }

    protected override void Forget() => Seek(_shownTime ?? 0);

    private void Seek(long target)
    {
        MediaFoundation.Release(_ahead);
        _ahead = null;
        _ended = false;
        _shownTime = null;
        _reader.SetCurrentPosition(Guid.Empty, PropVariant.FromLong(Math.Max(0, target)));
    }

    /// <summary>Reads the next frame into <see cref="_ahead"/>; false at the end of the video.</summary>
    private bool ReadAhead()
    {
        while (!_ended)
        {
            _reader.ReadSample(MediaFoundation.FirstVideoStream, 0, out _, out int flags, out long timestamp, out var sample);
            if ((flags & MediaFoundation.CurrentMediaTypeChangedFlag) != 0)
            {
                _format = ReadFormat();
            }

            if ((flags & MediaFoundation.EndOfStreamFlag) != 0)
            {
                _ended = true;
            }

            if (sample is not null)
            {
                _ahead = sample;
                _aheadTime = timestamp;
                return true;
            }
        }

        return false;
    }

    /// <summary>Copies the frame's visible area, turned upright when the video says it is rotated.</summary>
    private Bitmap ToBitmap(IMFSample sample)
    {
        sample.ConvertToContiguousBuffer(out var buffer);
        var bitmap = new Bitmap(_format.Visible.Width, _format.Visible.Height, PixelFormat.Format32bppRgb);
        try
        {
            IntPtr scan0;
            int pitch;
            var buffer2D = buffer as IMF2DBuffer;
            if (buffer2D is not null)
            {
                buffer2D.Lock2D(out scan0, out pitch);
            }
            else
            {
                buffer.Lock(out scan0, out _, out _);
                pitch = _format.Stride;
                if (pitch < 0)
                {
                    scan0 += -pitch * (_format.Height - 1);
                }
            }

            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                var row = new byte[bitmap.Width * 4];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(scan0 + (y + _format.Visible.Y) * pitch + _format.Visible.X * 4, row, 0, row.Length);
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
                if (buffer2D is not null)
                {
                    buffer2D.Unlock2D();
                }
                else
                {
                    buffer.Unlock();
                }
            }

            if (_format.Rotation != RotateFlipType.RotateNoneFlipNone)
            {
                bitmap.RotateFlip(_format.Rotation);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        finally
        {
            MediaFoundation.Release(buffer);
        }
    }

    private Format ReadFormat()
    {
        _reader.GetCurrentMediaType(MediaFoundation.FirstVideoStream, out var type);
        try
        {
            var (width, height) = MediaFoundation.GetPair(type, MediaFoundation.Keys.FrameSize);
            int stride = MediaFoundation.TryGetInt(type, MediaFoundation.Keys.DefaultStride) ?? width * 4;
            var visible = new Rectangle(0, 0, width, height);

            // Decoders pad frames to whole macroblocks (1080 → 1088): the aperture is what is meant to show.
            var aperture = new byte[16];
            if (type.GetBlob(MediaFoundation.Keys.MinimumDisplayAperture, aperture, aperture.Length, out int size) >= 0 && size == 16)
            {
                var area = new Rectangle(
                    BitConverter.ToInt16(aperture, 2),
                    BitConverter.ToInt16(aperture, 6),
                    BitConverter.ToInt32(aperture, 8),
                    BitConverter.ToInt32(aperture, 12));
                if (area.Width > 0 && area.Height > 0 && new Rectangle(0, 0, width, height).Contains(area))
                {
                    visible = area;
                }
            }

            return new Format(width, height, stride, visible, Rotation(type));
        }
        finally
        {
            MediaFoundation.Release(type);
        }
    }

    /// <summary>Phones store portrait videos sideways, with the rotation to apply in the file.</summary>
    private RotateFlipType Rotation(IMFMediaType current)
    {
        int? degrees = MediaFoundation.TryGetInt(current, MediaFoundation.Keys.VideoRotation);
        if (degrees is null && _reader.GetNativeMediaType(MediaFoundation.FirstVideoStream, 0, out var native) >= 0)
        {
            degrees = MediaFoundation.TryGetInt(native, MediaFoundation.Keys.VideoRotation);
            MediaFoundation.Release(native);
        }

        return degrees switch
        {
            90 => RotateFlipType.Rotate90FlipNone,
            180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        };
    }

    private sealed record Format(int Width, int Height, int Stride, Rectangle Visible, RotateFlipType Rotation);
}
