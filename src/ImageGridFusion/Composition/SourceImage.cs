namespace ImageGridFusion.Composition;

/// <summary>
/// An image of the grid: an owned 32bpp copy, its dominant color and the file it came from, if any.
/// A file with several pages (PDF, video, long text) keeps its <see cref="Pages"/> and shows one of them.
/// </summary>
public sealed class SourceImage : IDisposable
{
    public SourceImage(Bitmap bitmap, string? filePath, PageSource? pages = null, int page = 0)
    {
        Bitmap = bitmap;
        FilePath = filePath;
        Pages = pages;
        Page = page;
        Dominant = DominantColor.Compute(bitmap);
    }

    public Bitmap Bitmap { get; private set; }

    public string? FilePath { get; }

    public PageSource? Pages { get; }

    /// <summary>Index of the page <see cref="Bitmap"/> shows, in <see cref="Pages"/>.</summary>
    public int Page { get; private set; }

    public Color Dominant { get; private set; }

    public Size Size => Bitmap.Size;

    public bool IsDisposed { get; private set; }

    /// <summary>Shows another page: takes ownership of <paramref name="bitmap"/> and disposes the previous one.</summary>
    public void ShowPage(int page, Bitmap bitmap)
    {
        var previous = Bitmap;
        Bitmap = bitmap;
        Page = page;
        Dominant = DominantColor.Compute(bitmap);
        previous.Dispose();
    }

    public void Dispose()
    {
        IsDisposed = true;
        Bitmap.Dispose();
        Pages?.Dispose();
    }
}
