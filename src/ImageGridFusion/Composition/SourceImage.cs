namespace ImageGridFusion.Composition;

/// <summary>An image of the grid: an owned 32bpp copy, its dominant color and the file it came from, if any.</summary>
public sealed class SourceImage : IDisposable
{
    public SourceImage(Bitmap bitmap, string? filePath)
    {
        Bitmap = bitmap;
        FilePath = filePath;
        Dominant = DominantColor.Compute(bitmap);
    }

    public Bitmap Bitmap { get; }

    public string? FilePath { get; }

    public Color Dominant { get; }

    public Size Size => Bitmap.Size;

    public void Dispose() => Bitmap.Dispose();
}
