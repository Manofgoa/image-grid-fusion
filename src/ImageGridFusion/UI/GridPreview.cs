using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Preview of the composed result, and the surface to select, remove and swap its images.
/// Owns the images it shows.
/// </summary>
internal sealed class GridPreview : Control
{
    private readonly List<SourceImage> _images = [];
    private Bitmap? _cache;
    private int _selected = -1;
    private int _hovered = -1;
    private bool _hoveringClose;
    private int _pressed = -1;
    private Point _pressPoint;
    private bool _dragging;
    private int _dropTarget = -1;
    private int _externalTarget = -1;

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

    public IReadOnlyList<SourceImage> Images => _images;

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
                Replace(_selected >= 0 ? _selected : _images.Count - 1, image);
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

    /// <summary>Highlights the cell an external drag (files from Explorer) would replace; -1 clears it.</summary>
    public void ShowDropTarget(int index)
    {
        if (index != _externalTarget)
        {
            _externalTarget = index;
            Invalidate();
        }
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

        if (_images.Count == 0)
        {
            PaintEmptyState(g, canvas);
            return;
        }

        // Rendered at display size, and only again when the images or the size change.
        if (_cache is null || _cache.Size != canvas.Size)
        {
            _cache?.Dispose();
            _cache = new Bitmap(canvas.Width, canvas.Height);
            using var cacheGraphics = Graphics.FromImage(_cache);
            Compositor.Draw(cacheGraphics, _images, canvas.Size);
        }

        g.DrawImageUnscaled(_cache, canvas.Location);

        var cells = CellBounds();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int highlighted = _dragging && _dropTarget != _pressed ? _dropTarget : _externalTarget;
        if (highlighted >= 0 && highlighted < cells.Length)
        {
            using var brush = new SolidBrush(Color.FromArgb(90, SystemColors.Highlight));
            g.FillRectangle(brush, cells[highlighted]);
        }

        if (_selected >= 0)
        {
            using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(3)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, cells[_selected]);
        }

        if (_hovered >= 0 && !_dragging)
        {
            PaintCloseButton(g, CloseBounds(cells[_hovered]), _hoveringClose);
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

            _dragging = true;
            Cursor = Cursors.SizeAll;
        }

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

        _pressed = -1;
        _dragging = false;
        _dropTarget = -1;
        Cursor = Cursors.Default;
        _hovered = -1;
        UpdateHover(e.Location);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hovered >= 0 && !_dragging)
        {
            _hovered = -1;
            _hoveringClose = false;
            Invalidate();
        }
    }

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
        ImagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateHover(Point location)
    {
        int hovered = CellAt(location);
        bool onClose = hovered >= 0 && CloseBounds(CellBounds()[hovered]).Contains(location);
        if (hovered == _hovered && onClose == _hoveringClose)
        {
            return;
        }

        _hovered = hovered;
        _hoveringClose = onClose;
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
        if (_images.Count == 0 || canvas.IsEmpty)
        {
            return [];
        }

        var cells = GridLayout.Cells(_images.Count, canvas.Size);
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

    private void PaintEmptyState(Graphics g, Rectangle canvas)
    {
        using (var pen = new Pen(ForeColor, LogicalToDeviceUnits(2)) { DashStyle = DashStyle.Dash })
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
