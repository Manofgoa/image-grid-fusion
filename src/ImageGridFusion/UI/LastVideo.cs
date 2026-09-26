using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// The last MP4 or GIF Copy generated in the session — its file in the temp folder — offered back by
/// Copy last (on the clipboard again) and Save last (copied elsewhere) without generating it again.
/// App state, not an effect: nothing that happens to the grid touches it; the next animated copy
/// replaces it, quitting forgets it, and the next start removes its file with the temp folder.
/// </summary>
/// <param name="FilePath">The file, in the temp folder.</param>
/// <param name="Format">MP4 or GIF.</param>
/// <param name="GeneratedAt">When its export ended.</param>
/// <param name="Video">What the export produced: size, frames, length, sounds.</param>
/// <param name="Bytes">The file's size, read once: the file may be gone when a summary is built.</param>
/// <param name="Encoding">How long the export took.</param>
/// <param name="GridVersion">The grid's content version when it was generated: differing now, the grid has changed since.</param>
internal sealed record LastVideo(string FilePath, GridExport.Format Format, DateTime GeneratedAt, GridExport.Result Video, long Bytes, TimeSpan Encoding, int GridVersion)
{
    public string FileName => Path.GetFileName(FilePath);
}
