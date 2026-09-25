using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// Colored icons of the effects and options rows, drawn at the size asked for, so they stay crisp at
/// any DPI without an image file or a dependency. The caller owns the bitmap.
/// </summary>
internal static class EffectIcons
{
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
