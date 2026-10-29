using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.UI;

/// <summary>
/// The image on the clipboard, read for a paste: its PNG format first, which keeps the transparency — this
/// app's copies and the browsers put one there —, then the standard bitmap, flattened by whoever put it
/// (workfiles/20261029-copy-single-cell.md § Pasting Into a Cell).
/// </summary>
internal static class ClipboardImage
{
    /// <summary>The clipboard's image, the caller's to dispose; <c>null</c> without one.</summary>
    public static Image? Get() => FromPng() ?? Clipboard.GetImage();

    private static Bitmap? FromPng()
    {
        if (!Clipboard.ContainsData("PNG") || !Clipboard.TryGetData("PNG", out MemoryStream? png) || png is null)
        {
            return null;
        }

        try
        {
            // Copied out of the stream: a bitmap read from one needs it open as long as it lives.
            using (png)
            using (var image = Image.FromStream(png))
            {
                var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bitmap);
                g.DrawImage(image, 0, 0, image.Width, image.Height);
                return bitmap;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            // Not a PNG after all: the standard bitmap is read instead.
            return null;
        }
    }
}
