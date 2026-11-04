using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Where an animated GIF's upscaled frames are kept, never all in memory: one PNG per frame in a subfolder of the
/// app's temp folder, keyed by the GIF, its factor and the frame, read back on demand (<see cref="UpscaledPages"/>);
/// emptied at start-up by the normal instance. See workfiles/20261029-image-upscale.md § Animated GIFs.
/// </summary>
internal static class UpscaleCache
{
    public const string FolderName = "upscale-cache";

    public static string Folder => Path.Combine(Path.GetTempPath(), "ImageGridFusion", FolderName);

    /// <summary>
    /// A short tag of the GIF a source shows: its file's full path, size and last write when it has one — the same
    /// file's frames then reused — else a tag of its own, never shared.
    /// </summary>
    public static string Tag(string? filePath)
    {
        string what = Guid.NewGuid().ToString("N");
        if (filePath is not null)
        {
            try
            {
                var info = new FileInfo(filePath);
                what = $"{Path.GetFullPath(filePath)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Unreadable: a tag of its own.
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(what)))[..16];
    }

    /// <summary>The file of frame <paramref name="page"/> of the GIF tagged <paramref name="tag"/>, enlarged <paramref name="factor"/> times by <paramref name="model"/>.</summary>
    public static string PathOf(string tag, int factor, UpscaleModel model, int page) =>
        Path.Combine(Folder, $"{tag}-x{factor}-{model.ToString().ToLowerInvariant()}-{page:D5}.png");

    /// <summary>Writes <paramref name="frame"/> as the file of <see cref="PathOf"/>, through a partial file so a frame read back is whole.</summary>
    public static void Save(string tag, int factor, UpscaleModel model, int page, Bitmap frame)
    {
        Directory.CreateDirectory(Folder);
        string path = PathOf(tag, factor, model, page);
        string partial = path + ".~writing";
        frame.Save(partial, ImageFormat.Png);
        File.Move(partial, path, overwrite: true);
    }

    /// <summary>The frame <see cref="Save"/> wrote, read into memory so the file stays free; <c>null</c> when it is not there.</summary>
    public static Bitmap? Load(string tag, int factor, UpscaleModel model, int page)
    {
        string path = PathOf(tag, factor, model, page);
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var image = Image.FromStream(stream);
        return ImageLoader.Copy(image);
    }

    /// <summary>
    /// Deletes what earlier sessions upscaled. A file in use is left for a later session. Run by the normal instance
    /// only: a <c>--new-instance</c> one writes nothing of its own accord.
    /// </summary>
    public static void Clean()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(Folder))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use, or protected: left for a later session.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The folder unreadable: nothing to clean this time.
        }
    }
}
