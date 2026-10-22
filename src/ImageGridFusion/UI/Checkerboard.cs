using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// White and grey squares, the usual sign of transparency, behind the file explorer's thumbnails and
/// the URL zone's preview: finer than the preview's, their side scaled to the box they fill, so every
/// box shows about as many squares whatever its size. See
/// workfiles/20261022-explorer-transparency-checkerboard.md.
/// </summary>
internal static class Checkerboard
{
    /// <summary>The grey squares — the preview's checkerboard's too.</summary>
    public static readonly Color Grey = Color.FromArgb(204, 204, 204);

    // A square's side is the box's height divided by this, never under MinSquare device pixels.
    private const int RowsPerBox = 16;
    private const int MinSquare = 3;

    // Brushes kept, one per square side: a handful in use at a time — the tiles, the Details thumbnails,
    // the strips, the URL preview — more only as the tile size changes, so the cache is emptied past this.
    private const int MaxCached = 32;

    private static readonly Dictionary<int, TextureBrush> Cache = [];

    /// <summary>Fills <paramref name="box"/> with the squares, a white one at its top-left corner.</summary>
    public static void Fill(Graphics g, Rectangle box)
    {
        if (box.Width <= 0 || box.Height <= 0)
        {
            return;
        }

        var brush = BrushOf(Math.Max(MinSquare, box.Height / RowsPerBox));

        // Anchored on the box, so the squares do not slide when the list scrolls.
        brush.ResetTransform();
        brush.TranslateTransform(box.X, box.Y);

        // Sharp squares: nearest texel, sampled at the pixels' centers.
        var interpolation = g.InterpolationMode;
        var offset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.FillRectangle(brush, box);
        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = offset;
    }

    private static TextureBrush BrushOf(int square)
    {
        if (Cache.TryGetValue(square, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= MaxCached)
        {
            foreach (var old in Cache.Values)
            {
                old.Dispose();
            }

            Cache.Clear();
        }

        // The brush keeps its own copy of the tile.
        using var tile = new Bitmap(2 * square, 2 * square);
        using (var g = Graphics.FromImage(tile))
        {
            using var grey = new SolidBrush(Grey);
            g.Clear(Color.White);
            g.FillRectangle(grey, square, 0, square, square);
            g.FillRectangle(grey, 0, square, square, square);
        }

        var brush = new TextureBrush(tile);
        Cache[square] = brush;
        return brush;
    }
}
