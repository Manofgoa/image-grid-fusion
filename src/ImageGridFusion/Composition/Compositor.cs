using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>
/// What a cell shows: a bitmap, what colors its bands, the actions on the image, if any, and the
/// <see cref="Time"/> on the clock of the grid the motion of its Animations effect is drawn at — 0, its
/// starting state, for a still export.
/// </summary>
public readonly record struct Frame(Bitmap Bitmap, BandColor BandColor, ImageLook? Look = null, TimeSpan Time = default)
{
    /// <summary>Size of the image as drawn: its crop's kept part, rotated by its look — what the fitting rule and the canvas sizing read.</summary>
    public Size Size => (Look ?? ImageLook.None).Shown(Bitmap.Size);
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

    /// <summary>Renders the final image at <paramref name="ratio"/>, at the size given by <see cref="CanvasSizer"/>.</summary>
    public static Bitmap Render(IReadOnlyList<SourceImage> images, GridLayout layout, double ratio, GridBorders? borders = null) =>
        Render(images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look)).ToList(), layout, ratio, borders);

    /// <summary>
    /// Renders frames at <paramref name="ratio"/>, at the size given by <see cref="CanvasSizer"/>; frame i
    /// goes into cell i. With an alpha channel, transparent where a cell, or the gap between the cells,
    /// has no fill: a PNG keeps it.
    /// </summary>
    public static Bitmap Render(IReadOnlyList<Frame> frames, GridLayout layout, double ratio, GridBorders? borders = null)
    {
        var canvas = CanvasSizer.Compute(frames.Select(f => f.Size).ToList(), layout, ratio);
        var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, frames, layout, canvas, borders);
        return bitmap;
    }

    /// <summary>The cells as drawn on <paramref name="canvas"/>: the layout's, shrunk by the gap of <paramref name="borders"/>.</summary>
    public static Rectangle[] Cells(GridLayout layout, Size canvas, GridBorders? borders)
    {
        var slots = layout.Cells(canvas);
        return borders?.Inset(slots, canvas) ?? slots;
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
        var turned = FitCalculator.ComputeTurned(cell, frame.Size, look.ZoomAt(frame.Time, cell, frame.Size), look.FocusAt(frame.Time, cell, frame.Size), look.FineAngle);
        using var turn = turned.Transform();
        var source = Uncropped(turn is null ? turned.Fit.Source : TurnedPart(cell, turned.Fit, frame.Size, turn).Source, frame.Bitmap.Size, look);
        bool oriented = look is not { Rotation: 0, FlipX: false, FlipY: false };
        return frame.BandColor.For(oriented ? BitmapPart(frame.Bitmap.Size, look, source) : source, frame.Bitmap.Size);
    }

    /// <summary>
    /// Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; image i goes into cell i of
    /// the layout, its motion at <paramref name="time"/> on the clock of the grid.
    /// </summary>
    public static void Draw(Graphics g, IReadOnlyList<SourceImage> images, GridLayout layout, Size canvas, GridBorders? borders = null, TimeSpan time = default) =>
        Draw(g, images.Select(i => new Frame(i.Bitmap, i.BandColor, i.Look, time)).ToList(), layout, canvas, borders);

    /// <summary>
    /// Draws the grid in the rectangle (0, 0, canvas) of <paramref name="g"/>; frame i goes into cell i of
    /// the layout. The borders, a global effect, are drawn here too, at the grid level.
    /// </summary>
    public static void Draw(Graphics g, IReadOnlyList<Frame> frames, GridLayout layout, Size canvas, GridBorders? borders = null)
    {
        var slots = layout.Cells(canvas);
        var cells = borders?.Inset(slots, canvas) ?? slots;
        for (int i = 0; i < frames.Count; i++)
        {
            DrawCell(g, frames[i], cells[i]);
        }

        borders?.Draw(g, slots, canvas);
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

        var turned = FitCalculator.ComputeTurned(cell, frame.Size, look.ZoomAt(frame.Time, cell, frame.Size), look.FocusAt(frame.Time, cell, frame.Size), look.FineAngle);
        var fit = turned.Fit;
        using var turn = turned.Transform();
        var (source, shown) = turn is null ? (fit.Source, fit.Destination) : TurnedPart(cell, fit, frame.Size, turn);
        source = Uncropped(source, frame.Bitmap.Size, look);
        bool oriented = look is not { Rotation: 0, FlipX: false, FlipY: false };
        var bitmapPart = oriented ? BitmapPart(frame.Bitmap.Size, look, source) : source;

        var destination = Rectangle.Round(shown);
        void DrawImage(Graphics target)
        {
            // The clip stays in the cell: it is not turned with the drawing.
            var state = target.Save();
            target.SetClip(cell, CombineMode.Intersect);
            if (turn is not null)
            {
                target.MultiplyTransform(turn);
            }

            if (!oriented)
            {
                target.DrawImage(
                    frame.Bitmap,
                    destination,
                    source.X, source.Y, source.Width, source.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
            else
            {
                DrawOriented(target, frame.Bitmap, look, source, bitmapPart, destination, attributes);
            }

            target.Restore(state);
        }

        // Bands, and transparent pixels, show the background: the band color of the part shown, or the
        // chosen one, at its opacity, the image's edges extended over the bands in an extending mode;
        // none while the effect is off, the cell left transparent.
        if (look.Background is { } background)
        {
            var automatic = background.Automatic ? frame.BandColor.For(bitmapPart, frame.Bitmap.Size) : background.Color;
            DrawBackground(g, background, automatic, look.Grayscale, cell, EdgeExtension.Covered(fit.Destination, cell, turn is not null), DrawImage, fast);
        }

        DrawImage(g);

        // Every effect is drawn here, so the preview, the exports and a playing video all show it.
        using var clip = g.Clip;
        g.SetClip(cell, CombineMode.Intersect);
        BlurRenderer.Draw(g, frame, cell, fast);
        g.Clip = clip;
    }

    /// <summary>
    /// The background of <paramref name="cell"/>: the flat fill — <paramref name="automatic"/> or the
    /// chosen color, at the opacity, turned gray by <paramref name="grayscale"/> — or, in an extending
    /// mode with bands around the image's place <paramref name="covered"/>, the image's edges extended:
    /// read from the image alone, which <paramref name="drawImage"/> draws at the cell's size.
    /// </summary>
    private static void DrawBackground(Graphics g, BackgroundEffect background, Color automatic, double? grayscale, Rectangle cell, Rectangle? covered, Action<Graphics> drawImage, bool fast)
    {
        var fill = background.Fill(automatic);
        if (grayscale is { } gray)
        {
            fill = Gray(fill, gray);
        }

        if (!background.Extends || covered is not { } place)
        {
            using var brush = new SolidBrush(fill);
            g.FillRectangle(brush, cell);
            return;
        }

        using var image = new Bitmap(cell.Width, cell.Height, PixelFormat.Format32bppPArgb);
        using (var ig = Graphics.FromImage(image))
        {
            ig.InterpolationMode = fast ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
            ig.PixelOffsetMode = PixelOffsetMode.HighQuality;
            ig.CompositingQuality = fast ? CompositingQuality.HighSpeed : CompositingQuality.HighQuality;
            ig.TranslateTransform(-cell.X, -cell.Y);
            drawImage(ig);
        }

        EdgeExtension.Draw(g, image, cell, place, background, fill);
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

    /// <summary>
    /// The crop's edit view of <paramref name="frame"/> in <paramref name="cell"/>, for the preview: the
    /// whole image, rotated and flipped but neither zoomed nor turned by a fine angle, fitted whole into
    /// the cell so every edge can be reached, over the background the cropped image gets there. Returns
    /// where the image lands.
    /// </summary>
    public static RectangleF DrawUncropped(Graphics g, Frame frame, Rectangle cell)
    {
        var look = frame.Look ?? ImageLook.None;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        var size = look.Oriented(frame.Bitmap.Size);
        var image = UncroppedBounds(size, cell);

        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        if (look.Grayscale is { } grayscale)
        {
            attributes.SetColorMatrix(GrayscaleMatrix(grayscale));
        }

        void DrawImage(Graphics target)
        {
            var state = target.Save();
            target.SetClip(cell, CombineMode.Intersect);
            DrawOriented(target, frame.Bitmap, look, new RectangleF(PointF.Empty, size), new RectangleF(PointF.Empty, frame.Bitmap.Size), Rectangle.Round(image), attributes);
            target.Restore(state);
        }

        // In an extending mode, the edges of the whole image the view shows are extended.
        if (look.Background is { } background)
        {
            var automatic = background.Automatic ? AutomaticBackground(frame, cell) : background.Color;
            DrawBackground(g, background, automatic, look.Grayscale, cell, EdgeExtension.Covered(image, cell, turned: false), DrawImage, fast: false);
        }

        DrawImage(g);
        return image;
    }

    /// <summary>Where <see cref="DrawUncropped"/> draws an oriented image of <paramref name="size"/>: fitted whole into <paramref name="cell"/>, centered.</summary>
    public static RectangleF UncroppedBounds(Size size, Rectangle cell)
    {
        double scale = Math.Min(cell.Width / (double)size.Width, cell.Height / (double)size.Height);
        float width = (float)(size.Width * scale);
        float height = (float)(size.Height * scale);
        return new RectangleF(cell.X + (cell.Width - width) / 2, cell.Y + (cell.Height - height) / 2, width, height);
    }

    /// <summary>
    /// The part <paramref name="source"/> of the cropped image, as the fitting rule gives it, in the
    /// oriented whole image of a bitmap of <paramref name="size"/>: moved by where the kept part lies in it.
    /// </summary>
    private static RectangleF Uncropped(RectangleF source, Size size, ImageLook look)
    {
        if (look.Crop is null)
        {
            return source;
        }

        var kept = look.Cropped(size);
        using var orientation = look.Orientation(size);
        PointF[] corners = [new(kept.Left, kept.Top), new(kept.Right, kept.Bottom)];
        orientation.TransformPoints(corners);
        source.Offset(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[0].Y, corners[1].Y));
        return source;
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
