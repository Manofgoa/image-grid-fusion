using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// The Seams global effect as drawn on one grid: each pixel near a seam takes the average of the flat
/// fills under a square around it, as wide as twice the depth — so along a straight seam, each cell goes
/// linearly from its own fill to the half-and-half mix on the seam, a corner mixes every cell meeting
/// there, and a cell drawn again on its own meets its neighbours without a step. The average is taken
/// with the alpha (premultiplied), over the cells that fade only: an extending background keeps its
/// seams sharp, and an off one fades as a transparent fill. The canvas's edges have no seam.
/// </summary>
public sealed class SeamField
{
    private readonly Rectangle[] _cells;
    private readonly Color?[] _fills;
    private readonly int _depth;
    private readonly Rectangle _canvas;

    private SeamField(Rectangle[] cells, Color?[] fills, int depth)
    {
        this._cells = cells;
        this._fills = fills;
        this._depth = depth;
        this._canvas = cells.Aggregate(Rectangle.Union);
    }

    /// <summary>Per cell, the flat fill that fades; <c>null</c> for a cell whose seams stay sharp.</summary>
    public IReadOnlyList<Color?> Fills => this._fills;

    /// <summary>
    /// The seams of <paramref name="cells"/>, frame i in cell i; <c>null</c> when <paramref name="seams"/>
    /// is off, the <paramref name="borders"/> leave a gap between the cells, or nothing can fade.
    /// </summary>
    public static SeamField? Of(IReadOnlyList<Frame> frames, Rectangle[] cells, SeamFade? seams, GridBorders? borders)
    {
        if (seams is null || borders is { HasGap: true } || cells.Length < 2)
        {
            return null;
        }

        int depth = (int)Math.Round(seams.Depth * cells.Min(c => Math.Min(c.Width, c.Height)));
        if (depth < 1)
        {
            return null;
        }

        var fills = new Color?[cells.Length];
        for (int i = 0; i < cells.Length && i < frames.Count; i++)
        {
            fills[i] = Compositor.FlatFill(frames[i], cells[i]);
        }

        return fills.Count(f => f is not null) < 2 ? null : new SeamField(cells, fills, depth);
    }

    /// <summary>
    /// Draws the flat fill of <paramref name="cell"/>, its seams faded: the inside plain, a strip along
    /// each side that has a neighbour. <c>false</c>, nothing drawn, when the cell does not fade.
    /// </summary>
    public bool Draw(Graphics g, Rectangle cell)
    {
        int index = Array.IndexOf(this._cells, cell);
        if (index < 0 || this._fills[index] is not { } fill)
        {
            return false;
        }

        int top = cell.Top > this._canvas.Top ? Math.Min(this._depth, cell.Height) : 0;
        int bottom = cell.Bottom < this._canvas.Bottom ? Math.Min(this._depth, cell.Height - top) : 0;
        int left = cell.Left > this._canvas.Left ? Math.Min(this._depth, cell.Width) : 0;
        int right = cell.Right < this._canvas.Right ? Math.Min(this._depth, cell.Width - left) : 0;
        if (top + bottom + left + right == 0)
        {
            return false;
        }

        var inside = Rectangle.FromLTRB(cell.Left + left, cell.Top + top, cell.Right - right, cell.Bottom - bottom);
        if (inside.Width > 0 && inside.Height > 0)
        {
            using var brush = new SolidBrush(fill);
            g.FillRectangle(brush, inside);
        }

        // The top and bottom strips span the cell, the side ones lie between them: none overlaps.
        Rectangle[] strips =
        [
            new(cell.Left, cell.Top, cell.Width, top),
            new(cell.Left, cell.Bottom - bottom, cell.Width, bottom),
            new(cell.Left, cell.Top + top, left, cell.Height - top - bottom),
            new(cell.Right - right, cell.Top + top, right, cell.Height - top - bottom),
        ];

        var state = g.Save();
        g.CompositingMode = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        foreach (var strip in strips.Where(s => s.Width > 0 && s.Height > 0))
        {
            using var bitmap = this.Strip(strip);
            g.DrawImage(bitmap, strip, 0, 0, strip.Width, strip.Height, GraphicsUnit.Pixel);
        }

        g.Restore(state);
        return true;
    }

    /// <summary>
    /// The pixels of <paramref name="strip"/>: around each one, a square of side twice the depth, clipped
    /// to the canvas, overlaps each fading cell over a width and a height computed apart; the fills are
    /// averaged by those areas, premultiplied.
    /// </summary>
    private Bitmap Strip(Rectangle strip)
    {
        int[] fading = [.. Enumerable.Range(0, this._cells.Length).Where(k => this._fills[k] is not null)];
        int n = fading.Length;
        var across = new double[n, strip.Width];
        var down = new double[n, strip.Height];
        for (int x = 0; x < strip.Width; x++)
        {
            double center = strip.X + x + 0.5;
            double low = Math.Max(center - this._depth, this._canvas.Left);
            double high = Math.Min(center + this._depth, this._canvas.Right);
            for (int j = 0; j < n; j++)
            {
                var other = this._cells[fading[j]];
                across[j, x] = Math.Max(0, Math.Min(high, other.Right) - Math.Max(low, other.Left));
            }
        }

        for (int y = 0; y < strip.Height; y++)
        {
            double center = strip.Y + y + 0.5;
            double low = Math.Max(center - this._depth, this._canvas.Top);
            double high = Math.Min(center + this._depth, this._canvas.Bottom);
            for (int j = 0; j < n; j++)
            {
                var other = this._cells[fading[j]];
                down[j, y] = Math.Max(0, Math.Min(high, other.Bottom) - Math.Max(low, other.Top));
            }
        }

        // Premultiplied channels of each fading fill.
        var premultiplied = new (double A, double R, double G, double B)[n];
        for (int j = 0; j < n; j++)
        {
            var color = this._fills[fading[j]]!.Value;
            double alpha = color.A / 255.0;
            premultiplied[j] = (color.A, color.R * alpha, color.G * alpha, color.B * alpha);
        }

        var pixels = new int[strip.Width * strip.Height];
        for (int y = 0; y < strip.Height; y++)
        {
            for (int x = 0; x < strip.Width; x++)
            {
                double total = 0, a = 0, r = 0, gr = 0, b = 0;
                for (int j = 0; j < n; j++)
                {
                    double weight = across[j, x] * down[j, y];
                    if (weight <= 0)
                    {
                        continue;
                    }

                    total += weight;
                    a += weight * premultiplied[j].A;
                    r += weight * premultiplied[j].R;
                    gr += weight * premultiplied[j].G;
                    b += weight * premultiplied[j].B;
                }

                if (total <= 0)
                {
                    continue;
                }

                int alpha = Channel(a / total, 255);
                pixels[y * strip.Width + x] = alpha << 24 | Channel(r / total, alpha) << 16 | Channel(gr / total, alpha) << 8 | Channel(b / total, alpha);
            }
        }

        var bitmap = new Bitmap(strip.Width, strip.Height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, strip.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < strip.Height; y++)
            {
                Marshal.Copy(pixels, y * strip.Width, data.Scan0 + y * data.Stride, strip.Width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>A channel rounded and kept within 0 and <paramref name="max"/>: a premultiplied color never exceeds its alpha.</summary>
    private static int Channel(double value, int max) => Math.Clamp((int)Math.Round(value), 0, max);
}
