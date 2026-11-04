using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The placement guides of a dragged layer (workfiles/20261029-layer-ergonomics.md): the grid's center and
/// edges, every cell's, and the other layers' — fainter — snap the layer's bounding rectangle by proximity
/// and show as it comes near them, Shift ignoring them all. The drag only: neither the wheel nor the
/// rotation handle.
/// </summary>
internal sealed partial class GridPreview
{
    /// <summary>A reference of the dragged layer within this many logical pixels of a line snaps onto it.</summary>
    private const int LayerGuideSnap = 6;

    /// <summary>The share of a guide's opacity another layer's line keeps.</summary>
    private const double FaintGuideOpacity = 0.5;

    // Where the drag has taken the layer's center, snaps left out, in fractions of the grid; whether it
    // moved yet, and whether Shift freed it from the guides at its last move.
    private double _layerRawX;
    private double _layerRawY;
    private bool _layerMoved;
    private bool _layerSnapFree;

    /// <summary>A drag of the selected layer starts at <paramref name="layer"/>'s place.</summary>
    private void BeginLayerDrag(Layer layer)
    {
        this._layerRawX = layer.Place.X;
        this._layerRawY = layer.Place.Y;
        this._layerMoved = false;
        this._layerSnapFree = false;
    }

    /// <summary>
    /// The place of the dragged <paramref name="layer"/> once the mouse moved by <paramref name="delta"/>:
    /// where the drag takes it, then snapped onto the nearest guide on each axis — unless Shift is held.
    /// </summary>
    private LayerPlacement DraggedPlace(Layer layer, SizeF delta, Rectangle canvas)
    {
        var raw = (layer.Place with { X = this._layerRawX, Y = this._layerRawY }).MovedBy(delta, canvas.Size);
        this._layerRawX = raw.X;
        this._layerRawY = raw.Y;
        this._layerMoved |= !delta.IsEmpty;
        this._layerSnapFree = (ModifierKeys & Keys.Shift) != 0;
        if (this._layerSnapFree)
        {
            return raw;
        }

        var bounds = LayerRectangle(layer with { Place = raw }, canvas);
        var (vertical, horizontal) = this.LayerGuideLines(canvas);
        float snap = this.LogicalToDeviceUnits(LayerGuideSnap);
        var offset = new SizeF(LayerSnap.Offset(bounds.Left, bounds.Right, vertical, snap), LayerSnap.Offset(bounds.Top, bounds.Bottom, horizontal, snap));
        return offset.IsEmpty ? raw : raw.MovedBy(offset, canvas.Size);
    }

    /// <summary>
    /// The lines a dragged layer snaps onto, in the control: vertical ones by their x, horizontal ones by their
    /// y — the grid's edges and center, every cell's as drawn, the other shown layers' bounding rectangles, faint
    /// — one per position.
    /// </summary>
    private (List<LayerGuide> Vertical, List<LayerGuide> Horizontal) LayerGuideLines(Rectangle canvas)
    {
        var vertical = new List<LayerGuide>();
        var horizontal = new List<LayerGuide>();
        void Add(RectangleF r, bool faint)
        {
            vertical.AddRange([new(r.Left, faint), new(r.Left + r.Width / 2, faint), new(r.Right, faint)]);
            horizontal.AddRange([new(r.Top, faint), new(r.Top + r.Height / 2, faint), new(r.Bottom, faint)]);
        }

        Add(canvas, false);
        foreach (var cell in this.CellBounds())
        {
            Add(cell, false);
        }

        for (int i = 0; i < this._layers.Count; i++)
        {
            if (i != this._selectedLayer && this._layers.Items[i].Visible)
            {
                Add(LayerRectangle(this._layers.Items[i], canvas), true);
            }
        }

        return (LayerSnap.Merge(vertical), LayerSnap.Merge(horizontal));
    }

    /// <summary>A layer's bounding rectangle in the control: the smallest upright one holding its turned box.</summary>
    private static RectangleF LayerRectangle(Layer layer, Rectangle canvas)
    {
        var corners = layer.Place.Corners(layer.Shown, canvas.Size);
        return RectangleF.FromLTRB(
            corners.Min(c => c.X) + canvas.X,
            corners.Min(c => c.Y) + canvas.Y,
            corners.Max(c => c.X) + canvas.X,
            corners.Max(c => c.Y) + canvas.Y);
    }

    /// <summary>
    /// The guides of the dragged layer, across the whole grid, dashed green over the black halo: on each axis
    /// every line the layer rests on, at full opacity — half for another layer's —, else the nearest one within
    /// the approach distance — a share of the layer's bounding rectangle — fainter the farther, with its gap
    /// bar. None before the drag moved it, nor while Shift frees it; preview only.
    /// </summary>
    private void PaintLayerGuides(Graphics g, Rectangle canvas)
    {
        if (this._pressedLayer < 0 || !this._layerMoved || this._layerSnapFree || canvas.IsEmpty
            || this.SelectedLayer is not { Visible: true } layer || this._pressedLayer != this._selectedLayer)
        {
            return;
        }

        var bounds = LayerRectangle(layer, canvas);
        var (vertical, horizontal) = this.LayerGuideLines(canvas);
        int inset = this.LogicalToDeviceUnits(2);
        var center = Point.Round(new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        var guides = new List<(Point From, Point To, double Opacity)>();
        var bars = new List<(Point From, Point To)>();

        // The lines shown on each axis, at the opacity they would have as another layer's line.
        var shownX = new List<(float At, double Opacity)>();
        var shownY = new List<(float At, double Opacity)>();

        // A line on the canvas's edge is drawn just inside it, where the clip lets it show.
        int X(float at) => Math.Clamp((int)Math.Round(at), canvas.Left + inset, canvas.Right - 1 - inset);
        int Y(float at) => Math.Clamp((int)Math.Round(at), canvas.Top + inset, canvas.Bottom - 1 - inset);
        double Opacity(LayerGuide line, double opacity) => line.Faint ? opacity * FaintGuideOpacity : opacity;

        var resting = LayerSnap.Resting(bounds.Left, bounds.Right, vertical);
        float reachX = bounds.Width * this.GuideApproach / 100f;
        foreach (var line in resting)
        {
            guides.Add((new Point(X(line.At), canvas.Top), new Point(X(line.At), canvas.Bottom), Opacity(line, 1)));
            shownX.Add((line.At, FaintGuideOpacity));
        }

        if (resting.Count == 0 && this.GuideApproach > 0 && LayerSnap.Nearest(bounds.Left, bounds.Right, vertical, reachX) is { } nearX)
        {
            int x = X(nearX.Line.At);
            guides.Add((new Point(x, canvas.Top), new Point(x, canvas.Bottom), Opacity(nearX.Line, ApproachOpacity(nearX.Gap, reachX))));
            shownX.Add((nearX.Line.At, FaintGuideOpacity * ApproachOpacity(nearX.Gap, reachX)));
            int y = this.GapBarAt(center.Y, canvas.Top, canvas.Bottom);
            bars.Add((new Point(this.GapBarStart(bounds.X, bounds.Width, x), y), new Point(x, y)));
        }

        resting = LayerSnap.Resting(bounds.Top, bounds.Bottom, horizontal);
        float reachY = bounds.Height * this.GuideApproach / 100f;
        foreach (var line in resting)
        {
            guides.Add((new Point(canvas.Left, Y(line.At)), new Point(canvas.Right, Y(line.At)), Opacity(line, 1)));
            shownY.Add((line.At, FaintGuideOpacity));
        }

        if (resting.Count == 0 && this.GuideApproach > 0 && LayerSnap.Nearest(bounds.Top, bounds.Bottom, horizontal, reachY) is { } nearY)
        {
            int y = Y(nearY.Line.At);
            guides.Add((new Point(canvas.Left, y), new Point(canvas.Right, y), Opacity(nearY.Line, ApproachOpacity(nearY.Gap, reachY))));
            shownY.Add((nearY.Line.At, FaintGuideOpacity * ApproachOpacity(nearY.Gap, reachY)));
            int x = this.GapBarAt(center.X, canvas.Left, canvas.Right);
            bars.Add((new Point(x, this.GapBarStart(bounds.Y, bounds.Height, y)), new Point(x, y)));
        }

        foreach (var (from, to, opacity) in guides)
        {
            this.PaintGuideLines(g, canvas, [(from, to)], opacity);
        }

        this.PaintGapBars(g, bars);
        this.PaintMatchingLayers(g, canvas, shownX, shownY);
    }

    /// <summary>
    /// Every other shown layer one of whose references lies on a guide shown, its turned box outlined dashed —
    /// at the opacity of its line as another layer's — so the user sees which layer the dragged one lines up with.
    /// </summary>
    private void PaintMatchingLayers(Graphics g, Rectangle canvas, List<(float At, double Opacity)> shownX, List<(float At, double Opacity)> shownY)
    {
        if (shownX.Count == 0 && shownY.Count == 0)
        {
            return;
        }

        var state = g.Save();
        g.SetClip(canvas, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < this._layers.Count; i++)
        {
            var other = this._layers.Items[i];
            if (i == this._selectedLayer || !other.Visible)
            {
                continue;
            }

            var bounds = LayerRectangle(other, canvas);
            double opacity = shownX.Where(l => LayerSnap.Touches(bounds.Left, bounds.Right, l.At))
                .Concat(shownY.Where(l => LayerSnap.Touches(bounds.Top, bounds.Bottom, l.At)))
                .Select(l => l.Opacity)
                .DefaultIfEmpty(0)
                .Max();
            if (opacity <= 0)
            {
                continue;
            }

            var corners = other.Place.Corners(other.Shown, canvas.Size);
            for (int c = 0; c < corners.Length; c++)
            {
                corners[c] = new PointF(corners[c].X + canvas.X, corners[c].Y + canvas.Y);
            }

            using var halo = new Pen(Color.FromArgb((int)(HelperHalo.A * opacity), HelperHalo), this.LogicalToDeviceUnits(4));
            using var dashed = this.GuidePen(opacity);
            g.DrawPolygon(halo, corners);
            g.DrawPolygon(dashed, corners);
        }

        g.Restore(state);
    }
}
