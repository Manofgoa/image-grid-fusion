using System.Drawing.Imaging;
using ImageGridFusion.UI;

namespace ImageGridFusion.Tests.UI;

/// <summary>The emoji icons' disk cache (workfiles/20261105-initial-load-speed.md § A).</summary>
[TestClass]
public sealed class IconCacheTests
{
    private string _root = "";

    [TestInitialize]
    public void CreateRoot()
    {
        this._root = Path.Combine(Path.GetTempPath(), "icon-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(this._root))
        {
            Directory.Delete(this._root, recursive: true);
        }
    }

    [TestMethod]
    public void FileNameChangesWithTheGlyphAndTheSize()
    {
        Assert.AreEqual("1f50d_20.png", IconCache.FileNameOf("\U0001F50D", 20));
        Assert.AreEqual("1f50d_32.png", IconCache.FileNameOf("\U0001F50D", 32));
        Assert.AreEqual("1f5bc-fe0f_20.png", IconCache.FileNameOf("\U0001F5BC️", 20));
        Assert.AreEqual("1f468-200d-1f9b0_16.png", IconCache.FileNameOf("\U0001F468‍\U0001F9B0", 16));
    }

    [TestMethod]
    public void StampChangesWithTheEmojiFontsFile()
    {
        string font = Path.Combine(this._root, "font.ttf");
        File.WriteAllBytes(font, new byte[10]);
        File.SetLastWriteTimeUtc(font, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        string first = IconCache.StampOf(new FileInfo(font));

        File.WriteAllBytes(font, new byte[11]);
        File.SetLastWriteTimeUtc(font, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        string resized = IconCache.StampOf(new FileInfo(font));

        File.SetLastWriteTimeUtc(font, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        string redated = IconCache.StampOf(new FileInfo(font));

        Assert.AreNotEqual(first, resized);
        Assert.AreNotEqual(resized, redated);
        Assert.IsTrue(first.StartsWith($"v{IconCache.DrawingVersion}-", StringComparison.Ordinal), first);
        Assert.AreEqual($"v{IconCache.DrawingVersion}-none", IconCache.StampOf(new FileInfo(Path.Combine(this._root, "missing.ttf"))));
    }

    [TestMethod]
    public async Task IconReadBackIsTheOneWritten()
    {
        var cache = new IconCache(this._root, "v1-test", writes: true);
        using var icon = Sample(20);

        await cache.Write("\U0001F50D", 20, icon);
        using var read = cache.TryRead("\U0001F50D", 20);

        Assert.IsNotNull(read);
        Assert.AreEqual(PixelFormat.Format32bppArgb, read.PixelFormat);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                Assert.AreEqual(icon.GetPixel(x, y).ToArgb(), read.GetPixel(x, y).ToArgb(), $"Pixel {x}, {y}");
            }
        }
    }

    [TestMethod]
    public void MissingOrUnreadableFileReadsAsNoEntry()
    {
        var cache = new IconCache(this._root, "v1-test", writes: true);
        Assert.IsNull(cache.TryRead("\U0001F50D", 20));

        Directory.CreateDirectory(cache.Folder);
        File.WriteAllText(Path.Combine(cache.Folder, IconCache.FileNameOf("\U0001F50D", 20)), "not a png");
        Assert.IsNull(cache.TryRead("\U0001F50D", 20));
    }

    [TestMethod]
    public async Task IconOfAnotherSizeReadsAsNoEntry()
    {
        var cache = new IconCache(this._root, "v1-test", writes: true);
        using var icon = Sample(16);
        await cache.Write("\U0001F50D", 20, icon);

        Assert.IsNull(cache.TryRead("\U0001F50D", 20));
    }

    [TestMethod]
    public async Task InstanceNotWritingReadsAndWritesNothing()
    {
        var writer = new IconCache(this._root, "v1-test", writes: true);
        using var icon = Sample(20);
        await writer.Write("\U0001F50D", 20, icon);

        var reader = new IconCache(this._root, "v1-test", writes: false);
        await reader.Write("\U0001F50E", 20, icon);

        using var read = reader.TryRead("\U0001F50D", 20);
        Assert.IsNotNull(read);
        Assert.IsFalse(File.Exists(Path.Combine(reader.Folder, IconCache.FileNameOf("\U0001F50E", 20))));
    }

    [TestMethod]
    public async Task FirstWriteDeletesTheOtherStamps()
    {
        string old = Path.Combine(this._root, "v0-old");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "1f50d_20.png"), "");

        var cache = new IconCache(this._root, "v1-test", writes: true);
        using var icon = Sample(20);
        await cache.Write("\U0001F50D", 20, icon);

        Assert.IsFalse(Directory.Exists(old));
        Assert.IsTrue(Directory.Exists(cache.Folder));
    }

    // A square with transparent, half-transparent and opaque pixels.
    private static Bitmap Sample(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bitmap.SetPixel(x, y, Color.FromArgb((x * 13) % 256, (y * 29) % 256, (x + y) * 5 % 256, (x * y) % 256));
            }
        }

        return bitmap;
    }
}
