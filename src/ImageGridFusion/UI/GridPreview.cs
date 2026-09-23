using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Preview of the composed result, and the surface to select, remove and swap its images.
/// Owns the images it shows.
/// </summary>
internal sealed class GridPreview : Control
{
    private const float GhostScale = 0.4f;
    private const float GhostOpacity = 0.7f;
    private const int SelectionWidth = 3;
    private const int EmptyBorderWidth = 2;
    private const int HoverOutlineWidth = 1;

    private static readonly Color HoverOutlineColor = Color.FromArgb(128, Color.White);

    private readonly List<SourceImage> _images = [];
    private GridLayout? _layout;
    private Bitmap? _cache;
    private int _selected = -1;
    private int _hovered = -1;
    private bool _hoveringClose;
    private bool _hoveringCanvas;
    private int _pressed = -1;
    private Point _pressPoint;
    private bool _dragging;
    private int _dropTarget = -1;
    private Bitmap? _ghost;
    private Size _ghostOffset;
    private Point _dragPoint;
    private Rectangle _externalTarget;
    private bool _externalDrag;
    private Rectangle _externalHover;

    public GridPreview()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.FromArgb(64, 64, 64);
        ForeColor = Color.Gainsboro;
    }

    public event EventHandler? ImagesChanged;

    /// <summary>Raised when the active layout changes, picked by the user or reset with the image count.</summary>
    public event EventHandler? LayoutChanged;

    public IReadOnlyList<SourceImage> Images => _images;

    /// <summary>Layout the images are shown and exported with; <c>null</c> while there is no image.</summary>
    public GridLayout? ActiveLayout => _layout;

    public int FreeSlots => GridLayout.MaxImages - _images.Count;

    public bool HasSelection => _selected >= 0;

    /// <summary>
    /// Adds images: the first one replaces <paramref name="targetCell"/> when given (a drop onto a
    /// cell); the others fill the free slots; the first excess image replaces the selected cell, else
    /// the last one; any further excess is disposed. Returns the number of images ignored.
    /// </summary>
    public int Add(IReadOnlyList<SourceImage> images, int targetCell = -1)
    {
        int ignored = 0;
        bool excessReplaced = false;
        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            if (i == 0 && targetCell >= 0 && targetCell < _images.Count)
            {
                Replace(targetCell, image);
            }
            else if (_images.Count < GridLayout.MaxImages)
            {
                _images.Add(image);
            }
            else if (!excessReplaced)
            {
                Replace(ExcessTarget(), image);
                excessReplaced = true;
            }
            else
            {
                image.Dispose();
                ignored++;
            }
        }

        OnImagesChanged();
        return ignored;
    }

    /// <summary>
    /// Highlights what an external drag (files from Explorer) at <paramref name="location"/> would
    /// fill: the cell under it, else the whole canvas while there is room, else the cell the
    /// excess rule replaces. Outlines the cell or empty canvas under it. <c>null</c> clears both.
    /// </summary>
    public void ShowDropTarget(Point? location)
    {
        var target = location is { } point ? DropTargetBounds(point) : Rectangle.Empty;
        var hover = location is { } at ? SurfaceAt(at) : Rectangle.Empty;
        _externalDrag = location is not null;
        if (target != _externalTarget || hover != _externalHover)
        {
            _externalTarget = target;
            _externalHover = hover;
            Invalidate();
        }
    }

    /// <summary>Applies a layout for the current image count; the images keep their order.</summary>
    public void SetLayout(GridLayout layout)
    {
        if (layout.Count != _images.Count)
        {
            throw new ArgumentException($"Layout {layout.Id} holds {layout.Count} images, not {_images.Count}.", nameof(layout));
        }

        _layout = layout;
        _hovered = -1;
        _hoveringClose = false;
        _cache?.Dispose();
        _cache = null;
        Invalidate();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public int CellAt(Point location) => Array.FindIndex(CellBounds(), c => c.Contains(location));

    public void RemoveSelected()
    {
        if (_selected >= 0)
        {
            RemoveAt(_selected);
        }
    }

    public void ClearSelection()
    {
        if (_selected >= 0)
        {
            _selected = -1;
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _images.ForEach(i => i.Dispose());
            _images.Clear();
            _cache?.Dispose();
            _ghost?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var canvas = CanvasBounds();
        if (canvas.IsEmpty)
        {
            return;
        }

        using var highlight = new SolidBrush(Color.FromArgb(90, SystemColors.Highlight));
        if (_images.Count == 0)
        {
            PaintEmptyState(g, canvas);
            g.FillRectangle(highlight, _externalTarget);
            PaintHoverOutline(g, HoverOutlineBounds(canvas, []));
            return;
        }

        // Rendered at display size, and only again when the images or the size change.
        if (_cache is null || _cache.Size != canvas.Size)
        {
            _cache?.Dispose();
            _cache = new Bitmap(canvas.Width, canvas.Height);
            using var cacheGraphics = Graphics.FromImage(_cache);
            Compositor.Draw(cacheGraphics, _images, _layout!, canvas.Size);
        }

        g.DrawImageUnscaled(_cache, canvas.Location);

        var cells = CellBounds();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_dragging && _pressed < cells.Length)
        {
            using var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(dim, cells[_pressed]);
        }

        if (_dragging)
        {
            if (_dropTarget >= 0 && _dropTarget != _pressed && _dropTarget < cells.Length)
            {
                g.FillRectangle(highlight, cells[_dropTarget]);
            }
        }
        else
        {
            g.FillRectangle(highlight, _externalTarget);
        }

        if (_selected >= 0)
        {
            using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(SelectionWidth)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, cells[_selected]);
        }

        PaintHoverOutline(g, HoverOutlineBounds(canvas, cells));

        if (_hovered >= 0 && !_dragging)
        {
            PaintCloseButton(g, CloseBounds(cells[_hovered]), _hoveringClose);
        }

        if (_dragging && _ghost is not null)
        {
            PaintGhost(g, _ghost, GhostBounds());
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = CellAt(e.Location);
        if (index < 0)
        {
            ClearSelection();
            return;
        }

        if (CloseBounds(CellBounds()[index]).Contains(e.Location))
        {
            RemoveAt(index);
            UpdateHover(e.Location);
            return;
        }

        _selected = index;
        _pressed = index;
        _pressPoint = e.Location;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressed < 0 || e.Button != MouseButtons.Left)
        {
            UpdateHover(e.Location);
            return;
        }

        if (!_dragging)
        {
            var drag = SystemInformation.DragSize;
            var dead = new Rectangle(_pressPoint.X - drag.Width / 2, _pressPoint.Y - drag.Height / 2, drag.Width, drag.Height);
            if (dead.Contains(e.Location))
            {
                return;
            }

            StartDrag(e.Location);
            return;
        }

        Invalidate(GhostBounds());
        _dragPoint = e.Location;
        Invalidate(GhostBounds());

        int target = CellAt(e.Location);
        if (target != _dropTarget)
        {
            _dropTarget = target;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging && _dropTarget >= 0 && _dropTarget != _pressed)
        {
            Swap(_pressed, _dropTarget);
        }

        EndDrag();
        _hovered = -1;
        UpdateHover(e.Location);
    }

    // The capture is released right after OnMouseUp; losing it earlier (Alt+Tab, a dialog) cancels the drag.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_pressed >= 0)
        {
            EndDrag();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if ((_hovered >= 0 || _hoveringCanvas) && !_dragging)
        {
            _hovered = -1;
            _hoveringClose = false;
            _hoveringCanvas = false;
            Invalidate();
        }
    }

    /// <summary>
    /// Takes the ghost from the source cell as the preview shows it, scaled down, and keeps the
    /// grab point at the same relative place inside it.
    /// </summary>
    private void StartDrag(Point location)
    {
        var cells = CellBounds();
        if (_pressed >= cells.Length)
        {
            EndDrag();
            return;
        }

        _dragging = true;
        _dragPoint = location;
        _dropTarget = CellAt(location);
        Cursor = Cursors.SizeAll;

        var canvas = CanvasBounds();
        var cell = cells[_pressed];
        _ghostOffset = new Size(
            (int)((_pressPoint.X - cell.X) * GhostScale),
            (int)((_pressPoint.Y - cell.Y) * GhostScale));
        if (_cache is not null)
        {
            var source = cell with { X = cell.X - canvas.X, Y = cell.Y - canvas.Y };
            _ghost = new Bitmap(Math.Max(1, (int)(cell.Width * GhostScale)), Math.Max(1, (int)(cell.Height * GhostScale)));
            using var ghostGraphics = Graphics.FromImage(_ghost);
            ghostGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            ghostGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            ghostGraphics.DrawImage(_cache, new Rectangle(Point.Empty, _ghost.Size), source, GraphicsUnit.Pixel);
        }

        Invalidate();
    }

    private void EndDrag()
    {
        _pressed = -1;
        _dragging = false;
        _dropTarget = -1;
        _ghost?.Dispose();
        _ghost = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    /// <summary>Cell the first excess image replaces when the grid is full: the selected one, else the last.</summary>
    private int ExcessTarget() => _selected >= 0 ? _selected : _images.Count - 1;

    private Rectangle DropTargetBounds(Point location)
    {
        var cells = CellBounds();
        int cell = Array.FindIndex(cells, c => c.Contains(location));
        if (cell >= 0)
        {
            return cells[cell];
        }

        return _images.Count < GridLayout.MaxImages ? CanvasBounds() : cells[ExcessTarget()];
    }

    /// <summary>Surface under <paramref name="location"/>: its cell, else the empty canvas, else none.</summary>
    private Rectangle SurfaceAt(Point location)
    {
        if (_images.Count > 0)
        {
            return Array.Find(CellBounds(), c => c.Contains(location));
        }

        var canvas = CanvasBounds();
        return canvas.Contains(location) ? canvas : Rectangle.Empty;
    }

    /// <summary>
    /// Surface the hover outline marks: the one under an external drag, else the empty canvas under
    /// the mouse, else the swap target during a drag, else the hovered cell. Kept inside the border
    /// already drawn there (the dashed empty state, or the selection) so both stay visible.
    /// </summary>
    private Rectangle HoverOutlineBounds(Rectangle canvas, Rectangle[] cells)
    {
        Rectangle CellOrNone(int index) => index >= 0 && index < cells.Length ? cells[index] : Rectangle.Empty;

        var bounds = _externalDrag ? _externalHover
            : _images.Count == 0 ? (_hoveringCanvas ? canvas : Rectangle.Empty)
            : _dragging ? CellOrNone(_dropTarget)
            : CellOrNone(_hovered);
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        int border = _images.Count == 0 ? EmptyBorderWidth
            : bounds == CellOrNone(_selected) ? SelectionWidth
            : 0;
        int inset = LogicalToDeviceUnits(border);
        return Rectangle.Inflate(bounds, -inset, -inset);
    }

    private Rectangle GhostBounds() =>
        _ghost is null ? Rectangle.Empty : new Rectangle(_dragPoint - _ghostOffset, _ghost.Size);

    private void RemoveAt(int index)
    {
        _images[index].Dispose();
        _images.RemoveAt(index);
        _selected = -1;
        _hovered = -1;
        _hoveringClose = false;
        OnImagesChanged();
    }

    private void Replace(int index, SourceImage image)
    {
        _images[index].Dispose();
        _images[index] = image;
    }

    private void Swap(int a, int b)
    {
        (_images[a], _images[b]) = (_images[b], _images[a]);
        _selected = b;
        OnImagesChanged();
    }

    private void OnImagesChanged()
    {
        _cache?.Dispose();
        _cache = null;
        Invalidate();

        // A new image count starts on its default layout, mirror off.
        int? count = _images.Count == 0 ? null : _images.Count;
        bool layoutReset = _layout?.Count != count;
        if (layoutReset)
        {
            _layout = count is { } n ? GridLayout.Default(n) : null;
        }

        ImagesChanged?.Invoke(this, EventArgs.Empty);
        if (layoutReset)
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateHover(Point location)
    {
        int hovered = CellAt(location);
        bool onClose = hovered >= 0 && CloseBounds(CellBounds()[hovered]).Contains(location);
        bool onCanvas = _images.Count == 0 && CanvasBounds().Contains(location);
        if (hovered == _hovered && onClose == _hoveringClose && onCanvas == _hoveringCanvas)
        {
            return;
        }

        _hovered = hovered;
        _hoveringClose = onClose;
        _hoveringCanvas = onCanvas;
        Cursor = onClose ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    /// <summary>Largest rectangle at the output ratio that fits the control, centered.</summary>
    private Rectangle CanvasBounds()
    {
        int margin = LogicalToDeviceUnits(16);
        var area = Rectangle.Inflate(ClientRectangle, -margin, -margin);
        if (area.Width <= 0 || area.Height <= 0)
        {
            return Rectangle.Empty;
        }

        int width = area.Width;
        int height = GridLayout.HeightFor(width);
        if (height > area.Height)
        {
            height = area.Height;
            width = (int)(height * (double)GridLayout.RatioWidth / GridLayout.RatioHeight);
        }

        return new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }

    private Rectangle[] CellBounds()
    {
        var canvas = CanvasBounds();
        if (_layout is null || canvas.IsEmpty)
        {
            return [];
        }

        var cells = _layout.Cells(canvas.Size);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].Offset(canvas.Location);
        }

        return cells;
    }

    private Rectangle CloseBounds(Rectangle cell)
    {
        int size = LogicalToDeviceUnits(24);
        int inset = LogicalToDeviceUnits(6);
        return new Rectangle(cell.Right - inset - size, cell.Y + inset, size, size);
    }

    private void PaintCloseButton(Graphics g, Rectangle bounds, bool hot)
    {
        using (var brush = new SolidBrush(Color.FromArgb(hot ? 230 : 150, 0, 0, 0)))
        {
            g.FillEllipse(brush, bounds);
        }

        int pad = bounds.Width * 3 / 10;
        using var pen = new Pen(Color.White, LogicalToDeviceUnits(2));
        g.DrawLine(pen, bounds.Left + pad, bounds.Top + pad, bounds.Right - pad, bounds.Bottom - pad);
        g.DrawLine(pen, bounds.Right - pad, bounds.Top + pad, bounds.Left + pad, bounds.Bottom - pad);
    }

    /// <summary>Contour drawn inside <paramref name="bounds"/> as four bands, crisp and evenly translucent.</summary>
    private void PaintHoverOutline(Graphics g, Rectangle bounds)
    {
        int width = LogicalToDeviceUnits(HoverOutlineWidth);
        if (bounds.Width <= 2 * width || bounds.Height <= 2 * width)
        {
            return;
        }

        using var brush = new SolidBrush(HoverOutlineColor);
        g.FillRectangles(
            brush,
            [
                new Rectangle(bounds.X, bounds.Y, bounds.Width, width),
                new Rectangle(bounds.X, bounds.Bottom - width, bounds.Width, width),
                new Rectangle(bounds.X, bounds.Y + width, width, bounds.Height - 2 * width),
                new Rectangle(bounds.Right - width, bounds.Y + width, width, bounds.Height - 2 * width),
            ]);
    }

    private static void PaintGhost(Graphics g, Bitmap ghost, Rectangle bounds)
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = GhostOpacity });
        g.DrawImage(ghost, bounds, 0, 0, ghost.Width, ghost.Height, GraphicsUnit.Pixel, attributes);
    }

    private void PaintEmptyState(Graphics g, Rectangle canvas)
    {
        using (var pen = new Pen(ForeColor, LogicalToDeviceUnits(EmptyBorderWidth)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(pen, canvas);
        }

        TextRenderer.DrawText(
            g,
            "Drop 1 to 4 images here, or paste them with Ctrl+V",
            Font,
            canvas,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}
