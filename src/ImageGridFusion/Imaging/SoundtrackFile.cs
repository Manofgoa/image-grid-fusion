using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>Opens the file of a soundtrack: any audio or video file Windows can decode a sound track from.</summary>
internal static class SoundtrackFile
{
    /// <summary>Audio files offered by the picker; videos are offered too, for their sound track.</summary>
    public static readonly string[] AudioExtensions = ["mp3", "wav", "m4a", "aac", "wma", "flac"];

    /// <summary>Videos the pickers offer, mirroring <see cref="VideoFrames"/>.</summary>
    public static readonly string[] VideoExtensions = ["mp4", "m4v", "mov", "avi", "wmv", "asf", "mkv", "webm", "3gp", "3g2", "mpg", "mpeg", "ts", "m2ts", "mts"];

    /// <summary>
    /// Whether <paramref name="path"/> may carry a sound track, by its extension — an audio or a video file —
    /// what a drag onto the Soundtrack is accepted for at the hover; <see cref="TryOpen"/> still decides at the drop.
    /// </summary>
    public static bool MayCarrySound(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return extension.Length > 0
            && (AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                || VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The soundtrack of <paramref name="path"/>, at 100 %; <c>null</c> when Windows reads no sound
    /// track from it, or no length. Blocks: meant to run off the UI thread.
    /// </summary>
    public static Soundtrack? TryOpen(string path)
    {
        IMFSourceReader? reader = null;
        try
        {
            reader = MediaFoundation.CreateSourceReader(path, attributes: null);
            if (reader.GetNativeMediaType(MediaFoundation.FirstAudioStream, 0, out var native) < 0)
            {
                return null;
            }

            MediaFoundation.Release(native);
            if (reader.GetPresentationAttribute(MediaFoundation.MediaSource, MediaFoundation.Keys.Duration, out var duration) < 0
                || duration.Value <= 0)
            {
                return null;
            }

            return new Soundtrack(path, TimeSpan.FromTicks(duration.Value));
        }
        catch (Exception e) when (e is COMException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
        finally
        {
            MediaFoundation.Release(reader);
        }
    }
}
