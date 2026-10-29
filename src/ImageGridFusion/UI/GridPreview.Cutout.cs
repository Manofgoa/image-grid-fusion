using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>A point clicked on the selected cell while Pick is armed: where on the image as loaded, and how it joins the pick.</summary>
internal readonly record struct CutoutPick(CutoutPoint Point, bool Add);

/// <summary>
/// The Cutout effect in the preview: its cut-outs asked for at every paint and their cells drawn again
/// once ready, the <i>Cutting out…</i> badge on a cell waiting for one, the Pick view showing the whole
/// image, the click picking an object while Pick is armed, the object under the hovering cursor
/// outlined, and the points of the pick drawn as helper indicators. See workfiles/20261029-cutout.md § UI.
/// </summary>
internal sealed partial class GridPreview
{
    // How often the badge's dots move, in ms.
    private const int CutoutBadgeTick = 400;

    // In logical pixels: an included point's dot, an excluded point's ring, and the badge's padding.
    private const int CutoutDotRadius = 5;
    private const int CutoutRingRadius = 7;
    private const int CutoutBadgePad = 6;

    // The opacity the Pick view draws the whole image at, over the cell as composed.
    private const float PickViewOpacity = 0.35f;

    private readonly List<Rectangle> _cutoutBadges = [];
    private System.Windows.Forms.Timer? _cutoutBadgeTimer;
    private CutoutService? _cutouts;
    private bool _cutoutPick;
    private bool _cutoutPoints;

    // The Pick view's whole image, drawn at the cell's size, for the image and look it shows.
    private (SourceImage Image, ImageLook Look, Rectangle Cell, Bitmap Bitmap)? _pickView;

    // The hover: the next pick to outline, whether a decode runs, and the outline drawn — for the look it was made for.
    private CutoutPick? _hoverNext;
    private bool _hoverBusy;
    private (SourceImage Image, ImageLook Look, Bitmap Edge)? _hoverOutline;

    /// <summary>A point was clicked on the selected cell while Pick was armed.</summary>
    public event EventHandler<CutoutPick>? CutoutPicked;

    /// <summary>Pick was disarmed: a plain click picked, or it was cancelled.</summary>
    public event EventHandler? CutoutPickEnded;

    /// <summary>What computes the cut-outs; the preview asks it for every image drawn uncut and draws a cell again once its cut-out is ready.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CutoutService? Cutouts
    {
        get => this._cutouts;
        set
        {
            if (this._cutouts is { } previous)
            {
                previous.Completed -= this.OnCutoutCompleted;
            }

            this._cutouts = value;
            if (value is not null)
            {
                value.Completed += this.OnCutoutCompleted;
            }
        }
    }

    /// <summary>Whether the next click on the selected cell picks an object.</summary>
    public bool CutoutPickArmed => this._cutoutPick;

    /// <summary>Whether the selected cell shows the points of its pick: while the Cutout tab is selected and the effect on.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowsCutoutPoints
    {
        get => this._cutoutPoints;
        set
        {
            if (value != this._cutoutPoints)
            {
                this._cutoutPoints = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>
    /// Whether the selected cell shows the Pick view — the whole image see-through over the cell — while
    /// selecting: Pick armed, the Cutout tab selected and the effect on. Otherwise the cell shows the
    /// cutout as it is, its treatments included.
    /// </summary>
    private bool InPickView => this._cutoutPoints && this._cutoutPick;

    /// <summary>
    /// Arms Pick on the selected cell — a layer is picked on in its edit view, where it is the cell — and
    /// has the image read at once, so the hover's outline comes quickly.
    /// </summary>
    public void ArmCutoutPick()
    {
        if (this.SelectedCellImage is not { } image || this._locked)
        {
            return;
        }

        this._cutoutPick = true;
        this.Cursor = Cursors.Cross;
        _ = this._cutouts?.PreviewAsync(image, []);
        this.Invalidate();
    }

    /// <summary>Disarms Pick, nothing picked: Escape, a right click, a click elsewhere, another selection.</summary>
    public void CancelCutoutPick()
    {
        if (!this._cutoutPick)
        {
            return;
        }

        this._cutoutPick = false;
        this.Cursor = Cursors.Default;
        this.ClearHover();
        this.Invalidate();
        this.CutoutPickEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A press while Pick is armed: a left click on the selected cell picks the image point under it —
    /// Shift adds an included point, Ctrl a left-out one, both keeping Pick armed; any other press cancels.
    /// Returns whether the press was taken.
    /// </summary>
    private bool CutoutMouseDown(MouseEventArgs e)
    {
        if (!this._cutoutPick)
        {
            return false;
        }

        if (e.Button != MouseButtons.Left || this.CutoutPickAt(e.Location) is not { } pick)
        {
            this.CancelCutoutPick();
            return true;
        }

        this.ClearHover();
        this.CutoutPicked?.Invoke(this, pick);
        if (!pick.Add)
        {
            this.CancelCutoutPick();
        }

        return true;
    }

    /// <summary>
    /// A move while Pick is armed: the crosshair over the selected cell, the object a click would pick there
    /// outlined; the arrow elsewhere. The cell's other hovers wait.
    /// </summary>
    private bool CutoutMouseMove(MouseEventArgs e)
    {
        if (!this._cutoutPick)
        {
            return false;
        }

        var pick = this.CutoutPickAt(e.Location);
        this.Cursor = pick is null ? Cursors.Default : Cursors.Cross;
        if (pick is null)
        {
            this.ClearHover();
        }
        else
        {
            this._hoverNext = pick;
            _ = this.OutlineHoverAsync();
        }

        return true;
    }

    /// <summary>The pick a click at <paramref name="location"/> makes, as the modifier keys say; <c>null</c> off the selected cell.</summary>
    private CutoutPick? CutoutPickAt(Point location)
    {
        var cells = this.CellBounds();
        if (this.SelectedCellImage is not { } image || this._selected >= cells.Length || !cells[this._selected].Contains(location))
        {
            return null;
        }

        var cell = cells[this._selected];
        var look = image.DrawnLook;
        var shown = look.Shown(image.Bitmap.Size);
        var time = this._player.GridTime;
        var spot = FitCalculator.ImagePointAt(cell, shown, look.ZoomAt(time, cell, shown), look.FocusAt(time, cell, shown), look.FineAngle, location);
        var loaded = look.LoadedPoint(spot, image.Bitmap.Size);
        var point = new CutoutPoint(Math.Clamp(loaded.X, 0, 1), Math.Clamp(loaded.Y, 0, 1), Included: (ModifierKeys & Keys.Control) == 0);
        return new CutoutPick(point, Add: (ModifierKeys & (Keys.Shift | Keys.Control)) != 0);
    }

    /// <summary>
    /// Outlines the object the last hovered pick would keep: one decode at a time, a move meanwhile only
    /// recording its pick, decoded once the running one ends (workfiles/20261029-cutout.md § Hover Outline).
    /// </summary>
    private async Task OutlineHoverAsync()
    {
        if (this._hoverBusy || this._hoverNext is not { } pick || this.SelectedCellImage is not { } image || this._cutouts is not { } cutouts)
        {
            return;
        }

        this._hoverNext = null;
        this._hoverBusy = true;
        var look = image.Look;
        var points = pick.Add && look.Cutout is { Mode: CutoutMode.Picked } cutout
            ? (IReadOnlyList<CutoutPoint>)[.. cutout.Points, pick.Point]
            : [pick.Point with { Included = true }];
        CutoutMask? mask;
        try
        {
            mask = await cutouts.PreviewAsync(image, points);
        }
        finally
        {
            this._hoverBusy = false;
        }

        if (this.IsDisposed)
        {
            return;
        }

        if (mask is not null && this._cutoutPick && this.SelectedCellImage == image && image.Look == look)
        {
            var previous = this._hoverOutline?.Edge;
            this._hoverOutline = (image, look, EdgeOf(mask));
            previous?.Dispose();
            this.Invalidate();
        }

        // The cursor moved on meanwhile: its last place is outlined next.
        if (this._hoverNext is not null && this._cutoutPick)
        {
            await this.OutlineHoverAsync();
        }
    }

    private void ClearHover()
    {
        this._hoverNext = null;
        if (this._hoverOutline is { } outline)
        {
            this._hoverOutline = null;
            outline.Edge.Dispose();
            this.Invalidate();
        }
    }

    /// <summary>
    /// The outline of <paramref name="mask"/>'s kept part, at the mask's size: its edge pixels in the helper
    /// color, a dark halo around them, the rest transparent — stretched over the image when drawn.
    /// </summary>
    private static Bitmap EdgeOf(CutoutMask mask)
    {
        int width = mask.Width, height = mask.Height;
        bool On(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && mask.Alpha[y * width + x] >= 128;
        var edge = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                edge[y * width + x] = On(x, y) && (!On(x - 1, y) || !On(x + 1, y) || !On(x, y - 1) || !On(x, y + 1));
            }
        }

        var pixels = new int[width * height];
        int line = HelperColor.ToArgb(), halo = HelperHalo.ToArgb();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (edge[y * width + x])
                {
                    pixels[y * width + x] = line;
                    continue;
                }

                // Next to an edge pixel: the halo keeping the line legible on any image.
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                {
                    for (int dx = -1; dx <= 1 && !near; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        near = nx >= 0 && ny >= 0 && nx < width && ny < height && edge[ny * width + nx];
                    }
                }

                pixels[y * width + x] = near ? halo : 0;
            }
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, data.Scan0 + y * data.Stride, width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>
    /// The Cutout's share of a paint: every image drawn uncut asked for its cut-out, a badge on each cell
    /// waiting for one, and on the selected cell the Pick view, the hover's outline and the pick's points.
    /// </summary>
    private void PaintCutout(Graphics g, Rectangle[] cells)
    {
        foreach (var cellImage in this._images)
        {
            this._cutouts?.Request(cellImage);
        }

        if (this.ShownLayers is { } layers)
        {
            foreach (var layer in layers.Items)
            {
                this._cutouts?.Request(layer.Image);
            }
        }

        this._cutoutBadges.Clear();
        for (int i = 0; i < this._images.Count && i < cells.Length; i++)
        {
            if (this.CutoutBadge(this._images[i]) is { } text)
            {
                this._cutoutBadges.Add(this.PaintCutoutBadge(g, cells[i], text));
            }
        }

        if (this._cutoutBadges.Count > 0)
        {
            this.CutoutBadgeTimer.Start();
        }
        else
        {
            this._cutoutBadgeTimer?.Stop();
        }

        if (this.SelectedCellImage is not { } image || this._selected >= cells.Length)
        {
            return;
        }

        var cell = cells[this._selected];
        if (this.InPickView)
        {
            this.PaintPickView(g, cell, image);
        }

        if (this._cutoutPick && this._hoverOutline is { } outline && outline.Image == image && outline.Look == image.Look)
        {
            this.PaintPickOutline(g, cell, image, outline.Edge);
        }

        if (this._cutoutPoints && image.Look.Cutout is { Mode: CutoutMode.Picked } cutout)
        {
            this.PaintCutoutPoints(g, cell, image, cutout);
        }
    }

    /// <summary>
    /// The Pick view: the whole image, uncut and without its background, drawn see-through over the cell
    /// as composed — the kept part looks as it does, the part left out shows faintly. Drawn once per image,
    /// look and cell, then reused (workfiles/20261029-cutout.md § Pick View).
    /// </summary>
    private void PaintPickView(Graphics g, Rectangle cell, SourceImage image)
    {
        if (this._pickView is not { } view || view.Image != image || view.Look != image.Look || view.Cell != cell)
        {
            this._pickView?.Bitmap.Dispose();
            var bitmap = new Bitmap(Math.Max(1, cell.Width), Math.Max(1, cell.Height), PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(bitmap))
            {
                bg.TranslateTransform(-cell.X, -cell.Y);
                var look = image.DrawnLook.WithBackground(null);
                Compositor.DrawCell(bg, new Frame(image.Bitmap, image.BandColor, look, this._player.GridTime, image.Sharpening), cell);
            }

            view = (image, image.Look, cell, bitmap);
            this._pickView = view;
        }

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = PickViewOpacity });
        g.DrawImage(view.Bitmap, cell, 0, 0, view.Bitmap.Width, view.Bitmap.Height, GraphicsUnit.Pixel, attributes);
    }

    /// <summary>The hover's outline, placed by the cell's own mapping: the edge bitmap drawn as the image, with its look's geometry only.</summary>
    private void PaintPickOutline(Graphics g, Rectangle cell, SourceImage image, Bitmap edge)
    {
        var state = g.Save();
        var look = image.DrawnLook.WithBackground(null).WithColors(null).WithBlur(null).WithCutout(null);
        Compositor.DrawCell(g, new Frame(edge, image.BandColor, look, this._player.GridTime), cell, fast: true);
        g.Restore(state);
    }

    // What the badge of an image says while its cut-out is not ready; null for none.
    private string? CutoutBadge(SourceImage image)
    {
        if (!image.CutoutPending)
        {
            return null;
        }

        string dots = new('.', 1 + (int)(Environment.TickCount64 / CutoutBadgeTick % 3));
        return this._cutouts?.IsWorking(image) == true ? $"✂ Cutting out{dots}"
            : !CutoutEngineFiles.IsAvailable ? "✂ Waiting for the cutout engine"
            : null;
    }

    // The badge at the bottom of the cell: a dark pill, its text white. Returns where it was drawn.
    private Rectangle PaintCutoutBadge(Graphics g, Rectangle cell, string text)
    {
        // As wide as the text with its longest dots, so the pill does not breathe with them.
        string widest = text.TrimEnd('.') + "...";
        var size = TextRenderer.MeasureText(g, widest, this.Font);
        int pad = this.LogicalToDeviceUnits(CutoutBadgePad);
        var badge = new Rectangle(cell.X + (cell.Width - size.Width) / 2 - pad, cell.Bottom - size.Height - 3 * pad, size.Width + 2 * pad, size.Height + pad);
        badge.Intersect(cell);
        using var brush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
        g.FillRectangle(brush, badge);
        TextRenderer.DrawText(g, text, this.Font, new Point(badge.X + pad, badge.Y + pad / 2), Color.White);
        return badge;
    }

    // The pick's points as helper indicators: an included one a dot, a left-out one a ring crossed.
    private void PaintCutoutPoints(Graphics g, Rectangle cell, SourceImage image, CutoutEffect cutout)
    {
        var look = image.DrawnLook;
        var shown = look.Shown(image.Bitmap.Size);
        var time = this._player.GridTime;
        double zoom = look.ZoomAt(time, cell, shown);
        var focus = look.FocusAt(time, cell, shown);
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        using var halo = new Pen(HelperHalo, this.LogicalToDeviceUnits(4));
        using var pen = new Pen(HelperColor, this.LogicalToDeviceUnits(2));
        using var fill = new SolidBrush(HelperColor);
        foreach (var point in cutout.Points)
        {
            var at = FitCalculator.DrawnAt(cell, shown, zoom, focus, look.FineAngle, look.ShownPoint(new PointF(point.X, point.Y), image.Bitmap.Size));
            if (point.Included)
            {
                float r = this.LogicalToDeviceUnits(CutoutDotRadius);
                g.DrawEllipse(halo, at.X - r, at.Y - r, 2 * r, 2 * r);
                g.FillEllipse(fill, at.X - r, at.Y - r, 2 * r, 2 * r);
            }
            else
            {
                float r = this.LogicalToDeviceUnits(CutoutRingRadius);
                float d = r * 0.6f;
                foreach (var stroke in (Pen[])[halo, pen])
                {
                    g.DrawEllipse(stroke, at.X - r, at.Y - r, 2 * r, 2 * r);
                    g.DrawLine(stroke, at.X - d, at.Y - d, at.X + d, at.Y + d);
                    g.DrawLine(stroke, at.X - d, at.Y + d, at.X + d, at.Y - d);
                }
            }
        }

        g.Restore(state);
        g.SmoothingMode = smoothing;
    }

    private System.Windows.Forms.Timer CutoutBadgeTimer
    {
        get
        {
            if (this._cutoutBadgeTimer is null)
            {
                this._cutoutBadgeTimer = new System.Windows.Forms.Timer { Interval = CutoutBadgeTick };
                this._cutoutBadgeTimer.Tick += (_, _) => this._cutoutBadges.ForEach(this.Invalidate);
                this.Disposed += (_, _) =>
                {
                    this._cutoutBadgeTimer.Dispose();
                    this._pickView?.Bitmap.Dispose();
                    this._hoverOutline?.Edge.Dispose();
                };
            }

            return this._cutoutBadgeTimer;
        }
    }

    private void OnCutoutCompleted(object? sender, SourceImage image)
    {
        if (!this.IsDisposed)
        {
            this.RedrawCell(image);
            this._cutoutBadges.ForEach(this.Invalidate);
        }
    }
}
