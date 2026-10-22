using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The thumbnail Windows shows for a file in Explorer, from its thumbnail handler. Never an icon:
/// a file Windows has no thumbnail for gets none.
/// </summary>
public static class ShellThumbnail
{
    internal const int RequestedSide = 1024;

    /// <summary>The side Windows gives a folder's thumbnail, whatever the size asked.</summary>
    internal const int FolderSide = 256;

    // The highest value of each channel still taken for the black around an opaque folder.
    private const int NearBlack = 8;

    /// <summary>
    /// Returns null when Windows has no thumbnail for the file. <paramref name="side"/> is the size
    /// asked for, a bigger one accepted: the cells' previews take the largest, the file explorer's
    /// tiles a small one.
    /// </summary>
    public static Bitmap? TryLoad(string path, int side = RequestedSide)
    {
        IntPtr hbitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref iid, out var factory);
            try
            {
                int hr = factory.GetImage(new NativeSize(side, side), ThumbnailOnly | BiggerSizeOk, out hbitmap);
                return hr == 0 && hbitmap != IntPtr.Zero ? ToBitmap(hbitmap) : null;
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch (Exception e) when (e is COMException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero)
            {
                DeleteObject(hbitmap);
            }
        }
    }

    /// <summary>
    /// A folder's thumbnail, asked at <see cref="FolderSide"/>: at some other sizes Windows gives some
    /// folders an opaque one, black where the folder is transparent. Should it still come opaque, the
    /// black joined to its edges is made transparent; the black inside the picture stays. See
    /// workfiles/20261022-explorer-transparency-checkerboard.md.
    /// </summary>
    public static Bitmap? TryLoadFolder(string path)
    {
        var bitmap = TryLoad(path, FolderSide);
        if (bitmap is not null)
        {
            ClearOuterBlack(bitmap);
        }

        return bitmap;
    }

    /// <summary>
    /// On a fully opaque <paramref name="bitmap"/>, the near-black pixels reached from its border through
    /// near-black pixels only become transparent. One with any transparency is left as it is.
    /// </summary>
    private static void ClearOuterBlack(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int[] pixels = new int[width * height];
        var bounds = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width, width);
            }

            foreach (int pixel in pixels)
            {
                if ((uint)pixel >> 24 != 0xFF)
                {
                    return;
                }
            }

            // A cleared pixel is 0, fully transparent: never visited twice.
            var queue = new Queue<int>();
            void Visit(int i)
            {
                if (pixels[i] != 0 && IsNearBlack(pixels[i]))
                {
                    pixels[i] = 0;
                    queue.Enqueue(i);
                }
            }

            for (int x = 0; x < width; x++)
            {
                Visit(x);
                Visit((height - 1) * width + x);
            }

            for (int y = 0; y < height; y++)
            {
                Visit(y * width);
                Visit(y * width + width - 1);
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % width;
                if (x > 0)
                {
                    Visit(i - 1);
                }

                if (x < width - 1)
                {
                    Visit(i + 1);
                }

                if (i >= width)
                {
                    Visit(i - width);
                }

                if (i < (height - 1) * width)
                {
                    Visit(i + width);
                }
            }

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, data.Scan0 + y * data.Stride, width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static bool IsNearBlack(int argb) =>
        (argb & 0xFF) <= NearBlack && ((argb >> 8) & 0xFF) <= NearBlack && ((argb >> 16) & 0xFF) <= NearBlack;

    /// <summary>
    /// Copies the bitmap, keeping its alpha channel: <see cref="Image.FromHbitmap(IntPtr)"/> takes the
    /// rows in the right order but reports 32bpp pixels as opaque, so the alpha bytes it still holds
    /// are read back raw (transparent areas would otherwise turn black).
    /// </summary>
    private static Bitmap ToBitmap(IntPtr hbitmap)
    {
        using var opaque = Image.FromHbitmap(hbitmap);
        if (opaque.PixelFormat != PixelFormat.Format32bppRgb)
        {
            return ImageLoader.Copy(opaque);
        }

        // Row by row: a bottom-up bitmap has a negative stride.
        var bounds = new Rectangle(Point.Empty, opaque.Size);
        int rowBytes = bounds.Width * 4;
        byte[] pixels = new byte[rowBytes * bounds.Height];
        var source = opaque.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            for (int y = 0; y < bounds.Height; y++)
            {
                Marshal.Copy(source.Scan0 + y * source.Stride, pixels, y * rowBytes, rowBytes);
            }
        }
        finally
        {
            opaque.UnlockBits(source);
        }

        // Some handlers leave the alpha channel empty: their pixels are opaque.
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length && !hasAlpha; i += 4)
        {
            hasAlpha = pixels[i] != 0;
        }

        if (!hasAlpha)
        {
            return ImageLoader.Copy(opaque);
        }

        using var translucent = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        var target = translucent.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < bounds.Height; y++)
            {
                Marshal.Copy(pixels, y * rowBytes, target.Scan0 + y * target.Stride, rowBytes);
            }
        }
        finally
        {
            translucent.UnlockBits(target);
        }

        return ImageLoader.Copy(translucent);
    }

    private const int BiggerSizeOk = 0x01;
    private const int ThumbnailOnly = 0x08;

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr hbitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeSize(int Width, int Height);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
