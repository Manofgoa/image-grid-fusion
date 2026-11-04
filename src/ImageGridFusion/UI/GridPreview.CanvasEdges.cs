using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The canvas's edges and corners dragged to change the output format's shape
/// (workfiles/20261104-output-format-edge-drag.md): only while the Format tab of the Global toolbar is
/// selected, its grips drawn meanwhile. A vertical edge changes the width, a horizontal one the height, a
/// corner both, Ctrl moving the opposite edge too; the opposite edge stays put during the drag, the canvas
/// fitted and centered again on release. The form turns each step into the Custom format's fields.
/// </summary>
internal sealed partial class GridPreview
{
    /// <summary>How far from an edge, on both of its sides, a press grabs it, in logical px.</summary>
    private const int EdgeReach = 6;

    /// <summary>The drag rectangle's smallest side, in logical px, whatever the bounds of the Custom fields.</summary>
    private const int MinEdgeDragSide = 24;

    /// <summary>The live readout's text size, in logical px: large, at the canvas's center.</summary>
    private const int EdgeReadoutTextSize = 28;

    private bool _formatActive;
    private CanvasEdge _hoveredEdges;
    private EdgeDrag? _edgeDrag;

    /// <summary>The canvas's sides an edge grab moves: one for an edge, two for a corner.</summary>
    [Flags]
    private enum CanvasEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
    }

    /// <summary>
    /// A drag of the canvas's edges: the sides moved, the canvas at the press, the cursor's distance to the
    /// grabbed sides, the Custom fields' units per device pixel and the canvas's size in them at the press,
    /// and the rectangle the canvas is drawn in meanwhile.
    /// </summary>
    private sealed class EdgeDrag(CanvasEdge edges, Rectangle start, Size grab, double scale, Size pixels)
    {
        public CanvasEdge Edges { get; } = edges;

        public Rectangle Start { get; } = start;

        public Size Grab { get; } = grab;

        public double Scale { get; } = scale;

        public Size Pixels { get; } = pixels;

        public Rectangle Shown { get; set; } = start;

        public Size LastPixels { get; set; } = pixels;

        public bool Moved { get; set; }
    }

    /// <summary>
    /// A drag of the canvas's edges begins: the handler gives the canvas's size in the Custom fields' units —
    /// the PNG export's size of the format in force — or leaves it <c>null</c>, and nothing is dragged.
    /// </summary>
    public event EventHandler<CanvasResizeStartingEventArgs>? CanvasResizeStarting;

    /// <summary>
    /// The canvas dragged to a new size, in the Custom fields' units: the handler puts the Custom format in
    /// force with it, brought within its bounds; the canvas is then drawn at what <see cref="Custom"/> holds.
    /// </summary>
    public event EventHandler<Size>? CanvasResized;

    /// <summary>Whether the Format tab of the Global toolbar is selected: only then do the canvas's edges take the mouse.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FormatActive
    {
        get => this._formatActive;
        set
        {
            if (value == this._formatActive)
            {
                return;
            }

            this._formatActive = value;
            if (!value)
            {
                this.EndEdgeDrag();
                this._hoveredEdges = CanvasEdge.None;
            }

            this.Invalidate();
        }
    }

    /// <summary>Whether the canvas's edges take the mouse now: the Format tab selected, no export, no layer edited, no picker armed.</summary>
    private bool EdgesTakeMouse =>
        this._formatActive && !this._locked && this._edit is null && this._eyedropper is null && !this._cutoutPick;

    /// <summary>The control less its margins and the drop zone: where the canvas is laid out.</summary>
    private Rectangle CanvasArea()
    {
        int margin = this.LogicalToDeviceUnits(CanvasMargin);
        var area = Rectangle.Inflate(this.ClientRectangle, -margin, -margin);
        area.Width -= this.LogicalToDeviceUnits(DropZoneWidth) + margin;
        return area;
    }

    /// <summary>The canvas's sides within reach of <paramref name="location"/>: two at a corner, none away from the edges.</summary>
    private CanvasEdge EdgesAt(Point location)
    {
        var canvas = this.CanvasBounds();
        int reach = this.LogicalToDeviceUnits(EdgeReach);
        if (canvas.IsEmpty || !Rectangle.Inflate(canvas, reach, reach).Contains(location))
        {
            return CanvasEdge.None;
        }

        var edges = CanvasEdge.None;
        if (Math.Abs(location.X - canvas.Left) <= reach)
        {
            edges |= CanvasEdge.Left;
        }
        else if (Math.Abs(location.X - canvas.Right) <= reach)
        {
            edges |= CanvasEdge.Right;
        }

        if (Math.Abs(location.Y - canvas.Top) <= reach)
        {
            edges |= CanvasEdge.Top;
        }
        else if (Math.Abs(location.Y - canvas.Bottom) <= reach)
        {
            edges |= CanvasEdge.Bottom;
        }

        return edges;
    }

    private static Cursor EdgeCursor(CanvasEdge edges)
    {
        bool horizontal = (edges & (CanvasEdge.Left | CanvasEdge.Right)) != 0;
        bool vertical = (edges & (CanvasEdge.Top | CanvasEdge.Bottom)) != 0;
        if (horizontal && vertical)
        {
            return edges.HasFlag(CanvasEdge.Left) == edges.HasFlag(CanvasEdge.Top) ? Cursors.SizeNWSE : Cursors.SizeNESW;
        }

        return horizontal ? Cursors.SizeWE : Cursors.SizeNS;
    }

    /// <summary>A press on the canvas's edges, while they take the mouse, starts dragging them. Whether it took the press.</summary>
    private bool EdgeMouseDown(MouseEventArgs e)
    {
        if (!this.EdgesTakeMouse || this.EdgesAt(e.Location) is not (var edges and not CanvasEdge.None))
        {
            return false;
        }

        var canvas = this.CanvasBounds();
        var args = new CanvasResizeStartingEventArgs(canvas.Size);
        this.CanvasResizeStarting?.Invoke(this, args);
        if (args.Pixels is not { Width: > 0, Height: > 0 } pixels)
        {
            return true;
        }

        var grab = new Size(
            edges.HasFlag(CanvasEdge.Left) ? canvas.Left - e.X : edges.HasFlag(CanvasEdge.Right) ? canvas.Right - e.X : 0,
            edges.HasFlag(CanvasEdge.Top) ? canvas.Top - e.Y : edges.HasFlag(CanvasEdge.Bottom) ? canvas.Bottom - e.Y : 0);
        this._edgeDrag = new EdgeDrag(edges, canvas, grab, pixels.Width / (double)canvas.Width, pixels);
        this._hoveredEdges = edges;
        this.Cursor = EdgeCursor(edges);
        this.Invalidate();
        return true;
    }

    /// <summary>
    /// A move: drags the canvas's edges while they are pressed, else shows the resize cursor over them. Whether
    /// it took the move — off the edges, the cells' hover takes it.
    /// </summary>
    private bool EdgeMouseMove(MouseEventArgs e)
    {
        if (this._edgeDrag is { } drag)
        {
            this.DragEdges(drag, e.Location);
            return true;
        }

        var edges = e.Button == MouseButtons.None && this.EdgesTakeMouse ? this.EdgesAt(e.Location) : CanvasEdge.None;
        if (edges == this._hoveredEdges)
        {
            return edges != CanvasEdge.None;
        }

        bool left = edges == CanvasEdge.None;
        this._hoveredEdges = edges;
        this.Invalidate();
        if (left)
        {
            // The cells' hover sets the cursor again, whatever it held before the edges took it.
            this._hovered = int.MinValue;
            return false;
        }

        this.Cursor = EdgeCursor(edges);
        return true;
    }

    /// <summary>
    /// The grabbed sides follow the cursor — the opposite ones still, or, Ctrl held, moving the other way so
    /// the center stays — within the working area; the form puts the size in force as the Custom fields, and
    /// the canvas is drawn at what they hold once brought within their bounds.
    /// </summary>
    private void DragEdges(EdgeDrag drag, Point location)
    {
        var start = drag.Start;
        var area = this.CanvasArea();
        bool symmetric = (ModifierKeys & Keys.Control) != 0;
        int min = this.LogicalToDeviceUnits(MinEdgeDragSide);
        int width = Span(drag.Edges.HasFlag(CanvasEdge.Left), drag.Edges.HasFlag(CanvasEdge.Right), start.Left, start.Right, location.X + drag.Grab.Width, area.Left, area.Right);
        int height = Span(drag.Edges.HasFlag(CanvasEdge.Top), drag.Edges.HasFlag(CanvasEdge.Bottom), start.Top, start.Bottom, location.Y + drag.Grab.Height, area.Top, area.Bottom);

        var pixels = new Size(
            width == start.Width ? drag.Pixels.Width : (int)Math.Round(width * drag.Scale),
            height == start.Height ? drag.Pixels.Height : (int)Math.Round(height * drag.Scale));
        if (pixels != drag.LastPixels)
        {
            drag.LastPixels = pixels;
            drag.Moved = true;
            this.CanvasResized?.Invoke(this, pixels);
        }

        // The canvas as the fields hold it, brought within their bounds: never a shape the format refuses.
        if (this._custom is { } custom && this._format == OutputFormat.Custom && drag.Moved)
        {
            if ((drag.Edges & (CanvasEdge.Left | CanvasEdge.Right)) != 0)
            {
                width = (int)Math.Round(custom.Width / drag.Scale);
            }

            if ((drag.Edges & (CanvasEdge.Top | CanvasEdge.Bottom)) != 0)
            {
                height = (int)Math.Round(custom.Height / drag.Scale);
            }
        }

        width = Math.Max(min, width);
        height = Math.Max(min, height);
        int x = Place(drag.Edges.HasFlag(CanvasEdge.Left), drag.Edges.HasFlag(CanvasEdge.Right), start.Left, start.Right, width);
        int y = Place(drag.Edges.HasFlag(CanvasEdge.Top), drag.Edges.HasFlag(CanvasEdge.Bottom), start.Top, start.Bottom, height);
        var shown = new Rectangle(x, y, width, height);
        if (shown != drag.Shown)
        {
            drag.Shown = shown;
            this._cache?.Dispose();
            this._cache = null;
            this.Invalidate();
        }

        // The extent of one axis: unchanged when neither of its sides is grabbed; from the still side to the
        // cursor; or, symmetric, twice the cursor's distance from the center — within the working area.
        int Span(bool low, bool high, int from, int to, int at, int areaFrom, int areaTo)
        {
            if (!low && !high)
            {
                return to - from;
            }

            if (symmetric)
            {
                double center = (from + to) / 2.0;
                double half = Math.Min(Math.Abs(at - center), Math.Min(center - areaFrom, areaTo - center));
                return (int)Math.Round(2 * half);
            }

            return low ? to - Math.Max(at, areaFrom) : Math.Min(at, areaTo) - from;
        }

        // Where an axis starts once its extent is known: the still side kept, or the center with Ctrl.
        int Place(bool low, bool high, int from, int to, int extent)
        {
            if (!low && !high)
            {
                return from;
            }

            if (symmetric)
            {
                return (int)Math.Round((from + to - extent) / 2.0);
            }

            return low ? to - extent : from;
        }
    }

    /// <summary>
    /// The drag of the canvas's edges ends — released, the capture lost, the tab left: the canvas fitted and
    /// centered again at its new ratio, drawn in full. Whether one was running.
    /// </summary>
    private bool EndEdgeDrag()
    {
        if (this._edgeDrag is not { } drag)
        {
            return false;
        }

        this._edgeDrag = null;
        if (drag.Moved)
        {
            this._cache?.Dispose();
            this._cache = null;
        }

        this.UpdateRatio();
        this.Invalidate();
        return true;
    }

    /// <summary>
    /// The grips at the middle of each of the canvas's edges and at its corners, while its edges take the
    /// mouse — helper indicators, white while hovered or dragged — and, while a drag runs, the readout of
    /// the format it makes, at the canvas's center. Preview only.
    /// </summary>
    private void PaintCanvasEdges(Graphics g, Rectangle canvas)
    {
        if (!this.EdgesTakeMouse && this._edgeDrag is null)
        {
            return;
        }

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.None;
        var hot = this._edgeDrag?.Edges ?? this._hoveredEdges;
        int length = this.LogicalToDeviceUnits(BarGripLength);
        int width = this.LogicalToDeviceUnits(BarGripWidth);
        int midX = canvas.Left + canvas.Width / 2;
        int midY = canvas.Top + canvas.Height / 2;
        this.PaintEdgeGrip(g, new Rectangle(canvas.Left - width / 2, midY - length / 2, width, length), hot == CanvasEdge.Left);
        this.PaintEdgeGrip(g, new Rectangle(canvas.Right - width / 2, midY - length / 2, width, length), hot == CanvasEdge.Right);
        this.PaintEdgeGrip(g, new Rectangle(midX - length / 2, canvas.Top - width / 2, length, width), hot == CanvasEdge.Top);
        this.PaintEdgeGrip(g, new Rectangle(midX - length / 2, canvas.Bottom - width / 2, length, width), hot == CanvasEdge.Bottom);
        foreach (var corner in new[] { CanvasEdge.Left | CanvasEdge.Top, CanvasEdge.Right | CanvasEdge.Top, CanvasEdge.Left | CanvasEdge.Bottom, CanvasEdge.Right | CanvasEdge.Bottom })
        {
            int x = corner.HasFlag(CanvasEdge.Left) ? canvas.Left : canvas.Right;
            int y = corner.HasFlag(CanvasEdge.Top) ? canvas.Top : canvas.Bottom;
            this.PaintEdgeGrip(g, new Rectangle(x - width, y - width, 2 * width, 2 * width), hot == corner);
        }

        g.Restore(state);
        this.PaintEdgeReadout(g, canvas);
    }

    private void PaintEdgeGrip(Graphics g, Rectangle bounds, bool hot)
    {
        using var brush = new SolidBrush(hot ? Color.White : HelperColor);
        using var pen = new Pen(HelperHalo, this.LogicalToDeviceUnits(1));
        g.FillRectangle(brush, bounds);
        g.DrawRectangle(pen, bounds);
    }

    /// <summary>The format the drag makes, in large type at the canvas's center: <c>1080 × 1350 px</c>, or the ratio <c>1.54:1</c>.</summary>
    private void PaintEdgeReadout(Graphics g, Rectangle canvas)
    {
        if (this._edgeDrag is not { Moved: true } || this._format != OutputFormat.Custom || this._custom is not { } custom)
        {
            return;
        }

        string text = custom.Exact
            ? $"{custom.Width} × {custom.Height} px"
            : string.Create(CultureInfo.InvariantCulture, $"{custom.Ratio:0.00}:1");
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var path = new GraphicsPath();
        path.AddString(text, this.Font.FontFamily, (int)FontStyle.Bold, this.LogicalToDeviceUnits(EdgeReadoutTextSize), new PointF(canvas.Left + canvas.Width / 2f, canvas.Top + canvas.Height / 2f), format);
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var outline = new Pen(HelperHalo, this.LogicalToDeviceUnits(5)) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(outline, path);
        }

        using (var fill = new SolidBrush(HelperColor))
        {
            g.FillPath(fill, path);
        }

        g.Restore(state);
    }
}

/// <summary>
/// A drag of the canvas's edges beginning: the canvas as shown, and the handler's answer — its size in the
/// Custom fields' units, <c>null</c> to refuse the drag.
/// </summary>
internal sealed class CanvasResizeStartingEventArgs(Size canvas) : EventArgs
{
    /// <summary>The canvas as the preview shows it at the press, in device pixels.</summary>
    public Size Canvas { get; } = canvas;

    /// <summary>The canvas's size in the Custom fields' units, given by the handler; <c>null</c> refuses the drag.</summary>
    public Size? Pixels { get; set; }
}
