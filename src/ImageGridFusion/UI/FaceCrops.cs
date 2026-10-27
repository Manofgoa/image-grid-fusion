using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.UI;

/// <summary>
/// The pictures of the People view's tiles: a face cut out of its file at the tile's 4:3 shape, a margin
/// around it, read from the full image — a Shell thumbnail would blur a small face. Asked for through the
/// tiles' thumbnail cache by a key holding the file and the face's box (<see cref="Key"/>). See
/// workfiles/20261027-face-detection.md § People View.
/// </summary>
internal static class FaceCrops
{
    // A key: the file, then the box in fractions; '|' is never in a Windows path.
    private const string Marker = "|face|";

    // The crop's height as a share of the face's, the face's center a little above the crop's.
    private const float HeightShare = 1.9f;
    private const float CenterRaise = 0.05f;

    private static readonly object Last = new();
    private static (string Path, byte[] Pixels, int Width, int Height)? _last;

    /// <summary>The thumbnail cache's key of a face's picture.</summary>
    public static string Key(string fullPath, RectangleF face) => string.Create(
        CultureInfo.InvariantCulture,
        $"{fullPath}{Marker}{face.X:R},{face.Y:R},{face.Width:R},{face.Height:R}");

    /// <summary>The file and the box a key names; false for a file's own thumbnail key.</summary>
    public static bool TryParse(string key, out string fullPath, out RectangleF face)
    {
        fullPath = key;
        face = RectangleF.Empty;
        int at = key.LastIndexOf(Marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        string[] values = key[(at + Marker.Length)..].Split(',');
        if (values.Length != 4 || !values.All(v => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        fullPath = key[..at];
        var f = values.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        face = new RectangleF(f[0], f[1], f[2], f[3]);
        return true;
    }

    /// <summary>
    /// The face cut out of its file, fitted into <paramref name="box"/>; null when the file cannot be
    /// decoded. The last file decoded is kept: a photo's faces are asked for one after the other.
    /// </summary>
    public static Bitmap? Load(string fullPath, RectangleF face, Size box)
    {
        byte[] pixels;
        int width;
        int height;
        lock (Last)
        {
            if (_last is { } last && string.Equals(last.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                (_, pixels, width, height) = last;
            }
            else if (ContentExtractor.DecodePixels(fullPath) is var (decoded, w, h))
            {
                _last = (fullPath, decoded, w, h);
                (pixels, width, height) = (decoded, w, h);
            }
            else
            {
                return null;
            }
        }

        var crop = Crop(face, width, height);
        using var source = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppPArgb);
        var data = source.LockBits(new Rectangle(0, 0, crop.Width, crop.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < crop.Height; y++)
            {
                Marshal.Copy(pixels, ((crop.Y + y) * width + crop.X) * 4, data.Scan0 + y * data.Stride, crop.Width * 4);
            }
        }
        finally
        {
            source.UnlockBits(data);
        }

        double scale = Math.Min(1, Math.Min((double)box.Width / crop.Width, (double)box.Height / crop.Height));
        var fitted = new Bitmap(Math.Max(1, (int)Math.Round(crop.Width * scale)), Math.Max(1, (int)Math.Round(crop.Height * scale)), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(fitted);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, fitted.Width, fitted.Height), new Rectangle(0, 0, crop.Width, crop.Height), GraphicsUnit.Pixel);
        return fitted;
    }

    /// <summary>Forgets the last file decoded, its memory given back.</summary>
    public static void Release()
    {
        lock (Last)
        {
            _last = null;
        }
    }

    /// <summary>
    /// Where the face's picture is cut, in the image's pixels: a 4:3 rectangle <see cref="HeightShare"/>
    /// times as tall as the face, centered on it a little low, shrunk to fit the image and slid inside it.
    /// </summary>
    private static Rectangle Crop(RectangleF face, int width, int height)
    {
        float faceW = face.Width * width;
        float faceH = face.Height * height;
        float h = Math.Max(faceH * HeightShare, faceW * HeightShare * 3 / 4);
        float w = h * 4 / 3;
        float fit = Math.Min(1, Math.Min(width / w, height / h));
        w *= fit;
        h *= fit;
        float cx = (face.X + face.Width / 2) * width;
        float cy = (face.Y + face.Height / 2) * height + h * CenterRaise;
        int cw = Math.Clamp((int)Math.Round(w), 1, width);
        int ch = Math.Clamp((int)Math.Round(h), 1, height);
        int x = Math.Clamp((int)Math.Round(cx - w / 2), 0, width - cw);
        int y = Math.Clamp((int)Math.Round(cy - h / 2), 0, height - ch);
        return new Rectangle(x, y, cw, ch);
    }
}
