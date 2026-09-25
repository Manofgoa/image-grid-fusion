using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Writes an MP4 file with the Media Foundation Sink Writer: H.264 video from 32-bit RGB frames, and
/// optionally the sounds of video files, each looped with its video and scaled by its volume, mixed
/// and re-encoded to one AAC track. Used from a single thread-pool thread.
/// </summary>
internal sealed class VideoEncoder : IFrameEncoder
{
    private const int AacBytesPerSecond = 24000;

    /// <summary>Sound is written this far ahead of the video, so the file interleaves both.</summary>
    private static readonly long SoundLead = TimeSpan.FromSeconds(1).Ticks;

    private readonly IMFSinkWriter _writer;
    private readonly Size _size;
    private readonly int _videoStream;
    private readonly Mixer? _sound;

    private VideoEncoder(IMFSinkWriter writer, Size size, int videoStream, Mixer? sound)
    {
        _writer = writer;
        _size = size;
        _videoStream = videoStream;
        _sound = sound;
    }

    /// <summary>Files whose sound went into the mix.</summary>
    public IReadOnlyList<string> MixedSounds { get; private set; } = [];

    /// <summary>Files whose sound was asked for, but Windows cannot re-encode: left out of the mix.</summary>
    public IReadOnlyList<string> FailedSounds { get; private set; } = [];

    /// <summary>
    /// Creates the file, its sound mixed from <paramref name="sounds"/> for the <paramref name="length"/>
    /// of the video; a sound Windows cannot re-encode is left out, listed in <see cref="FailedSounds"/>.
    /// </summary>
    public static VideoEncoder Create(string path, Size size, TimeSpan length, IReadOnlyList<MixedSound> sounds)
    {
        var failed = new List<string>();
        var mixer = Mixer.Create(sounds, length.Ticks, failed);
        if (mixer is not null)
        {
            try
            {
                var encoder = Open(path, size, mixer);
                encoder.MixedSounds = mixer.Paths;
                encoder.FailedSounds = failed;
                return encoder;
            }
            catch (Exception e) when (e is COMException or InvalidOperationException)
            {
                failed.AddRange(mixer.Paths);
            }
        }

        var silent = Open(path, size, sound: null);
        silent.FailedSounds = failed;
        return silent;
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

    private static VideoEncoder Open(string path, Size size, Mixer? sound)
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
    /// The sounds of the grid, mixed into one 16-bit stereo PCM stream fed to the AAC encoder: each
    /// voice scaled by its gain, the sum clipped to full scale, written in steps of a tenth of a second
    /// until the video ends.
    /// </summary>
    private sealed class Mixer : IDisposable
    {
        private readonly List<Voice> _voices;
        private readonly int _rate;
        private readonly long _lengthFrames;
        private readonly int[] _mix;
        private readonly short[] _clipped;
        private int _stream;
        private long _frames;

        private Mixer(List<Voice> voices, int rate, long length)
        {
            _voices = voices;
            _rate = rate;
            _lengthFrames = length * rate / TimeSpan.TicksPerSecond;
            _mix = new int[StepFrames * 2];
            _clipped = new short[StepFrames * 2];
        }

        public IReadOnlyList<string> Paths => _voices.Select(v => v.Path).ToList();

        private int StepFrames => _rate / 10;

        /// <summary>
        /// Opens every sound, all decoded at one rate: 44.1 kHz when they all are, else 48 kHz. The ones
        /// that fail go to <paramref name="failed"/>; <c>null</c> when none is left.
        /// </summary>
        public static Mixer? Create(IReadOnlyList<MixedSound> sounds, long length, List<string> failed)
        {
            var voices = new List<Voice>();
            foreach (var sound in sounds)
            {
                try
                {
                    voices.Add(new Voice(sound));
                }
                catch (Exception e) when (e is COMException or InvalidOperationException)
                {
                    failed.Add(sound.Path);
                }
            }

            int rate = voices.Count > 0 && voices.All(v => v.NativeRate == 44100) ? 44100 : 48000;
            foreach (var voice in voices.ToList())
            {
                try
                {
                    voice.Decode(rate);
                }
                catch (Exception e) when (e is COMException or InvalidOperationException)
                {
                    failed.Add(voice.Path);
                    voice.Dispose();
                    voices.Remove(voice);
                }
            }

            return voices.Count == 0 ? null : new Mixer(voices, rate, length);
        }

        /// <summary>Adds the AAC stream, stereo at the mix's rate.</summary>
        public void AddStream(IMFSinkWriter writer)
        {
            var output = MediaFoundation.CreateMediaType();
            var input = PcmType(2, _rate);
            try
            {
                output.SetGUID(MediaFoundation.Keys.MajorType, MediaFoundation.Formats.Audio);
                output.SetGUID(MediaFoundation.Keys.Subtype, MediaFoundation.Formats.Aac);
                output.SetUINT32(MediaFoundation.Keys.AudioBitsPerSample, 16);
                output.SetUINT32(MediaFoundation.Keys.AudioSamplesPerSecond, _rate);
                output.SetUINT32(MediaFoundation.Keys.AudioChannels, 2);
                output.SetUINT32(MediaFoundation.Keys.AudioAverageBytesPerSecond, AacBytesPerSecond);
                writer.AddStream(output, out _stream);
                writer.SetInputMediaType(_stream, input, null);
            }
            finally
            {
                MediaFoundation.Release(input);
                MediaFoundation.Release(output);
            }
        }

        public void WriteUntil(IMFSinkWriter writer, long until)
        {
            while (_frames < _lengthFrames && Time(_frames) < until)
            {
                int frames = (int)Math.Min(StepFrames, _lengthFrames - _frames);
                Array.Clear(_mix, 0, frames * 2);
                foreach (var voice in _voices)
                {
                    voice.MixInto(_mix, frames);
                }

                for (int i = 0; i < frames * 2; i++)
                {
                    _clipped[i] = (short)Math.Clamp(_mix[i], short.MinValue, short.MaxValue);
                }

                Write(writer, frames);
                _frames += frames;
            }
        }

        public void Dispose() => _voices.ForEach(v => v.Dispose());

        private long Time(long frames) => frames * TimeSpan.TicksPerSecond / _rate;

        private void Write(IMFSinkWriter writer, int frames)
        {
            int bytes = frames * 4;
            var buffer = MediaFoundation.CreateMemoryBuffer(bytes);
            IMFSample? sample = null;
            try
            {
                buffer.Lock(out var destination, out _, out _);
                try
                {
                    Marshal.Copy(_clipped, 0, destination, frames * 2);
                }
                finally
                {
                    buffer.Unlock();
                }

                buffer.SetCurrentLength(bytes);
                sample = MediaFoundation.CreateSample();
                sample.AddBuffer(buffer);
                sample.SetSampleTime(Time(_frames));
                sample.SetSampleDuration(Time(_frames + frames) - Time(_frames));
                writer.WriteSample(_stream, sample);
            }
            finally
            {
                MediaFoundation.Release(sample);
                MediaFoundation.Release(buffer);
            }
        }
    }

    /// <summary>
    /// The sound of one video, decoded to 16-bit PCM by a Source Reader: from its starting point, then
    /// from the start again every loop, cut at the end of each loop, silent where the sound is shorter
    /// than its loop; added to the mix at its gain, as stereo.
    /// </summary>
    private sealed class Voice : IDisposable
    {
        private readonly IMFSourceReader _reader;
        private readonly long _loop;
        private readonly long _start;
        private readonly double _gain;
        private int _rate;
        private int _channels;
        private long _loopFrames;

        // Decoded frames, as stereo, starting at frame _pendingFrame of the loop.
        private short[] _pending = [];
        private int _pendingCount;
        private long _pendingFrame;

        // Frame of the loop the next mixed frame comes from; _ended once the sound ended before the loop.
        private long _position;
        private bool _ended;

        public Voice(MixedSound sound)
        {
            Path = sound.Path;
            _loop = sound.Loop.Ticks;
            _start = sound.Start.Ticks;
            _gain = sound.Gain;
            _reader = MediaFoundation.CreateSourceReader(sound.Path, attributes: null);
            try
            {
                _reader.SetStreamSelection(MediaFoundation.AllStreams, false);
                _reader.SetStreamSelection(MediaFoundation.FirstAudioStream, true);
                Marshal.ThrowExceptionForHR(_reader.GetNativeMediaType(MediaFoundation.FirstAudioStream, 0, out var native));
                try
                {
                    NativeRate = MediaFoundation.TryGetInt(native, MediaFoundation.Keys.AudioSamplesPerSecond) ?? 48000;
                    _channels = Math.Clamp(MediaFoundation.TryGetInt(native, MediaFoundation.Keys.AudioChannels) ?? 2, 1, 2);
                }
                finally
                {
                    MediaFoundation.Release(native);
                }
            }
            catch
            {
                MediaFoundation.Release(_reader);
                throw;
            }
        }

        public string Path { get; }

        public int NativeRate { get; }

        /// <summary>Decodes to PCM at <paramref name="rate"/>, mono or stereo, and seeks to the starting point.</summary>
        public void Decode(int rate)
        {
            var pcm = PcmType(_channels, rate);
            try
            {
                Marshal.ThrowExceptionForHR(_reader.SetCurrentMediaType(MediaFoundation.FirstAudioStream, IntPtr.Zero, pcm));
            }
            finally
            {
                MediaFoundation.Release(pcm);
            }

            _reader.GetCurrentMediaType(MediaFoundation.FirstAudioStream, out var current);
            try
            {
                _channels = MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioChannels) ?? _channels;
                if ((MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioSamplesPerSecond) ?? rate) != rate
                    || (MediaFoundation.TryGetInt(current, MediaFoundation.Keys.AudioBitsPerSample) ?? 16) != 16
                    || _channels is not (1 or 2))
                {
                    throw new InvalidOperationException("Unsupported sound format.");
                }
            }
            finally
            {
                MediaFoundation.Release(current);
            }

            _rate = rate;
            _loopFrames = _loop * rate / TimeSpan.TicksPerSecond;
            if (_loopFrames <= 0)
            {
                throw new InvalidOperationException("No loop to play the sound in.");
            }

            // The first loop is cut short: the video's time 0 is the sound's time start.
            _position = _start * rate / TimeSpan.TicksPerSecond % _loopFrames;
            if (_start > 0)
            {
                _reader.SetCurrentPosition(Guid.Empty, PropVariant.FromLong(_start));
            }
        }

        /// <summary>Adds the next <paramref name="frames"/> frames, at the voice's gain, to <paramref name="mix"/> (stereo, interleaved).</summary>
        public void MixInto(int[] mix, int frames)
        {
            int done = 0;
            while (done < frames)
            {
                if (_position >= _loopFrames)
                {
                    Rewind();
                }

                int wanted = (int)Math.Min(frames - done, _loopFrames - _position);
                int count;
                if (!Fill())
                {
                    // The sound ended before its loop: silent until it starts over.
                    count = wanted;
                }
                else if (_pendingFrame > _position)
                {
                    // A gap in the sound.
                    count = (int)Math.Min(wanted, _pendingFrame - _position);
                }
                else
                {
                    int offset = (int)(_position - _pendingFrame);
                    count = Math.Min(wanted, _pendingCount - offset);
                    for (int i = 0; i < count * 2; i++)
                    {
                        mix[done * 2 + i] += (int)Math.Round(_pending[offset * 2 + i] * _gain);
                    }
                }

                done += count;
                _position += count;
            }
        }

        public void Dispose() => MediaFoundation.Release(_reader);

        private void Rewind()
        {
            _position = 0;
            _ended = false;
            _pendingCount = 0;
            _pendingFrame = 0;
            _reader.SetCurrentPosition(Guid.Empty, PropVariant.FromLong(0));
        }

        /// <summary>Decodes until the pending frames reach the position; <c>false</c> once the sound ended in this loop.</summary>
        private bool Fill()
        {
            while (!_ended && _pendingFrame + _pendingCount <= _position)
            {
                ReadNext();
            }

            return _pendingFrame + _pendingCount > _position;
        }

        private void ReadNext()
        {
            _reader.ReadSample(MediaFoundation.FirstAudioStream, 0, out _, out int flags, out long timestamp, out var sample);
            try
            {
                long frame = timestamp * _rate / TimeSpan.TicksPerSecond;
                if ((flags & MediaFoundation.EndOfStreamFlag) != 0 || (sample is not null && frame >= _loopFrames))
                {
                    _ended = true;
                    return;
                }

                if (sample is null)
                {
                    return;
                }

                sample.ConvertToContiguousBuffer(out var buffer);
                try
                {
                    buffer.Lock(out var source, out _, out int bytes);
                    try
                    {
                        int count = bytes / (_channels * 2);
                        if (_pending.Length < count * 2)
                        {
                            _pending = new short[count * 2];
                        }

                        Marshal.Copy(source, _pending, 0, count * _channels);

                        // A mono sound goes to both sides: spread from the end, so no sample is overwritten before it is read.
                        if (_channels == 1)
                        {
                            for (int i = count - 1; i >= 0; i--)
                            {
                                _pending[2 * i + 1] = _pending[i];
                                _pending[2 * i] = _pending[i];
                            }
                        }

                        _pendingCount = count;
                        _pendingFrame = frame;
                    }
                    finally
                    {
                        buffer.Unlock();
                    }
                }
                finally
                {
                    MediaFoundation.Release(buffer);
                }
            }
            finally
            {
                MediaFoundation.Release(sample);
            }
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

/// <summary>A sound of an exported video's mix: a video file, looping every <paramref name="Loop"/> from <paramref name="Start"/>, scaled by <paramref name="Gain"/>.</summary>
internal sealed record MixedSound(string Path, TimeSpan Loop, TimeSpan Start, double Gain);
