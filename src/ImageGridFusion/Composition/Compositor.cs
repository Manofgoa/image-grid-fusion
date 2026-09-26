using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>What a cell shows: a bitmap, what colors its bands, and the actions on the image, if any.</summary>
public readonly record struct Frame(Bitmap Bitmap, BandColor BandColor, ImageLook? Look = null)
{
    /// <summary>Size of the image as drawn: once rotated by its look.</summary>
    public Size Size => (Look ?? ImageLook.None).Oriented(Bitmap.Size);
}

/// <summary>Draws the images into their cells, at full resolution or at any preview size.</summary>
public static class Compositor
{
    /// <summary>Luminance weights of the black &amp; white effect.</summary>
    private static readonly float[] Luminance = [0.299f, 0.587f, 0.114f];

    /// <summary>Each channel moved <paramref name="intensity"/> of the way from itself to the luminance.</summary>
    private static ColorMatrix GrayscaleMatrix(double intensity)
    {
        float t = (float)intensity;
        var matrix = new ColorMatrix();
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                matrix[row, column] = t * Luminance[row] + (row == column ? 1 - t : 0);
            }
        }

        return matrix;
    }

    /// <summary>Renders the final image at the size given by <see cref="CanvasSizer"/>.</summary>
    public static Bitmap Render(IReadOnlyList<SourceImage> images, GridLayout layout) =>
        Render(images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look)).ToList(), layout);

    /// <summary>
    /// Renders frames at the size given by <see cref="CanvasSizer"/>; frame i goes into cell i. With an
    /// alpha channel, transparent where a cell has no background: a PNG keeps it.
    /// </summary>
    public static Bitmap Render(IReadOnlyList<Frame> frames, GridLayout layout)
    {
        var canvas = CanvasSizer.Compute(frames.Select(f => f.Size).ToList(), layout);
        var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, frames, layout, canvas);
        return bitmap;
    }

    /// <summary>What an output without alpha shows of <paramref name="image"/>: its transparency flattened on white.</summary>
    public static Bitmap Flattened(Bitmap image)
    {
        var flat = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(flat);
        g.Clear(Color.White);
        g.DrawImageUnscaled(image, 0, 0);
        return flat;
    }

    /// <summary>
    /// <see cref="Flattened(Bitmap)"/>, downscaled so that its long edge is at most
    /// <paramref name="maxEdge"/> px, aspect ratio kept; never upscaled.
    /// </summary>
    public static Bitmap Flattened(Bitmap image, int maxEdge)
    {
        double scale = Math.Min(1.0, (double)maxEdge / Math.Max(image.Width, image.Height));
        if (scale >= 1.0)
        {
            return Flattened(image);
        }

        var size = new Size(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
        var flat = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(flat);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        // Edge pixels mirrored rather than blended with the outside, which would darken the borders.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(image, new Rectangle(Point.Empty, size), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        return flat;
    }

    /// <summary>
    /// The automatic background color of <paramref name="frame"/> in <paramref name="cell"/>: the band
    /// color of the part of the image shown, as <see cref="DrawCell"/> computes it, before black &amp; white.
    /// </summary>
    public static Color AutomaticBackground(Frame frame, Rectangle cell)
    {
        var look = frame.Look ?? ImageLook.None;
        var turned = FitCalculator.ComputeTurned(cell, frame.Size, look.Zoom, look.Focus, look.FineAngle);
        using var turn = turned.Transform();
        var source = turn is null ? turned.Fit.Source : TurnedPart(cell, turned.Fit, frame.Size, turn).Source;
        bool oriented = look is not { Rotation: 0, FlipX: false, FlipY: false };
        return frame.BandColor.For(oriented ? BitmapPart(frame.Bitmap.Size, look, source) : source, frame.Bitmap.Size);
    }

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; image i goes into cell i of the layout.</summary>
    public static void Draw(Graphics g, IReadOnlyList<SourceImage> images, GridLayout layout, Size canvas) =>
        Draw(g, images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look)).ToList(), layout, canvas);

    /// <summary>Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; frame i goes into cell i of the layout.</summary>
    public static void Draw(Graphics g, IReadOnlyList<Frame> frames, GridLayout layout, Size canvas)
    {
        var cells = layout.Cells(canvas);
        for (int i = 0; i < frames.Count; i++)
        {
            DrawCell(g, frames[i], cells[i]);
        }
    }

    /// <summary>
    /// Draws one frame into its cell, leaving the rest of <paramref name="g"/> untouched.
    /// <paramref name="fast"/> trades smoothing for speed, the frame landing at the same place.
    /// </summary>
    public static void DrawCell(Graphics g, Frame frame, Rectangle cell, bool fast = false)
    {
        g.InterpolationMode = fast ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = fast ? CompositingQuality.HighSpeed : CompositingQuality.HighQuality;

        var look = frame.Look ?? ImageLook.None;

        // TileFlipXY avoids the semi-transparent halo GDI+ leaves on the edges of scaled images.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        if (look.Grayscale is { } grayscale)
        {
            attributes.SetColorMatrix(GrayscaleMatrix(grayscale));
        }

        var turned = FitCalculator.ComputeTurned(cell, frame.Size, look.Zoom, look.Focus, look.FineAngle);
        var fit = turned.Fit;
        using var turn = turned.Transform();
        var (source, shown) = turn is null ? (fit.Source, fit.Destination) : TurnedPart(cell, fit, frame.Size, turn);
        bool oriented = look is not { Rotation: 0, FlipX: false, FlipY: false };
        var bitmapPart = oriented ? BitmapPart(frame.Bitmap.Size, look, source) : source;

        // Bands, and transparent pixels, show the background: the band color of the part shown, or the
        // chosen one, at its opacity; none while the effect is off, the cell left transparent.
        if (look.Background is { } background)
        {
            var fill = background.Fill(background.Automatic ? frame.BandColor.For(bitmapPart, frame.Bitmap.Size) : background.Color);
            using var brush = new SolidBrush(look.Grayscale is { } gray ? Gray(fill, gray) : fill);
            g.FillRectangle(brush, cell);
        }

        var destination = Rectangle.Round(shown);
        using var clip = g.Clip;
        g.SetClip(cell, CombineMode.Intersect);

        // The clip stays in the cell: it is not turned with the drawing.
        using var transform = g.Transform;
        if (turn is not null)
        {
            g.MultiplyTransform(turn);
        }

        if (!oriented)
        {
            g.DrawImage(
                frame.Bitmap,
                destination,
                source.X, source.Y, source.Width, source.Height,
                GraphicsUnit.Pixel,
                attributes);
        }
        else
        {
            DrawOriented(g, frame.Bitmap, look, source, bitmapPart, destination, attributes);
        }

        g.Transform = transform;

        // Every effect is drawn here, so the preview, the exports and a playing video all show it.
        BlurRenderer.Draw(g, frame, cell, fast);
        g.Clip = clip;
    }

    /// <summary>
    /// Part of the oriented image of <paramref name="size"/> that lands in the cell once turned, and
    /// where it is drawn before the turn: the image within the cell turned back.
    /// </summary>
    private static (RectangleF Source, RectangleF Shown) TurnedPart(Rectangle cell, Fit fit, Size size, Matrix turn)
    {
        using var back = turn.Clone();
        back.Invert();
        PointF[] corners = [new(cell.Left, cell.Top), new(cell.Right, cell.Top), new(cell.Left, cell.Bottom), new(cell.Right, cell.Bottom)];
        back.TransformPoints(corners);
        var reached = RectangleF.FromLTRB(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
        var shown = RectangleF.Intersect(fit.Image, reached);
        float scale = fit.Image.Width / size.Width;
        var source = new RectangleF((shown.X - fit.Image.X) / scale, (shown.Y - fit.Image.Y) / scale, shown.Width / scale, shown.Height / scale);
        return (source, shown);
    }

    /// <summary>Rectangle of a bitmap of <paramref name="size"/> that the part <paramref name="source"/> of the oriented image comes from.</summary>
    private static RectangleF BitmapPart(Size size, ImageLook look, RectangleF source)
    {
        using var inverse = look.Orientation(size);
        inverse.Invert();

        PointF[] corners = [new(source.Left, source.Top), new(source.Right, source.Top), new(source.Left, source.Bottom), new(source.Right, source.Bottom)];
        inverse.TransformPoints(corners);
        return RectangleF.FromLTRB(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
    }

    /// <summary>
    /// Draws the part <paramref name="source"/> of the oriented image, which comes from
    /// <paramref name="bitmapPart"/>, into <paramref name="destination"/>: the bitmap part goes to a
    /// parallelogram whose corners follow the rotation and flips.
    /// </summary>
    private static void DrawOriented(Graphics g, Bitmap bitmap, ImageLook look, RectangleF source, RectangleF bitmapPart, Rectangle destination, ImageAttributes attributes)
    {
        using var orientation = look.Orientation(bitmap.Size);

        // Upper-left, upper-right and lower-left corners of the bitmap part, as the cell shows them.
        PointF[] points = [new(bitmapPart.Left, bitmapPart.Top), new(bitmapPart.Right, bitmapPart.Top), new(bitmapPart.Left, bitmapPart.Bottom)];
        orientation.TransformPoints(points);
        float scaleX = destination.Width / source.Width;
        float scaleY = destination.Height / source.Height;
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new PointF(destination.X + (points[i].X - source.X) * scaleX, destination.Y + (points[i].Y - source.Y) * scaleY);
        }

        g.DrawImage(bitmap, points, bitmapPart, GraphicsUnit.Pixel, attributes);
    }

    /// <summary>The band color as the black &amp; white effect turns the image: <paramref name="intensity"/> of the way to its luminance.</summary>
    private static Color Gray(Color color, double intensity)
    {
        double luminance = Luminance[0] * color.R + Luminance[1] * color.G + Luminance[2] * color.B;
        int Mix(int channel) => (int)Math.Round(channel + (luminance - channel) * intensity);
        return Color.FromArgb(color.A, Mix(color.R), Mix(color.G), Mix(color.B));
    }
}
