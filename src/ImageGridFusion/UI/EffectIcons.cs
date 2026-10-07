using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

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

    /// <summary>A picture breathing: a blue frame inside a larger dashed one, a purple arrow from corner to corner.</summary>
    public static Bitmap Animations(int size) => Draw(size, (g, s) =>
    {
        float outer = s * 0.08f;
        float inner = s * 0.3f;
        using (var dashed = new Pen(Color.FromArgb(150, 110, 220), Math.Max(1, s * 0.08f)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(dashed, outer, outer, s - 2 * outer, s - 2 * outer);
        }

        var picture = new RectangleF(inner, inner, s - 2 * inner, s - 2 * inner);
        using (var fill = new LinearGradientBrush(picture, Color.FromArgb(170, 240, 255), Color.FromArgb(0, 150, 230), 45f))
        {
            g.FillRectangle(fill, picture);
        }

        using var arrow = new Pen(Color.FromArgb(120, 60, 200), Math.Max(1.2f, s * 0.1f));
        using var cap = new AdjustableArrowCap(2f, 2f);
        arrow.CustomStartCap = cap;
        arrow.CustomEndCap = cap;
        g.DrawLine(arrow, s * 0.6f, s * 0.4f, s * 0.86f, s * 0.14f);
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

    /// <summary>A strip of film: a dark band with sprocket holes, and two frames, blue and amber.</summary>
    public static Bitmap Frames(int size) => Draw(size, (g, s) =>
    {
        var strip = new RectangleF(s * 0.05f, s * 0.15f, s * 0.9f, s * 0.7f);
        using (var film = new SolidBrush(Color.FromArgb(50, 50, 60)))
        {
            g.FillRectangle(film, strip);
        }

        float hole = Math.Max(1, s * 0.09f);
        for (int i = 0; i < 4; i++)
        {
            float x = strip.X + strip.Width * (i + 0.5f) / 4 - hole / 2;
            g.FillRectangle(Brushes.White, x, strip.Top + hole * 0.5f, hole, hole);
            g.FillRectangle(Brushes.White, x, strip.Bottom - hole * 1.5f, hole, hole);
        }

        float top = strip.Top + hole * 2;
        float height = strip.Height - hole * 4;
        float width = strip.Width / 2 - s * 0.08f;
        using (var blue = new SolidBrush(Color.FromArgb(0, 170, 255)))
        {
            g.FillRectangle(blue, strip.X + s * 0.04f, top, width, height);
        }

        using var amber = new SolidBrush(Color.FromArgb(255, 180, 0));
        g.FillRectangle(amber, strip.X + strip.Width / 2 + s * 0.04f, top, width, height);
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

    /// <summary>The two orange crop marks, crossing at opposite corners of the part kept.</summary>
    public static Bitmap Crop(int size) => Draw(size, (g, s) =>
    {
        float w = Math.Max(1.5f, s * 0.14f);
        float a = s * 0.25f;
        float b = s * 0.75f;
        using var pen = new Pen(Color.FromArgb(255, 140, 0), w) { StartCap = LineCap.Flat, EndCap = LineCap.Flat, LineJoin = LineJoin.Miter };
        g.DrawLines(pen, [new PointF(a, s * 0.04f), new PointF(a, b), new PointF(s * 0.96f, b)]);
        g.DrawLines(pen, [new PointF(s * 0.04f, a), new PointF(b, a), new PointF(b, s * 0.96f)]);
    });

    /// <summary>The Format tab: a landscape frame in blue over a portrait one in orange, two ratios of the canvas.</summary>
    public static Bitmap Format(int size) => Draw(size, (g, s) =>
    {
        float line = Math.Max(1f, s * 0.08f);
        var portrait = new RectangleF(s * 0.12f, s * 0.06f, s * 0.48f, s * 0.86f);
        var landscape = new RectangleF(s * 0.3f, s * 0.36f, s * 0.64f, s * 0.42f);
        using (var fill = new SolidBrush(Color.FromArgb(255, 170, 60)))
        using (var pen = new Pen(Color.FromArgb(180, 90, 0), line))
        {
            g.FillRectangle(fill, portrait);
            g.DrawRectangle(pen, portrait.X, portrait.Y, portrait.Width, portrait.Height);
        }

        using var fillLandscape = new SolidBrush(Color.FromArgb(110, 190, 255));
        using var penLandscape = new Pen(Color.FromArgb(20, 90, 200), line);
        g.FillRectangle(fillLandscape, landscape);
        g.DrawRectangle(penLandscape, landscape.X, landscape.Y, landscape.Width, landscape.Height);
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

    /// <summary>A dark blue loudspeaker sending two green sound waves to the right.</summary>
    public static Bitmap Volume(int size) => Draw(size, (g, s) =>
    {
        PointF[] speaker =
        [
            new(s * 0.08f, s * 0.36f), new(s * 0.28f, s * 0.36f), new(s * 0.52f, s * 0.12f),
            new(s * 0.52f, s * 0.88f), new(s * 0.28f, s * 0.64f), new(s * 0.08f, s * 0.64f),
        ];
        using (var body = new SolidBrush(Color.FromArgb(40, 70, 150)))
        {
            g.FillPolygon(body, speaker);
        }

        using var wave = new Pen(Color.FromArgb(30, 190, 90), Math.Max(1, s * 0.09f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(wave, s * 0.42f, s * 0.3f, s * 0.3f, s * 0.4f, -50, 100);
        g.DrawArc(wave, s * 0.4f, s * 0.14f, s * 0.52f, s * 0.72f, -50, 100);
    });

    /// <summary>Two beamed eighth notes, violet to blue.</summary>
    public static Bitmap Soundtrack(int size) => Draw(size, (g, s) =>
    {
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(170, 70, 255), Color.FromArgb(30, 110, 230), 45f);
        float stem = Math.Max(1.5f, s * 0.1f);
        float headWidth = s * 0.34f;
        float headHeight = s * 0.26f;
        float left = s * 0.3f;
        float right = s * 0.86f;

        // The stems rise from the heads' right side to the beam, which slants up to the right.
        g.FillRectangle(brush, left - stem, s * 0.2f, stem, s * 0.56f);
        g.FillRectangle(brush, right - stem, s * 0.08f, stem, s * 0.58f);
        g.FillPolygon(brush, [new PointF(left - stem, s * 0.2f), new PointF(right, s * 0.08f), new PointF(right, s * 0.08f + s * 0.18f), new PointF(left - stem, s * 0.38f)]);
        g.FillEllipse(brush, left - headWidth, s * 0.7f, headWidth, headHeight);
        g.FillEllipse(brush, right - headWidth, s * 0.58f, headWidth, headHeight);
    });

    /// <summary>A sea-green hill rising, holding and falling, the shape of a fade.</summary>
    public static Bitmap Fade(int size) => Draw(size, (g, s) =>
    {
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(20, 170, 140), Color.FromArgb(30, 110, 230), 0f);
        g.FillPolygon(brush, FadeShape(new RectangleF(s * 0.04f, s * 0.2f, s * 0.92f, s * 0.64f), FadeCurve.Squared, closed: true));
    });

    /// <summary>
    /// The fade's <paramref name="curve"/>, twice as wide as tall: a rise, a hold and a fall, drawn in dark
    /// blue over a light fill.
    /// </summary>
    public static Bitmap Curve(int size, FadeCurve curve)
    {
        size = Math.Max(8, size);
        var bitmap = new Bitmap(size * 2, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var area = new RectangleF(size * 0.1f, size * 0.15f, size * 1.8f, size * 0.7f);
        using (var fill = new SolidBrush(Color.FromArgb(90, 20, 170, 140)))
        {
            g.FillPolygon(fill, FadeShape(area, curve, closed: true));
        }

        using var pen = new Pen(Color.FromArgb(40, 70, 150), Math.Max(1.5f, size * 0.11f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(pen, FadeShape(area, curve, closed: false));
        return bitmap;
    }

    /// <summary>
    /// The outline of a fade over <paramref name="area"/>: the rise over its first third, the hold, the
    /// fall over its last third, the gain as <paramref name="curve"/> gives it; <paramref name="closed"/>
    /// along the bottom edge, to be filled.
    /// </summary>
    private static PointF[] FadeShape(RectangleF area, FadeCurve curve, bool closed)
    {
        const int Steps = 12;
        var fade = new SoundFade(TimeSpan.FromSeconds(1), curve);
        var points = new List<PointF>();
        for (int i = 0; i <= Steps * 3; i++)
        {
            double t = i / (double)Steps;
            double gain = fade.GainAt(t, 3);
            points.Add(new PointF(area.Left + (float)(t / 3) * area.Width, area.Bottom - (float)gain * area.Height));
        }

        if (closed)
        {
            points.Add(new PointF(area.Right, area.Bottom));
            points.Add(new PointF(area.Left, area.Bottom));
        }

        return [.. points];
    }

    /// <summary>A hot pink L-bracket over the corner of a grey picture, the signature of the corner borders.</summary>
    public static Bitmap Borders(int size) => Draw(size, (g, s) =>
    {
        var picture = new RectangleF(s * 0.12f, s * 0.12f, s * 0.76f, s * 0.76f);
        using (var fill = new SolidBrush(Color.FromArgb(200, 205, 215)))
        {
            g.FillRectangle(fill, picture);
        }

        float arm = Math.Max(2, s * 0.18f);
        using var bracket = new SolidBrush(Color.FromArgb(255, 60, 170));
        g.FillRectangle(bracket, picture.X, picture.Y, picture.Width * 0.6f, arm);
        g.FillRectangle(bracket, picture.X, picture.Y, arm, picture.Height * 0.6f);
    });

    /// <summary>A wedge growing to the right, yellow to red.</summary>
    public static Bitmap Intensity(int size) => Draw(size, (g, s) =>
    {
        PointF[] wedge = [new(0, s * 0.8f), new(s - 1, s * 0.1f), new(s - 1, s * 0.8f)];
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(255, 210, 0), Color.FromArgb(230, 40, 30), 0f);
        g.FillPolygon(brush, wedge);
    });

    /// <summary>A framed picture, its upper-left half filled in blue, the other half the grey and white squares of transparency.</summary>
    public static Bitmap Background(int size) => Draw(size, (g, s) =>
    {
        var frame = new RectangleF(s * 0.08f, s * 0.16f, s * 0.84f, s * 0.68f);
        Checker(g, frame, s);
        using (var fill = new LinearGradientBrush(frame, Color.FromArgb(120, 200, 255), Color.FromArgb(30, 100, 220), 45f))
        {
            g.FillPolygon(fill, [new PointF(frame.Left, frame.Top), new PointF(frame.Right, frame.Top), new PointF(frame.Left, frame.Bottom)]);
        }

        using var rim = new Pen(Color.FromArgb(60, 60, 60), Math.Max(1, s / 16f));
        g.DrawRectangle(rim, frame.X, frame.Y, frame.Width, frame.Height);
    });

    /// <summary>The squares of transparency fading into a solid blue, left to right.</summary>
    public static Bitmap Opacity(int size) => Draw(size, (g, s) =>
    {
        var bar = new RectangleF(0, s * 0.2f, s - 1, s * 0.6f);
        Checker(g, bar, s);
        using var fill = new LinearGradientBrush(bar, Color.FromArgb(0, 30, 100, 220), Color.FromArgb(255, 30, 100, 220), 0f);
        g.FillRectangle(fill, bar);
    });

    /// <summary>Grey and white squares over <paramref name="area"/>, four to the icon's width.</summary>
    private static void Checker(Graphics g, RectangleF area, int size)
    {
        float square = Math.Max(2, size / 4f);
        var state = g.Save();
        g.SetClip(area);
        g.FillRectangle(Brushes.White, area);
        using var grey = new SolidBrush(Color.FromArgb(190, 190, 190));
        for (int row = 0; row * square < area.Height; row++)
        {
            for (int column = (row + 1) % 2; column * square < area.Width; column += 2)
            {
                g.FillRectangle(grey, area.X + column * square, area.Y + row * square, square, square);
            }
        }

        g.Restore(state);
    }

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
