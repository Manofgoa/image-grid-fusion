using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The layers laid over the grid (workfiles/20261029-layers.md): drawn over everything while their effect
/// is on, taking the mouse only while the Layers tab of the Global toolbar is selected — the cells
/// behave as ever otherwise. A layer is selected first, then dragged to move it, the wheel over it
/// resizing it, Shift + wheel turning it.
/// </summary>
internal sealed partial class GridPreview
{
    /// <summary>A notch of the wheel grows or shrinks the selected layer by 5 %, like a resizable zone.</summary>
    private const double LayerNotch = 1.05;

    /// <summary>A notch of Shift + wheel turns the selected layer by 5°.</summary>
    private const double LayerTurnNotch = 5;

    private const int LayerOutlineWidth = 2;
    private const int LayerHaloWidth = 5;

    // The rotation handle: a knob above the middle of the top edge, at the end of a stem; Shift snaps its turn.
    private const int RotationStem = 24;
    private const int RotationKnob = 7;
    private const double RotationSnap = 15;

    // A text box's side handles: a square on the middle of its left and right edges (workfiles/20261029-text-layer-alignment.md).
    private const int BoxHandle = 5;

    private GridLayers _layers = GridLayers.Initial;
    private bool _layersShown;
    private bool _layersActive;
    private int _selectedLayer = -1;

    // The selected layer pressed to be moved, and where the mouse last was; a layer pressed while not
    // selected is only selected, and does not move.
    private int _pressedLayer = -1;
    private Point _layerPoint;
    private int _layerWheelDelta;

    // The wheel resized or turned the selected layer, drawn fast: drawn in full once its burst rests.
    private bool _layerWheeled;

    // The rotation handle hovered, or dragged.
    private bool _hoveringRotation;
    private bool _rotatingLayer;

    // The text box's side hovered, or dragged: -1 the left, 1 the right, 0 none.
    private int _hoveredBoxSide;
    private int _resizingBox;

    // A layer's edit view: the grid set aside meanwhile, the layer shown alone as its one cell.
    private EditState? _edit;

    /// <summary>What a layer's edit view sets aside, given back when it closes.</summary>
    private sealed record EditState(int Layer, List<SourceImage> Images, GridLayout? Layout, double Ratio, double FreeRatio, GridBorders? Borders, SeamFade? Seams);

    /// <summary>Raised when a layer's edit view opens or closes.</summary>
    public event EventHandler? LayerEditChanged;

    /// <summary>Whether a layer's edit view shows (workfiles/20261029-layers.md § Edit View).</summary>
    public bool EditingLayer => this._edit is not null;

    /// <summary>Raised when the layers change — added, moved, turned, resized, tuned, removed, restacked — or the selected one.</summary>
    public event EventHandler? LayersChanged;

    /// <summary>
    /// Draws a text layer again, as it says — its box's width changed by a drag —; given by the form, which
    /// owns the drawing of the texts. <c>null</c> when it cannot be drawn.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Layer, Layer?>? RenderTextLayer { get; set; }

    /// <summary>The layers, the last one on top, whether their effect is on or not.</summary>
    public GridLayers Layers => this._layers;

    /// <summary>Whether the Layers effect is on: its layers drawn over the grid, in the preview and the exports.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool LayersShown
    {
        get => this._layersShown;
        set
        {
            if (value == this._layersShown)
            {
                return;
            }

            this.EndLayerEdit();
            this._layersShown = value;
            if (!value)
            {
                this.SelectLayer(-1);
            }

            this.RedrawAll();
            this.SyncPlayer();
            this.ContentVersion++;
        }
    }

    /// <summary>Whether the Layers tab of the Global toolbar is selected: only then do the layers take the mouse.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool LayersActive
    {
        get => this._layersActive;
        set
        {
            if (value == this._layersActive)
            {
                return;
            }

            this.EndLayerEdit();
            this._layersActive = value;
            if (!value)
            {
                this.SelectLayer(-1);
            }

            this.Invalidate();
        }
    }

    public bool HasLayerSelection => this._selectedLayer >= 0;

    /// <summary>
    /// The selected layer, the one the effects toolbar acts on instead of a cell; <c>null</c> for none —
    /// and in its edit view, where it is the selected cell.
    /// </summary>
    public Layer? SelectedLayer => this._edit is null ? this.ListedLayer : null;

    /// <summary>The selected layer, the one the layer list shows selected, its edit view open or not.</summary>
    public Layer? ListedLayer => this._selectedLayer >= 0 && this._selectedLayer < this._layers.Count ? this._layers.Items[this._selectedLayer] : null;

    public int SelectedLayerIndex => this.ListedLayer is null ? -1 : this._selectedLayer;

    /// <summary>The layers drawn now — the visible ones —: none while their effect is off, or a layer's edit view shows.</summary>
    private GridLayers? ShownLayers => this._layersShown && this._edit is null ? this._layers.Shown : null;

    /// <summary>The contents the player plays beside the cells: the shown layers'.</summary>
    private IReadOnlyList<SourceImage> ShownLayerImages => this.ShownLayers?.Items.Select(l => l.Image).ToList() ?? [];

    /// <summary>Whether the layers take the mouse now: their tab selected, their effect on, no export running.</summary>
    private bool LayersTakeMouse => this._layersActive && this._layersShown && !this._locked && this._edit is null && this._layers.Count > 0;

    /// <summary>
    /// Puts <paramref name="layers"/> in place — a change of the window, an undo, a gesture — each content
    /// taking its layer's look; the contents no layer holds any more are let go (<see cref="ReleaseImage"/>,
    /// which the undo history keeps alive). <paramref name="selected"/> becomes the selected layer — none for
    /// -1 —, else the selection stays where it was while it still exists.
    /// </summary>
    public void SetLayers(GridLayers layers, int? selected = null)
    {
        // Given from outside, the layers change under the edit view: it closes first.
        this.EndLayerEdit();
        var before = this._layers;
        if (ReferenceEquals(layers, before) && selected is null)
        {
            return;
        }

        this._layers = layers;
        foreach (var layer in layers.Items)
        {
            var previous = before.Items.FirstOrDefault(l => l.Image == layer.Image);
            layer.Image.Look = layer.Look;
            if (previous is not null && previous.Look.Frames != layer.Look.Frames)
            {
                this.ShowFrames(layer.Image);
            }

            if (layer.Look.Motion is not null)
            {
                this._progressTimer.Start();
            }
        }

        // The selection changes with its index or its content; a move or a resize keeps it.
        int selection = selected ?? this._selectedLayer;
        selection = selection >= 0 && selection < layers.Count ? selection : -1;
        bool selectionChanged = selection != this._selectedLayer
            || (selection >= 0 && (selection >= before.Count || before.Items[selection].Image != layers.Items[selection].Image));
        this._selectedLayer = selection;
        if (this._selectedLayer >= 0)
        {
            this.ClearCellSelection();
        }

        this.RedrawLayersChange(before, layers);
        this.SyncPlayer();

        // Let go last: the area drawn again reads the contents left, which the history may dispose at once.
        var kept = layers.Items.Select(l => l.Image).ToHashSet();
        foreach (var gone in before.Items.Select(l => l.Image).Distinct().Where(i => !kept.Contains(i)).ToList())
        {
            this.ReleaseImage(gone);
        }

        this.ContentVersion++;
        this.LayersChanged?.Invoke(this, EventArgs.Empty);
        if (selectionChanged)
        {
            this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Adds <paramref name="layer"/> on top of the others, selected — the cell's selection cleared.</summary>
    public void AddLayer(Layer layer) => this.SetLayers(this._layers.Adding(layer), this._layers.Count);

    /// <summary>The selected layer removed; its content let go.</summary>
    public void RemoveSelectedLayer()
    {
        if (this.SelectedLayer is not null && !this._locked)
        {
            this.SetLayers(this._layers.Without(this._selectedLayer), -1);
        }
    }

    /// <summary>The selected layer moved to <paramref name="to"/> in the stacking order — 0 at the back — staying selected.</summary>
    public void RestackSelectedLayer(int to)
    {
        if (this.SelectedLayer is null || this._locked)
        {
            return;
        }

        to = Math.Clamp(to, 0, this._layers.Count - 1);
        if (to != this._selectedLayer)
        {
            this.SetLayers(this._layers.Moving(this._selectedLayer, to), to);
        }
    }

    /// <summary>The selected layer replaced by <paramref name="layer"/> — its text or style changed.</summary>
    public void ReplaceSelectedLayer(Layer layer)
    {
        if (this.SelectedLayer is not null)
        {
            this.SetLayers(this._layers.With(this._selectedLayer, layer));
        }
    }

    /// <summary>Selects the layer at <paramref name="index"/> — none for -1 —, clearing the cell's selection.</summary>
    public void SelectLayer(int index)
    {
        index = index >= 0 && index < this._layers.Count ? index : -1;
        if (index == this._selectedLayer)
        {
            return;
        }

        this.EndLayerEdit();

        this.CancelEyedropper();
        var previous = this.SelectedLayer;
        this._selectedLayer = index;
        if (index >= 0)
        {
            this.ClearCellSelection();
        }

        this.InvalidateLayerOutline(previous);
        this.InvalidateLayerOutline(this.SelectedLayer);
        this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The cell's selection cleared without touching the layer's: the two are exclusive.</summary>
    private void ClearCellSelection()
    {
        if (this._selected >= 0)
        {
            this._selected = -1;
            this._hoveredBar = null;
            this._hoveredCorner = null;
            this._keyPan = null;
            this.Invalidate();
        }
    }

    /// <summary>Gives the selected layer a new look: an effect of the effects toolbar toggled or tuned.</summary>
    private void SetLayerLook(ImageLook look)
    {
        if (this.SelectedLayer is { } layer && look != layer.Look)
        {
            this.SetLayers(this._layers.With(this._selectedLayer, layer with { Look = look }));
            this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The cell the selected layer is drawn into, in its own space; <c>null</c> without one.</summary>
    private Rectangle? SelectedLayerCell
    {
        get
        {
            var canvas = this.CanvasBounds();
            return this.SelectedLayer is { } layer && !canvas.IsEmpty
                ? Compositor.LayerCell(new LayerFrame(layer.FrameAt(this._player.GridTime), layer.Place), canvas.Size)
                : null;
        }
    }

    /// <summary>The topmost layer under <paramref name="location"/> of the control; -1 for none.</summary>
    private int LayerAt(Point location)
    {
        var canvas = this.CanvasBounds();
        return canvas.IsEmpty ? -1 : this._layers.At(new PointF(location.X - canvas.X, location.Y - canvas.Y), canvas.Size);
    }

    /// <summary>Whether <paramref name="location"/> of the control falls on the selected layer, whatever lies over it.</summary>
    private bool OnSelectedLayer(Point location)
    {
        var canvas = this.CanvasBounds();
        return this.SelectedLayer is { Visible: true } layer && !canvas.IsEmpty
            && layer.Place.Contains(new PointF(location.X - canvas.X, location.Y - canvas.Y), layer.Shown, canvas.Size);
    }

    /// <summary>
    /// A press, while the layers take the mouse: on a layer not selected, it selects it only; on the
    /// selected one, it starts moving it. Elsewhere it is the cells' — a cell selected clears the layer's.
    /// Whether the layers took it.
    /// </summary>
    private bool LayerMouseDown(MouseEventArgs e)
    {
        if (!this.LayersTakeMouse)
        {
            return false;
        }

        // The rotation handle first: it lies outside the layer, over whatever is there.
        if (this.OnRotationHandle(e.Location))
        {
            this._rotatingLayer = true;
            this.InvalidateLayerOutline(this.SelectedLayer);
            return true;
        }

        // Then a text box's side handles, half outside it.
        if (this.OnBoxHandle(e.Location) is var side and not 0)
        {
            this._resizingBox = side;
            this.InvalidateLayerOutline(this.SelectedLayer);
            return true;
        }

        int hit =this.OnSelectedLayer(e.Location) ? this._selectedLayer : this.LayerAt(e.Location);
        if (hit < 0)
        {
            return false;
        }

        if (hit != this._selectedLayer)
        {
            this.SelectLayer(hit);
            return true;
        }

        // A double-click on the selected layer opens its edit view.
        if (e.Clicks == 2)
        {
            this.BeginLayerEdit();
            return true;
        }

        this._pressedLayer = hit;
        this._layerPoint = e.Location;
        this.Cursor = Cursors.SizeAll;
        return true;
    }

    /// <summary>The mouse moved: the pressed layer follows it; over a layer, the cursor tells what a press does. Whether the layers took it.</summary>
    private bool LayerMouseMove(MouseEventArgs e)
    {
        if (this._rotatingLayer)
        {
            if (e.Button == MouseButtons.Left && this.SelectedLayer is { } turned)
            {
                var canvas = this.CanvasBounds();
                var center = turned.Place.Center(canvas.Size);
                double angle = Math.Atan2(e.Y - canvas.Y - center.Y, e.X - canvas.X - center.X) * 180 / Math.PI + 90;
                if ((ModifierKeys & Keys.Shift) != 0)
                {
                    angle = Math.Round(angle / RotationSnap) * RotationSnap;
                }

                this.SetLayers(this._layers.With(this._selectedLayer, turned with { Place = turned.Place.WithAngle(angle) }));
                this.Update();
            }

            return true;
        }

        if (this._resizingBox != 0)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.ResizeTextBox(e.Location);
            }

            return true;
        }

        if (this._pressedLayer >= 0)
        {
            if (e.Button == MouseButtons.Left && this.SelectedLayer is { } layer && this._pressedLayer == this._selectedLayer)
            {
                var canvas = this.CanvasBounds();
                var moved = layer.Place.MovedBy(new SizeF(e.X - this._layerPoint.X, e.Y - this._layerPoint.Y), canvas.Size);
                this._layerPoint = e.Location;
                this.SetLayers(this._layers.With(this._selectedLayer, layer with { Place = moved }));
                this.Update();
            }

            return true;
        }

        if (!this.LayersTakeMouse || this._pressed >= 0 || this._draggedBar is not null || this._draggedCorner is not null
            || this._draggedSeparator is not null || this._movedCrop is not null)
        {
            return false;
        }

        bool rotation = this.OnRotationHandle(e.Location);
        if (rotation != this._hoveringRotation)
        {
            this._hoveringRotation = rotation;
            this.InvalidateLayerOutline(this.SelectedLayer);
        }

        int boxSide = this.OnBoxHandle(e.Location);
        if (boxSide != this._hoveredBoxSide)
        {
            this._hoveredBoxSide = boxSide;
            this.InvalidateLayerOutline(this.SelectedLayer);
        }

        bool selected = rotation || boxSide != 0 || this.OnSelectedLayer(e.Location);
        if (!selected && this.LayerAt(e.Location) < 0)
        {
            return false;
        }

        // Over a layer, the cell under it shows no hover: its buttons would not take the click.
        if (this._hovered >= 0 || this._hoveredBar is not null || this._hoveredCorner is not null || this._hoveredSeparator is not null)
        {
            this._hovered = -1;
            this._hoveringClose = false;
            this._hoveringHandle = false;
            this._hoveredBar = null;
            this._hoveredCorner = null;
            this._hoveringKept = false;
            this._hoveredSeparator = null;
            this.Invalidate();
        }

        this.Cursor = boxSide != 0 ? Cursors.SizeWE : rotation || !selected ? Cursors.Hand : Cursors.SizeAll;
        return true;
    }

    /// <summary>
    /// A text box's side dragged to <paramref name="location"/>, along the box's own axis: the box as wide as
    /// that, at least a line, its opposite side staying where it is, the text wrapped in it again.
    /// </summary>
    private void ResizeTextBox(Point location)
    {
        var canvas = this.CanvasBounds();
        if (this.SelectedLayer is not { TextBox: { } lines } layer || this.RenderTextLayer is not { } render || canvas.IsEmpty || layer.Place.LinePixels <= 0)
        {
            return;
        }

        var box = layer.Place.BoxSize(layer.Shown, canvas.Size);
        var center = layer.Place.Center(canvas.Size);
        double radians = layer.Place.Angle * Math.PI / 180, ax = Math.Cos(radians), ay = Math.Sin(radians);
        double along = (location.X - canvas.X - center.X) * ax + (location.Y - canvas.Y - center.Y) * ay;
        double fixedSide = -this._resizingBox * box.Width / 2;

        // A line on the canvas, and the drawing's margins around the box, in lines — kept as they are.
        double line = Math.Min(canvas.Width, canvas.Height) * layer.Place.Size;
        double margins = layer.Image.Bitmap.Width / layer.Place.LinePixels - lines;
        double wanted = Math.Clamp(this._resizingBox * (along - fixedSide) / line - margins, Layer.MinTextBox, Layer.MaxTextBox);
        if (Math.Abs(wanted - lines) < 0.01)
        {
            return;
        }

        double middle = fixedSide + this._resizingBox * (wanted + margins) * line / 2;
        var place = layer.Place with { X = (center.X + middle * ax) / canvas.Width, Y = (center.Y + middle * ay) / canvas.Height };
        if (render(layer with { Place = place, TextBox = wanted }) is { } drawn)
        {
            this.SetLayers(this._layers.With(this._selectedLayer, drawn));
            this.Update();
        }
    }

    /// <summary>The selected text layer's box handles in the control — the middles of its left and right sides, turned with it —; none without a box.</summary>
    private PointF[]? BoxHandles(Rectangle canvas)
    {
        if (this.SelectedLayer is not { Visible: true, TextBox: not null } layer || canvas.IsEmpty)
        {
            return null;
        }

        var box = layer.Place.BoxSize(layer.Shown, canvas.Size);
        PointF[] points = [new(-box.Width / 2, 0), new(box.Width / 2, 0)];
        using var matrix = layer.Place.ToCanvas(canvas.Size);
        matrix.TransformPoints(points);
        return [.. points.Select(p => new PointF(p.X + canvas.X, p.Y + canvas.Y))];
    }

    /// <summary>The side of the selected text box whose handle <paramref name="location"/> grabs, while the layers take the mouse: -1 the left, 1 the right, 0 none.</summary>
    private int OnBoxHandle(Point location)
    {
        if (!this.LayersTakeMouse || this.BoxHandles(this.CanvasBounds()) is not { } handles)
        {
            return 0;
        }

        double reach = this.LogicalToDeviceUnits(BoxHandle + 3);
        for (int i = 0; i < handles.Length; i++)
        {
            double dx = location.X - handles[i].X, dy = location.Y - handles[i].Y;
            if (dx * dx + dy * dy <= reach * reach)
            {
                return i == 0 ? -1 : 1;
            }
        }

        return 0;
    }

    /// <summary>A move of a layer ends: drawn again in full. Whether a layer was pressed.</summary>
    private bool EndLayerPress()
    {
        if (this._pressedLayer < 0 && !this._rotatingLayer && this._resizingBox == 0)
        {
            return false;
        }

        this._pressedLayer = -1;
        this._rotatingLayer = false;
        this._resizingBox = 0;
        this.Cursor = Cursors.Default;
        this.RedrawLayerArea(this.SelectedLayer, fast: false);
        return true;
    }

    /// <summary>
    /// The wheel over the selected layer: each notch grows or shrinks it by 5 %, or with Shift turns it by
    /// 5°. Elsewhere it is the cells' wheel. Whether the layers took it.
    /// </summary>
    private bool LayerWheel(MouseEventArgs e)
    {
        if (!this.LayersTakeMouse || this.SelectedLayer is not { } layer || this._pressed >= 0 || this._pressedLayer >= 0 || this._resizingBox != 0 || !this.OnSelectedLayer(e.Location))
        {
            this._layerWheelDelta = 0;
            return false;
        }

        this._layerWheelDelta += e.Delta;
        int notches = this._layerWheelDelta / WheelNotch;
        this._layerWheelDelta -= notches * WheelNotch;
        if (notches == 0)
        {
            return true;
        }

        var place = (ModifierKeys & Keys.Shift) != 0
            ? layer.Place.WithAngle(layer.Place.Angle + notches * LayerTurnNotch)
            : layer.Place.WithSize(layer.Place.Size * Math.Pow(LayerNotch, notches));
        this.SetLayers(this._layers.With(this._selectedLayer, layer with { Place = place }));

        // The burst is one step of the undo history, drawn in full once it rests.
        this._layerWheeled = true;
        this._wheelEnd.Stop();
        this._wheelEnd.Start();
        return true;
    }

    /// <summary>The selected layer's outline, a helper indicator: its turned box, green over the black halo; preview only.</summary>
    private void PaintLayerSelection(Graphics g, Rectangle canvas)
    {
        if (!this._layersActive || !this._layersShown || this.SelectedLayer is not { Visible: true } layer || this._dragging)
        {
            return;
        }

        var corners = layer.Place.Corners(layer.Shown, canvas.Size);
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = new PointF(corners[i].X + canvas.X, corners[i].Y + canvas.Y);
        }

        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var halo = new Pen(HelperHalo, this.LogicalToDeviceUnits(LayerHaloWidth)) { LineJoin = LineJoin.Miter })
        {
            g.DrawPolygon(halo, corners);
        }

        using (var outline = new Pen(this._pressedLayer >= 0 ? Color.White : HelperColor, this.LogicalToDeviceUnits(LayerOutlineWidth)) { LineJoin = LineJoin.Miter })
        {
            g.DrawPolygon(outline, corners);
        }

        // The rotation handle: a stem from the top edge's middle, a knob at its end; white while hovered or dragged.
        var (from, knob) = this.RotationHandle(layer, canvas);
        float radius = this.LogicalToDeviceUnits(RotationKnob);
        var color = this._rotatingLayer || this._hoveringRotation ? Color.White : HelperColor;
        using (var halo = new Pen(HelperHalo, this.LogicalToDeviceUnits(LayerHaloWidth)))
        {
            g.DrawLine(halo, from, knob);
            g.DrawEllipse(halo, knob.X - radius, knob.Y - radius, 2 * radius, 2 * radius);
        }

        using (var stem = new Pen(color, this.LogicalToDeviceUnits(LayerOutlineWidth)))
        {
            g.DrawLine(stem, from, knob);
        }

        using (var fill = new SolidBrush(color))
        {
            g.FillEllipse(fill, knob.X - radius, knob.Y - radius, 2 * radius, 2 * radius);
        }

        // A text box's side handles: a square on each side's middle, turned with it; white while hovered or dragged.
        if (this.BoxHandles(canvas) is { } handles)
        {
            float half = this.LogicalToDeviceUnits(BoxHandle);
            float halo = this.LogicalToDeviceUnits(2);
            var state = g.Save();
            for (int i = 0; i < handles.Length; i++)
            {
                int side = i == 0 ? -1 : 1;
                g.ResetTransform();
                g.TranslateTransform(handles[i].X, handles[i].Y);
                g.RotateTransform((float)layer.Place.Angle);
                using (var dark = new SolidBrush(HelperHalo))
                {
                    g.FillRectangle(dark, -half - halo, -half - halo, 2 * (half + halo), 2 * (half + halo));
                }

                using var square = new SolidBrush(this._resizingBox == side || this._hoveredBoxSide == side ? Color.White : HelperColor);
                g.FillRectangle(square, -half, -half, 2 * half, 2 * half);
            }

            g.Restore(state);
        }

        g.SmoothingMode = smoothing;
    }

    /// <summary>The rotation handle of <paramref name="layer"/> in the control: the middle of its top edge, and its knob's center beyond it, turned with it.</summary>
    private (PointF From, PointF Knob) RotationHandle(Layer layer, Rectangle canvas)
    {
        var box = layer.Place.BoxSize(layer.Shown, canvas.Size);
        PointF[] points = [new(0, -box.Height / 2), new(0, -box.Height / 2 - this.LogicalToDeviceUnits(RotationStem))];
        using var matrix = layer.Place.ToCanvas(canvas.Size);
        matrix.TransformPoints(points);
        return (new PointF(points[0].X + canvas.X, points[0].Y + canvas.Y), new PointF(points[1].X + canvas.X, points[1].Y + canvas.Y));
    }

    /// <summary>Whether <paramref name="location"/> grabs the selected layer's rotation handle, while the layers take the mouse.</summary>
    private bool OnRotationHandle(Point location)
    {
        var canvas = this.CanvasBounds();
        if (!this.LayersTakeMouse || this.SelectedLayer is not { Visible: true } layer || canvas.IsEmpty)
        {
            return false;
        }

        var knob = this.RotationHandle(layer, canvas).Knob;
        double reach = this.LogicalToDeviceUnits(RotationKnob + 3);
        double dx = location.X - knob.X, dy = location.Y - knob.Y;
        return dx * dx + dy * dy <= reach * reach;
    }

    /// <summary>Repaints where <paramref name="layer"/>'s outline is drawn.</summary>
    private void InvalidateLayerOutline(Layer? layer)
    {
        var canvas = this.CanvasBounds();
        if (layer is not null && !canvas.IsEmpty)
        {
            var bounds = this.LayerBounds(layer, canvas.Size);
            bounds.Offset(canvas.Location);
            this.Invalidate(bounds);
        }
    }

    /// <summary>The area <paramref name="layer"/> covers on the canvas, its outline's width around it.</summary>
    private Rectangle LayerBounds(Layer layer, Size canvas)
    {
        var corners = layer.Place.Corners(layer.Shown, canvas);
        var bounds = Rectangle.FromLTRB(
            (int)Math.Floor(corners.Min(c => c.X)),
            (int)Math.Floor(corners.Min(c => c.Y)),
            (int)Math.Ceiling(corners.Max(c => c.X)),
            (int)Math.Ceiling(corners.Max(c => c.Y)));
        // The rotation handle reaches beyond the box, whichever way the layer is turned.
        int margin = this.LogicalToDeviceUnits(LayerHaloWidth + RotationStem + RotationKnob) + 2;
        bounds.Inflate(margin, margin);
        return bounds;
    }

    /// <summary>
    /// The layers changed from <paramref name="before"/> to <paramref name="after"/>: only the area of the one
    /// layer that changed is drawn again — fast during a gesture —, else the whole grid.
    /// </summary>
    private void RedrawLayersChange(GridLayers before, GridLayers after)
    {
        if (!this._layersShown)
        {
            this.Invalidate();
            return;
        }

        var canvas = this.CanvasBounds();
        int changed = before.Count == after.Count
            ? Enumerable.Range(0, after.Count).Count(i => before.Items[i] != after.Items[i])
            : -1;
        if (changed == 0)
        {
            this.Invalidate();
            return;
        }

        if (changed != 1 || canvas.IsEmpty)
        {
            this.RedrawAll();
            return;
        }

        int index = Enumerable.Range(0, after.Count).First(i => before.Items[i] != after.Items[i]);
        var area = Rectangle.Union(this.LayerBounds(before.Items[index], canvas.Size), this.LayerBounds(after.Items[index], canvas.Size));
        this.RedrawArea(area, fast: this.InGesture);
    }

    /// <summary>Draws <paramref name="layer"/>'s area again into the cached preview: a new frame of its content, its motion.</summary>
    private void RedrawLayerArea(Layer? layer, bool fast)
    {
        var canvas = this.CanvasBounds();
        if (layer is not null && !canvas.IsEmpty)
        {
            this.RedrawArea(this.LayerBounds(layer, canvas.Size), fast);
        }
    }

    /// <summary>The cache dropped: the whole grid is drawn again at the next paint.</summary>
    private void RedrawAll()
    {
        this._cache?.Dispose();
        this._cache = null;
        this.Invalidate();
    }

    /// <summary>
    /// Draws <paramref name="area"/> of the canvas again into the cached preview — the cells under it, the
    /// borders, then the layers — and repaints it: what a layer that moved, turned or played leaves.
    /// </summary>
    private void RedrawArea(Rectangle area, bool fast)
    {
        var canvas = this.CanvasBounds();
        if (this._cache is null || this._cache.Size != canvas.Size)
        {
            this.Invalidate();
            return;
        }

        // No cell under the layers: the whole canvas is drawn again, the layers alone.
        if (this._layout is null)
        {
            this.RedrawAll();
            return;
        }

        area.Intersect(new Rectangle(Point.Empty, canvas.Size));
        if (area.IsEmpty)
        {
            return;
        }

        var slots = this._layout.Cells(canvas.Size);
        var cells = Compositor.Cells(this._layout, canvas.Size, this._borders);
        var seams = this.SeamsOf(cells);
        using (var g = Graphics.FromImage(this._cache))
        {
            g.SetClip(area);
            g.Clear(Color.Transparent);
            for (int i = 0; i < cells.Length && i < this._images.Count; i++)
            {
                if (cells[i].IntersectsWith(area))
                {
                    Compositor.DrawCell(g, this.FrameOf(this._images[i]), cells[i], fast, seams);
                }
            }

            this._borders?.Draw(g, slots, canvas.Size);
            this.DrawLayersInto(g, canvas.Size, fast);
        }

        this._borders?.CutCorners(this._cache, area);
        area.Offset(canvas.Location);
        this.Invalidate(area);
    }

    /// <summary>The shown layers drawn into <paramref name="g"/>, at the grid's time, within its clip.</summary>
    private void DrawLayersInto(Graphics g, Size canvas, bool fast)
    {
        if (this.ShownLayers is { } layers)
        {
            Compositor.DrawLayers(g, [.. layers.Items.Select(l => new LayerFrame(l.FrameAt(this._player.GridTime), l.Place))], canvas, fast);
        }
    }

    /// <summary>The layer showing <paramref name="image"/>, or <c>null</c>.</summary>
    private Layer? LayerOf(SourceImage image) => this._layers.Items.FirstOrDefault(l => l.Image == image);

    /// <summary>
    /// Opens the selected layer's edit view (workfiles/20261029-layers.md § Edit View): the grid set aside,
    /// the canvas takes the layer's shape and holds it alone as its one cell, upright and selected — so
    /// every tool of a cell works on it: the effects toolbar, the blur's bars, the crop's edit view, the
    /// wheel's zoom, the drag within the box. Its look changes as the layer's; its place is left alone.
    /// </summary>
    public void BeginLayerEdit()
    {
        if (this._edit is not null || this.SelectedLayer is not { Visible: true } layer || this._locked || !this._layersShown)
        {
            return;
        }

        this.CancelEyedropper();
        this.EndLayerPress();
        this.EndDrag();
        this.EndKeyPan();
        this._edit = new EditState(this._selectedLayer, [.. this._images], this._layout, this._ratio, this.FreeRatio, this._borders, this._seams);
        this._images.Clear();
        layer.Image.Look = layer.Look;
        this._images.Add(layer.Image);
        this._layout = GridLayout.Default(1);
        this._borders = null;
        this._seams = null;
        this._selected = 0;
        this.ResetHover();
        this._ratio = this.EditRatio();
        this.EnterOrLeaveEdit();
    }

    /// <summary>Closes a layer's edit view: the grid given back as it was, the layer selected with the look it got.</summary>
    public void EndLayerEdit()
    {
        if (this._edit is not { } edit)
        {
            return;
        }

        this.CancelEyedropper();
        this.EndDrag();
        this.EndLive();
        this.EndKeyPan();
        this._edit = null;
        this._images.Clear();
        this._images.AddRange(edit.Images);
        this._layout = edit.Layout;
        this._borders = edit.Borders;
        this._seams = edit.Seams;
        this._ratio = edit.Ratio;
        this._selected = -1;
        this._selectedLayer = edit.Layer < this._layers.Count ? edit.Layer : -1;
        this.ResetHover();
        this.EnterOrLeaveEdit();
    }

    /// <summary>The preview drawn and played anew for the grid it now shows, and the window told.</summary>
    private void EnterOrLeaveEdit()
    {
        this._cache?.Dispose();
        this._cache = null;
        this.FitPagesToCells();
        this.SyncPlayer();
        this.Invalidate();
        this.ContentVersion++;
        this.LayerEditChanged?.Invoke(this, EventArgs.Empty);
        this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The hover of a cell, its bars and its separators forgotten: the cells under the mouse changed.</summary>
    private void ResetHover()
    {
        this._hovered = -1;
        this._hoveringClose = false;
        this._hoveringHandle = false;
        this._hoveredBar = null;
        this._hoveredCorner = null;
        this._hoveringKept = false;
        this._hoveredSeparator = null;
        this._hoveringRotation = false;
        this._fittedName = null;
    }

    /// <summary>The edit view's canvas ratio: the edited layer's box, its content as shown.</summary>
    private double EditRatio()
    {
        var image = this._images.Count > 0 ? this._images[0] : null;
        var shown = image is null ? new Size(1, 1) : image.Look.Shown(image.Bitmap.Size);
        return Math.Clamp(shown.Width / (double)Math.Max(1, shown.Height), 0.05, 20);
    }

    /// <summary>In the edit view, the layer takes the look its cell was just given: the undo history and the exports read the layer.</summary>
    private void SyncEditedLayer(SourceImage image)
    {
        if (this._edit is { } edit && edit.Layer < this._layers.Count && this._layers.Items[edit.Layer] is var layer
            && layer.Image == image && layer.Look != image.Look)
        {
            this._layers = this._layers.With(edit.Layer, layer with { Look = image.Look });
        }
    }

    /// <summary>The frames of the shown layers' contents are decoded at the size of their boxes.</summary>
    private void UpdateLayerDisplaySizes()
    {
        var canvas = this.CanvasBounds();
        if (this.ShownLayers is not { } layers || canvas.IsEmpty)
        {
            return;
        }

        foreach (var layer in layers.Items)
        {
            var cell = Compositor.LayerCell(new LayerFrame(layer.FrameAt(TimeSpan.Zero), layer.Place), canvas.Size);
            if (cell.Width > 0 && cell.Height > 0)
            {
                this._player.SetDisplaySize(layer.Image, FrameDisplaySize(layer.Image, cell));
            }
        }
    }
}
