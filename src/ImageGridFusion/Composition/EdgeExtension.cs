using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// The Background's extending modes: the image's edge pixels — the outermost row or column of the image
/// as drawn in the cell — stretched over the bands out to the cell's edges, the corners filled by the
/// mode, laid over the flat fill at the edge opacity, blended into it with the distance and softened
/// if asked, then painted at the background's opacity.
/// </summary>
internal static class EdgeExtension
{
    // Softened, a pixel of the extension averages its edge over a window this many pixels wide on each
    // side per pixel of distance from the image: sharp against it, softer further out, and relative to
    // the cell, so the preview and an export at another size look the same.
    private const double SoftenSpread = 0.5;

    // The opacity of the line a Miter corner draws over its diagonal, in the image's corner pixel color.
    private const double MiterLineOpacity = 0.5;

    /// <summary>
    /// The rectangle of <paramref name="cell"/> the image covers, drawn at <paramref name="drawn"/>, whose
    /// edges are extended; null when it covers the whole cell, or nothing. A turned image covers it with
    /// its antialiased edges around it: the rectangle is taken a pixel inside.
    /// </summary>
    public static Rectangle? Covered(RectangleF drawn, Rectangle cell, bool turned)
    {
        var covered = Rectangle.Intersect(Rectangle.Round(drawn), cell);
        if (turned)
        {
            covered.Inflate(-1, -1);
        }

        return covered.Width < 1 || covered.Height < 1 || covered == cell ? null : covered;
    }

    /// <summary>
    /// Paints the background of <paramref name="cell"/> on <paramref name="g"/>: <paramref name="fill"/>,
    /// made opaque, under the image's place <paramref name="covered"/>, the extension around it read from
    /// <paramref name="image"/> — the image alone, drawn at the cell's size — the whole at the opacity.
    /// </summary>
    public static void Draw(Graphics g, Bitmap image, Rectangle cell, Rectangle covered, BackgroundEffect background, Color fill)
    {
        var inside = covered with { X = covered.X - cell.X, Y = covered.Y - cell.Y };
        int width = cell.Width;
        int height = cell.Height;
        var edges = Edges.Read(image, inside);
        var flat = new Pixel(255, fill.R, fill.G, fill.B);
        int flatArgb = flat.Argb;
        var pixels = new int[width * height];
        Array.Fill(pixels, flatArgb);
        double spread = background.Soften ? SoftenSpread : 0;

        for (int y = 0; y < height; y++)
        {
            int dy = y < inside.Top ? inside.Top - y : y >= inside.Bottom ? y - inside.Bottom + 1 : 0;
            int depthY = y < inside.Top ? inside.Top : height - inside.Bottom;
            for (int x = 0; x < width; x++)
            {
                int dx = x < inside.Left ? inside.Left - x : x >= inside.Right ? x - inside.Right + 1 : 0;
                if (dx == 0 && dy == 0)
                {
                    // The image's place keeps the flat fill: its transparent pixels show it.
                    x = inside.Right - 1;
                    continue;
                }

                int depthX = x < inside.Left ? inside.Left : width - inside.Right;
                var row = y < inside.Top ? edges.Top : edges.Bottom;
                var column = x < inside.Left ? edges.Left : edges.Right;
                Pixel? sample;
                double distance;
                if (dx == 0)
                {
                    sample = row.Sample(x - inside.Left, spread * dy);
                    distance = (dy - 0.5) / depthY;
                }
                else if (dy == 0)
                {
                    sample = column.Sample(y - inside.Top, spread * dx);
                    distance = (dx - 0.5) / depthX;
                }
                else
                {
                    // A corner: the row and the column meet at the image's corner pixel.
                    int rowEnd = x < inside.Left ? 0 : row.Count - 1;
                    int rowInward = x < inside.Left ? 1 : -1;
                    int columnEnd = y < inside.Top ? 0 : column.Count - 1;
                    int columnInward = y < inside.Top ? 1 : -1;
                    sample = background.Mode switch
                    {
                        BackgroundFill.CornerPixel => Pixel.Mix(
                            row.Sample(rowEnd, spread * dy),
                            column.Sample(columnEnd, spread * dx),
                            dy / (double)(dx + dy)),

                        BackgroundFill.Miter => Miter(row, column, rowEnd, rowInward, columnEnd, columnInward, dx, dy, depthX, depthY),
                        _ => null,
                    };
                    distance = Math.Max((dx - 0.5) / depthX, (dy - 0.5) / depthY);
                }

                // The edge opacity lets the flat fill through, then the blend goes to it with the distance.
                pixels[y * width + x] = sample is { } s
                    ? s.Over(flat).Toward(flat, 1 - background.EdgeOpacity).Toward(flat, background.Blend * Math.Clamp(distance, 0, 1)).Argb
                    : flatArgb;
            }
        }

        using var back = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = back.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, data.Scan0 + y * data.Stride, width);
            }
        }
        finally
        {
            back.UnlockBits(data);
        }

        // TileFlipXY keeps the edges of the bitmap opaque, as for the image itself.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        if (background.Opacity < 1)
        {
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)background.Opacity });
        }

        g.DrawImage(back, cell, 0, 0, width, height, GraphicsUnit.Pixel, attributes);
    }

    /// <summary>
    /// A Miter corner's pixel, <paramref name="dx"/> and <paramref name="dy"/> pixels out from the image's
    /// corner in a corner <paramref name="depthX"/> by <paramref name="depthY"/>: split on the diagonal from
    /// the image's corner to the cell's, the row's side flat in the row's pixel next to the corner, the
    /// column's side in the column's; the corner pixel's color laid at half opacity over the diagonal.
    /// </summary>
    private static Pixel Miter(Line row, Line column, int rowEnd, int rowInward, int columnEnd, int columnInward, int dx, int dy, int depthX, int depthY)
    {
        var part = dy * (long)depthX >= dx * (long)depthY
            ? row.Sample(rowEnd + rowInward, 0)
            : column.Sample(columnEnd + columnInward, 0);

        // The 1 px line: the pixels whose center lies within half a pixel of the diagonal.
        double across = Math.Abs((dx - 0.5) * depthY - (dy - 0.5) * depthX) / Math.Sqrt((double)depthX * depthX + (double)depthY * depthY);
        return across < 0.5 ? Pixel.Mix(row.Sample(rowEnd, 0), part, MiterLineOpacity) : part;
    }

    /// <summary>A color with straight (not premultiplied) channels, from 0 to 255.</summary>
    private readonly record struct Pixel(double A, double R, double G, double B)
    {
        public int Argb => (Byte(this.A) << 24) | (Byte(this.R) << 16) | (Byte(this.G) << 8) | Byte(this.B);

        public static Pixel FromArgb(int argb) => new((argb >> 24) & 0xFF, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);

        /// <summary>This color laid over the opaque <paramref name="under"/>: opaque, the under one showing through its transparency.</summary>
        public Pixel Over(Pixel under)
        {
            double a = this.A / 255;
            return new(255, this.R * a + under.R * (1 - a), this.G * a + under.G * (1 - a), this.B * a + under.B * (1 - a));
        }

        /// <summary><paramref name="amount"/> of the way to <paramref name="other"/>.</summary>
        public Pixel Toward(Pixel other, double amount) => amount <= 0 ? this : new(
            this.A + (other.A - this.A) * amount,
            this.R + (other.R - this.R) * amount,
            this.G + (other.G - this.G) * amount,
            this.B + (other.B - this.B) * amount);

        /// <summary><paramref name="first"/> at <paramref name="weight"/>, <paramref name="second"/> at the rest, their transparency weighed in.</summary>
        public static Pixel Mix(Pixel first, Pixel second, double weight)
        {
            double a = first.A * weight + second.A * (1 - weight);
            if (a <= 0)
            {
                return default;
            }

            double Channel(double one, double two) => (one * first.A * weight + two * second.A * (1 - weight)) / a;
            return new(a, Channel(first.R, second.R), Channel(first.G, second.G), Channel(first.B, second.B));
        }

        private static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 255));
    }

    /// <summary>The image's four outermost lines of pixels, each read once.</summary>
    private sealed record Edges(Line Top, Line Bottom, Line Left, Line Right)
    {
        public static Edges Read(Bitmap image, Rectangle inside)
        {
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride / 4;
                var pixels = new int[stride * image.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                int[] Row(int y) => Enumerable.Range(inside.Left, inside.Width).Select(x => pixels[y * stride + x]).ToArray();
                int[] Column(int x) => Enumerable.Range(inside.Top, inside.Height).Select(y => pixels[y * stride + x]).ToArray();
                return new(new Line(Row(inside.Top)), new Line(Row(inside.Bottom - 1)), new Line(Column(inside.Left)), new Line(Column(inside.Right - 1)));
            }
            finally
            {
                image.UnlockBits(data);
            }
        }
    }

    /// <summary>
    /// One line of edge pixels, with running sums of its premultiplied channels, so the average over any
    /// window — past either end, the end pixel repeating — costs the same.
    /// </summary>
    private sealed class Line
    {
        private readonly Pixel[] _pixels;

        // _sums[i] holds the premultiplied channels of the pixels before i: A, R·A, G·A, B·A.
        private readonly (double A, double R, double G, double B)[] _sums;

        public Line(int[] argb)
        {
            this._pixels = argb.Select(Pixel.FromArgb).ToArray();
            this._sums = new (double, double, double, double)[this._pixels.Length + 1];
            for (int i = 0; i < this._pixels.Length; i++)
            {
                var p = this._pixels[i];
                var s = this._sums[i];
                this._sums[i + 1] = (s.A + p.A, s.R + p.R * p.A, s.G + p.G * p.A, s.B + p.B * p.A);
            }
        }

        public int Count => this._pixels.Length;

        /// <summary>The pixel at <paramref name="center"/>, or, with a <paramref name="radius"/>, the average of the window around it.</summary>
        public Pixel Sample(double center, double radius)
        {
            int last = this._pixels.Length - 1;
            if (radius < 0.5)
            {
                return this._pixels[Math.Clamp((int)Math.Round(center), 0, last)];
            }

            int from = (int)Math.Floor(center - radius);
            int to = (int)Math.Floor(center + radius);
            int count = to - from + 1;
            double a = 0, r = 0, g = 0, b = 0;
            void Add(Pixel p, int times)
            {
                a += p.A * times;
                r += p.R * p.A * times;
                g += p.G * p.A * times;
                b += p.B * p.A * times;
            }

            if (from < 0)
            {
                Add(this._pixels[0], Math.Min(-from, count));
            }

            if (to > last)
            {
                Add(this._pixels[last], Math.Min(to - last, count));
            }

            int start = Math.Clamp(from, 0, last + 1);
            int end = Math.Clamp(to + 1, 0, last + 1);
            if (end > start)
            {
                a += this._sums[end].A - this._sums[start].A;
                r += this._sums[end].R - this._sums[start].R;
                g += this._sums[end].G - this._sums[start].G;
                b += this._sums[end].B - this._sums[start].B;
            }

            return a <= 0 ? new Pixel(0, 0, 0, 0) : new Pixel(a / count, r / a, g / a, b / a);
        }
    }
}
