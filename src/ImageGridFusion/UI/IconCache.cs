using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ImageGridFusion.UI;

/// <summary>
/// The emoji icons already drawn, kept as PNGs in <see cref="FolderName"/> next to the exe, so a later start reads them
/// instead of drawing them again (workfiles/20261105-initial-load-speed.md § A). One subfolder per <see cref="Stamp"/> —
/// the drawing's version and the emoji font's file —, the others left behind by an older drawing or font deleted the
/// first time this one writes. Written by the normal instance only: a <c>--new-instance</c> reads it and writes nothing
/// (RULES.md § Single Instance).
/// </summary>
internal sealed class IconCache
{
    /// <summary>The cache's folder, next to the exe (../CLAUDE.md § Folder Names).</summary>
    public const string FolderName = "icon-cache";

    /// <summary>The version of <see cref="UiIcon"/>'s emoji drawing: a change of it draws every icon again.</summary>
    public const int DrawingVersion = 1;

    private readonly string _root;
    private bool _cleaned;

    public IconCache(string root, string stamp, bool writes)
    {
        this._root = root;
        this.Stamp = stamp;
        this.Writes = writes;
    }

    /// <summary>The app's cache: next to the exe, keyed on Segoe UI Emoji's file; written unless set otherwise.</summary>
    public static IconCache Default { get; set; } =
        new(Path.Combine(AppContext.BaseDirectory, FolderName), StampOf(EmojiFontFile()), writes: true);

    /// <summary>What the icons kept were drawn with: the drawing's version, then the emoji font's file size and date.</summary>
    public string Stamp { get; }

    /// <summary>Whether a drawn icon is written to the disk: never by a <c>--new-instance</c>.</summary>
    public bool Writes { get; set; }

    /// <summary>The folder of the icons drawn with <see cref="Stamp"/>.</summary>
    public string Folder => Path.Combine(this._root, this.Stamp);

    /// <summary>Segoe UI Emoji's file, the font the emoji icons are drawn with.</summary>
    public static FileInfo EmojiFontFile() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf"));

    /// <summary>The stamp of a drawing with <paramref name="font"/>: <c>v1-…</c>, its size and date in hexadecimal, or <c>none</c> without it.</summary>
    public static string StampOf(FileInfo font)
    {
        string fontPart;
        try
        {
            fontPart = font.Exists
                ? string.Create(CultureInfo.InvariantCulture, $"{font.Length:x}-{font.LastWriteTimeUtc.Ticks:x}")
                : "none";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            fontPart = "none";
        }

        return string.Create(CultureInfo.InvariantCulture, $"v{DrawingVersion}-{fontPart}");
    }

    /// <summary>
    /// The file name of <paramref name="glyph"/> at <paramref name="size"/> device pixels: its code points in hexadecimal,
    /// joined by hyphens, then the size — <c>1f50d_20.png</c>.
    /// </summary>
    public static string FileNameOf(string glyph, int size)
    {
        var points = new List<string>();
        for (int i = 0; i < glyph.Length; i += char.IsSurrogatePair(glyph, i) ? 2 : 1)
        {
            points.Add(char.ConvertToUtf32(glyph, i).ToString("x", CultureInfo.InvariantCulture));
        }

        return string.Create(CultureInfo.InvariantCulture, $"{string.Join('-', points)}_{size}.png");
    }

    /// <summary>The icon kept for <paramref name="glyph"/> at <paramref name="size"/>, a bitmap the caller owns; null when none can be read.</summary>
    public Bitmap? TryRead(string glyph, int size)
    {
        string path = Path.Combine(this.Folder, FileNameOf(glyph, size));
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            // Read whole, so the file is never held open, then copied into a bitmap of its own.
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            using var read = new Bitmap(stream);
            if (read.Width != size || read.Height != size)
            {
                return null;
            }

            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImageUnscaled(read, 0, 0);
            }

            return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps <paramref name="icon"/> as <paramref name="glyph"/> at <paramref name="size"/>, off the UI thread, when
    /// <see cref="Writes"/>: a copy is written, the caller keeps its bitmap. A write that fails is left: the icon is drawn
    /// again at the next start.
    /// </summary>
    public Task Write(string glyph, int size, Bitmap icon)
    {
        if (!this.Writes)
        {
            return Task.CompletedTask;
        }

        var copy = new Bitmap(icon);
        bool clean = !this._cleaned;
        this._cleaned = true;
        return Task.Run(() =>
        {
            using (copy)
            {
                try
                {
                    if (clean)
                    {
                        this.DeleteOtherStamps();
                    }

                    Directory.CreateDirectory(this.Folder);
                    string path = Path.Combine(this.Folder, FileNameOf(glyph, size));

                    // Written aside, then moved over: a start reading it meanwhile never sees half a file.
                    string partial = path + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".partial";
                    copy.Save(partial, ImageFormat.Png);
                    File.Move(partial, path, overwrite: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ExternalException)
                {
                    // Drawn again next time.
                }
            }
        });
    }

    // The folders of an older drawing or emoji font, never read again.
    private void DeleteOtherStamps()
    {
        if (!Directory.Exists(this._root))
        {
            return;
        }

        foreach (string folder in Directory.EnumerateDirectories(this._root))
        {
            if (!string.Equals(Path.GetFileName(folder), this.Stamp, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Left for another start.
                }
            }
        }
    }
}
