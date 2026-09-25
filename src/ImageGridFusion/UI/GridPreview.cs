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
    private const int HandleSize = 48;
    private const int WheelNotch = 120;
    private const int NotchesPerDoubling = 4;
    private const int WheelEndDelay = 150;
    private const int BarReach = 12;
    private const int BarSnap = 6;
    private const int BarMinGap = 8;
    private const int BarGripLength = 32;
    private const int BarGripWidth = 8;
    private const int PanResistance = 24;

    private static readonly Color HoverOutlineColor = Color.FromArgb(128, Color.White);

    // Fluorescent green: the bars of the blur show on any image.
    private static readonly Color BarColor = Color.FromArgb(57, 255, 20);

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
    private bool _hoveringDropZone;
    private bool _pressedDropZone;
    private readonly PageLoader _pageLoader = new();
    private readonly AnimationPlayer _player = new();
    private bool _locked;
    private bool _hoveringHandle;
    private bool _panning;
    private Point _panPoint;
    private readonly PanMagnet _panX = new();
    private readonly PanMagnet _panY = new();
    private bool _showsBlurBars;
    private BlurSide? _hoveredBar;
    private BlurSide? _draggedBar;
    private int _barGrab;

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

    /// <summary>Raised when another cell is selected, or none, and when the selected image changes its look.</summary>
    public event EventHandler? SelectedImageChanged;

    /// <summary>Raised when the drop zone right of the canvas is clicked.</summary>
    public event EventHandler? DropZoneClicked;

    /// <summary>Raised when the active layout changes, picked by the user or reset with the image count.</summary>
    public event EventHandler? LayoutChanged;

    public IReadOnlyList<SourceImage> Images => _images;

    /// <summary>Layout the images are shown and exported with; <c>null</c> while there is no image.</summary>
    public GridLayout? ActiveLayout => _layout;

    public int FreeSlots => GridLayout.MaxImages - _images.Count;

    public bool HasSelection => _selected >= 0;

    /// <summary>The image of the selected cell, the one the effects toolbar acts on; <c>null</c> without a selection.</summary>
    public SourceImage? SelectedImage => _selected >= 0 && _selected < _images.Count ? _images[_selected] : null;

    /// <summary>
    /// Draws the bars of the blur on the selected cell, and lets them be dragged: set while the blur is
    /// the selected effect of the effects toolbar.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowsBlurBars
    {
        get => _showsBlurBars;
        set
        {
            if (value != _showsBlurBars)
            {
                _showsBlurBars = value;
                _hoveredBar = null;
                Invalidate();
            }
        }
    }

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
    /// "Force as image": no animation plays, nor its sound, each cell showing the page it stands on.
    /// Released, they play on from there, but the frozen ones.
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
        _hoveringHandle = false;
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

    public void ClearSelection() => Select(-1);

    /// <summary>Gives the selected image a new look: an effect toggled or tuned from the toolbars.</summary>
    public void SetSelectedLook(ImageLook look)
    {
        if (SelectedImage is not null)
        {
            SetLook(_selected, look);
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
        Select(-1);
        _hovered = -1;
        _hoveringClose = false;
        _hoveringHandle = false;
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

        // Rendered at display size, and only again when the images, the layout, the size or the carousel step change.
        if (_cache is null || _cache.Size != canvas.Size)
        {
            _cache?.Dispose();
            _cache = new Bitmap(canvas.Width, canvas.Height);
            using var cacheGraphics = Graphics.FromImage(_cache);
            IReadOnlyList<SourceImage> shown = _carouselStep == 0 ? _images : Carousel.Arrange(_images, _layout!, _carouselStep);
            Compositor.Draw(cacheGraphics, shown, _layout!, canvas.Size);
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

        if (!_dragging && ShownBlur(_selected) is { } blur && ShownCell(_selected) < cells.Length)
        {
            PaintBlurBars(g, cells[ShownCell(_selected)], blur);
        }

        if (_panning && !_dragging && _pressed >= 0 && _pressed < cells.Length)
        {
            PaintPanGuides(g, cells[_pressed], _images[_pressed]);
        }

        PaintHoverOutline(g, HoverOutlineBounds(canvas, cells));

        if (_hovered >= 0 && !_dragging && !_locked)
        {
            PaintCloseButton(g, CloseBounds(cells[_hovered]), _hoveringClose);
            PaintHandle(g, HandleBounds(cells[_hovered]), _hoveringHandle);
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
        if (_locked)
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

        // The bars of the blur come before the handle and the pan, on their own reach only.
        if (ShownBlur(index) is { } blur && BarAt(CellBounds()[index], blur, e.Location) is { } bar)
        {
            var area = blur.Area(CellBounds()[index]);
            _draggedBar = bar;
            _barGrab = bar switch
            {
                BlurSide.Left => area.Left - e.X,
                BlurSide.Right => area.Right - e.X,
                BlurSide.Top => area.Top - e.Y,
                _ => area.Bottom - e.Y,
            };
            BeginLive(index);
            return;
        }

        // Only the handle swaps the image; a drag anywhere else moves it within its cell.
        Select(index);
        _pressed = index;
        _pressPoint = e.Location;
        _panning = !HandleBounds(CellBounds()[index]).Contains(e.Location);
        _panPoint = e.Location;
        _panX.Reset();
        _panY.Reset();
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
        if (_draggedBar is { } bar)
        {
            DragBar(bar, e.Location);
            return;
        }

        if (_pressed < 0 || e.Button != MouseButtons.Left)
        {
            UpdateHover(e.Location);
            return;
        }

        // The drag moves the image at every zoom, and never turns into a swap.
        if (_panning)
        {
            if (_pressed < _images.Count)
            {
                Cursor = Cursors.SizeAll;
                BeginLive(_pressed);
                PanBy(_pressed, new Size(e.X - _panPoint.X, e.Y - _panPoint.Y));
            }

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
        if (_draggedBar is not null)
        {
            _draggedBar = null;
            EndLive();
            UpdateHover(e.Location);
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
        if (index < 0 || _locked || _pressed >= 0 || _draggedBar is not null)
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

        if (_draggedBar is not null)
        {
            _draggedBar = null;
            EndLive();
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if ((_hovered >= 0 || _hoveringCanvas || _hoveringDropZone) && !_dragging && !_panning && _draggedBar is null)
        {
            _hovered = -1;
            _hoveredBar = null;
            _hoveringClose = false;
            _hoveringHandle = false;
            _hoveringCanvas = false;
            _hoveringDropZone = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

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
    /// the start. A minimized window stays visible and plays on. Called by the window: WinForms raises
    /// a child's <see cref="Control.VisibleChanged"/> when its parent is shown, never when it is hidden.
    /// </summary>
    public void WindowVisibleChanged()
    {
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
        _panX.Reset();
        _panY.Reset();
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

        // The images after it move into another cell: effects belong to the cell and its image (RULES.md).
        for (int i = index; i < _images.Count; i++)
        {
            _images[i].Look = _images[i].Look.WithoutEffects();
        }

        Select(-1);
        _hovered = -1;
        _hoveringClose = false;
        _hoveringHandle = false;
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
        Select(b);
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
        bool actions = hovered >= 0 && !_locked;
        bool onHandle = actions && HandleBounds(CellBounds()[hovered]).Contains(location);
        bool onControl = onClose || onDropZone;
        var onBar = actions && !onControl && ShownBlur(hovered) is { } blur ? BarAt(CellBounds()[hovered], blur, location) : null;
        if (hovered == _hovered && onClose == _hoveringClose && onCanvas == _hoveringCanvas && onDropZone == _hoveringDropZone
            && onHandle == _hoveringHandle && onBar == _hoveredBar)
        {
            return;
        }

        _hovered = hovered;
        _hoveringClose = onClose;
        _hoveringCanvas = onCanvas;
        _hoveringDropZone = onDropZone;
        _hoveringHandle = onHandle;
        _hoveredBar = onBar;
        Cursor = onControl ? Cursors.Hand
            : onBar is { } bar ? BarCursor(bar)
            : onHandle ? Cursors.SizeAll
            : Cursors.Default;
        Invalidate();
    }

    /// <summary>
    /// Plays the animated images, at the size of their cells, and shows the frozen ones on their
    /// frame. Nothing plays behind a hidden window: showing it syncs again.
    /// </summary>
    private void SyncPlayer()
    {
        if (!Visible)
        {
            return;
        }

        _player.Sync(_images);
        UpdateDisplaySizes();
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
            Compositor.DrawCell(g, new Frame(image.Bitmap, image.BandColor, image.Look), cell, fast: live);
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
    /// The drag handle that swaps the image, centered in the cell. It shrinks, still centered, while
    /// it would come closer than a gap to the ×; below the size of a button it falls back just below it.
    /// </summary>
    private Rectangle HandleBounds(Rectangle cell)
    {
        int gap = LogicalToDeviceUnits(ButtonGap);
        var close = CloseBounds(cell);
        var center = new Point(cell.X + cell.Width / 2, cell.Y + cell.Height / 2);
        for (int size = LogicalToDeviceUnits(HandleSize); size >= LogicalToDeviceUnits(ButtonSize); size--)
        {
            var bounds = new Rectangle(center.X - size / 2, center.Y - size / 2, size, size);
            var clearance = Rectangle.Inflate(bounds, gap, gap);
            if (cell.Contains(bounds) && !close.IntersectsWith(clearance))
            {
                return bounds;
            }
        }

        return close with { Y = close.Bottom + gap };
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
                // A frozen text stays about where its frames effect points, on the new pages.
                int page = pages.Resize(shape, _pageLoader.Target(_images[i]));
                _pageLoader.Request(_images[i], _images[i].IsFrozen ? _images[i].StartPage : page);
                _player.Refresh(_images[i]);
            }
        }
    }

    /// <summary>
    /// The frames effect of an image changed: the player plays it from its new starting point, and a
    /// frozen (or forced still) image shows the page the effect points at.
    /// </summary>
    private void ShowFrames(SourceImage image)
    {
        _player.Update(image);
        if ((image.IsFrozen || ForceStill) && image.IsAnimated && _pageLoader.Target(image) != image.StartPage)
        {
            _pageLoader.Request(image, image.StartPage);
        }
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
        bool frames = look.Frames != image.Look.Frames;
        image.Look = look;
        if (turned)
        {
            FitPagesToCells();
        }

        if (frames)
        {
            ShowFrames(image);
        }

        // During a gesture, the frame shown is scaled; decoding it again at each step would only slow it down.
        if (image != _live)
        {
            UpdateDisplaySizes();
        }

        RedrawCell(image);
        if (index == _selected)
        {
            SelectedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sets the zoom of the selected image, from the options of the zoom effect: shown fast while the
    /// slider moves, in full once it rests. A zoom always brings the image back within its stops.
    /// </summary>
    public void ZoomSelected(double zoom)
    {
        var cells = CellBounds();
        if (_selected < 0 || _selected >= cells.Length || _locked)
        {
            return;
        }

        var image = _images[_selected];
        var zoomed = image.Look.WithZoom(zoom);
        var size = zoomed.Oriented(image.Bitmap.Size);
        BeginLive(_selected);
        SetLook(_selected, zoomed.WithFocus(FitCalculator.WithinStops(cells[_selected], size, zoomed.Zoom, zoomed.Focus, zoomed.FineAngle)));
        _wheelEnd.Start();
    }
    /// <summary>
    /// Zooms by <paramref name="notches"/> of the wheel, <see cref="NotchesPerDoubling"/> of them
    /// doubling the zoom, keeping the point of the image under <paramref name="location"/> in place
    /// as far as the image stays within its stops; lands on 100 % when crossing it.
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

        // The point under the mouse, where it is actually shown, then the placement that keeps it there.
        var cell = cells[index];
        var zoomed = look.WithZoom(zoom);
        var size = look.Oriented(image.Bitmap.Size);
        var before = FitCalculator.ComputeTurned(cell, size, look.Zoom, look.Focus, look.FineAngle).Fit.Image;
        var after = FitCalculator.DrawnSize(cell, size, zoomed.Zoom);
        var under = TurnedBack(cell, image, location);
        double x = Math.Clamp((under.X - before.X) / before.Width, 0, 1);
        double y = Math.Clamp((under.Y - before.Y) / before.Height, 0, 1);
        var origin = new PointF((float)(under.X - x * after.Width), (float)(under.Y - y * after.Height));
        var focus = FitCalculator.FocusAt(cell, after, origin);
        SetLook(index, zoomed.WithFocus(FitCalculator.WithinStops(cell, size, zoomed.Zoom, focus, zoomed.FineAngle)));
    }

    /// <summary>
    /// A point of the cell brought back into the unturned drawing of an image with a fine angle, so
    /// the wheel zooms around the point under the mouse; unchanged without one.
    /// </summary>
    private static PointF TurnedBack(Rectangle cell, SourceImage image, PointF point)
    {
        var look = image.Look;
        using var turn = FitCalculator.ComputeTurned(cell, look.Oriented(image.Bitmap.Size), look.Zoom, look.Focus, look.FineAngle).Transform();
        if (turn is null)
        {
            return point;
        }

        turn.Invert();
        PointF[] points = [point];
        turn.TransformPoints(points);
        return points[0];
    }

    /// <summary>
    /// Moves an image by <paramref name="delta"/> from where it is actually shown, held by the
    /// magnetic stops unless Shift is down, and never past the share of the cell it keeps covering.
    /// With a fine angle, the stops are those of the turned image's box, which follows the mouse.
    /// </summary>
    private void PanBy(int index, Size delta)
    {
        var cells = CellBounds();
        if (index >= cells.Length || delta.IsEmpty)
        {
            return;
        }

        var cell = cells[index];
        var image = _images[index];
        var look = image.Look;
        var size = look.Oriented(image.Bitmap.Size);
        var shown = FitCalculator.ComputeTurned(cell, size, look.Zoom, look.Focus, look.FineAngle);
        var bounds = shown.Bounds;
        var stops = FitCalculator.Stops(cell, bounds.Size);
        bool free = (ModifierKeys & Keys.Shift) != 0;
        float resistance = LogicalToDeviceUnits(PanResistance);
        var (heldX, heldY) = (_panX.Held, _panY.Held);
        float x = _panX.Move(bounds.X, delta.Width, stops.Left, stops.Right, cell.X + (cell.Width - bounds.Width) / 2, resistance, free);
        float y = _panY.Move(bounds.Y, delta.Height, stops.Top, stops.Bottom, cell.Y + (cell.Height - bounds.Height) / 2, resistance, free);

        // Stored where it is drawn, so a drag past the covered share does not pile up out of sight.
        var focus = shown.FocusAt(cell, new PointF(x + bounds.Width / 2, y + bounds.Height / 2));
        var moved = FitCalculator.ComputeTurned(cell, size, look.Zoom, focus, look.FineAngle);
        var middle = new PointF(moved.Bounds.X + moved.Bounds.Width / 2, moved.Bounds.Y + moved.Bounds.Height / 2);
        SetLook(index, look.WithFocus(moved.FocusAt(cell, middle)));

        // A guide can appear or go while the image stays put.
        if (_panX.Held != heldX || _panY.Held != heldY)
        {
            Invalidate(cell);
        }
    }

    private void Select(int index)
    {
        if (index == _selected)
        {
            return;
        }

        _selected = index;
        _hoveredBar = null;
        Invalidate();
        SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The blur whose bars cell <paramref name="index"/> shows: only the selected one, while the blur is selected and the grid is not locked.</summary>
    private BlurEffect? ShownBlur(int index) =>
        _showsBlurBars && !_locked && index >= 0 && index == _selected && index < _images.Count ? _images[index].Look.Blur : null;

    /// <summary>The bar within reach of <paramref name="location"/>, the nearest one when several are.</summary>
    private BlurSide? BarAt(Rectangle cell, BlurEffect blur, Point location)
    {
        if (!cell.Contains(location))
        {
            return null;
        }

        var area = blur.Area(cell);
        int reach = LogicalToDeviceUnits(BarReach);
        BlurSide? nearest = null;
        int best = int.MaxValue;
        foreach (var (side, distance) in new[]
        {
            (BlurSide.Left, Math.Abs(location.X - area.Left)),
            (BlurSide.Right, Math.Abs(location.X - area.Right)),
            (BlurSide.Top, Math.Abs(location.Y - area.Top)),
            (BlurSide.Bottom, Math.Abs(location.Y - area.Bottom)),
        })
        {
            if (distance <= reach && distance < best)
            {
                nearest = side;
                best = distance;
            }
        }

        return nearest;
    }

    private static Cursor BarCursor(BlurSide side) => side is BlurSide.Left or BlurSide.Right ? Cursors.SizeWE : Cursors.SizeNS;

    /// <summary>
    /// Moves the bar being dragged along its own axis. Within <see cref="BarSnap"/> of its edge of the
    /// cell it lands exactly on it, so no strip of a pixel or two stays sharp there.
    /// </summary>
    private void DragBar(BlurSide side, Point location)
    {
        var cells = CellBounds();
        if (_selected < 0 || _selected >= cells.Length || _images[_selected].Look.Blur is not { } blur)
        {
            return;
        }

        var cell = cells[_selected];
        bool vertical = side is BlurSide.Left or BlurSide.Right;
        int length = vertical ? cell.Width : cell.Height;
        int offset = (vertical ? location.X - cell.X : location.Y - cell.Y) + _barGrab;
        int snap = LogicalToDeviceUnits(BarSnap);
        double fraction = side is BlurSide.Left or BlurSide.Top
            ? (offset <= snap ? 0 : offset / (double)length)
            : (length - offset <= snap ? 1 : offset / (double)length);
        double gap = LogicalToDeviceUnits(BarMinGap) / (double)length;
        Cursor = BarCursor(side);
        SetLook(_selected, _images[_selected].Look.WithBlur(blur.WithSide(side, fraction, gap)));
    }

    /// <summary>
    /// The four bars as guides across the whole cell, fluorescent green and outlined so they show on
    /// any image, with a grip at the middle of each side of the sharp rectangle; the grip turns white
    /// while hovered or dragged.
    /// </summary>
    private void PaintBlurBars(Graphics g, Rectangle cell, BlurEffect blur)
    {
        var area = blur.Area(cell);

        // A bar on the right or bottom side sits on the last sharp pixel, inside the cell.
        int left = area.Left;
        int right = Math.Max(area.Left, area.Right - 1);
        int top = area.Top;
        int bottom = Math.Max(area.Top, area.Bottom - 1);
        int midX = (area.Left + area.Right) / 2;
        int midY = (area.Top + area.Bottom) / 2;

        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.None;
        using (var outline = new Pen(Color.FromArgb(160, 0, 0, 0), LogicalToDeviceUnits(4)))
        using (var line = new Pen(BarColor, LogicalToDeviceUnits(2)))
        {
            foreach (var pen in new[] { outline, line })
            {
                g.DrawLine(pen, left, cell.Top, left, cell.Bottom);
                g.DrawLine(pen, right, cell.Top, right, cell.Bottom);
                g.DrawLine(pen, cell.Left, top, cell.Right, top);
                g.DrawLine(pen, cell.Left, bottom, cell.Right, bottom);
            }
        }

        int length = LogicalToDeviceUnits(BarGripLength);
        int width = LogicalToDeviceUnits(BarGripWidth);
        PaintBarGrip(g, BlurSide.Left, new Rectangle(left - width / 2, midY - length / 2, width, length));
        PaintBarGrip(g, BlurSide.Right, new Rectangle(right - width / 2, midY - length / 2, width, length));
        PaintBarGrip(g, BlurSide.Top, new Rectangle(midX - length / 2, top - width / 2, length, width));
        PaintBarGrip(g, BlurSide.Bottom, new Rectangle(midX - length / 2, bottom - width / 2, length, width));
        g.Restore(state);
    }

    /// <summary>
    /// Guides of the magnetic stops holding a moved image, dashed, in the green of the blur bars: along
    /// the cell edge the image edge is aligned on, or through the center of the cell.
    /// </summary>
    private void PaintPanGuides(Graphics g, Rectangle cell, SourceImage image)
    {
        if (_panX.Held == PanStop.None && _panY.Held == PanStop.None)
        {
            return;
        }

        var look = image.Look;
        var drawn = FitCalculator.ComputeTurned(cell, look.Oriented(image.Bitmap.Size), look.Zoom, look.Focus, look.FineAngle).Bounds.Size;
        int inset = LogicalToDeviceUnits(2);
        int left = cell.Left + inset;
        int right = cell.Right - 1 - inset;
        int top = cell.Top + inset;
        int bottom = cell.Bottom - 1 - inset;
        int midX = cell.X + cell.Width / 2;
        int midY = cell.Y + cell.Height / 2;

        // The edge guide sits where the image would leave the cell: an image covering the cell uncovers
        // the side it moves away from, a smaller one crosses the side it moves toward.
        var lines = new List<(Point From, Point To)>();
        if (_panX.Held == PanStop.Center)
        {
            lines.Add((new Point(midX, cell.Top), new Point(midX, cell.Bottom)));
        }
        else if (_panX.Held == PanStop.Edge)
        {
            int x = (_panX.Outward > 0) == (drawn.Width >= cell.Width - 0.5f) ? left : right;
            lines.Add((new Point(x, cell.Top), new Point(x, cell.Bottom)));
        }

        if (_panY.Held == PanStop.Center)
        {
            lines.Add((new Point(cell.Left, midY), new Point(cell.Right, midY)));
        }
        else if (_panY.Held == PanStop.Edge)
        {
            int y = (_panY.Outward > 0) == (drawn.Height >= cell.Height - 0.5f) ? top : bottom;
            lines.Add((new Point(cell.Left, y), new Point(cell.Right, y)));
        }

        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.None;
        using var outline = new Pen(Color.FromArgb(160, 0, 0, 0), LogicalToDeviceUnits(4));
        using var dashed = new Pen(BarColor, LogicalToDeviceUnits(2)) { DashPattern = [4, 3] };
        foreach (var (from, to) in lines)
        {
            g.DrawLine(outline, from, to);
        }

        foreach (var (from, to) in lines)
        {
            g.DrawLine(dashed, from, to);
        }

        g.Restore(state);
    }

    private void PaintBarGrip(Graphics g, BlurSide side, Rectangle bounds)
    {
        bool hot = _hoveredBar == side || _draggedBar == side;
        using var brush = new SolidBrush(hot ? Color.White : BarColor);
        using var pen = new Pen(Color.FromArgb(160, 0, 0, 0), LogicalToDeviceUnits(1));
        g.FillRectangle(brush, bounds);
        g.DrawRectangle(pen, bounds);
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

    /// <summary>Round button like the ×, with four arrows pointing out of its center (✥), thicker as it grows.</summary>
    private void PaintHandle(Graphics g, Rectangle bounds, bool hot)
    {
        using (var brush = new SolidBrush(Color.FromArgb(hot ? 230 : 150, 0, 0, 0)))
        {
            g.FillEllipse(brush, bounds);
        }

        int pad = bounds.Width / 5;
        var glyph = Rectangle.Inflate(bounds, -pad, -pad);
        var center = new PointF(glyph.X + glyph.Width / 2f, glyph.Y + glyph.Height / 2f);
        using var pen = new Pen(Color.White, LogicalToDeviceUnits(2) * bounds.Width / (float)LogicalToDeviceUnits(ButtonSize));
        using var arrow = new AdjustableArrowCap(2f, 2f);
        pen.CustomEndCap = arrow;
        g.DrawLine(pen, center, new PointF(center.X, glyph.Top));
        g.DrawLine(pen, center, new PointF(glyph.Right, center.Y));
        g.DrawLine(pen, center, new PointF(center.X, glyph.Bottom));
        g.DrawLine(pen, center, new PointF(glyph.Left, center.Y));
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
