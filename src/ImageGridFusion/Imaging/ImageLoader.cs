using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Decodes files and clipboard images into independent 32bpp copies, so no file stays locked and
/// clipboard data stays valid after the clipboard changes.
/// </summary>
public static class ImageLoader
{
    /// <summary>Returns null when the file cannot be read or is not an image.</summary>
    public static SourceImage? TryLoadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            return new SourceImage(Copy(image), path);
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            // GDI+ reports unsupported formats as OutOfMemoryException or ArgumentException.
            return null;
        }
    }

    public static SourceImage FromImage(Image image) => new(Copy(image), filePath: null);

    private static Bitmap Copy(Image image)
    {
        var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(copy);
        // Explicit size: ignores the source DPI, which would otherwise rescale the drawing.
        g.DrawImage(image, 0, 0, image.Width, image.Height);
        return copy;
    }
}
