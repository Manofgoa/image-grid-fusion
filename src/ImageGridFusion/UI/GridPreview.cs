using System.ComponentModel;
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
    private const int CanvasMargin = 16;
    private const int DropZoneWidth = 96;

    private const int ButtonSize = 24;
    private const int ButtonInset = 6;
    private const int ButtonGap = 4;
    private const int MinZoomSliderHeight = 80;
    private const int WheelNotch = 120;
    private const int NotchesPerDoubling = 4;
    private const int WheelEndDelay = 150;

    private static readonly Color HoverOutlineColor = Color.FromArgb(128, Color.White);

    /// <summary>Buttons of the hover toolbar, in their order along the top of the cell.</summary>
    private enum Tool
    {
        RotateLeft,
        RotateRight,
        FlipX,
        FlipY,
        Grayscale,
        Reset,
    }

    private readonly List<SourceImage> _images = [];
    private GridLayout? _layout;
    private double _cropThreshold = FitCalculator.DefaultCropThreshold;
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
    private bool _hoveringDropZone;
    private bool _pressedDropZone;
    private readonly PageLoader _pageLoader = new();
    private readonly AnimationPlayer _player = new();
    private int _sliding = -1;
    private bool _hoveringSlider;
    private bool _locked;
    private Tool? _hoveredTool;
    private bool _hoveringZoom;
    private int _zooming = -1;
    private bool _panning;
    private Point _panPoint;

    // The image panned or zoomed right now: drawn fast and painted at once, drawn again in full at the end.
    private SourceImage? _live;

    // The wheel has no release: its zoom ends once no notch came for a moment.
    private readonly System.Windows.Forms.Timer _wheelEnd = new() { Interval = WheelEndDelay };
    private int _wheelCell = -1;
    private int _wheelDelta;

    // Carousel: the step shown, 0 being the user's own arrangement, which the pause shows too.
    private readonly System.Windows.Forms.Timer _carouselTimer = new() { Interval = (int)Carousel.StepDuration.TotalMilliseconds };
    private bool _playsCarousel;
    private bool _carouselPaused;
    private int _carouselStep;

    public GridPreview()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.FromArgb(64, 64, 64);
        ForeColor = Color.Gainsboro;
        _pageLoader.PageShown += (_, _) =>
        {
            _cache?.Dispose();
            _cache = null;
            Invalidate();
        };
        _player.FrameShown += (_, image) => RedrawCell(image);
        _wheelEnd.Tick += (_, _) => EndLive();
        _carouselTimer.Tick += (_, _) => OnCarouselTick();
    }

    public event EventHandler? ImagesChanged;

    /// <summary>Raised when the drop zone right of the canvas is clicked.</summary>
    public event EventHandler? DropZoneClicked;

    /// <summary>Raised when the active layout changes, picked by the user or reset with the image count.</summary>
    public event EventHandler? LayoutChanged;

    public IReadOnlyList<SourceImage> Images => _images;

    /// <summary>Layout the images are shown and exported with; <c>null</c> while there is no image.</summary>
    public GridLayout? ActiveLayout => _layout;

    /// <summary>Share of the overflowing axis that may be cropped, shown and exported with; see <see cref="FitCalculator"/>.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double CropThreshold
    {
        get => _cropThreshold;
        set
        {
            if (value != _cropThreshold)
            {
                _cropThreshold = value;
                _cache?.Dispose();
                _cache = null;
                Invalidate();
            }
        }
    }

    public int FreeSlots => GridLayout.MaxImages - _images.Count;

    public bool HasSelection => _selected >= 0;

    /// <summary>
    /// While an export runs: images can be neither removed nor swapped, and the drop zone is inert.
    /// Browsing pages, and the live animation, go on.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            _pressed = -1;
            EndDrag();
        }
    }

    /// <summary>
    /// "Force as image": no animation plays, nor its sound, each cell showing the page its slider
    /// selects — still browsed live. Released, they resume like a released hover.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ForceStill
    {
        get => _player.ForceStill;
        set
        {
            if (value == _player.ForceStill)
            {
                return;
            }

            _player.ForceStill = value;
            if (value)
            {
                // The last frame played sits between two pages, scaled down: the page itself replaces it.
                foreach (var image in _images.Where(i => i.IsAnimated))
                {
                    _pageLoader.Request(image, _pageLoader.Target(image));
                }
            }
        }
    }

    /// <summary>
    /// The carousel: every second, the images move one cell clockwise (see <see cref="Carousel"/>).
    /// While the mouse or a drag is over the preview, it pauses on the user's own arrangement, which
    /// stays fully editable; it starts again from there once the mouse leaves. Needs two images.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PlaysCarousel
    {
        get => _playsCarousel;
        set
        {
            if (value != _playsCarousel)
            {
                _playsCarousel = value;
                SyncCarousel();
            }
        }
    }

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
    /// fill: the drop zone or the cell under it, else the whole canvas while there is room, else the
    /// cell the excess rule replaces. Outlines the drop zone, cell or empty canvas under it.
    /// <c>null</c> clears both.
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
        _hoveringSlider = false;
        _hoveredTool = null;
        _hoveringZoom = false;
        _cache?.Dispose();
        _cache = null;
        FitPagesToCells();
        SyncCarousel();
        SyncPlayer();
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

    /// <summary>Removes every image at once, raising <see cref="ImagesChanged"/> a single time.</summary>
    public void Clear()
    {
        if (_images.Count == 0)
        {
            return;
        }

        _images.ForEach(i => i.Dispose());
        _images.Clear();
        _selected = -1;
        _hovered = -1;
        _hoveringClose = false;
        _hoveredTool = null;
        _hoveringZoom = false;
        _zooming = -1;
        EndDrag();
        OnImagesChanged();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wheelEnd.Dispose();
            _carouselTimer.Dispose();
            _player.Dispose();
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
        PaintDropZone(g, DropZoneBounds(canvas));
        if (_images.Count == 0)
        {
            PaintEmptyState(g, canvas);
            g.FillRectangle(highlight, _externalTarget);
            PaintHoverOutline(g, HoverOutlineBounds(canvas, []));
            return;
        }

        // Rendered at display size, and only again when the images, the layout, the threshold, the size or the carousel step change.
        if (_cache is null || _cache.Size != canvas.Size)
        {
            _cache?.Dispose();
            _cache = new Bitmap(canvas.Width, canvas.Height);
            using var cacheGraphics = Graphics.FromImage(_cache);
            IReadOnlyList<SourceImage> shown = _carouselStep == 0 ? _images : Carousel.Arrange(_images, _layout!, _carouselStep);
            Compositor.Draw(cacheGraphics, shown, _layout!, canvas.Size, _cropThreshold);
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
            g.DrawRectangle(pen, cells[ShownCell(_selected)]);
        }

        PaintHoverOutline(g, HoverOutlineBounds(canvas, cells));

        if (_hovered >= 0 && !_dragging && !_locked)
        {
            PaintCloseButton(g, CloseBounds(cells[_hovered]), _hoveringClose);
            PaintToolbar(g, cells[_hovered], _images[_hovered]);
        }

        int zoom = _zooming >= 0 ? _zooming : _hovered;
        if (zoom >= 0 && zoom < cells.Length && !_dragging && !_locked)
        {
            PaintZoomSlider(g, _images[zoom], ZoomSliderBounds(cells[zoom], _images[zoom]));
        }

        int slider = _sliding >= 0 ? _sliding : _hovered;
        if (slider >= 0 && slider < cells.Length && !_dragging)
        {
            PaintSlider(g, _images[slider], SliderBounds(cells[slider], _images[slider]));
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

        // The selection is kept: a full grid replaces the selected cell with the first added image.
        if (_locked && !IsOnSlider(e.Location))
        {
            return;
        }

        if (DropZoneBounds(CanvasBounds()).Contains(e.Location))
        {
            _pressedDropZone = true;
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

        // Browsing pages, and acting on the image, neither select the cell nor start a swap.
        if (SliderBounds(CellBounds()[index], _images[index]).Contains(e.Location))
        {
            _sliding = index;
            UpdateHold();
            SlideTo(e.X);
            return;
        }

        if (ToolAt(CellBounds()[index], _images[index], e.Location) is { } tool)
        {
            Apply(index, tool);
            UpdateHover(e.Location);
            return;
        }

        if (ZoomSliderBounds(CellBounds()[index], _images[index]).Contains(e.Location))
        {
            _zooming = index;
            UpdateHold();
            BeginLive(index);
            ZoomTo(e.Y);
            return;
        }

        // A zoomed-in image is dragged within its cell; Ctrl + drag swaps it, as any other.
        _selected = index;
        _pressed = index;
        _pressPoint = e.Location;
        _panning = _images[index].Look.Zoom > 1 && (ModifierKeys & Keys.Control) == 0;
        _panPoint = e.Location;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        PauseCarousel();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PauseCarousel();
        if (_sliding >= 0)
        {
            SlideTo(e.X);
            return;
        }

        if (_zooming >= 0)
        {
            ZoomTo(e.Y);
            return;
        }

        if (_pressed < 0 || e.Button != MouseButtons.Left)
        {
            UpdateHover(e.Location);
            return;
        }

        if (_panning)
        {
            Cursor = Cursors.SizeAll;
            BeginLive(_pressed);
            PanBy(_pressed, new Size(e.X - _panPoint.X, e.Y - _panPoint.Y));
            _panPoint = e.Location;
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
        if (_sliding >= 0)
        {
            _sliding = -1;
            UpdateHover(e.Location);
            Invalidate();
            return;
        }

        if (_zooming >= 0)
        {
            _zooming = -1;
            EndLive();
            UpdateHover(e.Location);
            UpdateHold();
            Invalidate();
            return;
        }

        if (_pressedDropZone)
        {
            _pressedDropZone = false;
            if (DropZoneBounds(CanvasBounds()).Contains(e.Location))
            {
                DropZoneClicked?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (_dragging && _dropTarget >= 0 && _dropTarget != _pressed)
        {
            Swap(_pressed, _dropTarget);
        }

        EndDrag();
        _hovered = -1;
        UpdateHover(e.Location);
    }

    /// <summary>The wheel zooms the cell under the mouse, around the point under it; not while another gesture runs.</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int index = CellAt(e.Location);
        if (index < 0 || _locked || _pressed >= 0 || _sliding >= 0 || _zooming >= 0)
        {
            return;
        }

        // Fine-grained wheels send parts of a notch; they add up, per cell.
        if (index != _wheelCell)
        {
            _wheelCell = index;
            _wheelDelta = 0;
        }

        _wheelDelta += e.Delta;
        int notches = _wheelDelta / WheelNotch;
        _wheelDelta -= notches * WheelNotch;
        if (notches == 0)
        {
            return;
        }

        BeginLive(index);
        ZoomAt(index, e.Location, notches);
        _wheelEnd.Start();
    }

    // The capture is released right after OnMouseUp; losing it earlier (Alt+Tab, a dialog) cancels the drag.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_pressed >= 0)
        {
            EndDrag();
        }

        if (_sliding >= 0 || _zooming >= 0)
        {
            _sliding = -1;
            _zooming = -1;
            EndLive();
            UpdateHold();
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if ((_hovered >= 0 || _hoveringCanvas || _hoveringDropZone) && !_dragging && _sliding < 0 && _zooming < 0 && !_panning)
        {
            _hovered = -1;
            _hoveringClose = false;
            _hoveringSlider = false;
            _hoveredTool = null;
            _hoveringZoom = false;
            _hoveringCanvas = false;
            _hoveringDropZone = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        UpdateHold();
        ResumeCarousel();
    }

    // Files dragged from Explorer bring no mouse message: they pause the carousel on their own.
    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        base.OnDragEnter(drgevent);
        PauseCarousel();
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        ResumeCarousel();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateDisplaySizes();
    }

    /// <summary>
    /// The window hidden in the tray stops the animations and the sound; shown again, they play from
    /// the start. A minimized window stays visible and plays on.
    /// </summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            SyncPlayer();
        }
        else
        {
            _player.Stop();
        }

        SyncCarousel();
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
        _panning = false;
        EndLive();
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
        var zone = DropZoneBounds(CanvasBounds());
        if (zone.Contains(location))
        {
            return zone;
        }

        var cells = CellBounds();
        int cell = Array.FindIndex(cells, c => c.Contains(location));
        if (cell >= 0)
        {
            return cells[cell];
        }

        return _images.Count < GridLayout.MaxImages ? CanvasBounds() : cells[ExcessTarget()];
    }

    /// <summary>Surface under <paramref name="location"/>: the drop zone, its cell, else the empty canvas, else none.</summary>
    private Rectangle SurfaceAt(Point location)
    {
        var zone = DropZoneBounds(CanvasBounds());
        if (zone.Contains(location))
        {
            return zone;
        }

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

        int border = _images.Count == 0 || bounds == DropZoneBounds(canvas) ? EmptyBorderWidth
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
        _hoveringSlider = false;
        _hoveredTool = null;
        _hoveringZoom = false;
        _sliding = -1;
        _zooming = -1;
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

        FitPagesToCells();
        SyncCarousel();
        SyncPlayer();

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
        bool onDropZone = DropZoneBounds(CanvasBounds()).Contains(location);
        bool onSlider = hovered >= 0 && SliderBounds(CellBounds()[hovered], _images[hovered]).Contains(location);
        bool actions = hovered >= 0 && !_locked;
        var onTool = actions ? ToolAt(CellBounds()[hovered], _images[hovered], location) : null;
        bool onZoom = actions && ZoomSliderBounds(CellBounds()[hovered], _images[hovered]).Contains(location);
        if (hovered == _hovered && onClose == _hoveringClose && onCanvas == _hoveringCanvas && onDropZone == _hoveringDropZone
            && onSlider == _hoveringSlider && onTool == _hoveredTool && onZoom == _hoveringZoom)
        {
            return;
        }

        _hovered = hovered;
        _hoveringClose = onClose;
        _hoveringCanvas = onCanvas;
        _hoveringDropZone = onDropZone;
        _hoveringSlider = onSlider;
        _hoveredTool = onTool;
        _hoveringZoom = onZoom;
        Cursor = onClose || onDropZone || onSlider || onTool is not null || onZoom ? Cursors.Hand : Cursors.Default;
        UpdateHold();
        Invalidate();
    }

    /// <summary>The hovered cell, or the one whose slider is dragged, holds its animation still.</summary>
    private void UpdateHold()
    {
        int held = _sliding >= 0 ? _sliding : _zooming >= 0 ? _zooming : _hovered;
        _player.Hold(held >= 0 && held < _images.Count ? _images[held] : null);
    }

    private bool IsOnSlider(Point location)
    {
        int index = CellAt(location);
        return index >= 0 && SliderBounds(CellBounds()[index], _images[index]).Contains(location);
    }

    /// <summary>
    /// Plays the animated images, at the size of their cells, and holds the hovered one. Nothing plays
    /// behind a hidden window: showing it syncs again.
    /// </summary>
    private void SyncPlayer()
    {
        if (!Visible)
        {
            return;
        }

        _player.Sync(_images);
        UpdateDisplaySizes();
        UpdateHold();
    }

    private void UpdateDisplaySizes()
    {
        var cells = CellBounds();
        for (int i = 0; i < cells.Length && i < _images.Count; i++)
        {
            _player.SetDisplaySize(_images[i], FrameDisplaySize(_images[i], cells[ShownCell(i)]));
        }
    }

    /// <summary>Cell image <paramref name="index"/> is shown in: its own, unless the carousel moved it.</summary>
    private int ShownCell(int index) =>
        _carouselStep == 0 || _layout is null ? index : Carousel.CellOf(index, _layout, _carouselStep);

    /// <summary>
    /// Starts or stops the carousel after a change of mode, images, layout or visibility: always back to
    /// the user's own arrangement, the next step a full second away. Nothing plays behind a hidden window.
    /// </summary>
    private void SyncCarousel()
    {
        _carouselTimer.Stop();
        ShowCarouselStep(0);
        if (_playsCarousel && Visible && Carousel.CanPlay(_images.Count))
        {
            _carouselPaused = IsPointerOver();
            _carouselTimer.Start();
        }
    }

    /// <summary>The mouse, or a drag, came over the preview: the user's own arrangement is shown, to edit.</summary>
    private void PauseCarousel()
    {
        if (_carouselTimer.Enabled && !_carouselPaused)
        {
            _carouselPaused = true;
            ShowCarouselStep(0);
        }
    }

    /// <summary>The mouse left: the arrangement stays a full second, then the carousel moves on.</summary>
    private void ResumeCarousel()
    {
        if (_carouselTimer.Enabled && _carouselPaused && !IsPointerOver())
        {
            _carouselPaused = false;
            _carouselTimer.Stop();
            _carouselTimer.Start();
        }
    }

    /// <summary>
    /// Moves on a step, unless the pointer is over the preview. Checked on every tick too, since a
    /// drop from Explorer may leave the pointer there, or gone, without any mouse message.
    /// </summary>
    private void OnCarouselTick()
    {
        if (IsPointerOver())
        {
            PauseCarousel();
            return;
        }

        if (_carouselPaused)
        {
            _carouselPaused = false;
            return;
        }

        ShowCarouselStep((_carouselStep + 1) % _images.Count);
    }

    private bool IsPointerOver() => Capture || ClientRectangle.Contains(PointToClient(MousePosition));

    private void ShowCarouselStep(int step)
    {
        if (step == _carouselStep)
        {
            return;
        }

        _carouselStep = step;
        _cache?.Dispose();
        _cache = null;
        UpdateDisplaySizes();
        Invalidate();
    }

    /// <summary>
    /// Size an animated frame is decoded at to be drawn about 1:1: the cell, in the frame's own
    /// orientation, enlarged by a zoom in.
    /// </summary>
    private static Size FrameDisplaySize(SourceImage image, Rectangle cell)
    {
        var size = image.Look.Oriented(cell.Size);
        double zoom = Math.Max(1, image.Look.Zoom);
        return new Size((int)Math.Ceiling(size.Width * zoom), (int)Math.Ceiling(size.Height * zoom));
    }

    /// <summary>
    /// Starts showing the look of an image live, while a gesture changes it: the cell is drawn fast,
    /// and painted before the next mouse message, which would otherwise hold the paint back.
    /// </summary>
    private void BeginLive(int index)
    {
        _wheelEnd.Stop();
        var image = _images[index];
        if (image != _live)
        {
            EndLive();
            _live = image;
        }
    }

    /// <summary>The gesture is over: the frames are decoded at the new zoom, and the cell drawn in full.</summary>
    private void EndLive()
    {
        _wheelEnd.Stop();
        if (_live is not { } image)
        {
            return;
        }

        _live = null;
        UpdateDisplaySizes();
        RedrawCell(image);
    }

    /// <summary>Draws the new frame of an image into the cached preview, and repaints only its cell.</summary>
    private void RedrawCell(SourceImage image)
    {
        int index = _images.IndexOf(image);
        var canvas = CanvasBounds();
        if (index < 0 || _layout is null || _cache is null || _cache.Size != canvas.Size)
        {
            Invalidate();
            return;
        }

        bool live = image == _live;
        var cell = _layout.Cells(canvas.Size)[ShownCell(index)];
        using (var g = Graphics.FromImage(_cache))
        {
            Compositor.DrawCell(g, new Frame(image.Bitmap, image.BandColor, image.Look), cell, _cropThreshold, fast: live);
        }

        cell.Offset(canvas.Location);
        Invalidate(cell);
        if (live)
        {
            Update();
        }
    }

    /// <summary>
    /// Largest rectangle at the output ratio that fits the control once the drop zone and its gap
    /// are reserved on the right, centered in what remains.
    /// </summary>
    private Rectangle CanvasBounds()
    {
        int margin = LogicalToDeviceUnits(CanvasMargin);
        var area = Rectangle.Inflate(ClientRectangle, -margin, -margin);
        area.Width -= LogicalToDeviceUnits(DropZoneWidth) + margin;
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

    /// <summary>Strip right of the canvas, as high as it, where dropped or picked files are added.</summary>
    private Rectangle DropZoneBounds(Rectangle canvas) =>
        canvas.IsEmpty
            ? Rectangle.Empty
            : new Rectangle(canvas.Right + LogicalToDeviceUnits(CanvasMargin), canvas.Y, LogicalToDeviceUnits(DropZoneWidth), canvas.Height);

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

    /// <summary>
    /// Pill along the bottom of a cell whose image has several pages; empty for a single page, or
    /// when the cell is too narrow to slide in.
    /// </summary>
    private Rectangle SliderBounds(Rectangle cell, SourceImage image)
    {
        if (image.Pages is not { Count: > 1 })
        {
            return Rectangle.Empty;
        }

        int height = LogicalToDeviceUnits(24);
        int inset = LogicalToDeviceUnits(6);
        var bounds = new Rectangle(cell.X + inset, cell.Bottom - inset - height, cell.Width - 2 * inset, height);
        return bounds.Width >= LogicalToDeviceUnits(120) ? bounds : Rectangle.Empty;
    }

    /// <summary>Horizontal span of the track, left of the label, which is sized for the widest one.</summary>
    private (int Left, int Width) SliderTrack(Rectangle bounds, SourceImage image)
    {
        var pages = image.Pages!;
        int pad = bounds.Height / 2;
        int label = TextRenderer.MeasureText(pages.Label(pages.Count - 1), Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        return (bounds.X + pad, Math.Max(1, bounds.Width - 3 * pad - label));
    }

    /// <summary>Requests the page under <paramref name="x"/> on the slider being dragged, rendered live.</summary>
    private void SlideTo(int x)
    {
        var cells = CellBounds();
        if (_sliding >= cells.Length)
        {
            return;
        }

        var image = _images[_sliding];
        var bounds = SliderBounds(cells[_sliding], image);
        if (bounds.IsEmpty)
        {
            return;
        }

        var (left, width) = SliderTrack(bounds, image);
        int page = (int)Math.Round(Math.Clamp((x - left) / (double)width, 0, 1) * (image.Pages!.Count - 1));
        if (page != _pageLoader.Target(image))
        {
            _pageLoader.Request(image, page);
            Invalidate(bounds);
        }
    }

    /// <summary>
    /// Text pages take the shape of their cell, at its size on a 1200 px canvas, so they never widen
    /// the canvas: laid out again, keeping the reading position, whenever that cell changes. A text
    /// turned a quarter takes the turned shape, so it still fills its cell once rotated.
    /// </summary>
    private void FitPagesToCells()
    {
        if (_layout is null)
        {
            return;
        }

        var cells = _layout.Cells(new Size(GridLayout.RatioWidth, GridLayout.RatioHeight));
        for (int i = 0; i < _images.Count; i++)
        {
            var shape = _images[i].Look.Oriented(cells[i].Size);
            if (_images[i].Pages is { PageSize: { } size } pages && size != shape)
            {
                _pageLoader.Request(_images[i], pages.Resize(shape, _pageLoader.Target(_images[i])));
                _player.Refresh(_images[i]);
            }
        }
    }

    /// <summary>
    /// Buttons along the top of the cell, left of the ×, wrapping onto more rows when the cell is too
    /// narrow. Reset comes last, and only while the image has an action to undo.
    /// </summary>
    private (Tool Tool, Rectangle Bounds)[] ToolBounds(Rectangle cell, SourceImage image)
    {
        var tools = Enum.GetValues<Tool>();
        var shown = image.Look.IsNone ? tools[..^1] : tools;
        return shown.Select(t => (t, ToolSlot(cell, (int)t))).ToArray();
    }

    /// <summary>Place of the <paramref name="index"/>-th button, whichever buttons are shown.</summary>
    private Rectangle ToolSlot(Rectangle cell, int index)
    {
        int size = LogicalToDeviceUnits(ButtonSize);
        int gap = LogicalToDeviceUnits(ButtonGap);
        int left = cell.X + LogicalToDeviceUnits(ButtonInset);
        int perRow = Math.Max(1, (CloseBounds(cell).Left - gap - left + gap) / (size + gap));
        return new Rectangle(
            left + index % perRow * (size + gap),
            cell.Y + LogicalToDeviceUnits(ButtonInset) + index / perRow * (size + gap),
            size,
            size);
    }

    private Tool? ToolAt(Rectangle cell, SourceImage image, Point location)
    {
        foreach (var (tool, bounds) in ToolBounds(cell, image))
        {
            if (bounds.Contains(location))
            {
                return tool;
            }
        }

        return null;
    }

    private void Apply(int index, Tool tool)
    {
        var look = _images[index].Look;
        SetLook(index, tool switch
        {
            Tool.RotateLeft => look.Rotate(-1),
            Tool.RotateRight => look.Rotate(1),
            Tool.FlipX => look.ToggleFlipX(),
            Tool.FlipY => look.ToggleFlipY(),
            Tool.Grayscale => look.ToggleGrayscale(),
            _ => ImageLook.None,
        });
    }

    /// <summary>Gives an image its new look, and redraws only its cell.</summary>
    private void SetLook(int index, ImageLook look)
    {
        var image = _images[index];
        if (look == image.Look)
        {
            return;
        }

        bool turned = look.SwapsAxes != image.Look.SwapsAxes;
        image.Look = look;
        if (turned)
        {
            FitPagesToCells();
        }

        // During a gesture, the frame shown is scaled; decoding it again at each step would only slow it down.
        if (image != _live)
        {
            UpdateDisplaySizes();
        }

        RedrawCell(image);
    }

    /// <summary>
    /// Vertical pill along the left edge of the cell, from below the button rows (as many as the
    /// full toolbar takes) down to the page slider or the bottom; empty when the cell is too short.
    /// </summary>
    private Rectangle ZoomSliderBounds(Rectangle cell, SourceImage image)
    {
        int gap = LogicalToDeviceUnits(ButtonGap);
        int top = ToolSlot(cell, (int)Tool.Reset).Bottom + gap;
        var pages = SliderBounds(cell, image);
        int bottom = pages.IsEmpty ? cell.Bottom - LogicalToDeviceUnits(ButtonInset) : pages.Top - gap;
        int left = cell.X + LogicalToDeviceUnits(ButtonInset);
        var bounds = Rectangle.FromLTRB(left, top, left + LogicalToDeviceUnits(ButtonSize), bottom);
        return bounds.Height >= LogicalToDeviceUnits(MinZoomSliderHeight) ? bounds : Rectangle.Empty;
    }

    /// <summary>Vertical span of the track; zoom goes up, on a log scale so that 50 % → 100 % and each doubling take the same length.</summary>
    private static (int Top, int Height) ZoomTrack(Rectangle bounds)
    {
        int pad = bounds.Width / 2;
        return (bounds.Y + pad, Math.Max(1, bounds.Height - 2 * pad));
    }

    private static double ZoomFraction(double zoom) =>
        (Math.Log2(zoom) - Math.Log2(ImageLook.MinZoom)) / (Math.Log2(ImageLook.MaxZoom) - Math.Log2(ImageLook.MinZoom));

    private static int ZoomY(Rectangle bounds, double zoom)
    {
        var (top, height) = ZoomTrack(bounds);
        return top + height - (int)Math.Round(height * ZoomFraction(zoom));
    }

    /// <summary>Sets the zoom under <paramref name="y"/> on the zoom slider being dragged; snaps to 100 % near its mark.</summary>
    private void ZoomTo(int y)
    {
        var cells = CellBounds();
        if (_zooming >= cells.Length)
        {
            return;
        }

        var bounds = ZoomSliderBounds(cells[_zooming], _images[_zooming]);
        if (bounds.IsEmpty)
        {
            return;
        }

        var (top, height) = ZoomTrack(bounds);
        double fraction = Math.Clamp((top + height - y) / (double)height, 0, 1);
        double zoom = Math.Abs(y - ZoomY(bounds, 1)) <= LogicalToDeviceUnits(4)
            ? 1
            : Math.Pow(2, Math.Log2(ImageLook.MinZoom) + fraction * (Math.Log2(ImageLook.MaxZoom) - Math.Log2(ImageLook.MinZoom)));
        SetLook(_zooming, _images[_zooming].Look.WithZoom(zoom));
    }

    /// <summary>
    /// Zooms by <paramref name="notches"/> of the wheel, <see cref="NotchesPerDoubling"/> of them
    /// doubling the zoom, keeping the point of the image under <paramref name="location"/> in place;
    /// lands on 100 % when crossing it.
    /// </summary>
    private void ZoomAt(int index, Point location, int notches)
    {
        var cells = CellBounds();
        if (index >= cells.Length)
        {
            return;
        }

        var image = _images[index];
        var look = image.Look;
        double zoom = look.Zoom * Math.Pow(2, notches / (double)NotchesPerDoubling);
        if ((look.Zoom - 1) * (zoom - 1) < 0)
        {
            zoom = 1;
        }

        var zoomed = look.WithZoom(zoom);
        if (zoomed.Zoom <= 1)
        {
            SetLook(index, zoomed);
            return;
        }

        // The point under the mouse, where it is actually shown, then the part that keeps it there.
        var size = look.Oriented(image.Bitmap.Size);
        var before = FitCalculator.Compute(cells[index], size, _cropThreshold, look.Zoom, look.Focus);
        var after = FitCalculator.Compute(cells[index], size, _cropThreshold, zoomed.Zoom, zoomed.Focus);
        double scaleBefore = before.Destination.Width / before.Source.Width;
        double scaleAfter = after.Destination.Width / after.Source.Width;
        double x = Math.Clamp(before.Source.X + (location.X - before.Destination.X) / scaleBefore, 0, size.Width);
        double y = Math.Clamp(before.Source.Y + (location.Y - before.Destination.Y) / scaleBefore, 0, size.Height);
        var focus = new PointF(
            (float)((x - (location.X - after.Destination.X) / scaleAfter + after.Source.Width / 2) / size.Width),
            (float)((y - (location.Y - after.Destination.Y) / scaleAfter + after.Source.Height / 2) / size.Height));
        SetLook(index, zoomed.WithFocus(focus));
    }

    /// <summary>Moves a zoomed-in image by <paramref name="delta"/> within its cell, from where it is actually shown.</summary>
    private void PanBy(int index, Size delta)
    {
        var cells = CellBounds();
        if (index >= cells.Length || delta.IsEmpty)
        {
            return;
        }

        var image = _images[index];
        var look = image.Look;
        var size = look.Oriented(image.Bitmap.Size);
        var fit = FitCalculator.Compute(cells[index], size, _cropThreshold, look.Zoom, look.Focus);
        double scale = fit.Destination.Width / fit.Source.Width;
        var focus = new PointF(
            (float)((fit.Source.X + fit.Source.Width / 2 - delta.Width / scale) / size.Width),
            (float)((fit.Source.Y + fit.Source.Height / 2 - delta.Height / scale) / size.Height));
        SetLook(index, look.WithFocus(focus));
    }

    private void PaintToolbar(Graphics g, Rectangle cell, SourceImage image)
    {
        foreach (var (tool, bounds) in ToolBounds(cell, image))
        {
            bool on = tool switch
            {
                Tool.FlipX => image.Look.FlipX,
                Tool.FlipY => image.Look.FlipY,
                Tool.Grayscale => image.Look.Grayscale,
                _ => false,
            };
            PaintToolButton(g, tool, bounds, _hoveredTool == tool, on);
        }
    }

    /// <summary>Round button like the ×, filled with the highlight color while its action is on.</summary>
    private void PaintToolButton(Graphics g, Tool tool, Rectangle bounds, bool hot, bool on)
    {
        var fill = on ? Color.FromArgb(hot ? 255 : 210, SystemColors.Highlight) : Color.FromArgb(hot ? 230 : 150, 0, 0, 0);
        using (var brush = new SolidBrush(fill))
        {
            g.FillEllipse(brush, bounds);
        }

        int pad = bounds.Width * 3 / 10;
        var glyph = Rectangle.Inflate(bounds, -pad, -pad);
        using var pen = new Pen(Color.White, LogicalToDeviceUnits(2));
        using var arrow = new AdjustableArrowCap(2.5f, 2.5f);
        switch (tool)
        {
            case Tool.RotateLeft:
                pen.CustomEndCap = arrow;
                g.DrawArc(pen, glyph, 0, -220);
                break;
            case Tool.RotateRight:
                pen.CustomEndCap = arrow;
                g.DrawArc(pen, glyph, 180, 220);
                break;
            case Tool.FlipX:
                PaintFlipGlyph(g, glyph, vertical: false);
                break;
            case Tool.FlipY:
                PaintFlipGlyph(g, glyph, vertical: true);
                break;
            case Tool.Grayscale:
                g.FillPie(Brushes.White, glyph, 90, 180);
                g.DrawEllipse(pen, glyph);
                break;
            case Tool.Reset:
                pen.CustomEndCap = arrow;
                g.DrawArc(pen, glyph, 90, -300);
                int dot = Math.Max(2, glyph.Width / 4);
                g.FillEllipse(Brushes.White, glyph.X + (glyph.Width - dot) / 2, glyph.Y + (glyph.Height - dot) / 2, dot, dot);
                break;
        }
    }

    /// <summary>A filled triangle and its outlined mirror image on each side of the flip axis.</summary>
    private void PaintFlipGlyph(Graphics g, Rectangle glyph, bool vertical)
    {
        float mid = glyph.Width / 2f;
        float gap = LogicalToDeviceUnits(2);
        PointF[] Mirror(PointF[] points) => points.Select(p => new PointF(glyph.Width - p.X, p.Y)).ToArray();
        PointF[] Place(PointF[] points) => points
            .Select(p => vertical ? new PointF(glyph.X + p.Y, glyph.Y + p.X) : new PointF(glyph.X + p.X, glyph.Y + p.Y))
            .ToArray();

        PointF[] left = [new(0, glyph.Height), new(mid - gap, 0), new(mid - gap, glyph.Height)];
        using var pen = new Pen(Color.White, LogicalToDeviceUnits(1));
        g.FillPolygon(Brushes.White, Place(left));
        g.DrawPolygon(pen, Place(Mirror(left)));
        var axis = Place([new(mid, 0), new(mid, glyph.Height)]);
        g.DrawLine(pen, axis[0], axis[1]);
    }

    /// <summary>Vertical pill with 100 % marked on its track; the percentage shows next to the thumb while hovered or dragged.</summary>
    private void PaintZoomSlider(Graphics g, SourceImage image, Rectangle bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        bool hot = _zooming >= 0 || _hoveringZoom;
        using (var brush = new SolidBrush(Color.FromArgb(hot ? 230 : 150, 0, 0, 0)))
        {
            g.FillRoundedRectangle(brush, bounds, new Size(bounds.Width, bounds.Width));
        }

        var (top, height) = ZoomTrack(bounds);
        int x = bounds.X + bounds.Width / 2;
        using (var pen = new Pen(Color.FromArgb(160, Color.White), LogicalToDeviceUnits(2)))
        {
            g.DrawLine(pen, x, top, x, top + height);
            int mark = ZoomY(bounds, 1);
            int half = bounds.Width / 4;
            g.DrawLine(pen, x - half, mark, x + half, mark);
        }

        int y = ZoomY(bounds, image.Look.Zoom);
        int thumb = bounds.Width - LogicalToDeviceUnits(10);
        g.FillEllipse(Brushes.White, x - thumb / 2, y - thumb / 2, thumb, thumb);

        if (hot)
        {
            string text = $"{image.Look.Zoom * 100:0} %";
            var size = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int pad = LogicalToDeviceUnits(ButtonGap);
            var label = new Rectangle(bounds.Right + pad, y - bounds.Width / 2, size.Width + bounds.Width / 2 * 2, bounds.Width);
            using (var brush = new SolidBrush(Color.FromArgb(230, 0, 0, 0)))
            {
                g.FillRoundedRectangle(brush, label, new Size(label.Height, label.Height));
            }

            TextRenderer.DrawText(g, text, Font, label, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private void PaintSlider(Graphics g, SourceImage image, Rectangle bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        bool hot = _sliding >= 0 || _hoveringSlider;
        using (var brush = new SolidBrush(Color.FromArgb(hot ? 230 : 150, 0, 0, 0)))
        {
            g.FillRoundedRectangle(brush, bounds, new Size(bounds.Height, bounds.Height));
        }

        var pages = image.Pages!;
        int page = Math.Clamp(_pageLoader.Target(image), 0, pages.Count - 1);
        var (left, width) = SliderTrack(bounds, image);
        int y = bounds.Y + bounds.Height / 2;
        using (var pen = new Pen(Color.FromArgb(160, Color.White), LogicalToDeviceUnits(2)))
        {
            g.DrawLine(pen, left, y, left + width, y);
        }

        int x = left + (int)Math.Round(width * (double)page / (pages.Count - 1));
        int thumb = bounds.Height - LogicalToDeviceUnits(10);
        g.FillEllipse(Brushes.White, x - thumb / 2, y - thumb / 2, thumb, thumb);

        var label = Rectangle.FromLTRB(left + width + bounds.Height / 2, bounds.Y, bounds.Right - bounds.Height / 2, bounds.Bottom);
        TextRenderer.DrawText(g, pages.Label(page), Font, label, Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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

    /// <summary>Dashed strip with a "+" above its label; brighter while the mouse is over it.</summary>
    private void PaintDropZone(Graphics g, Rectangle zone)
    {
        var color = _hoveringDropZone ? Color.White : ForeColor;
        using (var pen = new Pen(color, LogicalToDeviceUnits(EmptyBorderWidth)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(pen, zone);
        }

        int plus = Math.Min(zone.Width * 2 / 5, LogicalToDeviceUnits(32));
        int labelHeight = TextRenderer.MeasureText("Add images", Font).Height;
        int gap = LogicalToDeviceUnits(8);
        int top = zone.Y + (zone.Height - plus - gap - labelHeight) / 2;
        int centerX = zone.X + zone.Width / 2;
        using (var pen = new Pen(color, LogicalToDeviceUnits(3)))
        {
            g.DrawLine(pen, centerX, top, centerX, top + plus);
            g.DrawLine(pen, centerX - plus / 2, top + plus / 2, centerX + plus / 2, top + plus / 2);
        }

        TextRenderer.DrawText(
            g,
            "Add images",
            Font,
            new Rectangle(zone.X, top + plus + gap, zone.Width, labelHeight),
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
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
