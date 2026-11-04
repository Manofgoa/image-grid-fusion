namespace ImageGridFusion.Composition;

/// <summary>
/// What the canvas sizing reads of one image: its size as drawn (<see cref="ImageLook.Shown"/>) at its
/// unzoomed resolution, its look, and whether it <paramref name="Sharpens"/> — a PDF, a thumbnail or a
/// text renders again at any resolution, so it has none of its own to keep.
/// </summary>
public readonly record struct SizedImage(Size Shown, ImageLook Look, bool Sharpens);

/// <summary>
/// Picks the canvas size at which no image is downscaled, at the output format's ratio, its longer
/// side clamped to [1200, cap]: <see cref="MaxLongSide"/> for a PNG, lower for a PNG max size entry, a
/// JPEG for sharing or a video export (workfiles/20261015-png-native-resolution.md). An image is read as
/// it is drawn — its zoom (at time 0, an Animations motion's starting state), its fine angle, the
/// Borders' gap — so a zoomed-out image widens the canvas and a zoomed-in one does not.
/// </summary>
public static class CanvasSizer
{
    public const int MinLongSide = 1200;

    /// <summary>The PNG's cap.</summary>
    public const int MaxLongSide = 8192;

    /// <summary>The cap of a JPEG for sharing, and the largest Video size choice.</summary>
    public const int MaxSharedLongSide = 4096;

    /// <summary>
    /// The drawn scale is linear in the canvas but for the Borders' rounding: a few passes at the
    /// candidate size absorb it.
    /// </summary>
    private const int RefinePasses = 3;

    /// <param name="ratio">The canvas's width ÷ height: the output format's.</param>
    /// <param name="borders">The Borders shrinking the cells by their gap; <c>null</c> while off.</param>
    /// <param name="maxLongSide">The longer side's maximum: <see cref="MaxLongSide"/>, or a lower cap.</param>
    /// <param name="exact">The Custom format's exact pixels, when on: the canvas is that size instead (<see cref="Exactly"/>).</param>
    public static Size Compute(IReadOnlyList<SizedImage> images, GridLayout layout, double ratio, GridBorders? borders, int maxLongSide = MaxLongSide, Size? exact = null)
    {
        if (layout.Count != images.Count)
        {
            throw new ArgumentException($"Layout {layout.Id} holds {layout.Count} images, not {images.Count}.", nameof(layout));
        }

        if (exact is { } size)
        {
            return Exactly(size, maxLongSide);
        }

        int cap = Math.Max(MinLongSide, Math.Min(maxLongSide, MaxLongSide));
        var canvas = Smallest(ratio);
        double longSide = MinLongSide * Need(images, layout, canvas, borders);
        for (int pass = 0; pass < RefinePasses; pass++)
        {
            canvas = At(Clamped(longSide, cap), ratio);
            double next = LongSide(canvas) * Need(images, layout, canvas, borders);
            if (Math.Abs(next - longSide) < 1)
            {
                break;
            }

            longSide = next;
        }

        return At(Clamped(Math.Ceiling(longSide), cap), ratio);
    }

    /// <summary>The smallest canvas at <paramref name="ratio"/>: its longer side <see cref="MinLongSide"/>.</summary>
    public static Size Smallest(double ratio) => At(MinLongSide, ratio);

    /// <summary>
    /// How many times larger <paramref name="canvas"/> must be for no image to be downscaled: the largest
    /// count of source pixels per canvas pixel among the images; 0 with none to keep.
    /// </summary>
    private static double Need(IReadOnlyList<SizedImage> images, GridLayout layout, Size canvas, GridBorders? borders)
    {
        var cells = Compositor.Cells(layout, canvas, borders);
        double need = 0;
        for (int i = 0; i < images.Count; i++)
        {
            double scale = DrawnScale(cells[i], images[i]);
            if (scale > 0)
            {
                need = Math.Max(need, 1 / scale);
            }
        }

        return need;
    }

    /// <summary>
    /// Canvas pixels per source pixel of <paramref name="image"/> in <paramref name="cell"/>; 0 when it
    /// cannot be drawn. A content that sharpens is read at zoom 1, unturned: it never widens the canvas
    /// on its own, rendering sharp again at its cell's size.
    /// </summary>
    internal static double DrawnScale(Rectangle cell, SizedImage image)
    {
        var shown = image.Shown;
        if (cell.Width <= 0 || cell.Height <= 0 || shown.Width <= 0 || shown.Height <= 0)
        {
            return 0;
        }

        if (image.Sharpens)
        {
            return FitCalculator.Scale(cell.Width, cell.Height, shown);
        }

        var look = image.Look;
        double zoom = look.ZoomAt(TimeSpan.Zero, cell, shown);
        var turned = FitCalculator.ComputeTurned(cell, shown, zoom, look.FocusAt(TimeSpan.Zero, cell, shown), look.FineAngle);
        return turned.Fit.Image.Width / shown.Width * turned.Scale;
    }

    /// <summary>
    /// The canvas of a grid holding layers only, at <paramref name="ratio"/>: the smallest where no layer's
    /// content is shrunk — its size, of the grid's shorter side, measuring its content's longer side or a
    /// text's line —, its longer side within [<see cref="MinLongSide"/>, <paramref name="maxLongSide"/>]
    /// (workfiles/20261029-layers.md, Iteration 12). With cells, the layers never size the canvas.
    /// </summary>
    public static Size ForLayers(IEnumerable<LayerFrame> layers, double ratio, int maxLongSide = MaxLongSide, Size? exact = null)
    {
        if (exact is { } size)
        {
            return Exactly(size, maxLongSide);
        }

        int cap = Math.Max(MinLongSide, Math.Min(maxLongSide, MaxLongSide));
        var smallest = Smallest(ratio);
        double shorter = Math.Min(smallest.Width, smallest.Height);
        double need = 0;
        foreach (var layer in layers)
        {
            var shown = Frame.Unsharpened(layer.Frame.Size, layer.Frame.Sharpening);
            double reference = layer.Place.LinePixels > 0 ? layer.Place.LinePixels : Math.Max(shown.Width, shown.Height);
            if (layer.Place.Size > 0)
            {
                need = Math.Max(need, reference / layer.Place.Size / shorter);
            }
        }

        return At(Clamped(Math.Ceiling(LongSide(smallest) * need), cap), ratio);
    }

    /// <summary>
    /// The canvas of the Custom format's exact pixels (workfiles/20261104-custom-output-format.md § Sizing):
    /// <paramref name="exact"/> itself — enlarged or reduced from the native size, no <see cref="MinLongSide"/>
    /// floor — reduced to <paramref name="maxLongSide"/>, the ratio kept, when its longer side exceeds that cap.
    /// </summary>
    public static Size Exactly(Size exact, int maxLongSide = MaxLongSide)
    {
        int cap = Math.Min(maxLongSide, MaxLongSide);
        int longer = Math.Max(exact.Width, exact.Height);
        if (longer <= cap)
        {
            return exact;
        }

        double scale = cap / (double)longer;
        return new Size(Math.Max(1, (int)Math.Round(exact.Width * scale)), Math.Max(1, (int)Math.Round(exact.Height * scale)));
    }

    /// <summary>
    /// The canvas of a grid holding the Grid background's image alone — no cell, no layer —: the image sized as the
    /// one cell of a one-cell grid at <paramref name="ratio"/>, the smallest where it is not shrunk as drawn there
    /// (workfiles/20261104-grid-background-image.md § Drawing, Q&amp;A 12) — or the Custom format's <paramref name="exact"/>
    /// pixels. With a cell or a layer, it never sizes the canvas.
    /// </summary>
    public static Size ForBackground(BackgroundFrame background, double ratio, int maxLongSide = MaxLongSide, Size? exact = null) =>
        Compute([background.Frame.Sizing], GridLayout.Default(1), ratio, null, maxLongSide, exact);

    /// <summary>
    /// The canvas of any grid: the cells' (<see cref="Compute"/>), else the layers' (<see cref="ForLayers"/>), else the
    /// Grid background's image's (<see cref="ForBackground"/>) — <paramref name="backgroundImage"/> given only while
    /// the Grid background is on —, the Custom format's <paramref name="exact"/> pixels winning in each.
    /// </summary>
    public static Size For(IReadOnlyList<Frame> frames, GridLayout? layout, double ratio, GridBorders? borders, int maxLongSide, IReadOnlyList<LayerFrame>? layers, BackgroundFrame? backgroundImage, Size? exact = null) =>
        layout is not null ? Compute(frames.Select(f => f.Sizing).ToList(), layout, ratio, borders, maxLongSide, exact)
        : layers is not { Count: > 0 } && backgroundImage is { } background ? ForBackground(background, ratio, maxLongSide, exact)
        : ForLayers(layers ?? [], ratio, maxLongSide, exact);

    private static int Clamped(double longSide, int cap) => (int)Math.Round(Math.Clamp(longSide, MinLongSide, cap));

    private static int LongSide(Size canvas) => Math.Max(canvas.Width, canvas.Height);

    private static Size At(int longSide, double ratio) =>
        ratio >= 1 ? new Size(longSide, GridLayout.HeightFor(longSide, ratio)) : new Size(GridLayout.WidthFor(longSide, ratio), longSide);
}
