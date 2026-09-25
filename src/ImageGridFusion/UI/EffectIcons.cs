using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// Colored icons of the effects and options rows, drawn at the size asked for, so they stay crisp at
/// any DPI without an image file or a dependency. The caller owns the bitmap.
/// </summary>
internal static class EffectIcons
{
    /// <summary>A magnifying glass: a cyan lens in a dark rim, an orange handle.</summary>
    public static Bitmap Zoom(int size) => Draw(size, (g, s) =>
    {
        float r = s * 0.3f;
        float cx = s * 0.4f;
        float cy = s * 0.4f;
        using (var handle = new Pen(Color.FromArgb(255, 140, 0), s * 0.16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(handle, cx + r * 0.75f, cy + r * 0.75f, s * 0.9f, s * 0.9f);
        }

        using (var lens = new LinearGradientBrush(new RectangleF(cx - r, cy - r, 2 * r, 2 * r), Color.FromArgb(170, 240, 255), Color.FromArgb(0, 150, 230), 45f))
        {
            g.FillEllipse(lens, cx - r, cy - r, 2 * r, 2 * r);
        }

        using var rim = new Pen(Color.FromArgb(40, 60, 90), Math.Max(1, s * 0.09f));
        g.DrawEllipse(rim, cx - r, cy - r, 2 * r, 2 * r);
    });

    /// <summary>A clockwise arrow around a circle, orange to red.</summary>
    public static Bitmap Rotate(int size) => Draw(size, (g, s) =>
    {
        float inset = s * 0.18f;
        var circle = new RectangleF(inset, inset, s - 2 * inset, s - 2 * inset);
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(255, 170, 0), Color.FromArgb(230, 50, 30), 90f);
        using var pen = new Pen(brush, Math.Max(1.5f, s * 0.13f));
        using var arrow = new AdjustableArrowCap(2.2f, 2.2f);
        pen.CustomEndCap = arrow;
        g.DrawArc(pen, circle, 200, 280);
    });

    /// <summary>A solid purple triangle and its teal mirror image, on each side of a dashed axis.</summary>
    public static Bitmap Flip(int size) => Draw(size, (g, s) =>
    {
        float mid = s / 2f;
        float gap = Math.Max(1, s * 0.08f);
        float top = s * 0.12f;
        float bottom = s * 0.88f;
        using (var left = new SolidBrush(Color.FromArgb(150, 80, 255)))
        {
            g.FillPolygon(left, [new PointF(s * 0.04f, bottom), new PointF(mid - gap, top), new PointF(mid - gap, bottom)]);
        }

        using (var right = new SolidBrush(Color.FromArgb(0, 200, 180)))
        {
            g.FillPolygon(right, [new PointF(s * 0.96f, bottom), new PointF(mid + gap, top), new PointF(mid + gap, bottom)]);
        }

        using var axis = new Pen(Color.FromArgb(90, 90, 90), Math.Max(1, s / 16f)) { DashStyle = DashStyle.Dash };
        g.DrawLine(axis, mid, 0, mid, s);
    });

    /// <summary>A disc, half black and half white, in a gray ring.</summary>
    public static Bitmap BlackAndWhite(int size) => Draw(size, (g, s) =>
    {
        var disc = new RectangleF(s * 0.08f, s * 0.08f, s * 0.84f, s * 0.84f);
        g.FillPie(Brushes.White, disc.X, disc.Y, disc.Width, disc.Height, 90, 180);
        g.FillPie(Brushes.Black, disc.X, disc.Y, disc.Width, disc.Height, 270, 180);
        using var ring = new Pen(Color.FromArgb(128, 128, 128), Math.Max(1, s / 12f));
        g.DrawEllipse(ring, disc);
    });

    /// <summary>A counter-clockwise green arrow around a dot: back to the start.</summary>
    public static Bitmap Reset(int size) => Draw(size, (g, s) =>
    {
        float inset = s * 0.18f;
        var circle = new RectangleF(inset, inset, s - 2 * inset, s - 2 * inset);
        using var pen = new Pen(Color.FromArgb(30, 170, 70), Math.Max(1.5f, s * 0.13f));
        using var arrow = new AdjustableArrowCap(2.2f, 2.2f);
        pen.CustomEndCap = arrow;
        g.DrawArc(pen, circle, -20, -280);
        float dot = s * 0.2f;
        using var brush = new SolidBrush(Color.FromArgb(30, 170, 70));
        g.FillEllipse(brush, (s - dot) / 2, (s - dot) / 2, dot, dot);
    });

    /// <summary>A drop, blue to cyan, with a light reflection.</summary>
    public static Bitmap Blur(int size) => Draw(size, (g, s) =>
    {
        // A round bottom, and a point whose sides touch it.
        float r = s * 0.33f;
        float cx = s / 2f;
        float cy = s * 0.63f;
        using var drop = new GraphicsPath(FillMode.Winding);
        drop.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
        drop.AddPolygon([new PointF(cx, s * 0.04f), new PointF(cx + r * 0.82f, cy - r * 0.57f), new PointF(cx - r * 0.82f, cy - r * 0.57f)]);
        using (var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(0, 200, 255), Color.FromArgb(20, 90, 220), 90f))
        {
            g.FillPath(brush, drop);
        }

        using var shine = new SolidBrush(Color.FromArgb(170, Color.White));
        g.FillEllipse(shine, cx - r * 0.6f, cy - r * 0.35f, r * 0.4f, r * 0.55f);
    });

    /// <summary>A soft disc, magenta fading out to its edge.</summary>
    public static Bitmap Gaussian(int size) => Draw(size, (g, s) =>
    {
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, s - 1, s - 1);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(255, 220, 40, 200),
            SurroundColors = [Color.FromArgb(0, 120, 60, 255)],

            // A solid core, so the disc still reads at 16 px.
            FocusScales = new PointF(0.4f, 0.4f),
        };
        g.FillPath(brush, path);
    });

    /// <summary>Three by three squares of bright colors.</summary>
    public static Bitmap Pixelate(int size) => Draw(size, (g, s) =>
    {
        Color[] colors =
        [
            Color.FromArgb(255, 70, 70), Color.FromArgb(255, 170, 0), Color.FromArgb(255, 225, 0),
            Color.FromArgb(40, 200, 90), Color.FromArgb(0, 170, 255), Color.FromArgb(120, 90, 255),
            Color.FromArgb(230, 60, 200), Color.FromArgb(0, 210, 190), Color.FromArgb(255, 120, 40),
        ];
        float cell = s / 3f;
        float gap = Math.Max(1, s / 16f);
        for (int i = 0; i < colors.Length; i++)
        {
            using var brush = new SolidBrush(colors[i]);
            g.FillRectangle(brush, i % 3 * cell + gap / 2, i / 3 * cell + gap / 2, cell - gap, cell - gap);
        }
    });

    /// <summary>A wedge growing to the right, yellow to red.</summary>
    public static Bitmap Intensity(int size) => Draw(size, (g, s) =>
    {
        PointF[] wedge = [new(0, s * 0.8f), new(s - 1, s * 0.1f), new(s - 1, s * 0.8f)];
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(255, 210, 0), Color.FromArgb(230, 40, 30), 0f);
        g.FillPolygon(brush, wedge);
    });

    private static Bitmap Draw(int size, Action<Graphics, int> paint)
    {
        size = Math.Max(8, size);
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        paint(g, size);
        return bitmap;
    }
}
