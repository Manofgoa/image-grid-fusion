using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The thumbnail Windows shows for a file in Explorer, from its thumbnail handler. Never an icon:
/// a file Windows has no thumbnail for gets none.
/// </summary>
public static class ShellThumbnail
{
    private const int RequestedSide = 1024;

    /// <summary>Returns null when Windows has no thumbnail for the file.</summary>
    public static Bitmap? TryLoad(string path)
    {
        IntPtr hbitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref iid, out var factory);
            try
            {
                int hr = factory.GetImage(new NativeSize(RequestedSide, RequestedSide), ThumbnailOnly | BiggerSizeOk, out hbitmap);
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
