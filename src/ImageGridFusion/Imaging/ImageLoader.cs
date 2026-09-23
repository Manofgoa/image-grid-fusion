using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Decodes files and clipboard images into independent 32bpp copies, so no file stays locked and
/// clipboard data stays valid after the clipboard changes.
/// </summary>
public static class ImageLoader
{
    /// <summary>
    /// Loads a file as an animated GIF, an image, else as a preview: a video frame, a PDF page,
    /// rendered text, and last the thumbnail Windows shows for it. Returns null when the file has no
    /// preview at all. Blocks on file and WinRT calls: meant to run off the UI thread.
    /// </summary>
    /// <remarks>An SVG is text too: its thumbnail, when Windows has one, comes first so it shows as a drawing.</remarks>
    public static SourceImage? TryLoadFile(string path) =>
        TryPages(path, GifFrames.TryOpen(path))
        ?? TryDecode(path)
        ?? TryPages(path, VideoFrames.TryOpen(path))
        ?? TryPages(path, PdfPages.TryOpen(path))
        ?? (IsSvg(path) ? TryThumbnail(path) : null)
        ?? TryPages(path, TextPages.TryOpen(path, new Size(GridLayout.RatioWidth, GridLayout.RatioHeight)))
        ?? TryThumbnail(path);

    private static bool IsSvg(string path) => Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    public static SourceImage FromImage(Image image) => new(Copy(image), filePath: null);

    private static SourceImage? TryDecode(string path)
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

    /// <summary>Renders the initial page; a source that fails there is dropped, and the next producer tried.</summary>
    private static SourceImage? TryPages(string path, PageSource? pages)
    {
        if (pages is null)
        {
            return null;
        }

        try
        {
            return new SourceImage(pages.Render(pages.InitialPage), path, pages, pages.InitialPage);
        }
        catch (Exception)
        {
            // WinRT reports decoding failures with assorted exception types.
            pages.Dispose();
            return null;
        }
    }

    private static SourceImage? TryThumbnail(string path) =>
        ShellThumbnail.TryLoad(path) is { } thumbnail ? new SourceImage(thumbnail, path) : null;

    internal static Bitmap Copy(Image image)
    {
        var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(copy))
        {
            // Explicit size: ignores the source DPI, which would otherwise rescale the drawing.
            g.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        copy.RotateFlip(ExifRotation(image));
        return copy;
    }

    /// <summary>GDI+ does not apply the EXIF orientation: phone photos would appear rotated.</summary>
    private static RotateFlipType ExifRotation(Image image)
    {
        if (!image.PropertyIdList.Contains(OrientationTag))
        {
            return RotateFlipType.RotateNoneFlipNone;
        }

        byte[]? value = image.GetPropertyItem(OrientationTag)?.Value;
        return value is { Length: > 0 } ? value[0] switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        } : RotateFlipType.RotateNoneFlipNone;
    }

    private const int OrientationTag = 0x0112;
}
