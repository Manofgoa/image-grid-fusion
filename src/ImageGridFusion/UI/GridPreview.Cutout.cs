using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>A point clicked on the selected cell while Pick is armed: where on the image as loaded, and how it joins the pick.</summary>
internal readonly record struct CutoutPick(CutoutPoint Point, bool Add);

/// <summary>
/// The Cutout effect in the preview: its cut-outs asked for at every paint and their cells drawn again
/// once ready, the <i>Cutting out…</i> badge on a cell waiting for one, the click picking an object while
/// Pick is armed, and the points of the pick drawn as helper indicators. See workfiles/20261029-cutout.md § UI.
/// </summary>
internal sealed partial class GridPreview
{
    // How often the badge's dots move, in ms.
    private const int CutoutBadgeTick = 400;

    // In logical pixels: an included point's dot, an excluded point's ring, and the badge's padding.
    private const int CutoutDotRadius = 5;
    private const int CutoutRingRadius = 7;
    private const int CutoutBadgePad = 6;

    private readonly List<Rectangle> _cutoutBadges = [];
    private System.Windows.Forms.Timer? _cutoutBadgeTimer;
    private CutoutService? _cutouts;
    private bool _cutoutPick;
    private bool _cutoutPoints;

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

    /// <summary>Arms Pick on the selected cell — a layer is picked on in its edit view, where it is the cell.</summary>
    public void ArmCutoutPick()
    {
        if (this.SelectedCellImage is null || this._locked)
        {
            return;
        }

        this._cutoutPick = true;
        this.Cursor = Cursors.Cross;
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

        var cells = this.CellBounds();
        if (e.Button != MouseButtons.Left || this.SelectedCellImage is not { } image || this._selected >= cells.Length || !cells[this._selected].Contains(e.Location))
        {
            this.CancelCutoutPick();
            return true;
        }

        var cell = cells[this._selected];
        var look = image.DrawnLook;
        var shown = look.Shown(image.Bitmap.Size);
        var time = this._player.GridTime;
        var spot = FitCalculator.ImagePointAt(cell, shown, look.ZoomAt(time, cell, shown), look.FocusAt(time, cell, shown), look.FineAngle, e.Location);
        var loaded = look.LoadedPoint(spot, image.Bitmap.Size);
        var point = new CutoutPoint(Math.Clamp(loaded.X, 0, 1), Math.Clamp(loaded.Y, 0, 1), Included: (ModifierKeys & Keys.Control) == 0);
        bool add = (ModifierKeys & (Keys.Shift | Keys.Control)) != 0;
        this.CutoutPicked?.Invoke(this, new CutoutPick(point, add));
        if (!add)
        {
            this.CancelCutoutPick();
        }

        return true;
    }

    /// <summary>A move while Pick is armed: the crosshair over the selected cell, the arrow elsewhere; the cell's other hovers wait.</summary>
    private bool CutoutMouseMove(MouseEventArgs e)
    {
        if (!this._cutoutPick)
        {
            return false;
        }

        var cells = this.CellBounds();
        this.Cursor = this._selected >= 0 && this._selected < cells.Length && cells[this._selected].Contains(e.Location) ? Cursors.Cross : Cursors.Default;
        return true;
    }

    /// <summary>
    /// The Cutout's share of a paint: every image drawn uncut asked for its cut-out, a badge on each cell
    /// waiting for one, and the pick's points on the selected cell.
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

        if (this._cutoutPoints && this.SelectedCellImage is { Look.Cutout: { Mode: CutoutMode.Picked } cutout } image && this._selected < cells.Length)
        {
            this.PaintCutoutPoints(g, cells[this._selected], image, cutout);
        }
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
                this.Disposed += (_, _) => this._cutoutBadgeTimer.Dispose();
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
