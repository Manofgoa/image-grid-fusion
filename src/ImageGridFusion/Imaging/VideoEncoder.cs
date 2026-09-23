using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Writes an MP4 file with the Media Foundation Sink Writer: H.264 video from 32-bit RGB frames, and
/// optionally the sound of a video file, re-encoded to AAC and looped with that video. Used from a
/// single thread-pool thread.
/// </summary>
internal sealed class VideoEncoder : IDisposable
{
    private const int AacBytesPerSecond = 24000;

    /// <summary>Sound is written this far ahead of the video, so the file interleaves both.</summary>
    private static readonly long SoundLead = TimeSpan.FromSeconds(1).Ticks;

    private readonly IMFSinkWriter _writer;
    private readonly Size _size;
    private readonly int _videoStream;
    private readonly Sound? _sound;

    private VideoEncoder(IMFSinkWriter writer, Size size, int videoStream, Sound? sound)
    {
        _writer = writer;
        _size = size;
        _videoStream = videoStream;
        _sound = sound;
    }

    /// <summary>Why the sound could not go into the file; <c>null</c> when it did, or none was asked for.</summary>
    public string? SoundProblem { get; private set; }

    /// <summary>
    /// Creates the file. <paramref name="soundPath"/>, when given, is a video whose sound loops every
    /// <paramref name="soundLoop"/> for the <paramref name="length"/> of the video; a sound Windows
    /// cannot re-encode leaves the video silent, with a <see cref="SoundProblem"/>.
    /// </summary>
    public static VideoEncoder Create(string path, Size size, TimeSpan length, string? soundPath, TimeSpan soundLoop)
    {
        if (soundPath is not null)
        {
            try
            {
                return Open(path, size, new Sound(soundPath, soundLoop.Ticks, length.Ticks));
            }
            catch (Exception e) when (e is COMException or InvalidOperationException)
            {
                var encoder = Open(path, size, sound: null);
                encoder.SoundProblem = $"no sound ({Path.GetFileName(soundPath)}: its sound cannot be re-encoded)";
                return encoder;
            }
        }

        return Open(path, size, sound: null);
    }

    /// <summary>Writes a frame the size of the video, shown from <paramref name="time"/> for <paramref name="duration"/>.</summary>
    public void WriteFrame(Bitmap frame, TimeSpan time, TimeSpan duration)
    {
        int stride = _size.Width * 4;
        int length = stride * _size.Height;
        var buffer = MediaFoundation.CreateMemoryBuffer(length);
        IMFSample? sample = null;
        try
        {
            buffer.Lock(out var destination, out _, out _);
            var data = frame.LockBits(new Rectangle(Point.Empty, _size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                var row = new byte[stride];
                for (int y = 0; y < _size.Height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, stride);
                    Marshal.Copy(row, 0, destination + y * stride, stride);
                }
            }
            finally
            {
                frame.UnlockBits(data);
                buffer.Unlock();
            }

            buffer.SetCurrentLength(length);
            sample = MediaFoundation.CreateSample();
            sample.AddBuffer(buffer);
            sample.SetSampleTime(time.Ticks);
            sample.SetSampleDuration(duration.Ticks);
            _writer.WriteSample(_videoStream, sample);
        }
        finally
        {
            MediaFoundation.Release(sample);
            MediaFoundation.Release(buffer);
        }

        _sound?.WriteUntil(_writer, time.Ticks + duration.Ticks + SoundLead);
    }

    /// <summary>Writes the rest of the sound and completes the file.</summary>
    public void Finish()
    {
        _sound?.WriteUntil(_writer, long.MaxValue);
        _writer.DoFinalize();
    }

    /// <summary>Without <see cref="Finish"/>, the file is left incomplete: the caller deletes it.</summary>
    public void Dispose()
    {
        _sound?.Dispose();
        MediaFoundation.Release(_writer);
    }

    private static VideoEncoder Open(string path, Size size, Sound? sound)
    {
        IMFSinkWriter? writer = null;
        try
        {
            writer = MediaFoundation.CreateSinkWriter(path, attributes: null);
            int videoStream = AddVideoStream(writer, size);
            sound?.AddStream(writer);
            writer.BeginWriting();
            return new VideoEncoder(writer, size, videoStream, sound);
        }
        catch
        {
            sound?.Dispose();
            MediaFoundation.Release(writer);
            TryDelete(path);
            throw;
        }
    }

    private static int AddVideoStream(IMFSinkWriter writer, Size size)
    {
        // Enough for sharp stills and text at 30 fps: about 0.12 bit per pixel and frame.
        long bitrate = Math.Clamp((long)(size.Width * (double)size.Height * Animation.FramesPerSecond * 0.12), 2_000_000, 40_000_000);

        var output = MediaFoundation.CreateMediaType();
        var input = MediaFoundation.CreateMediaType();
        try
        {
            output.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Video);
            output.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.H264);
            output.SetUINT32(MediaFoundation.Keys.AverageBitrate, (int)bitrate);
            output.SetUINT32(MediaFoundation.Keys.Mpeg2Profile, MediaFoundation.H264HighProfile);
            SetFrameFormat(output, size);
            writer.AddStream(output, out int stream);

            input.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Video);
            input.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.Rgb32);
            input.SetUINT32(MediaFoundation.Keys.DefaultStride, size.Width * 4);
            SetFrameFormat(input, size);
            writer.SetInputMediaType(stream, input, null);
            return stream;
        }
        finally
        {
            MediaFoundation.Release(input);
            MediaFoundation.Release(output);
        }
    }

    private static void SetFrameFormat(IMFMediaType type, Size size)
    {
        type.SetUINT32(MediaFoundation.Keys.InterlaceMode, MediaFoundation.ProgressiveInterlace);
        MediaFoundation.SetPair(type, MediaFoundation.Keys.FrameSize, size.Width, size.Height);
        MediaFoundation.SetPair(type, MediaFoundation.Keys.FrameRate, Animation.FramesPerSecond, 1);
        MediaFoundation.SetPair(type, MediaFoundation.Keys.PixelAspectRatio, 1, 1);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left behind: nothing more to do.
        }
    }

    /// <summary>
    /// The sound of a video, decoded to 16-bit PCM by a Source Reader and fed to the AAC encoder, from
    /// the start again every loop, and cut at the end of each loop and of the video.
    /// </summary>
    private sealed class Sound : IDisposable
    {
        private readonly IMFSourceReader _reader;
        private readonly long _loop;
        private readonly long _length;
        private int _stream;
        private int _blockAlign;
        private int _samplesPerSecond;
        private long _loopStart;
        private long _written;
        private bool _done;

        public Sound(string path, long loop, long length)
        {
            _loop = loop;
            _length = length;
            _reader = MediaFoundation.CreateSourceReader(path, attributes: null);
            try
            {
                _reader.SetStreamSelection(MediaFoundation.AllStreams, false);
                _reader.SetStreamSelection(MediaFoundation.FirstAudioStream, true);
            }
            catch
            {
                MediaFoundation.Release(_reader);
                throw;
            }
        }

        /// <summary>Picks a PCM format the AAC encoder takes — 44.1 or 48 kHz, mono or stereo — and adds the AAC stream.</summary>
        public void AddStream(IMFSinkWriter writer)
        {
            Marshal.ThrowExceptionForHR(_reader.GetNativeMediaType(MediaFoundation.FirstAudioStream, 0, out var native));
            int channels, rate;
            try
            {
                channels = Math.Clamp(MediaFoundation.TryGetInt(native, MediaFoundation.Keys.AudioChannels) ?? 2, 1, 2);
                rate = MediaFoundation.TryGetInt(native, MediaFoundation.Keys.AudioSamplesPerSecond) is 44100 ? 44100 : 48000;
            }
            finally
            {
                MediaFoundation.Release(native);
            }

            var pcm = PcmType(channels, rate);
            try
            {
                Marshal.ThrowExceptionForHR(_reader.SetCurrentMediaType(MediaFoundation.FirstAudioStream, IntPtr.Zero, pcm));
            }
            finally
            {
                MediaFoundation.Release(pcm);
            }

            _reader.GetCurrentMediaType(MediaFoundation.FirstAudioStream, out var current);
            var output = MediaFoundation.CreateMediaType();
            try
            {
                channels = MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioChannels) ?? channels;
                _samplesPerSecond = MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioSamplesPerSecond) ?? rate;
                _blockAlign = MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioBlockAlignment) ?? channels * 2;
                if (_samplesPerSecond is not (44100 or 48000) || channels is not (1 or 2))
                {
                    throw new InvalidOperationException("Unsupported sound format.");
                }

                output.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Audio);
                output.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.Aac);
                output.SetUINT32(MediaFoundation.Keys.AudioBitsPerSample, 16);
                output.SetUINT32(MediaFoundation.Keys.AudioSamplesPerSecond, _samplesPerSecond);
                output.SetUINT32(MediaFoundation.Keys.AudioChannels, channels);
                output.SetUINT32(MediaFoundation.Keys.AudioAverageBytesPerSecond, AacBytesPerSecond);
                writer.AddStream(output, out _stream);
                writer.SetInputMediaType(_stream, current, null);
            }
            finally
            {
                MediaFoundation.Release(output);
                MediaFoundation.Release(current);
            }
        }

        public void WriteUntil(IMFSinkWriter writer, long until)
        {
            while (!_done && _written < until)
            {
                _reader.ReadSample(MediaFoundation.FirstAudioStream, 0, out _, out int flags, out long timestamp, out var sample);
                try
                {
                    bool ended = (flags & MediaFoundation.EndOfStreamFlag) != 0;
                    if (ended || (sample is not null && timestamp >= _loop))
                    {
                        NextLoop();
                        continue;
                    }

                    if (sample is not null)
                    {
                        Write(writer, sample, _loopStart + Math.Max(0, timestamp));
                    }
                }
                finally
                {
                    MediaFoundation.Release(sample);
                }
            }
        }

        public void Dispose() => MediaFoundation.Release(_reader);

        private void NextLoop()
        {
            _loopStart += _loop;
            if (_loopStart >= _length || _loop <= 0)
            {
                _done = true;
                return;
            }

            _reader.SetCurrentPosition(Guid.Empty, PropVariant.FromLong(0));
        }

        /// <summary>Writes a sample at <paramref name="time"/>, cut where its loop, or the video, ends.</summary>
        private void Write(IMFSinkWriter writer, IMFSample sample, long time)
        {
            long limit = Math.Min(_loopStart + _loop, _length);
            if (time >= limit)
            {
                if (limit == _length)
                {
                    _done = true;
                }

                return;
            }

            sample.ConvertToContiguousBuffer(out var buffer);
            try
            {
                buffer.GetCurrentLength(out int bytes);
                long duration = (long)bytes / _blockAlign * TimeSpan.TicksPerSecond / _samplesPerSecond;
                if (time + duration > limit)
                {
                    long frames = (limit - time) * _samplesPerSecond / TimeSpan.TicksPerSecond;
                    buffer.SetCurrentLength((int)(frames * _blockAlign));
                    duration = limit - time;
                }

                sample.SetSampleTime(time);
                sample.SetSampleDuration(duration);
                writer.WriteSample(_stream, sample);
                _written = time + duration;
            }
            finally
            {
                MediaFoundation.Release(buffer);
            }
        }

        private static IMFMediaType PcmType(int channels, int rate)
        {
            var type = MediaFoundation.CreateMediaType();
            type.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Audio);
            type.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.Pcm);
            type.SetUINT32(MediaFoundation.Keys.AudioBitsPerSample, 16);
            type.SetUINT32(MediaFoundation.Keys.AudioChannels, channels);
            type.SetUINT32(MediaFoundation.Keys.AudioSamplesPerSecond, rate);
            type.SetUINT32(MediaFoundation.Keys.AudioBlockAlignment, channels * 2);
            type.SetUINT32(MediaFoundation.Keys.AudioAverageBytesPerSecond, channels * 2 * rate);
            return type;
        }
    }
}
