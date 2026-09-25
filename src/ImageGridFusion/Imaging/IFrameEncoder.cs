namespace ImageGridFusion.Imaging;

/// <summary>
/// Where the animated export writes its frames, whatever the file: the MP4 video or the GIF. Frames
/// are the size given when the encoder was created. Without <see cref="Finish"/>, disposing leaves
/// the file incomplete: the caller deletes it.
/// </summary>
internal interface IFrameEncoder : IDisposable
{
    /// <summary>Writes a frame, shown from <paramref name="time"/> for <paramref name="duration"/>.</summary>
    void WriteFrame(Bitmap frame, TimeSpan time, TimeSpan duration);

    /// <summary>Completes the file.</summary>
    void Finish();
}
