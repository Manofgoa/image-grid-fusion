using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>How the borders are drawn: brackets at the grid's corners, or a line style filling the gap between the cells.</summary>
public enum BorderPattern
{
    Corners,
    Solid,
    Dashed,
    Dotted,
    Double,
}

/// <summary>
/// The Borders global effect: a gap between the cells filled by a line style, with an optional outer
/// frame, or brackets over the images at the grid's four corners; with <paramref name="Rounded"/>, the
/// grid's outer corners rounded the way Twitter / X shows a posted image, the brackets and the frame
/// following the curve. Its width is a fraction of the canvas's shorter side, so the preview and every
/// export size look the same.
/// </summary>
public sealed record GridBorders(BorderPattern Pattern, double Thickness, bool OuterFrame, Color Color, bool Rounded)
{
    public const double MinThickness = 0.001;
    public const double MaxThickness = 0.06;
    public const double DefaultThickness = 0.006;

    /// <summary>Share of the grid edge each arm of a corner bracket covers.</summary>
    public const double CornerArm = 0.1;

    /// <summary>
    /// Share of the grid's longer side the radius of the Twitter corners takes: Twitter / X rounds a
    /// posted image by 16 CSS px, and shows it about 540 px across its longer side.
    /// </summary>
    public const double CornerRadiusShare = 0.03;

    /// <summary>The borders as the app starts and as Clear all leaves them, in <paramref name="color"/>, their corners <paramref name="rounded"/> or not.</summary>
    public static GridBorders Initial(Color color, bool rounded) => new(BorderPattern.Corners, DefaultThickness, false, color, rounded);

    /// <summary>Whether the cells shrink to leave a gap between them: every style but the corners.</summary>
    public bool HasGap => Pattern != BorderPattern.Corners;

    /// <summary>The border width on <paramref name="canvas"/>, in pixels, at least one.</summary>
    public int Width(Size canvas) => Math.Max(1, (int)Math.Round(Thickness * Math.Min(canvas.Width, canvas.Height)));

    /// <summary>The radius of the grid's rounded corners on <paramref name="canvas"/>, in pixels; 0 when they are square.</summary>
    public float Radius(Size canvas) => Rounded ? (float)(CornerRadiusShare * Math.Max(canvas.Width, canvas.Height)) : 0f;

    /// <summary><paramref name="bounds"/> with its corners rounded by <paramref name="radius"/>; a plain rectangle when it is 0 or less.</summary>
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(2 * radius, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// The cells as drawn: each shrinks by half the width on every edge it shares with a neighbour, and,
    /// with the outer frame, by the whole width on the canvas edge. Unchanged in the corners style.
    /// </summary>
    public Rectangle[] Inset(Rectangle[] slots, Size canvas)
    {
        if (!HasGap)
        {
            return slots;
        }

        int width = Width(canvas);
        int half = width / 2;
        int outer = OuterFrame ? width : 0;
        return slots.Select(s =>
        {
            // The cell before a boundary ends half a width short of it, the one after starts a width later.
            int left = s.Left == 0 ? outer : s.Left - half + width;
            int top = s.Top == 0 ? outer : s.Top - half + width;
            int right = s.Right >= canvas.Width ? canvas.Width - outer : s.Right - half;
            int bottom = s.Bottom >= canvas.Height ? canvas.Height - outer : s.Bottom - half;
            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }).ToArray();
    }

    /// <summary>
    /// Draws the borders of the cells <paramref name="slots"/> (undrawn, as the layout tiles the canvas):
    /// the gap between them filled by the style, never over the images, or the corner brackets.
    /// </summary>
    public void Draw(Graphics g, Rectangle[] slots, Size canvas)
    {
        if (!HasGap)
        {
            DrawOver(g, canvas);
            return;
        }

        // What the style leaves unpainted stays transparent, like a cell without background.
        using var clip = g.Clip;
        using var gap = new Region(new Rectangle(Point.Empty, canvas));
        var cells = Inset(slots, canvas);
        foreach (var cell in cells)
        {
            gap.Exclude(cell);
        }

        g.SetClip(gap, CombineMode.Intersect);
        using var brush = new SolidBrush(Color);
        if (Pattern == BorderPattern.Solid)
        {
            g.FillRegion(brush, gap);
        }
        else
        {
            int width = Width(canvas);
            foreach (var band in Bands(slots, cells, canvas, width))
            {
                DrawBand(g, brush, band, width);
            }
        }

        g.Clip = clip;
        DrawOver(g, canvas);
    }

    /// <summary>
    /// Draws what lies over the images — the corner brackets; with rounded corners, the corners of the
    /// outer frame, which follow the curve over the corner cells; nothing else: what a cell drawn again
    /// must repaint over itself.
    /// </summary>
    public void DrawOver(Graphics g, Size canvas)
    {
        int width = Width(canvas);
        float radius = Radius(canvas);
        using var brush = new SolidBrush(Color);
        if (HasGap)
        {
            if (OuterFrame && radius > 0)
            {
                DrawFrameCorners(g, brush, canvas, width, radius);
            }

            return;
        }

        // An arm is at least as long as the radius, so the curve always fits in its bracket.
        int reach = (int)Math.Ceiling(radius);
        int armX = Math.Max(Math.Max(width, reach), (int)Math.Round(canvas.Width * CornerArm));
        int armY = Math.Max(Math.Max(width, reach), (int)Math.Round(canvas.Height * CornerArm));
        int right = canvas.Width - width, bottom = canvas.Height - width;
        if (radius <= 0)
        {
            g.FillRectangles(brush,
            [
                new(0, 0, armX, width), new(0, 0, width, armY),
                new(canvas.Width - armX, 0, armX, width), new(right, 0, width, armY),
                new(0, bottom, armX, width), new(0, canvas.Height - armY, width, armY),
                new(canvas.Width - armX, bottom, armX, width), new(right, canvas.Height - armY, width, armY),
            ]);
            return;
        }

        // Each bracket is the ring along the rounded outline, a width deep, kept within the reach of its arms.
        using var ring = Ring(canvas, radius, 0, width);
        FillWithin(g, brush, ring,
        [
            new(0, 0, armX, armY), new(canvas.Width - armX, 0, armX, armY),
            new(0, canvas.Height - armY, armX, armY), new(canvas.Width - armX, canvas.Height - armY, armX, armY),
        ]);
    }

    /// <summary>
    /// Makes the grid's rounded-off corners transparent on <paramref name="bitmap"/>, the whole canvas,
    /// with an anti-aliased edge: for the outputs keeping alpha, and the preview. Outputs without alpha
    /// keep their corners, Twitter / X rounding them itself. Nothing happens with square corners.
    /// </summary>
    public void CutCorners(Bitmap bitmap) => CutCorners(bitmap, new Rectangle(Point.Empty, bitmap.Size));

    /// <summary>
    /// <see cref="CutCorners(Bitmap)"/> within <paramref name="area"/> only: a cell drawn again is cut
    /// once, the pixels around it, already cut, left alone.
    /// </summary>
    public void CutCorners(Bitmap bitmap, Rectangle area)
    {
        float radius = Radius(bitmap.Size);
        if (radius <= 0)
        {
            return;
        }

        int reach = (int)Math.Ceiling(radius);
        int w = bitmap.Width, h = bitmap.Height;
        var corners = new[]
        {
            (Square: new Rectangle(0, 0, reach, reach), X: radius, Y: radius, SignX: -1, SignY: -1),
            (Square: new Rectangle(w - reach, 0, reach, reach), X: w - radius, Y: radius, SignX: 1, SignY: -1),
            (Square: new Rectangle(0, h - reach, reach, reach), X: radius, Y: h - radius, SignX: -1, SignY: 1),
            (Square: new Rectangle(w - reach, h - reach, reach, reach), X: w - radius, Y: h - radius, SignX: 1, SignY: 1),
        };
        var bounds = Rectangle.Intersect(area, new Rectangle(0, 0, w, h));
        foreach (var corner in corners)
        {
            var part = Rectangle.Intersect(corner.Square, bounds);
            if (part.Width <= 0 || part.Height <= 0)
            {
                continue;
            }

            var data = bitmap.LockBits(part, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[part.Width * 4];
                for (int y = 0; y < part.Height; y++)
                {
                    var line = data.Scan0 + y * data.Stride;
                    Marshal.Copy(line, row, 0, row.Length);
                    float dy = Math.Max(0f, corner.SignY * (part.Y + y + 0.5f - corner.Y));
                    for (int x = 0; x < part.Width; x++)
                    {
                        // How much of the pixel lies inside the curve, from its center's distance to it.
                        float dx = Math.Max(0f, corner.SignX * (part.X + x + 0.5f - corner.X));
                        float coverage = Math.Clamp(radius - MathF.Sqrt(dx * dx + dy * dy) + 0.5f, 0f, 1f);
                        row[4 * x + 3] = (byte)Math.Round(row[4 * x + 3] * coverage);
                    }

                    Marshal.Copy(row, 0, line, row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }

    /// <summary>
    /// The corners of the outer frame along the rounded outline, over the corner cells: the solid ring,
    /// or the double style's two lines.
    /// </summary>
    private void DrawFrameCorners(Graphics g, Brush brush, Size canvas, int width, float radius)
    {
        int reach = Math.Max(width, (int)Math.Ceiling(radius));
        Rectangle[] squares =
        [
            new(0, 0, reach, reach), new(canvas.Width - reach, 0, reach, reach),
            new(0, canvas.Height - reach, reach, reach), new(canvas.Width - reach, canvas.Height - reach, reach, reach),
        ];
        if (Pattern == BorderPattern.Double)
        {
            float line = Math.Max(1f, width / 3f);
            using var outer = Ring(canvas, radius, 0, line);
            using var inner = Ring(canvas, radius, width - line, width);
            FillWithin(g, brush, outer, squares);
            FillWithin(g, brush, inner, squares);
            return;
        }

        using var ring = Ring(canvas, radius, 0, width);
        FillWithin(g, brush, ring, squares);
    }

    /// <summary>
    /// The band between the canvas's outline rounded by <paramref name="radius"/> and inset by
    /// <paramref name="from"/>, and the same inset by <paramref name="to"/>: every outline keeps the
    /// same centers of curvature, its radius shrinking with the inset.
    /// </summary>
    private static GraphicsPath Ring(Size canvas, float radius, float from, float to)
    {
        var bounds = new RectangleF(PointF.Empty, canvas);
        using var outer = RoundedRectangle(RectangleF.Inflate(bounds, -from, -from), radius - from);
        using var inner = RoundedRectangle(RectangleF.Inflate(bounds, -to, -to), radius - to);
        var ring = new GraphicsPath(FillMode.Alternate);
        ring.AddPath(outer, connect: false);
        ring.AddPath(inner, connect: false);
        return ring;
    }

    /// <summary>Fills <paramref name="path"/>, anti-aliased, only within <paramref name="areas"/> and the clip already set.</summary>
    private static void FillWithin(Graphics g, Brush brush, GraphicsPath path, Rectangle[] areas)
    {
        var state = g.Save();
        using var within = new Region();
        within.MakeEmpty();
        foreach (var area in areas)
        {
            within.Union(area);
        }

        g.SetClip(within, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
        g.Restore(state);
    }

    /// <summary>
    /// The stretches of gap to draw a line style along: one per boundary line, cells facing each other
    /// over it merged into one stretch so a shared edge is drawn once; then the outer frame's sides. A
    /// stretch runs along the drawn cells, so it stops where a crossing gap begins.
    /// </summary>
    private IEnumerable<Band> Bands(Rectangle[] slots, Rectangle[] cells, Size canvas, int width)
    {
        int half = width / 2;
        foreach (bool vertical in new[] { true, false })
        {
            int end = vertical ? canvas.Width : canvas.Height;
            var lines = slots.Zip(cells)
                .SelectMany(p => vertical
                    ? new[] { (At: p.First.Left, From: p.Second.Top, To: p.Second.Bottom), (At: p.First.Right, From: p.Second.Top, To: p.Second.Bottom) }
                    : new[] { (At: p.First.Top, From: p.Second.Left, To: p.Second.Right), (At: p.First.Bottom, From: p.Second.Left, To: p.Second.Right) })
                .Where(l => l.At > 0 && l.At < end)
                .GroupBy(l => l.At);
            foreach (var line in lines)
            {
                foreach (var (from, to) in Merged(line.Select(l => (l.From, l.To))))
                {
                    yield return new Band(vertical, line.Key - half, from, to);
                }
            }
        }

        if (OuterFrame)
        {
            // Rounded, the frame's corners are drawn over the cells along the curve (DrawFrameCorners).
            int reach = Radius(canvas) > 0 ? Math.Max(width, (int)Math.Ceiling(Radius(canvas))) : 0;
            yield return new Band(true, 0, reach, canvas.Height - reach);
            yield return new Band(true, canvas.Width - width, reach, canvas.Height - reach);
            yield return new Band(false, 0, reach, canvas.Width - reach);
            yield return new Band(false, canvas.Height - width, reach, canvas.Width - reach);
        }
    }

    /// <summary>Overlapping or touching spans joined, in order.</summary>
    private static IEnumerable<(int From, int To)> Merged(IEnumerable<(int From, int To)> spans)
    {
        (int From, int To)? current = null;
        foreach (var span in spans.OrderBy(s => s.From))
        {
            if (current is { } c && span.From <= c.To)
            {
                current = (c.From, Math.Max(c.To, span.To));
                continue;
            }

            if (current is { } done)
            {
                yield return done;
            }

            current = span;
        }

        if (current is { } last)
        {
            yield return last;
        }
    }

    /// <summary>Fills one stretch of gap, <paramref name="width"/> across, with the dashes, dots or double line of the style.</summary>
    private void DrawBand(Graphics g, Brush brush, Band band, int width)
    {
        // A rectangle across the band from a to b along it, from c to d across it.
        RectangleF Part(float a, float b, float c, float d) => band.Vertical
            ? RectangleF.FromLTRB(band.Across + c, a, band.Across + d, b)
            : RectangleF.FromLTRB(a, band.Across + c, b, band.Across + d);

        switch (Pattern)
        {
            case BorderPattern.Dashed:
                for (float a = band.From; a < band.To; a += 5f * width)
                {
                    g.FillRectangle(brush, Part(a, Math.Min(a + 3f * width, band.To), 0, width));
                }

                break;
            case BorderPattern.Dotted:
                var smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (float a = band.From; a + width <= band.To; a += 2f * width)
                {
                    g.FillEllipse(brush, Part(a, a + width, 0, width));
                }

                g.SmoothingMode = smoothing;
                break;
            case BorderPattern.Double:
                float line = Math.Max(1f, width / 3f);
                g.FillRectangle(brush, Part(band.From, band.To, 0, line));
                g.FillRectangle(brush, Part(band.From, band.To, width - line, width));
                break;
        }
    }

    /// <summary>A stretch of gap: vertical or horizontal, starting <paramref name="Across"/> px from the canvas edge, from <paramref name="From"/> to <paramref name="To"/> along it.</summary>
    private readonly record struct Band(bool Vertical, int Across, int From, int To);
}
