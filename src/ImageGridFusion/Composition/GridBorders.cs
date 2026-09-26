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
/// frame, or brackets over the images at the grid's four corners. Its width is a fraction of the
/// canvas's shorter side, so the preview and every export size look the same.
/// </summary>
public sealed record GridBorders(BorderPattern Pattern, double Thickness, bool OuterFrame, Color Color)
{
    public const double MinThickness = 0.001;
    public const double MaxThickness = 0.06;
    public const double DefaultThickness = 0.006;

    /// <summary>Share of the grid edge each arm of a corner bracket covers.</summary>
    public const double CornerArm = 0.1;

    /// <summary>The borders as the app starts and as Clear all leaves them, in <paramref name="color"/>.</summary>
    public static GridBorders Initial(Color color) => new(BorderPattern.Corners, DefaultThickness, false, color);

    /// <summary>Whether the cells shrink to leave a gap between them: every style but the corners.</summary>
    public bool HasGap => Pattern != BorderPattern.Corners;

    /// <summary>The border width on <paramref name="canvas"/>, in pixels, at least one.</summary>
    public int Width(Size canvas) => Math.Max(1, (int)Math.Round(Thickness * Math.Min(canvas.Width, canvas.Height)));

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

        g.SetClip(gap, System.Drawing.Drawing2D.CombineMode.Intersect);
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
    }

    /// <summary>
    /// Draws what lies over the images — the corner brackets, nothing for the other styles: what a cell
    /// drawn again must repaint over itself.
    /// </summary>
    public void DrawOver(Graphics g, Size canvas)
    {
        if (HasGap)
        {
            return;
        }

        int width = Width(canvas);
        int armX = Math.Max(width, (int)Math.Round(canvas.Width * CornerArm));
        int armY = Math.Max(width, (int)Math.Round(canvas.Height * CornerArm));
        int right = canvas.Width - width, bottom = canvas.Height - width;
        using var brush = new SolidBrush(Color);
        g.FillRectangles(brush,
        [
            new(0, 0, armX, width), new(0, 0, width, armY),
            new(canvas.Width - armX, 0, armX, width), new(right, 0, width, armY),
            new(0, bottom, armX, width), new(0, canvas.Height - armY, width, armY),
            new(canvas.Width - armX, bottom, armX, width), new(right, canvas.Height - armY, width, armY),
        ]);
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
            yield return new Band(true, 0, 0, canvas.Height);
            yield return new Band(true, canvas.Width - width, 0, canvas.Height);
            yield return new Band(false, 0, 0, canvas.Width);
            yield return new Band(false, canvas.Height - width, 0, canvas.Width);
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
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
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
