using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Drawing.Imaging;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// Preview of the composed result, and the surface to select, remove and swap its images.
/// Owns the images it shows.
/// </summary>
internal sealed partial class GridPreview : Control
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
    private const int WheelEndDelay = 150;

    /// <summary>How long a zoomed document's look must stay unchanged before its pages are laid out or rendered again.</summary>
    private const int PagesFitDelay = 200;
    private const int BarReach = 12;
    private const int BarSnap = 6;
    private const int BarMinGap = 8;
    private const int BarGripLength = 32;
    private const int BarGripWidth = 8;
    private const int CornerArm = 16;
    private const int CornerWidth = 6;
    private const int SeparatorReach = 4;
    private const int SeparatorSnap = 6;
    private const int PanResistance = 24;

    // A guide shows from GuideApproach percent of the cell before its stop, its opacity growing from the minimum.
    private const double ApproachMinOpacity = 0.2;
    private const int ZoomBadgeHold = 1000;
    private const int ZoomBadgeFade = 300;
    private const int ZoomBadgeTick = 30;
    private const int ZoomBadgeTextSize = 16;
    private const int ReadoutCrossStroke = 30;
    private const double ReadoutLineOpacity = 0.5;
    private const int SourceNameTextSize = 12;
    private const int SourceIconSize = 16;
    private const int CheckerSquare = 8;
    private const int ProgressLineWidth = 2;
    private const int ProgressHaloWidth = 4;
    private const int ProgressTick = 16;

    // The moving cells are drawn again at the export's frame rate, not at each tick of the progress lines.
    private static readonly TimeSpan MotionInterval = TimeSpan.FromSeconds(1.0 / Animation.FramesPerSecond);

    private static readonly Color HoverOutlineColor = Color.FromArgb(128, Color.White);

    // Helper indicators drawn over a cell (see RULES.md): fluorescent green over a black halo, so they
    // show on any image.
    internal static readonly Color HelperColor = Color.FromArgb(57, 255, 20);
    internal static readonly Color HelperHalo = Color.FromArgb(160, 0, 0, 0);

    private readonly List<SourceImage> _images = [];
    private GridLayout? _layout;
    private Bitmap? _cache;

    // The grey and white squares under the grid, at the DPI they were made for: the transparency of a
    // cell without background, in the preview only.
    private Bitmap? _checkerTile;
    private TextureBrush? _checkerboard;
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
    private Rectangle _pressedPicker;
    private readonly PageLoader _pageLoader = new();
    private readonly AnimationPlayer _player = new();
    private Soundtrack? _soundtrack;
    private SoundFade? _fade;

    // The output format, and the ratio the canvas is drawn at: the format's, or the free one as last
    // computed — held while a separator or a crop bar is dragged.
    private OutputFormat _format = OutputFormat.Twitter;
    private double _ratio = OutputFormats.TwitterRatio;
    private GridBorders? _borders;

    // The seams global effect, and the fills its strips were last drawn from in the cache: a cell drawn
    // again whose fill changed draws its neighbours again too.
    private SeamFade? _seams;
    private VideoCascade? _cascade;
    private IReadOnlyList<Color?>? _seamFills;
    private bool _locked;
    private bool _hoveringHandle;
    private bool _panning;
    private Point _panPoint;

    // Whether the drag has moved the image yet, and whether its last move ignored the stops (Shift):
    // the guides of the stops it comes near show only once it moves, and never while free.
    private bool _panMoved;
    private bool _panFree;
    private readonly PanMagnet _panX = new();
    private readonly PanMagnet _panY = new();

    // The image the arrow keys moved last, with the look they left it: its pan guides show while a stop
    // holds it, until another gesture, cell or tab — or a change of its look — ends the move.
    private (SourceImage Image, ImageLook Look)? _keyPan;
    private ImageEffect? _barsEffect;
    private BarSide? _hoveredBar;
    private BarSide? _draggedBar;
    private int _barGrab;

    // The corner of the bars hovered, the one dragged, and how far from it it was grabbed on each axis.
    private BarCorner? _hoveredCorner;
    private BarCorner? _draggedCorner;
    private Size _cornerGrab;
    private bool _hoveringKept;

    // The crop's kept part being moved whole, and where the drag started.
    private CropEffect? _movedCrop;
    private Point _moveFrom;
    private Separator? _hoveredSeparator;
    private Separator? _draggedSeparator;
    private int _separatorGrab;
    private bool _separatorMoved;

    // The image panned or zoomed right now: drawn fast and painted at once, drawn again in full at the end.
    private SourceImage? _live;

    // Whether the grid ran at the last playback change: stopping draws the animated cells in full once.
    private bool _wasRunning = true;

    // The wheel has no release: its zoom ends once no notch came for a moment.
    private readonly System.Windows.Forms.Timer _wheelEnd = new() { Interval = WheelEndDelay };

    // A document's zoom changed: its pages follow once the gesture ends, the bitmap scaled meanwhile.
    private readonly System.Windows.Forms.Timer _pagesFit = new() { Interval = PagesFitDelay };
    private int _wheelCell = -1;
    private int _wheelDelta;
    private bool _wheelWithControl;

    // Alt held: the wheel zooms the image even over a resizable zone's bars. WM_MOUSEWHEEL's key flags never carry Alt.
    private bool _wheelWithAlt;

    // The wheel was turned with Alt held: releasing it must not put the window into menu mode.
    private bool _altWheeled;

    // The wheel is scaling the crop's kept part: the free format's ratio is held until the burst ends.
    private bool _wheelHoldsRatio;

    // The zoom percentage of the image last zoomed, over its cell: held a moment after each change, then faded out.
    private readonly System.Windows.Forms.Timer _zoomBadgeTimer = new();

    // What the last undo or redo changed on an image that a helper indicator shows — its blur bars, its
    // crop's edges, the guides of the stops it now rests on, per axis — shown briefly, with the zoom
    // badge's timing, since that moment (RULES.md § Undo History).
    private (SourceImage Image, bool Blur, bool Crop, bool GuidesX, bool GuidesY, long Since)? _restored;
    private readonly System.Windows.Forms.Timer _restoredTimer = new();
    private SourceImage? _zoomBadgeImage;
    private long _zoomBadgeChanged;
    private Rectangle _zoomBadgeBounds;

    // Every image's offset from its cell's center in export pixels, as last painted, with the cell it was
    // in; and the position readouts showing, per image, since its offset last changed — held a moment,
    // then faded out, with the zoom badge's timing (RULES.md § Position Readout).
    private Dictionary<SourceImage, (int Cell, Point Offset)> _offsets = [];
    private readonly Dictionary<SourceImage, long> _readouts = [];
    private readonly System.Windows.Forms.Timer _readoutTimer = new();

    // The progress line of every playing cell, along its bottom edge: its strip repainted about 60 times
    // a second while something plays, the strips of the last tick too, so a line that vanished is erased.
    private readonly System.Windows.Forms.Timer _progressTimer = new() { Interval = ProgressTick };
    private readonly List<Rectangle> _progressStrips = [];

    // The time on the grid's clock the moving cells were last drawn at.
    private TimeSpan _motionDrawn;

    // The file name at the bottom of the selected cell, shortened to its width, and its folder icon.
    private (SourceImage Image, int Width, float Size, string Text)? _fittedName;
    private bool _hoveringSourceName;
    private bool _hoveringSourceIcon;
    private readonly ToolTip _toolTip = new();

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
            UpdateRatio();
        };
        _player.FrameShown += (_, image) => RedrawCell(image);
        this._player.PlaybackChanged += (_, _) => this.OnPlaybackChanged();
        _wheelEnd.Tick += (_, _) => EndLive();
        this._pagesFit.Tick += (_, _) => this.OnPagesFitTick();
        _zoomBadgeTimer.Tick += (_, _) => OnZoomBadgeTick();
        this._restoredTimer.Tick += (_, _) => this.OnRestoredTick();
        this._readoutTimer.Tick += (_, _) => this.OnReadoutTick();
        _progressTimer.Tick += (_, _) => OnProgressTick();
    }

    public event EventHandler? ImagesChanged;

    /// <summary>Raised when another cell is selected, or none, and when the selected image changes its look.</summary>
    public event EventHandler? SelectedImageChanged;

    /// <summary>Raised when the drop zone right of the canvas, or the empty canvas, is clicked.</summary>
    public event EventHandler? AddImagesClicked;

    /// <summary>Raised with the file's path when the folder icon before the selected cell's file name is clicked.</summary>
    public event EventHandler<string>? ShowInExplorerClicked;

    /// <summary>Raised when the active layout changes, picked by the user or reset with the image count, or when its cells are resized.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Raised when the canvas takes another ratio: another output format, or the free one computed anew.</summary>
    public event EventHandler? RatioChanged;

    /// <summary>
    /// Raised as a cell dragged by its ✥ handle moves, with the mouse in screen coordinates — the
    /// drag may leave the preview, the capture keeping it — and with null when the drag ends.
    /// </summary>
    public event EventHandler<Point?>? SwapDragMoved;

    /// <summary>
    /// Raised when a cell dragged by its ✥ handle is released on no cell, with its image and the
    /// mouse in screen coordinates: nothing is swapped; the window may take it, as a favorite.
    /// </summary>
    public event EventHandler<(SourceImage Image, Point ScreenPoint)>? ReleasedOffGrid;

    /// <summary>The cells' images — the grid's, also while a layer's edit view shows that layer alone.</summary>
    public IReadOnlyList<SourceImage> Images => this._edit?.Images ?? _images;

    /// <summary>
    /// Counts the changes of the rendered result — images, layout, cell sizes, looks, borders,
    /// soundtrack, fade — so a caller can tell whether the grid is still what it exported (the last video).
    /// A selection change is not one.
    /// </summary>
    public int ContentVersion { get; private set; }

    /// <summary>The soundtrack mixed over the videos while the grid holds an image; <c>null</c> when it is off.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Soundtrack? Soundtrack
    {
        get => _soundtrack;
        set
        {
            if (value == _soundtrack)
            {
                return;
            }

            _soundtrack = value;
            _player.SetSoundtrack(value);
            ContentVersion++;
        }
    }

    /// <summary>The fade of the grid's sound mix, in and out on every loop; <c>null</c> when it is off.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SoundFade? Fade
    {
        get => _fade;
        set
        {
            if (value == _fade)
            {
                return;
            }

            _fade = value;
            _player.SetFade(value);
            ContentVersion++;
        }
    }

    /// <summary>
    /// The output format: the ratio of the canvas, in the preview and in every export (RULES.md
    /// § Output Format). Not persisted, <see cref="OutputFormat.Twitter"/> at first.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public OutputFormat Format
    {
        get => _format;
        set
        {
            // The grid changes: a layer's edit view gives it back first.
            this.EndLayerEdit();
            if (value != _format)
            {
                _format = value;
                UpdateRatio();
            }
        }
    }

    /// <summary>The canvas's width ÷ height as drawn: the format's, or the free one — the one rule every reader goes through.</summary>
    public double CanvasRatio => this._edit?.Ratio ?? _ratio;

    /// <summary>The ratio the free format gives the grid as it stands, whatever the format.</summary>
    public double FreeRatio => this._edit is { } edit ? edit.FreeRatio : _layout is null ? OutputFormats.TwitterRatio
        : OutputFormats.FreeRatio(_images.Select(i => i.Pages is { PageSize: not null } ? (Size?)null : i.Look.Shown(i.Size)).ToList(), _layout);

    /// <summary>The borders drawn on the grid, shrinking its cells for their gap; <c>null</c> when they are off.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridBorders? Borders
    {
        get => _borders;
        set
        {
            // The grid changes: a layer's edit view gives it back first.
            this.EndLayerEdit();
            if (value == _borders)
            {
                return;
            }

            _borders = value;
            _cache?.Dispose();
            _cache = null;

            // The gap resizes the cells, like a moved separator.
            FitPagesToCells();
            UpdateDisplaySizes();
            Invalidate();
            ContentVersion++;
        }
    }

    /// <summary>
    /// The seams fading the cells' flat fills into each other; <c>null</c> when they are off. Drawn by
    /// <see cref="Compositor"/>, which leaves them out while the borders leave a gap.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SeamFade? Seams
    {
        get => this._seams;
        set
        {
            // The grid changes: a layer's edit view gives it back first.
            this.EndLayerEdit();
            if (value == this._seams)
            {
                return;
            }

            this._seams = value;
            this._cache?.Dispose();
            this._cache = null;
            this.Invalidate();
            this.ContentVersion++;
        }
    }

    /// <summary>
    /// The Cascade playing the videos and animated GIFs one after the other; <c>null</c> when it is off.
    /// Turned on or off, or its pause changed, it starts the grid over, so the first one plays at once
    /// (workfiles/20261008-video-cascade.md).
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public VideoCascade? Cascade
    {
        get => this._cascade;
        set
        {
            // The grid changes: a layer's edit view gives it back first.
            this.EndLayerEdit();
            if (value == this._cascade)
            {
                return;
            }

            this._cascade = value;
            this._player.SetCascade(value);
            this._player.Restart();
            this._progressTimer.Start();
            this.ContentVersion++;
        }
    }

    /// <summary>
    /// Lets go of an image that left the grid — deleted, replaced, cleared: disposes it by default; the
    /// window hands it to the undo history instead, which keeps it while a step holds it.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<SourceImage> ReleaseImage { get; set; } = image => image.Dispose();

    /// <summary>The ⚙ menu's choices of <see cref="GuideApproach"/>, in percent of the cell.</summary>
    public static readonly int[] GuideApproachChoices = [0, 5, 10, 15, 20, 25];

    public const int DefaultGuideApproach = 10;

    /// <summary>
    /// How far before its magnetic stop a guide shows while an image is moved, in percent of the cell — an
    /// app setting of the ⚙ menu; 0 shows a guide only once its stop holds the image.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GuideApproach { get; set; } = DefaultGuideApproach;

    /// <summary>
    /// Whether every gap bar runs from the image's edge nearest its guide rather than from the image's
    /// center — an app setting of the ⚙ menu, the same point for every axis and every stop.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool GapBarsFromEdges { get; set; }

    /// <summary>
    /// Whether a gesture is changing the grid right now — a cell pressed (pan, ✥ swap), a bar or a
    /// separator dragged, a crop moved, a zoom by the wheel or the slider not yet at rest: the undo
    /// history commits nothing before it ends.
    /// </summary>
    public bool InGesture =>
        this._pressed >= 0 || this._draggedBar is not null || this._draggedCorner is not null || this._draggedSeparator is not null
        || this._movedCrop is not null || this._live is not null || this._wheelEnd.Enabled || this._eyedropper is not null || this._pressedLayer >= 0 || this._rotatingLayer;

    /// <summary>
    /// Whether the wheel was turned over the grid with Alt held since the last call, cleared by it: the
    /// form then swallows the menu mode that releasing Alt would start (RULES.md § Resizable Zones).
    /// </summary>
    public bool TakeAltWheel()
    {
        bool wheeled = this._altWheeled;
        this._altWheeled = false;
        return wheeled;
    }

    /// <summary>Layout the images are shown and exported with; <c>null</c> while there is no image.</summary>
    public GridLayout? ActiveLayout => this._edit is { } edit ? edit.Layout : _layout;

    public int FreeSlots => GridLayout.MaxImages - this.Images.Count;

    public bool HasSelection => _selected >= 0;

    /// <summary>
    /// The image the effects toolbar acts on: the selected layer's content, else the selected cell's;
    /// <c>null</c> without a selection — the two are exclusive (workfiles/20261029-layers.md).
    /// </summary>
    public SourceImage? SelectedImage => this.SelectedLayer?.Image ?? this.SelectedCellImage;

    /// <summary>The image of the selected cell — never a layer's —; <c>null</c> without one.</summary>
    private SourceImage? SelectedCellImage => _selected >= 0 && _selected < _images.Count ? _images[_selected] : null;

    /// <summary>
    /// The effect whose bars the selected cell shows and lets be dragged — the blur, or the crop in its
    /// edit view — set while that effect is the selected one of the effects toolbar and is on; <c>null</c>
    /// for none.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ImageEffect? BarsEffect
    {
        get => _barsEffect;
        set
        {
            if (value != _barsEffect)
            {
                _barsEffect = value;
                _hoveredBar = null;
                this._hoveredCorner = null;
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
            // The grid changes: a layer's edit view gives it back first.
            this.EndLayerEdit();
            _locked = value;
            _pressed = -1;
            EndSeparatorDrag();
            EndDrag();
        }
    }

    /// <summary>
    /// Adds images: the first one replaces <paramref name="targetCell"/> when given (a drop onto a
    /// cell); the others fill the free slots; the first excess image replaces the selected cell, else
    /// the last one; any further excess is disposed. Returns the number of images ignored. An
    /// arrival starts the grid over: every animated image from its starting point, the soundtrack
    /// from its beginning (RULES.md), and shows the arriving images whole (<see cref="ShowWhole"/>).
    /// </summary>
    public int Add(IReadOnlyList<SourceImage> images, int targetCell = -1)
    {
        this.EndLayerEdit();
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
                continue;
            }
        }

        this.OnImagesChanged(images.Where(this._images.Contains).ToList());
        if (ignored < images.Count)
        {
            _player.Restart();
        }

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

    /// <summary>The image of a cell adopted a file: the name it shows is measured again.</summary>
    public void RefreshSourceName()
    {
        _fittedName = null;
        Invalidate();
    }

    /// <summary>Applies a layout for the current image count; the images keep their order.</summary>
    public void SetLayout(GridLayout layout)
    {
        this.EndLayerEdit();
        if (layout.Count != _images.Count)
        {
            throw new ArgumentException($"Layout {layout.Id} holds {layout.Count} images, not {_images.Count}.", nameof(layout));
        }

        _layout = layout;
        _hovered = -1;
        _hoveringClose = false;
        _hoveringHandle = false;
        _hoveredSeparator = null;
        _cache?.Dispose();
        _cache = null;
        FitPagesToCells();
        SyncPlayer();
        Invalidate();
        ContentVersion++;
        UpdateRatio();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The cell under <paramref name="location"/>, by its slot: a point in the gap between two cells belongs to one of them.</summary>
    public int CellAt(Point location) => Array.FindIndex(SlotBounds(), c => c.Contains(location));

    public void RemoveSelected()
    {
        if (_selected >= 0)
        {
            RemoveAt(_selected);
        }
    }

    public void ClearSelection()
    {
        // In a layer's edit view, the layer stays the selected cell until the view closes.
        if (this._edit is not null)
        {
            return;
        }

        this.SelectLayer(-1);
        Select(-1);
    }

    /// <summary>
    /// Selects the cell under the mouse pointer when it holds an image, else keeps the selection: what a
    /// shortcut acting on the hovered cell, else on the selected one, does first. Whether an image is
    /// selected afterwards.
    /// </summary>
    public bool SelectUnderPointer()
    {
        var location = this.PointToClient(Cursor.Position);
        if (this.ClientRectangle.Contains(location) && this.CellAt(location) is var index and >= 0 && index < this._images.Count)
        {
            this.Select(index);
        }

        return this.SelectedImage is not null;
    }

    /// <summary>Puts every separator back where the layout's own proportions place it; the mirror stays.</summary>
    public void ResetCellSizes()
    {
        if (_layout is { IsResized: true } layout && !_locked)
        {
            ApplySizes(layout.WithDefaultSizes());
        }
    }

    /// <summary>
    /// The layout strip's Auto: moves the vertical (<paramref name="vertical"/>) or horizontal separators so
    /// every cell is shaped like its image (<see cref="LayoutAutoSize"/>) — side by side the images get the
    /// same height, stacked the same width — then shows every image whole, centered, as on its arrival.
    /// Texts are left out and keep filling their cell. One step of the undo history.
    /// </summary>
    public void AutoSizeCells(bool vertical)
    {
        if (this._layout is null || this._locked || this._images.Count == 0)
        {
            return;
        }

        // In the free format the new sizes give another ratio, which reshapes the cells again.
        for (int pass = 0; pass < 3; pass++)
        {
            var canvas = this.CanvasBounds();
            if (canvas.IsEmpty)
            {
                return;
            }

            var shown = this._images.Select(i => i.Pages is { PageSize: not null } ? (Size?)null : i.Look.Shown(i.Size)).ToList();
            var layout = LayoutAutoSize.Fitted(this._layout, vertical, shown, l => Compositor.Cells(l, canvas.Size, this._borders));
            if (layout.SameAs(this._layout))
            {
                break;
            }

            this.ApplySizes(layout);
        }

        this.ShowWhole(this._images, centered: true);
    }

    /// <summary>Gives the selected image a new look: an effect toggled or tuned from the toolbars.</summary>
    public void SetSelectedLook(ImageLook look)
    {
        if (this.SelectedLayer is not null)
        {
            this.SetLayerLook(look);
        }
        else if (SelectedImage is not null)
        {
            SetLook(_selected, look);
        }
    }

    /// <summary>Removes every image at once, raising <see cref="ImagesChanged"/> a single time.</summary>
    public void Clear()
    {
        this.EndLayerEdit();
        if (_images.Count == 0)
        {
            return;
        }

        var removed = this._images.ToList();
        _images.Clear();
        removed.ForEach(i => this.ReleaseImage(i));
        Select(-1);
        _hovered = -1;
        _hoveringClose = false;
        _hoveringHandle = false;
        EndDrag();
        OnImagesChanged();
    }

    /// <summary>
    /// Puts back a step of the undo history: the cells' images with their looks, the layout and the
    /// format, then <paramref name="selected"/> as the selected cell (none for -1). Images that differ
    /// start the grid over (RULES.md § Preview Playback); images only swapped, laid out or tuned play
    /// on, a frames change replaying its own image. Nothing is released: the history holds the images
    /// left out.
    /// </summary>
    public void Restore(IReadOnlyList<CellState> cells, GridLayout? layout, OutputFormat format, int selected)
    {
        this.EndLayerEdit();
        bool startOver = cells.Count != this._images.Count || cells.Any(c => !this._images.Contains(c.Image));
        bool layoutChanged = layout is null ? this._layout is not null : !layout.SameAs(this._layout);
        List<SourceImage> replayed = startOver ? [] : cells.Where(c => c.Look.Frames != c.Image.Look.Frames).Select(c => c.Image).ToList();

        // The images already in the grid whose look the step changes, with the look they leave.
        var changed = cells.Where(c => this._images.Contains(c.Image) && c.Look != c.Image.Look).Select(c => (c.Image, Before: c.Image.Look)).ToList();

        this._images.Clear();
        foreach (var cell in cells)
        {
            cell.Image.Look = cell.Look;
            this._images.Add(cell.Image);
        }

        this._layout = layout;
        this._format = format;
        this._selected = selected < this._images.Count ? selected : -1;
        this._hovered = -1;
        this._hoveringClose = false;
        this._hoveringHandle = false;
        this._hoveredBar = null;
        this._hoveredCorner = null;
        this._hoveredSeparator = null;
        this._fittedName = null;
        this._cache?.Dispose();
        this._cache = null;
        this.Invalidate();
        this.ContentVersion++;
        this.FitPagesToCells();
        this.SyncPlayer();
        this.UpdateRatio();
        replayed.ForEach(this.ShowFrames);
        if (startOver)
        {
            this._player.Restart();
        }

        this.ShowRestored(changed);
        this.ImagesChanged?.Invoke(this, EventArgs.Empty);
        if (layoutChanged)
        {
            this.LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        this.SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows briefly what a restored step changed that a helper indicator shows, whatever tab is selected:
    /// the zoom badge over an image whose zoom changed; for the first image with any, its blur bars, its
    /// crop's edges, the guides of the stops it now rests on along an axis it moved on — a turn or a flip
    /// moving the image along, not counted as a move (RULES.md § Undo History).
    /// </summary>
    private void ShowRestored(List<(SourceImage Image, ImageLook Before)> changed)
    {
        this.InvalidateRestored();
        this._restored = null;
        this._restoredTimer.Stop();
        var cells = this.CellBounds();
        foreach (var (image, before) in changed)
        {
            var look = image.Look;
            int index = this._images.IndexOf(image);
            var cell = index >= 0 && index < cells.Length ? cells[index] : Rectangle.Empty;
            if (ZoomOf(image, cell) != ZoomOf(image, cell, before))
            {
                this.ShowZoomBadge(image);
            }

            var effects = look.ChangedEffects(before);
            bool turned = effects.Contains(ImageEffect.Rotate) || effects.Contains(ImageEffect.Flip);
            bool blur = look.Blur is not null && look.Blur != before.Blur;
            bool crop = look.Crop is not null && look.Crop != before.Crop && !turned;
            bool guidesX = !turned && look.Focus.X != before.Focus.X;
            bool guidesY = !turned && look.Focus.Y != before.Focus.Y;
            if (this._restored is null && (blur || crop || guidesX || guidesY))
            {
                this._restored = (image, blur, crop, guidesX, guidesY, Environment.TickCount64);
            }
        }

        if (this._restored is not null)
        {
            this._restoredTimer.Interval = ZoomBadgeHold;
            this._restoredTimer.Start();
            this.InvalidateRestored();
        }
    }

    /// <summary>Waits out the hold in one tick, then repaints the indicators along their fade, and drops them once transparent.</summary>
    private void OnRestoredTick()
    {
        if (this._restored is not { } shown)
        {
            this._restoredTimer.Stop();
            return;
        }

        long elapsed = Environment.TickCount64 - shown.Since;
        if (elapsed >= ZoomBadgeHold + ZoomBadgeFade)
        {
            this._restoredTimer.Stop();
            this.InvalidateRestored();
            this._restored = null;
            return;
        }

        this._restoredTimer.Interval = elapsed < ZoomBadgeHold ? (int)(ZoomBadgeHold - elapsed) : ZoomBadgeTick;
        if (elapsed >= ZoomBadgeHold)
        {
            this.InvalidateRestored();
        }
    }

    /// <summary>Repaints the cell of the image whose restored indicators show.</summary>
    private void InvalidateRestored()
    {
        var cells = this.CellBounds();
        int index = this._restored is { } shown ? this._images.IndexOf(shown.Image) : -1;
        if (index >= 0 && index < cells.Length)
        {
            this.Invalidate(cells[index]);
        }
    }

    /// <summary>
    /// The indicators a restored step shows, faded out after their hold: the blur bars and the crop's
    /// edges without their grips — not handles —, unless the cell shows the same bars as handles, and the
    /// guides of the stops the image rests on.
    /// </summary>
    private void PaintRestored(Graphics g, Rectangle[] cells)
    {
        if (this._restored is not { } shown || this._dragging)
        {
            return;
        }

        int index = this._images.IndexOf(shown.Image);
        if (index < 0 || index >= cells.Length)
        {
            return;
        }

        long elapsed = Environment.TickCount64 - shown.Since;
        double opacity = Math.Clamp(1 - (elapsed - ZoomBadgeHold) / (double)ZoomBadgeFade, 0, 1);
        var cell = cells[index];
        var look = shown.Image.DrawnLook;
        bool handles = this.ShownBars(index) is not null;
        if (shown.Blur && look.Blur is { } blur && !(handles && this._barsEffect == ImageEffect.Blur))
        {
            this.PaintBars(g, cell, new Bars(blur.Area(cell), cell), opacity, grips: false);
        }

        var drawn = Rectangle.Round(FitCalculator.ComputeTurned(cell, look.Shown(shown.Image.Bitmap.Size), look.ZoomIn(cell, look.Shown(shown.Image.Bitmap.Size)), look.Focus, look.FineAngle).Bounds);
        if (shown.Crop && look.Crop is not null && !(handles && this._barsEffect == ImageEffect.Crop))
        {
            this.PaintBars(g, cell, new Bars(Rectangle.Intersect(drawn, cell), cell), opacity, grips: false);
        }

        if (shown.GuidesX || shown.GuidesY)
        {
            this.PaintGuideLines(g, cell, this.RestingGuides(cell, drawn, shown.GuidesX, shown.GuidesY), opacity);
        }
    }

    /// <summary>
    /// The guides of the magnetic stops an image drawn at <paramref name="drawn"/> rests on, along the axes
    /// asked for: through the center of the cell, else along the cell edge one of its edges lies on.
    /// </summary>
    private List<(Point From, Point To)> RestingGuides(Rectangle cell, Rectangle drawn, bool alongX, bool alongY)
    {
        const int Near = 1;
        int inset = this.LogicalToDeviceUnits(2);
        var lines = new List<(Point From, Point To)>();
        if (alongX)
        {
            int midX = cell.X + cell.Width / 2;
            bool left = Math.Abs(drawn.Left - cell.Left) <= Near, right = Math.Abs(drawn.Right - cell.Right) <= Near;
            int? x = Math.Abs(drawn.X + drawn.Width / 2 - midX) <= Near ? midX
                : left && !right ? cell.Left + inset
                : right && !left ? cell.Right - 1 - inset
                : null;
            if (x is { } at)
            {
                lines.Add((new Point(at, cell.Top), new Point(at, cell.Bottom)));
            }
        }

        if (alongY)
        {
            int midY = cell.Y + cell.Height / 2;
            bool top = Math.Abs(drawn.Top - cell.Top) <= Near, bottom = Math.Abs(drawn.Bottom - cell.Bottom) <= Near;
            int? y = Math.Abs(drawn.Y + drawn.Height / 2 - midY) <= Near ? midY
                : top && !bottom ? cell.Top + inset
                : bottom && !top ? cell.Bottom - 1 - inset
                : null;
            if (y is { } at)
            {
                lines.Add((new Point(cell.Left, at), new Point(cell.Right, at)));
            }
        }

        return lines;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wheelEnd.Dispose();
            this._pagesFit.Dispose();
            _zoomBadgeTimer.Dispose();
            this._restoredTimer.Dispose();
            this._readoutTimer.Dispose();
            _progressTimer.Dispose();
            _toolTip.Dispose();
            _player.Dispose();
            _images.ForEach(i => i.Dispose());
            _images.Clear();
            foreach (var layer in this._layers.Items)
            {
                layer.Image.Dispose();
            }
            _cache?.Dispose();
            _ghost?.Dispose();
            _checkerboard?.Dispose();
            _checkerTile?.Dispose();
            this._eyedropper?.Snapshot.Dispose();
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
        this.UpdateReadouts();
        if (_images.Count == 0)
        {
            PaintEmptyState(g, canvas);
            g.FillRectangle(highlight, _externalTarget);
            PaintHoverOutline(g, HoverOutlineBounds(canvas, []));
            this.PaintEyedropper(g);
            return;
        }

        // Rendered at display size, and only again when the images, the layout or the size change.
        if (_cache is null || _cache.Size != canvas.Size)
        {
            _cache?.Dispose();
            // Premultiplied, as GDI+ blends: blitted at every frame, a straight-alpha cache would be converted each time.
            _cache = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppPArgb);
            using (var cacheGraphics = Graphics.FromImage(_cache))
            {
                // Fast while the eyedropper previews a color, drawn in full once it ends.
                Compositor.Draw(cacheGraphics, _images, _layout!, canvas.Size, _borders, this._player.GridTime, this._seams, fast: this._eyedropper is not null, this.ShownLayers);
            }

            this._seamFills = this.SeamsOf(Compositor.Cells(_layout!, canvas.Size, _borders))?.Fills;

            // The Twitter corners cut as a PNG is: the preview shows what Twitter / X shows.
            _borders?.CutCorners(_cache);
        }

        PaintCheckerboard(g, canvas);
        g.DrawImageUnscaled(_cache, canvas.Location);

        var cells = CellBounds();
        // Painted again only where the paint reaches its cell: a playing cell elsewhere repaints its strip alone.
        if (!_dragging && _barsEffect == ImageEffect.Crop && ShownBars(_selected) is { } crop && e.ClipRectangle.IntersectsWith(cells[_selected]))
        {
            PaintCropEdit(g, canvas, cells[_selected], crop);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Under the interaction feedback, like the grid itself: a dragged cell dims its line too.
        PaintProgressLines(g, cells);

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

        // Under the blur bars, which keep their click priority over it.
        PaintSourceName(g);

        if (!_dragging && ShownBars(_selected) is { } bars)
        {
            PaintBars(g, cells[_selected], bars);
        }

        this.PaintRestored(g, cells);

        if (_panning && !_dragging && _pressed >= 0 && _pressed < cells.Length)
        {
            PaintPanGuides(g, cells[_pressed], _images[_pressed], dragged: true);
        }
        else if (this.KeyPanned() && this._selected < cells.Length)
        {
            this.PaintPanGuides(g, cells[this._selected], this._images[this._selected], dragged: false);
        }

        this.PaintReadouts(g, cells);

        PaintHoverOutline(g, HoverOutlineBounds(canvas, cells));

        if (_hovered >= 0 && !_dragging && !_locked && this._edit is null)
        {
            PaintCloseButton(g, CloseBounds(cells[_hovered]), _hoveringClose);
            if (this.HandleOf(_hovered) is { IsEmpty: false } handle)
            {
                PaintHandle(g, handle, _hoveringHandle);
            }
        }

        PaintZoomBadge(g);
        this.PaintLayerSelection(g, canvas);

        if (_dragging && _ghost is not null)
        {
            PaintGhost(g, _ghost, GhostBounds());
        }

        this.PaintEyedropper(g);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        this.EndKeyPan();

        // Clicked, the preview takes the keyboard focus: the arrows then move the selected image.
        this.Focus();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Opening Explorer changes nothing in the grid: it works during an export too.
        if (SourceHitAt(e.Location).Icon)
        {
            ShowInExplorerClicked?.Invoke(this, SelectedCellImage!.FilePath!);
            return;
        }

        // The selection is kept: a full grid replaces the selected cell with the first added image.
        if (_locked)
        {
            return;
        }

        // While their tab is selected, the layers come first, on their own area only (workfiles/20261029-layers.md).
        if (this.LayerMouseDown(e))
        {
            return;
        }

        var picker = PickerAt(e.Location);
        if (!picker.IsEmpty)
        {
            _pressedPicker = picker;
            return;
        }

        int index = CellAt(e.Location);
        if (index < 0)
        {
            // In a layer's edit view, a double-click outside the layer closes it; a click changes nothing.
            if (this._edit is not null)
            {
                if (e.Clicks == 2)
                {
                    this.EndLayerEdit();
                }

                return;
            }

            ClearSelection();
            return;
        }

        if (this._edit is null && CloseBounds(CellBounds()[index]).Contains(e.Location))
        {
            RemoveAt(index);
            UpdateHover(e.Location);
            return;
        }

        // A corner of the bars comes before them, on both of their reaches: it moves the two meeting there.
        if (this.ShownBars(index) is { } framed && this.CornerAt(this.CellBounds()[index], framed, e.Location) is { } corner)
        {
            this._draggedCorner = corner;
            this._cornerGrab = new Size(SideOf(framed.Area, corner.Vertical) - e.X, SideOf(framed.Area, corner.Horizontal) - e.Y);
            this.BeginLive(index);
            return;
        }

        // The bars of the blur, or of the crop, come before the handle and the pan, on their own reach only.
        if (ShownBars(index) is { } bars && BarAt(CellBounds()[index], bars, e.Location) is { } bar)
        {
            var area = bars.Area;
            _draggedBar = bar;
            _barGrab = bar switch
            {
                BarSide.Left => area.Left - e.X,
                BarSide.Right => area.Right - e.X,
                BarSide.Top => area.Top - e.Y,
                _ => area.Bottom - e.Y,
            };
            BeginLive(index);
            return;
        }

        // Inside the crop's kept part, in its edit view, a drag moves it whole; the handle still swaps.
        if (KeptPartAt(index, e.Location))
        {
            _movedCrop = _images[index].Look.Crop;
            _moveFrom = e.Location;
            BeginLive(index);
            return;
        }

        // A separator comes after the blur bars, before the handle and the pan, on its own band only.
        // It resizes the grid, not the cell: the selection stays.
        if (SeparatorAt(e.Location) is { } separator)
        {
            if (e.Clicks == 2)
            {
                ApplySizes(_layout!.WithSeparator(separator, _layout.DefaultPosition(separator)));
                return;
            }

            var canvas = CanvasBounds();
            _draggedSeparator = separator;
            _separatorGrab = separator.Vertical
                ? canvas.X + GridLayout.Boundary(canvas.Width, separator.Position) - e.X
                : canvas.Y + GridLayout.Boundary(canvas.Height, separator.Position) - e.Y;
            return;
        }

        // The crop's edit view shows the whole image: a drag off its kept part would pan the cropped one unseen.
        if (EditsCrop(index) && !this.HandleOf(index).Contains(e.Location))
        {
            return;
        }

        // Only the handle swaps the image; a drag anywhere else moves it within its cell.
        Select(index);
        _pressed = index;
        _pressPoint = e.Location;
        _panning = this._edit is not null || !this.HandleOf(index).Contains(e.Location);
        _panPoint = e.Location;
        _panX.Reset();
        _panY.Reset();
        this._panMoved = false;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (this._eyedropper is not null)
        {
            this.HoverEyedropper(e.Location);
            return;
        }

        if (this.LayerMouseMove(e))
        {
            return;
        }

        if (_draggedSeparator is { } separator)
        {
            DragSeparator(separator, e.Location);
            return;
        }

        if (this._draggedCorner is { } corner)
        {
            this.DragCorner(corner, e.Location);
            return;
        }

        if (_draggedBar is { } bar)
        {
            DragBar(bar, e.Location);
            return;
        }

        if (_movedCrop is { } crop)
        {
            MoveCrop(crop, e.Location);
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
                PanBy(_pressed, new Size(e.X - _panPoint.X, e.Y - _panPoint.Y), this.LogicalToDeviceUnits(PanResistance), (ModifierKeys & Keys.Shift) != 0);
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
        SwapDragMoved?.Invoke(this, PointToScreen(e.Location));

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
        if (this.EndLayerPress())
        {
            return;
        }

        if (_draggedSeparator is not null)
        {
            EndSeparatorDrag();
            UpdateHover(e.Location);
            return;
        }

        if (_draggedBar is not null || this._draggedCorner is not null || _movedCrop is not null)
        {
            _draggedBar = null;
            this._draggedCorner = null;
            _movedCrop = null;
            EndLive();
            UpdateRatio();
            UpdateHover(e.Location);
            Invalidate();
            return;
        }

        if (!_pressedPicker.IsEmpty)
        {
            bool released = PickerAt(e.Location) == _pressedPicker;
            _pressedPicker = Rectangle.Empty;
            if (released)
            {
                AddImagesClicked?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (_dragging && _dropTarget >= 0 && _dropTarget != _pressed)
        {
            Swap(_pressed, _dropTarget);
        }
        else if (_dragging && _dropTarget < 0 && _pressed < _images.Count)
        {
            ReleasedOffGrid?.Invoke(this, (_images[_pressed], PointToScreen(e.Location)));
        }

        EndDrag();
        _hovered = -1;
        UpdateHover(e.Location);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WheelSteps.WM_MOUSEWHEEL)
        {
            _wheelWithControl = WheelSteps.WithControl(m);
            this._wheelWithAlt = (ModifierKeys & Keys.Alt) != 0;
            this._altWheeled = this._wheelWithAlt;
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// The wheel zooms the cell under the mouse, around the point of the image under it, by steps of 5 % —
    /// 1 % with Control held, coarser above 200 % (<see cref="WheelSteps.Zoom"/>); not while another
    /// gesture runs. Over the selected cell showing the bars of a resizable zone, it scales that zone
    /// instead (<see cref="ScaleZone"/>) — unless Alt is held: it then zooms the image as without bars,
    /// around the point of the image under the mouse in the crop's edit view (<see cref="KeptPartPoint"/>).
    /// </summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (this._eyedropper is not null || this.LayerWheel(e))
        {
            return;
        }

        int index = CellAt(e.Location);
        if (index < 0 || _locked || _pressed >= 0 || _draggedBar is not null || _draggedSeparator is not null || _movedCrop is not null
            || this._draggedCorner is not null)
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
        var bars = this.ShownBars(index);
        if (bars is { } zone && !this._wheelWithAlt)
        {
            this.ScaleZone(zone, e.Location, notches, _wheelWithControl);
        }
        else if (bars is { } kept && this.EditsCrop(index))
        {
            this.ZoomAt(index, e.Location, notches, _wheelWithControl, KeptPartPoint(kept, e.Location));
        }
        else
        {
            this.ZoomAt(index, e.Location, notches, _wheelWithControl);
        }

        _wheelEnd.Start();
    }

    // The capture is released right after OnMouseUp; losing it earlier (Alt+Tab, a dialog) cancels the drag.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        this.EndLayerPress();
        if (_pressed >= 0)
        {
            EndDrag();
        }

        if (_draggedBar is not null || this._draggedCorner is not null || _movedCrop is not null)
        {
            _draggedBar = null;
            this._draggedCorner = null;
            _movedCrop = null;
            EndLive();
            UpdateRatio();
            Invalidate();
        }

        EndSeparatorDrag();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (this._eyedropper is not null)
        {
            this.HoverEyedropper(null);
            return;
        }

        if ((_hovered >= 0 || _hoveringCanvas || _hoveringDropZone) && !_dragging && !_panning && _draggedBar is null && this._draggedCorner is null
            && _draggedSeparator is null)
        {
            _hovered = -1;
            _hoveredBar = null;
            this._hoveredCorner = null;
            _hoveredSeparator = null;
            _hoveringClose = false;
            _hoveringHandle = false;
            _hoveringCanvas = false;
            _hoveringDropZone = false;
            SetSourceHover((false, false));
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        this.CancelEyedropper();
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
    }

    /// <summary>Raised when the grid is paused, resumes, is held or released, or its time jumps.</summary>
    public event EventHandler? PlaybackChanged;

    /// <summary>The grid paused by the user, from the transport; kept while the window is hidden.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PlaybackPaused
    {
        get => this._player.Paused;
        set => this._player.Paused = value;
    }

    /// <summary>The length of the timeline: the grid's loop, zero without one.</summary>
    public TimeSpan PlaybackLength => this._player.LoopLength;

    /// <summary>Where the grid stands in its loop.</summary>
    public TimeSpan PlaybackPosition => this._player.LoopPosition;

    /// <summary>Stands the grid still — a Copy, an export, a drag of the timeline — until <see cref="ReleasePlayback"/>.</summary>
    public void HoldPlayback() => this._player.Hold();

    /// <summary>Ends one <see cref="HoldPlayback"/>: the grid plays again unless paused.</summary>
    public void ReleasePlayback() => this._player.Release();

    /// <summary>
    /// Holds the grid and closes its readers for a Copy or an export, so the export's own readers are the
    /// only ones open; the cells keep their frames (workfiles/20261010-optimisation-lack-of-memory.md § 2.1).
    /// </summary>
    public void SuspendPlayback() => this._player.Suspend();

    /// <summary>Ends a <see cref="SuspendPlayback"/>: the readers open again where the grid stood, and it plays on unless paused.</summary>
    public void ResumePlayback() => this._player.Resume();

    /// <summary>
    /// Moves the grid to <paramref name="time"/> of its loop; while the timeline is dragged
    /// (<paramref name="scrub"/>), the mix heard there sounds for a short snippet.
    /// </summary>
    public void SeekPlayback(TimeSpan time, bool scrub)
    {
        if (scrub)
        {
            this._player.Scrub(time);
        }
        else
        {
            this._player.Seek(time);
        }
    }

    /// <summary>The cells moving on the grid's clock are drawn again at its new time; the progress lines follow.</summary>
    private void OnPlaybackChanged()
    {
        this._motionDrawn = TimeSpan.MaxValue;
        this._progressTimer.Start();

        // The grid just paused, held or stopped: its cells were drawn fast while it ran.
        bool running = this._player.Running;
        if (this._wasRunning && !running)
        {
            this.RedrawAnimatedInFull();
        }

        this._wasRunning = running;
        this.PlaybackChanged?.Invoke(this, EventArgs.Empty);
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
        bool dragging = _dragging;
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
        if (dragging)
        {
            SwapDragMoved?.Invoke(this, null);
        }
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
        int cell = CellAt(location);
        if (cell >= 0)
        {
            return cells[cell];
        }

        return _images.Count < GridLayout.MaxImages ? CanvasBounds() : cells[ExcessTarget()];
    }

    /// <summary>Surface under <paramref name="location"/> a click opens the Add images picker from: the drop zone, the empty canvas, else none.</summary>
    private Rectangle PickerAt(Point location)
    {
        var canvas = CanvasBounds();
        var zone = DropZoneBounds(canvas);
        return zone.Contains(location) ? zone
            : _images.Count == 0 && canvas.Contains(location) ? canvas
            : Rectangle.Empty;
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
            int cell = CellAt(location);
            return cell >= 0 ? CellBounds()[cell] : Rectangle.Empty;
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
        var removed = this._images[index];
        _images.RemoveAt(index);
        this.ReleaseImage(removed);

        // The images after it move into another cell: effects belong to the cell and its image (RULES.md).
        for (int i = index; i < _images.Count; i++)
        {
            _images[i].Look = _images[i].Look.WithoutEffects();
        }

        Select(-1);
        _hovered = -1;
        _hoveringClose = false;
        _hoveringHandle = false;
        this.OnImagesChanged(this._images.Skip(index).ToList());

        // A deletion starts the grid over, like an arrival (RULES.md).
        _player.Restart();
    }

    private void Replace(int index, SourceImage image)
    {
        var replaced = this._images[index];
        _images[index] = image;
        this.ReleaseImage(replaced);
    }

    private void Swap(int a, int b)
    {
        (_images[a], _images[b]) = (_images[b], _images[a]);
        Select(b);
        OnImagesChanged();
    }

    /// <summary>
    /// The images, their count or their order changed. <paramref name="arrived"/>, given by an arrival
    /// or a deletion, are the images new in their cell: once the layout and the ratio settle, they are
    /// shown whole, and so is every image whose zoom is still its arrival zoom.
    /// </summary>
    private void OnImagesChanged(IReadOnlyCollection<SourceImage>? arrived = null)
    {
        // The snapshot no longer shows the grid.
        this.CancelEyedropper();
        _cache?.Dispose();
        _cache = null;
        Invalidate();
        ContentVersion++;

        // A new image count starts on its default layout, mirror off.
        int? count = _images.Count == 0 ? null : _images.Count;
        bool layoutReset = _layout?.Count != count;
        if (layoutReset)
        {
            _layout = count is { } n ? GridLayout.Default(n) : null;
        }

        FitPagesToCells();
        SyncPlayer();
        UpdateRatio();
        if (arrived is not null)
        {
            this.ShowWhole(arrived);
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
        bool onDropZone = DropZoneBounds(CanvasBounds()).Contains(location);
        bool actions = hovered >= 0 && !_locked;
        bool onHandle = actions && this.HandleOf(hovered).Contains(location);
        var onSource = SourceHitAt(location);
        bool onControl = onClose || onDropZone || onCanvas || onSource.Icon;
        var onCorner = actions && !onControl && this.ShownBars(hovered) is { } framed
            ? this.CornerAt(this.CellBounds()[hovered], framed, location)
            : null;
        var onBar = actions && !onControl && onCorner is null && ShownBars(hovered) is { } bars ? BarAt(CellBounds()[hovered], bars, location) : null;
        bool onKept = actions && !onControl && onCorner is null && onBar is null && KeptPartAt(hovered, location);
        var onSeparator = actions && !onControl && onCorner is null && onBar is null && !onKept ? SeparatorAt(location) : null;
        if (hovered == _hovered && onClose == _hoveringClose && onCanvas == _hoveringCanvas && onDropZone == _hoveringDropZone
            && onHandle == _hoveringHandle && onCorner == this._hoveredCorner && onBar == _hoveredBar && onKept == _hoveringKept
            && onSeparator?.Vertical == _hoveredSeparator?.Vertical && onSource == (_hoveringSourceName, _hoveringSourceIcon))
        {
            return;
        }

        SetSourceHover(onSource);

        _hovered = hovered;
        _hoveringClose = onClose;
        _hoveringCanvas = onCanvas;
        _hoveringDropZone = onDropZone;
        _hoveringHandle = onHandle;
        this._hoveredCorner = onCorner;
        _hoveredBar = onBar;
        _hoveringKept = onKept;
        _hoveredSeparator = onSeparator;
        Cursor = onControl ? Cursors.Hand
            : onCorner is { } corner ? CornerCursor(corner)
            : onBar is { } bar ? BarCursor(bar)
            : onKept ? Cursors.SizeAll
            : onSeparator is { } separator ? SeparatorCursor(separator)
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

        _player.Sync(_images, this.ShownLayerImages);
        UpdateDisplaySizes();
        _progressTimer.Start();
    }

    private void UpdateDisplaySizes()
    {
        var cells = CellBounds();
        for (int i = 0; i < cells.Length && i < _images.Count; i++)
        {
            _player.SetDisplaySize(_images[i], FrameDisplaySize(_images[i], cells[i]));
        }

        this.UpdateLayerDisplaySizes();
    }

    /// <summary>
    /// Size an animated frame is decoded at to be drawn about 1:1: the cell, in the frame's own
    /// orientation, enlarged by a zoom in, and by a crop keeping only a part of the frame.
    /// </summary>
    private static Size FrameDisplaySize(SourceImage image, Rectangle cell)
    {
        var size = image.Look.Oriented(cell.Size);
        double zoom = Math.Max(1, image.Look.ZoomIn(cell, image.Look.Shown(image.Bitmap.Size)) * (image.Look.Motion is null ? 1 : 1 + MotionEffect.ZoomAmplitude));
        var crop = image.Look.Crop;
        double width = size.Width * zoom / (crop is null ? 1 : Math.Max(0.01, crop.Right - crop.Left));
        double height = size.Height * zoom / (crop is null ? 1 : Math.Max(0.01, crop.Bottom - crop.Top));
        return new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height));
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
        if (this._wheelHoldsRatio)
        {
            this._wheelHoldsRatio = false;
            this.UpdateRatio();
        }

        this.EndLayerWheel();
        if (_live is not { } image)
        {
            return;
        }

        _live = null;
        UpdateDisplaySizes();
        RedrawCell(image);
    }

    /// <summary>
    /// The wheel's burst on a layer rests: the layer drawn fast meanwhile is drawn again in full. Called by
    /// <see cref="EndLive"/>, which the wheel's timer ends with.
    /// </summary>
    private void EndLayerWheel()
    {
        if (this._layerWheeled)
        {
            this._layerWheeled = false;
            this.UpdateDisplaySizes();
            this.RedrawLayerArea(this.SelectedLayer, fast: false);
        }
    }

    /// <summary>What the cell of <paramref name="image"/> shows now: its frame, its motion at the grid's time.</summary>
    private Frame FrameOf(SourceImage image) => new(image.Bitmap, image.BandColor, image.DrawnLook, this._player.GridTime, image.Sharpening);

    /// <summary>Draws the new frame of an image into the cached preview, and repaints only its cell.</summary>
    private void RedrawCell(SourceImage image)
    {
        int index = _images.IndexOf(image);

        // A layer's content: its area drawn again, the cells under it and the layers over them.
        if (index < 0 && this._layersShown && this.LayerOf(image) is { } layer)
        {
            this.RedrawLayerArea(layer, fast: this._player.Running || this._eyedropper is not null);
            return;
        }

        var canvas = CanvasBounds();
        if (index < 0 || _layout is null || _cache is null || _cache.Size != canvas.Size)
        {
            Invalidate();
            return;
        }

        // Fast while the grid runs — a frame arrives about 1:1, where bilinear comes close to the full
        // render — and in full once it pauses or stops (OnPlaybackChanged); the exports keep the full render.
        bool live = image == _live;
        var area = this.DrawIntoCache([index], canvas.Size, fast: live || this._player.Running || this._eyedropper is not null);
        area.Offset(canvas.Location);
        Invalidate(area);
        if (live)
        {
            Update();
        }
    }

    /// <summary>The grid paused, held or stopped: the animated cells, drawn fast while it ran, are drawn in full once.</summary>
    private void RedrawAnimatedInFull()
    {
        var canvas = CanvasBounds();
        if (_layout is null || _cache is null || _cache.Size != canvas.Size)
        {
            Invalidate();
            return;
        }

        int[] indices = [.. Enumerable.Range(0, _images.Count).Where(i => _images[i].IsAnimated)];
        if (indices.Length == 0)
        {
            return;
        }

        var area = this.DrawIntoCache(indices, canvas.Size, fast: false);
        area.Offset(canvas.Location);
        Invalidate(area);
    }

    /// <summary>What the borders draw over the images (the corner brackets), repainted within a cell just drawn again.</summary>
    private void DrawBordersOver(Graphics g, Rectangle cell, Size canvas)
    {
        if (_borders is null)
        {
            return;
        }

        g.SetClip(cell);
        _borders.DrawOver(g, canvas);
        g.ResetClip();
    }

    /// <summary>A cell drawn again into the cache starts transparent: without background, its previous frame would show through.</summary>
    private static void ClearCell(Graphics g, Rectangle cell)
    {
        g.SetClip(cell);
        g.Clear(Color.Transparent);
        g.ResetClip();
    }

    /// <summary>
    /// Draws the cells <paramref name="indices"/> again into the cached preview — every cell when a fill
    /// the seams fade has changed since they were drawn, so the neighbours' strips follow it — and
    /// returns the area drawn, in the canvas.
    /// </summary>
    private Rectangle DrawIntoCache(int[] indices, Size canvas, bool fast)
    {
        var cells = Compositor.Cells(this._layout!, canvas, this._borders);
        var seams = this.SeamsOf(cells);
        var drawn = indices.Where(i => i < this._images.Count).ToList();
        if (seams is not null && !SameFills(seams.Fills, this._seamFills))
        {
            drawn = [.. Enumerable.Range(0, Math.Min(this._images.Count, cells.Length))];
        }

        this._seamFills = seams?.Fills;
        var area = Rectangle.Empty;
        using (var g = Graphics.FromImage(this._cache!))
        {
            foreach (int i in drawn)
            {
                ClearCell(g, cells[i]);
                Compositor.DrawCell(g, this.FrameOf(this._images[i]), cells[i], fast, seams);
                this.DrawBordersOver(g, cells[i], canvas);
                area = area.IsEmpty ? cells[i] : Rectangle.Union(area, cells[i]);
            }

            // The layers over the cells drawn again, within them: the others keep theirs.
            if (this.ShownLayers is not null && !area.IsEmpty)
            {
                foreach (int i in drawn)
                {
                    g.SetClip(cells[i]);
                    this.DrawLayersInto(g, canvas, fast);
                }

                g.ResetClip();
            }
        }

        // Each cell cut on its own: the union may cover cells not drawn again, already cut.
        foreach (int i in drawn)
        {
            this._borders?.CutCorners(this._cache!, cells[i]);
        }

        return area;
    }

    /// <summary>The seams of the grid as it plays now, on <paramref name="cells"/>; <c>null</c> when none fades.</summary>
    private SeamField? SeamsOf(Rectangle[] cells) =>
        this._seams is null ? null : SeamField.Of([.. this._images.Select(this.FrameOf)], cells, this._seams, this._borders);

    /// <summary>Whether two lists of fills are the same colors, compared by value.</summary>
    private static bool SameFills(IReadOnlyList<Color?> fills, IReadOnlyList<Color?>? others) =>
        others is not null && fills.Count == others.Count && fills.Zip(others).All(p => p.First?.ToArgb() == p.Second?.ToArgb());

    /// <summary>
    /// The grey and white squares of drawing apps under the whole grid, 8 logical px each: they show
    /// through wherever a cell has no background, or a partly transparent one. Never exported.
    /// </summary>
    private void PaintCheckerboard(Graphics g, Rectangle canvas)
    {
        int square = LogicalToDeviceUnits(CheckerSquare);
        if (_checkerboard is null || _checkerTile?.Width != 2 * square)
        {
            _checkerboard?.Dispose();
            _checkerTile?.Dispose();
            _checkerTile = new Bitmap(2 * square, 2 * square);
            using (var tile = Graphics.FromImage(_checkerTile))
            {
                using var grey = new SolidBrush(Checkerboard.Grey);
                tile.Clear(Color.White);
                tile.FillRectangle(grey, square, 0, square, square);
                tile.FillRectangle(grey, 0, square, square, square);
            }

            _checkerboard = new TextureBrush(_checkerTile);
        }

        // Anchored on the canvas, so the squares do not slide when the window is resized.
        _checkerboard.ResetTransform();
        _checkerboard.TranslateTransform(canvas.X, canvas.Y);

        // Within the Twitter corners, so the rounded-off corners show the background, as on Twitter / X.
        float radius = _borders?.Radius(canvas.Size) ?? 0f;
        if (radius <= 0)
        {
            g.FillRectangle(_checkerboard, canvas);
            return;
        }

        using var outline = GridBorders.RoundedRectangle(canvas, radius);
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(_checkerboard, outline);
        g.SmoothingMode = smoothing;
    }

    /// <summary>
    /// The automatic background color of the selected image in its cell, as the preview draws it: what
    /// the Background's color button shows while <i>Automatic color</i> is checked, and what unchecking
    /// it freezes. <c>null</c> with no cell selected.
    /// </summary>
    public Color? SelectedAutomaticBackground
    {
        get
        {
            if (this.SelectedLayer is { } layer && this.SelectedLayerCell is { } box)
            {
                return Compositor.AutomaticBackground(layer.FrameAt(this._player.GridTime), box);
            }

            var cells = CellBounds();
            if (SelectedCellImage is not { } image || _selected >= cells.Length)
            {
                return null;
            }

            return Compositor.AutomaticBackground(this.FrameOf(image), cells[_selected]);
        }
    }

    /// <summary>
    /// Largest rectangle at the canvas ratio that fits the control once the drop zone and its gap
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
        int height = GridLayout.HeightFor(width, _ratio);
        if (height > area.Height)
        {
            height = area.Height;
            width = Math.Min(area.Width, GridLayout.WidthFor(height, _ratio));
        }

        return new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }

    /// <summary>Strip right of the canvas, as high as it, where dropped or picked files are added.</summary>
    private Rectangle DropZoneBounds(Rectangle canvas) =>
        canvas.IsEmpty
            ? Rectangle.Empty
            : new Rectangle(canvas.Right + LogicalToDeviceUnits(CanvasMargin), canvas.Y, LogicalToDeviceUnits(DropZoneWidth), canvas.Height);

    /// <summary>The cells as drawn, shrunk by the gap of the borders: where the images, outlines and handles go.</summary>
    private Rectangle[] CellBounds() => OnCanvas(canvas => Compositor.Cells(_layout!, canvas, _borders));

    /// <summary>The cells as the layout tiles the canvas, gap included: what hit-testing uses, so a gap is never a dead zone.</summary>
    private Rectangle[] SlotBounds() => OnCanvas(canvas => _layout!.Cells(canvas));

    private Rectangle[] OnCanvas(Func<Size, Rectangle[]> cellsOf)
    {
        var canvas = CanvasBounds();
        if (_layout is null || canvas.IsEmpty)
        {
            return [];
        }

        var cells = cellsOf(canvas.Size);
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
    /// The ✥ handle of cell <paramref name="index"/>; empty — neither drawn nor grabbed — while its
    /// position readout shows, so a press at the center keeps moving the image.
    /// </summary>
    private Rectangle HandleOf(int index) =>
        index < this._images.Count && this._readouts.ContainsKey(this._images[index]) ? Rectangle.Empty : this.HandleBounds(this.CellBounds()[index]);

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
    /// Text pages take the shape of their cell, at its size on the smallest canvas
    /// (<see cref="CanvasSizer.Smallest"/>), so they never enlarge the canvas: laid out again, keeping the
    /// reading position, whenever that cell changes — the canvas's ratio included. A text turned a
    /// quarter takes the turned shape, so it still fills its cell once rotated.
    /// The zoom has its say too (workfiles/20260926-text-zoom-and-frames.md): a text is laid out for
    /// its zoom — at 100 % while cropped — and a page that can be rendered sharper (a PDF, a thumbnail,
    /// a cropped text) is rendered at the resolution its drawn size asks there, so a zoom stays sharp.
    /// </summary>
    private void FitPagesToCells()
    {
        if (_layout is null)
        {
            return;
        }

        var cells = Compositor.Cells(_layout, CanvasSizer.Smallest(_ratio), _borders);
        for (int i = 0; i < _images.Count; i++)
        {
            var image = this._images[i];
            if (image.Pages is not { } pages)
            {
                continue;
            }

            int page = this._pageLoader.Target(image);
            bool changed = false;
            var shape = image.Look.Oriented(cells[i].Size);
            if (pages.PageSize is { } size)
            {
                double zoom = image.LaysOutZoom ? image.Look.ZoomIn(cells[i], image.Look.Shown(shape)) : 1;
                if (size != shape || zoom != pages.LayoutZoom)
                {
                    page = pages.Resize(shape, zoom, page);
                    changed = true;
                }
            }

            if (pages.Sharpens)
            {
                changed |= pages.Sharpen(SharpSide(cells[i], image));
            }

            if (changed)
            {
                // A frozen text stays about where its frames effect points, on the new pages.
                this._pageLoader.Request(image, image.IsFrozen ? image.StartPage : page);
                this._player.Refresh(image);
            }
        }
    }

    /// <summary>A document's look rested: its pages follow it, unless a gesture still runs — then at the next tick.</summary>
    private void OnPagesFitTick()
    {
        if (this.InGesture)
        {
            return;
        }

        this._pagesFit.Stop();
        this.FitPagesToCells();
    }

    /// <summary>
    /// The long side, in pixels, <paramref name="image"/>'s bitmap needs to be drawn 1:1 in
    /// <paramref name="cell"/> as it is drawn now: its own, times the scale it is drawn at there —
    /// more than its own once zoomed in.
    /// </summary>
    private static int SharpSide(Rectangle cell, SourceImage image)
    {
        var look = image.DrawnLook;
        var shown = look.Shown(image.Bitmap.Size);
        var bounds = FitCalculator.ComputeTurned(cell, shown, look.ZoomIn(cell, shown), look.Focus, look.FineAngle).Bounds;
        double scale = Math.Max(bounds.Width / Math.Max(1, shown.Width), bounds.Height / Math.Max(1, shown.Height));
        return (int)Math.Ceiling(Math.Max(image.Bitmap.Width, image.Bitmap.Height) * scale);
    }

    /// <summary>
    /// The frames effect of an image changed: the player plays it from its new starting point, and a
    /// frozen image shows the page the effect points at.
    /// </summary>
    private void ShowFrames(SourceImage image)
    {
        _player.Update(image);
        _progressTimer.Start();
        if (image.IsFrozen && image.IsAnimated && _pageLoader.Target(image) != image.StartPage)
        {
            _pageLoader.Request(image, image.StartPage);
        }
    }

    /// <summary>
    /// Gives <paramref name="arrived"/>, and every image whose zoom is still its arrival zoom, the zoom
    /// <see cref="ZoomFit.Contain"/> gives in its cell, as a free zoom (<see cref="ImageLook.WithArrivalZoom"/>):
    /// the whole image shown, centered in its cell when <paramref name="centered"/>, its focus kept otherwise.
    /// A text laying its zoom out keeps filling its cell, at 100 %; an image arriving with a zoom of its
    /// own keeps it — but not on the layout strip's Auto (<paramref name="centered"/>), which replaces
    /// every image's own zoom and position.
    /// </summary>
    private void ShowWhole(IReadOnlyCollection<SourceImage> arrived, bool centered = false)
    {
        var cells = this.CellBounds();
        for (int i = 0; i < this._images.Count && i < cells.Length; i++)
        {
            var image = this._images[i];
            // An image arriving with a zoom of its own — an effect preset's — keeps it; Auto replaces it.
            if (image.LaysOutZoom || !(arrived.Contains(image) || image.Look.ZoomOnArrival)
                || (!centered && image.Look.IsActive(ImageEffect.Zoom) && !image.Look.ZoomOnArrival))
            {
                continue;
            }

            var look = image.Look;
            double zoom = FitCalculator.FitZoom(cells[i], look.Shown(image.Bitmap.Size), ZoomFit.Contain);
            this.SetLook(i, centered ? look.WithArrivalZoomCentered(zoom) : look.WithArrivalZoom(zoom));
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
        ContentVersion++;
        if (turned)
        {
            FitPagesToCells();
        }
        else if (image.Pages is { } pages && (pages.Sharpens || pages.LayoutZoom is not null))
        {
            // A zoom, a crop or a fine angle may ask for other pages: laid out or rendered once it rests.
            this._pagesFit.Stop();
            this._pagesFit.Start();
        }

        // A crop or a quarter turn changes the image's shape, so the free ratio.
        UpdateRatio();

        if (frames)
        {
            ShowFrames(image);
        }

        // A moving image is drawn again by the progress lines' timer.
        if (look.Motion is not null)
        {
            this._progressTimer.Start();
        }

        // During a gesture, the frame shown is scaled; decoding it again at each step would only slow it down.
        if (image != _live)
        {
            UpdateDisplaySizes();
        }

        this.SyncEditedLayer(image);
        RedrawCell(image);
        if (index == _selected)
        {
            SelectedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sets the zoom of the selected image, from the options of the zoom effect: shown fast while the
    /// slider moves, in full once it rests. A zoom keeps the image's center where it is.
    /// </summary>
    public void ZoomSelected(double zoom)
    {
        // A layer's box stands for its cell (workfiles/20261029-layers.md § Cell Effects on a Layer).
        if (this.SelectedLayer is { } layer && this.SelectedLayerCell is { } box)
        {
            if (!this._locked)
            {
                var zoomedLayer = layer.Look.WithZoom(zoom);
                var shown = zoomedLayer.Shown(layer.Image.Bitmap.Size);
                this.SetLayerLook(zoomedLayer.WithFocus(FitCalculator.FocusKeeping(box, shown, layer.Look.ZoomIn(box, shown), zoomedLayer.Zoom, layer.Look.Focus, layer.Look.FineAngle, FitCalculator.ImageCenter)));
            }

            return;
        }

        var cells = CellBounds();
        if (_selected < 0 || _selected >= cells.Length || _locked)
        {
            return;
        }

        var image = _images[_selected];
        var look = image.Look;
        var cell = cells[_selected];
        var zoomed = look.WithZoom(zoom);
        var size = zoomed.Shown(image.Bitmap.Size);
        BeginLive(_selected);
        SetLook(_selected, zoomed.WithFocus(FitCalculator.FocusKeeping(cell, size, look.ZoomIn(cell, size), zoomed.Zoom, look.Focus, look.FineAngle, FitCalculator.ImageCenter)));
        ShowZoomBadge(image);
        _wheelEnd.Start();
    }
    /// <summary>
    /// Puts the selected image in a fit mode, from the options of the zoom effect, the image recentered
    /// in its cell — once, at the press: it can still be moved afterwards; <see cref="ZoomFit.None"/>
    /// leaves the mode for the free zoom it gave in the cell, so nothing moves.
    /// </summary>
    public void FitSelected(ZoomFit fit)
    {
        bool onLayer = this.SelectedLayer is not null;
        var cells = this.CellBounds();
        if (onLayer ? this.SelectedLayerCell is null : this._selected < 0 || this._selected >= cells.Length || this._locked)
        {
            return;
        }

        var image = this.SelectedImage!;
        var cell = onLayer ? this.SelectedLayerCell!.Value : cells[this._selected];
        var look = image.Look;
        var fitted = fit == ZoomFit.None ? look.WithZoom(ZoomOf(image, cell)) : look.WithZoomFit(fit);
        var size = fitted.Shown(image.Bitmap.Size);
        var focus = fit == ZoomFit.None
            ? FitCalculator.FocusKeeping(cell, size, look.ZoomIn(cell, size), fitted.ZoomIn(cell, size), look.Focus, look.FineAngle, FitCalculator.ImageCenter)
            : FitCalculator.ImageCenter;
        if (onLayer)
        {
            this.SetLayerLook(fitted.WithFocus(focus));
            return;
        }

        this.SetLook(this._selected, fitted.WithFocus(focus));
        this.ShowZoomBadge(image);
    }

    /// <summary>
    /// The zoom <paramref name="look"/> — the selected image's own by default — draws the selected image at in
    /// its cell, or the selected layer's content in its box; 1 with none selected.
    /// </summary>
    public double SelectedZoom(ImageLook? look = null)
    {
        if (this.SelectedLayer is { } layer && this.SelectedLayerCell is { } box)
        {
            return ZoomOf(layer.Image, box, look);
        }

        var cells = this.CellBounds();
        return this.SelectedCellImage is { } image && this._selected < cells.Length ? ZoomOf(image, cells[this._selected], look) : 1;
    }

    /// <summary>The zoom <paramref name="look"/> — the image's own by default — draws <paramref name="image"/> at in <paramref name="cell"/>: its fit mode's there, else its free zoom.</summary>
    private static double ZoomOf(SourceImage image, Rectangle cell, ImageLook? look = null)
    {
        look ??= image.Look;
        return look.ZoomIn(cell, look.Shown(image.Bitmap.Size));
    }

    /// <summary>
    /// Zooms by <paramref name="notches"/> of the wheel, each moving the zoom onto the next multiple of
    /// its step — the finer one when <paramref name="fine"/> (<see cref="WheelSteps.Zoom"/>) —
    /// keeping the point of the image under <paramref name="location"/> where it is drawn — or the point
    /// <paramref name="at"/>, in fractions of the image shown (<see cref="FitCalculator.FocusKeeping"/>);
    /// lands on 100 % when crossing it.
    /// </summary>
    private void ZoomAt(int index, Point location, int notches, bool fine, PointF? at = null)
    {
        var cells = CellBounds();
        if (index >= cells.Length)
        {
            return;
        }

        var image = _images[index];
        var look = image.Look;
        var cell = cells[index];
        var size = look.Shown(image.Bitmap.Size);

        // From the zoom shown: a fit mode is left for the free zoom it gives in this cell.
        double current = look.ZoomIn(cell, size);
        double zoom = WheelSteps.Zoom(current * 100, notches, fine) / 100;
        if ((current - 1) * (zoom - 1) < 0)
        {
            zoom = 1;
        }

        var zoomed = look.WithZoom(zoom);
        var point = at ?? FitCalculator.ImagePointAt(cell, size, current, look.Focus, look.FineAngle, location);
        SetLook(index, zoomed.WithFocus(FitCalculator.FocusKeeping(cell, size, current, zoomed.Zoom, look.Focus, look.FineAngle, point)));
        ShowZoomBadge(image);
    }

    /// <summary>
    /// Shows the zoom percentage of an image over its cell — at full opacity for <see cref="ZoomBadgeHold"/>
    /// after each change, then fading out; a bound reached shows too, the zoom staying on it.
    /// </summary>
    private void ShowZoomBadge(SourceImage image)
    {
        InvalidateZoomBadge();
        this.InvalidateReadoutOf(image);
        _zoomBadgeImage = image;
        _zoomBadgeChanged = Environment.TickCount64;
        _zoomBadgeTimer.Stop();
        _zoomBadgeTimer.Interval = ZoomBadgeHold;
        _zoomBadgeTimer.Start();
        InvalidateZoomBadge();
    }

    /// <summary>Waits out the hold in one tick, then repaints the badge along its fade, and drops it once transparent.</summary>
    private void OnZoomBadgeTick()
    {
        long elapsed = Environment.TickCount64 - _zoomBadgeChanged;
        if (elapsed >= ZoomBadgeHold + ZoomBadgeFade)
        {
            _zoomBadgeTimer.Stop();
            InvalidateZoomBadge();
            this.InvalidateReadoutOf(_zoomBadgeImage);
            _zoomBadgeImage = null;
            return;
        }

        _zoomBadgeTimer.Interval = elapsed < ZoomBadgeHold ? (int)(ZoomBadgeHold - elapsed) : ZoomBadgeTick;
        if (elapsed >= ZoomBadgeHold)
        {
            InvalidateZoomBadge();
        }
    }

    /// <summary>Repaints where the badge was last painted, and where it is now — its text may have changed width.</summary>
    private void InvalidateZoomBadge()
    {
        if (!_zoomBadgeBounds.IsEmpty)
        {
            Invalidate(_zoomBadgeBounds);
        }

        using var path = ZoomBadgePath(out _);
        if (path is not null)
        {
            Invalidate(ZoomBadgeBounds(path));
        }
    }

    /// <summary>
    /// Repaints the progress line of every playing cell — its strip only, and the strips of the last
    /// tick, so a line that just vanished is erased — and draws again the cells whose Animations effect
    /// is on, at the export's frame rate; stops once nothing plays nor moves, the cascade's pauses aside. The next sync of the
    /// player, a frames effect changed, or a motion turned on starts it again.
    /// </summary>
    private void OnProgressTick()
    {
        var now = this._player.GridTime;
        if (now < this._motionDrawn || now - this._motionDrawn >= MotionInterval)
        {
            this._motionDrawn = now;
            foreach (var image in this._images.Where(i => i.Look.Motion is not null).ToList())
            {
                this.RedrawCell(image);
            }

            foreach (var layer in this.ShownLayers?.Items.Where(l => l.Look.Motion is not null).ToList() ?? [])
            {
                this.RedrawCell(layer.Image);
            }
        }

        foreach (var strip in _progressStrips)
        {
            Invalidate(strip);
        }

        _progressStrips.Clear();
        var cells = CellBounds();
        for (int i = 0; i < cells.Length && i < _images.Count; i++)
        {
            if (_player.ProgressOf(_images[i]) is not null)
            {
                var strip = ProgressStrip(cells[i]);
                Invalidate(strip);
                _progressStrips.Add(strip);
            }
        }

        // In the cascade, the next turn draws its line after a pause where none shows. The grid
        // paused, this tick drew it where it stands: the next ones would draw the same.
        if ((_progressStrips.Count == 0 && !this._player.Cascading) || !this._player.Running)
        {
            _progressTimer.Stop();
        }
    }

    /// <summary>
    /// The band a cell's progress line is drawn in: the halo's height, across the cell, its lower edge
    /// on the inner edge of the selection outline, so neither the outline nor the hover band covers it,
    /// and the file name of the selected cell, ending 6 px above the edge, sits right on top of it.
    /// </summary>
    private Rectangle ProgressStrip(Rectangle cell)
    {
        int halo = LogicalToDeviceUnits(ProgressHaloWidth);
        int bottom = cell.Bottom - LogicalToDeviceUnits(SelectionWidth);
        return new Rectangle(cell.Left, bottom - halo, cell.Width, halo);
    }

    /// <summary>
    /// The text of the zoom badge, right-aligned just below the × of the cell showing its image;
    /// <c>null</c> when there is none, or its image is no longer shown.
    /// </summary>
    private GraphicsPath? ZoomBadgePath(out Rectangle cell)
    {
        cell = Rectangle.Empty;
        var cells = CellBounds();
        int index = _zoomBadgeImage is null ? -1 : _images.IndexOf(_zoomBadgeImage);
        if (index < 0 || index >= cells.Length)
        {
            return null;
        }

        cell = cells[index];
        var close = CloseBounds(cell);
        var origin = new PointF(close.Right, close.Bottom + LogicalToDeviceUnits(ButtonGap));
        using var format = new StringFormat { Alignment = StringAlignment.Far };
        var path = new GraphicsPath();
        path.AddString($"{ZoomOf(_zoomBadgeImage!, cell) * 100:0} %", Font.FontFamily, (int)FontStyle.Bold, LogicalToDeviceUnits(ZoomBadgeTextSize), origin, format);
        return path;
    }

    private Rectangle ZoomBadgeBounds(GraphicsPath path)
    {
        int halo = LogicalToDeviceUnits(4);
        return Rectangle.Inflate(Rectangle.Ceiling(path.GetBounds()), halo, halo);
    }

    /// <summary>The zoom badge, a helper indicator: green over the black halo, faded out after its hold.</summary>
    private void PaintZoomBadge(Graphics g)
    {
        using var path = ZoomBadgePath(out var cell);
        if (path is null)
        {
            _zoomBadgeBounds = Rectangle.Empty;
            return;
        }

        _zoomBadgeBounds = ZoomBadgeBounds(path);
        long elapsed = Environment.TickCount64 - _zoomBadgeChanged;
        double opacity = Math.Clamp(1 - (elapsed - ZoomBadgeHold) / (double)ZoomBadgeFade, 0, 1);
        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        using (var outline = new Pen(Color.FromArgb((int)(HelperHalo.A * opacity), HelperHalo), LogicalToDeviceUnits(4)) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(outline, path);
        }

        using (var fill = new SolidBrush(Color.FromArgb((int)(255 * opacity), HelperColor)))
        {
            g.FillPath(fill, path);
        }

        g.Restore(state);
    }

    /// <summary>
    /// Every image's offset from its cell's center in the PNG export of the grid as it stands — its canvas
    /// and cells as <see cref="Compositor.Render(IReadOnlyList{Frame}, GridLayout, double, GridBorders?, SeamFade?, int)"/>
    /// gives them —, x to the right, y downward; <c>null</c> without a layout holding the images.
    /// </summary>
    private Point[]? ExportOffsets()
    {
        if (this._layout is not { } layout || layout.Count != this._images.Count)
        {
            return null;
        }

        var canvas = CanvasSizer.Compute(this._images.Select(i => i.Sizing).ToList(), layout, this.CanvasRatio, this._borders);
        var cells = Compositor.Cells(layout, canvas, this._borders);
        return this._images.Select((image, i) => CenterOffset(cells[i], image)).ToArray();
    }

    /// <summary>Where the center of the box <paramref name="image"/> is drawn in lies from the center of <paramref name="cell"/>, rounded.</summary>
    private static Point CenterOffset(Rectangle cell, SourceImage image)
    {
        var bounds = DrawnBounds(cell, image);
        return new Point(
            (int)Math.Round(bounds.X + bounds.Width / 2 - (cell.X + cell.Width / 2.0)),
            (int)Math.Round(bounds.Y + bounds.Height / 2 - (cell.Y + cell.Height / 2.0)));
    }

    /// <summary>The box <paramref name="image"/> is drawn in within <paramref name="cell"/>: the turned image's with a fine angle.</summary>
    private static RectangleF DrawnBounds(Rectangle cell, SourceImage image)
    {
        var look = image.DrawnLook;
        var shown = look.Shown(image.Bitmap.Size);
        return FitCalculator.ComputeTurned(cell, shown, look.ZoomIn(cell, shown), look.Focus, look.FineAngle).Bounds;
    }

    /// <summary>
    /// Compares every image's export offset with the one last painted, and shows the readout of each one
    /// whose offset changed while it stayed in its cell, whatever changed it. An image new in its cell, or
    /// in a cell showing the crop edit view, takes its offset as the new starting value and shows none.
    /// </summary>
    private void UpdateReadouts()
    {
        var offsets = this.ExportOffsets();
        var seen = new Dictionary<SourceImage, (int Cell, Point Offset)>();
        var cells = offsets is null ? Array.Empty<Rectangle>() : this.CellBounds();
        bool shown = false;
        for (int i = 0; offsets is not null && i < this._images.Count; i++)
        {
            var image = this._images[i];
            bool stayed = this._offsets.TryGetValue(image, out var last) && last.Cell == i;
            if (!stayed || this.EditsCrop(i))
            {
                this._readouts.Remove(image);
            }
            else if (last.Offset != offsets[i])
            {
                this._readouts[image] = Environment.TickCount64;
                shown = true;
                if (i < cells.Length)
                {
                    this.Invalidate(cells[i]);
                }
            }

            seen[image] = (i, offsets[i]);
        }

        this._offsets = seen;
        foreach (var gone in this._readouts.Keys.Where(image => !seen.ContainsKey(image)).ToList())
        {
            this._readouts.Remove(gone);
        }

        if (shown)
        {
            this.ScheduleReadouts();
        }
    }

    /// <summary>
    /// Keeps the readouts at full opacity while a gesture runs, so their hold starts when it ends; repaints
    /// them along their fade, and drops them once transparent.
    /// </summary>
    private void OnReadoutTick()
    {
        long now = Environment.TickCount64;
        bool held = this.InGesture;
        var cells = this.CellBounds();
        foreach (var (image, since) in this._readouts.ToList())
        {
            int index = this._images.IndexOf(image);
            long elapsed = now - since;
            if (held && index >= 0)
            {
                this._readouts[image] = now;
                continue;
            }

            if (elapsed >= ZoomBadgeHold + ZoomBadgeFade || index < 0)
            {
                this._readouts.Remove(image);
            }

            if (elapsed >= ZoomBadgeHold && index >= 0 && index < cells.Length)
            {
                this.Invalidate(cells[index]);
            }
        }

        this.ScheduleReadouts();
    }

    /// <summary>
    /// Ticks again when the first hold ends, often while a readout fades or a gesture holds them; stops
    /// once none shows.
    /// </summary>
    private void ScheduleReadouts()
    {
        if (this._readouts.Count == 0)
        {
            this._readoutTimer.Stop();
            return;
        }

        long now = Environment.TickCount64;
        long wait = this.InGesture ? ZoomBadgeTick : this._readouts.Values.Min(since => ZoomBadgeHold - (now - since));
        this._readoutTimer.Interval = (int)Math.Clamp(wait, ZoomBadgeTick, ZoomBadgeHold);
        this._readoutTimer.Start();
    }

    /// <summary>Repaints the cell of <paramref name="image"/> when its readout shows: its text follows the zoom badge coming or going.</summary>
    private void InvalidateReadoutOf(SourceImage? image)
    {
        var cells = this.CellBounds();
        int index = image is not null && this._readouts.ContainsKey(image) ? this._images.IndexOf(image) : -1;
        if (index >= 0 && index < cells.Length)
        {
            this.Invalidate(cells[index]);
        }
    }

    /// <summary>The position readouts, helper indicators, over their cells — none during a swap — faded out after their hold.</summary>
    private void PaintReadouts(Graphics g, Rectangle[] cells)
    {
        if (this._readouts.Count == 0 || this._dragging)
        {
            return;
        }

        long now = Environment.TickCount64;
        foreach (var (image, since) in this._readouts)
        {
            int index = this._images.IndexOf(image);
            if (index >= 0 && index < cells.Length && this._offsets.TryGetValue(image, out var seen))
            {
                double opacity = Math.Clamp(1 - (now - since - ZoomBadgeHold) / (double)ZoomBadgeFade, 0, 1);
                this.PaintReadout(g, cells[index], image, seen.Offset, opacity);
            }
        }
    }

    /// <summary>
    /// One position readout: an X cross on the image's center, a dashed line to it from the cell's center
    /// at half opacity — none at 0, 0 —, and the offset in export pixels on two lines in the cell's
    /// top-right corner, below the zoom badge when it shows there.
    /// </summary>
    private void PaintReadout(Graphics g, Rectangle cell, SourceImage image, Point offset, double opacity)
    {
        var bounds = DrawnBounds(cell, image);
        var center = new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        var middle = new PointF(cell.X + cell.Width / 2f, cell.Y + cell.Height / 2f);
        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Pen Halo(double alpha) => new(Color.FromArgb((int)(HelperHalo.A * alpha), HelperHalo), this.LogicalToDeviceUnits(4)) { LineJoin = LineJoin.Round };
        Color Green(double alpha) => Color.FromArgb((int)(255 * alpha), HelperColor);
        if (offset != Point.Empty)
        {
            using var faintHalo = Halo(opacity * ReadoutLineOpacity);
            using var dashed = new Pen(Green(opacity * ReadoutLineOpacity), this.LogicalToDeviceUnits(2)) { DashPattern = [4, 3] };
            g.DrawLine(faintHalo, middle, center);
            g.DrawLine(dashed, middle, center);
        }

        using var halo = Halo(opacity);
        using var stroke = new Pen(Green(opacity), this.LogicalToDeviceUnits(2));
        float arm = this.LogicalToDeviceUnits(ReadoutCrossStroke) / 2f / MathF.Sqrt(2);
        foreach (float slope in new[] { 1f, -1f })
        {
            var from = new PointF(center.X - arm, center.Y - slope * arm);
            var to = new PointF(center.X + arm, center.Y + slope * arm);
            g.DrawLine(halo, from, to);
        }

        foreach (float slope in new[] { 1f, -1f })
        {
            g.DrawLine(stroke, new PointF(center.X - arm, center.Y - slope * arm), new PointF(center.X + arm, center.Y + slope * arm));
        }

        using var fill = new SolidBrush(Green(opacity));
        using var text = this.ReadoutPath(cell, image, offset);
        g.DrawPath(halo, text);
        g.FillPath(fill, text);
        g.Restore(state);
    }

    /// <summary>
    /// The text of a readout, <c>x -35px</c> over <c>y +12px</c>, right-aligned in the top-right corner of
    /// <paramref name="cell"/> — the zoom badge's place, just below the × — or just below the zoom badge
    /// when it shows over the same image.
    /// </summary>
    private GraphicsPath ReadoutPath(Rectangle cell, SourceImage image, Point offset)
    {
        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

        var close = CloseBounds(cell);
        float top = close.Bottom + this.LogicalToDeviceUnits(ButtonGap);
        if (this._zoomBadgeImage == image)
        {
            using var badge = this.ZoomBadgePath(out _);
            if (badge is not null)
            {
                top = badge.GetBounds().Bottom + this.LogicalToDeviceUnits(ButtonGap);
            }
        }

        using var format = new StringFormat { Alignment = StringAlignment.Far };
        var path = new GraphicsPath();
        path.AddString($"x {Signed(offset.X)}px\ny {Signed(offset.Y)}px", this.Font.FontFamily, (int)FontStyle.Bold, this.LogicalToDeviceUnits(ZoomBadgeTextSize), new PointF(close.Right, top), format);
        return path;
    }

    /// <summary>
    /// Moves the selected image by <paramref name="delta"/> screen pixels, as a drag of that length would,
    /// for the arrow keys: a magnetic stop it reaches — an edge one on the way in too — holds it for this
    /// press only, the next one leaving it from the stop; Shift ignores them (see
    /// workfiles/20261006-keyboard-image-move.md). Returns whether there was an image to move: none in the
    /// crop's edit view, where a drag does not move it either.
    /// </summary>
    public bool PanSelected(Size delta) => this.KeyPan(_ => delta, (ModifierKeys & Keys.Shift) != 0);

    /// <summary>
    /// Moves the selected image straight onto the next magnetic stop ahead in <paramref name="direction"/>
    /// (a unit step) — the center or an edge, whichever comes first — held there with its guide, as a press
    /// landing on it; nothing moves when no stop lies ahead. Ctrl + Shift + arrow. Returns whether there
    /// was an image to move, as <see cref="PanSelected"/>.
    /// </summary>
    public bool JumpSelected(Size direction) => this.KeyPan(index => this.ToNextStop(index, direction), free: false);

    /// <summary>The move of an arrow key, <paramref name="delta"/> giving it for the cell moved; see <see cref="PanSelected"/>.</summary>
    private bool KeyPan(Func<int, Size> delta, bool free)
    {
        if (this.SelectedCellImage is not { } image || this._locked || this._pressed >= 0 || this._selected >= this.CellBounds().Length
            || this.EditsCrop(this._selected))
        {
            return false;
        }

        // A stop held by another gesture, or before the image changed, does not hold this move.
        if (!this.KeyPanned())
        {
            this._panX.Reset();
            this._panY.Reset();
        }

        this.BeginLive(this._selected);
        this.PanBy(this._selected, delta(this._selected), 0, free, stepwise: true);
        this._panX.Settle();
        this._panY.Settle();
        this._keyPan = (image, image.Look);
        this._wheelEnd.Start();
        return true;
    }

    /// <summary>
    /// The move bringing image <paramref name="index"/> onto the nearest magnetic stop ahead of it in
    /// <paramref name="direction"/>, on each axis; nothing on an axis with no stop ahead. The stops are
    /// those <see cref="PanBy"/> holds the image on.
    /// </summary>
    private Size ToNextStop(int index, Size direction)
    {
        var cell = this.CellBounds()[index];
        var image = this._images[index];
        var look = image.DrawnLook;
        var bounds = FitCalculator.ComputeTurned(cell, look.Shown(image.Bitmap.Size), look.ZoomIn(cell, look.Shown(image.Bitmap.Size)), look.Focus, look.FineAngle).Bounds;
        var stops = FitCalculator.Stops(cell, bounds.Size);
        return new Size(
            NextStop(bounds.X, direction.Width, stops.Left, stops.Right, cell.X + (cell.Width - bounds.Width) / 2),
            NextStop(bounds.Y, direction.Height, stops.Top, stops.Bottom, cell.Y + (cell.Height - bounds.Height) / 2));
    }

    /// <summary>
    /// Whole pixels from <paramref name="position"/> to the nearest of <paramref name="stops"/> ahead in
    /// <paramref name="direction"/>; 0 without one. A stop within half a pixel is where the image already is.
    /// </summary>
    private static int NextStop(float position, int direction, params float[] stops)
    {
        var ahead = stops.Select(s => (s - position) * direction).Where(d => d > 0.5f).ToList();
        return ahead.Count == 0 ? 0 : direction * (int)Math.Round(ahead.Min(), MidpointRounding.AwayFromZero);
    }

    /// <summary>The arrows are the preview's own keys while it has the focus: they never move the focus away.</summary>
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    /// <summary>Ends a move by the arrow keys: its guides go, and no stop holds the next one.</summary>
    public void EndKeyPan()
    {
        if (this._keyPan is null)
        {
            return;
        }

        this._keyPan = null;
        this._panX.Reset();
        this._panY.Reset();
        this.Invalidate();
    }

    /// <summary>Whether the selected image is still as the arrow keys left it, its move going on.</summary>
    private bool KeyPanned() =>
        this._keyPan is { } pan && this.SelectedCellImage is { } image && pan.Image == image && pan.Look == image.Look;

    /// <summary>
    /// Moves an image by <paramref name="delta"/> from where it is actually shown, held by the
    /// magnetic stops unless <paramref name="free"/> — until the move goes <paramref name="resistance"/> past
    /// them, its edge stops crossed inward and the stops landed on exactly holding too when
    /// <paramref name="stepwise"/> — and never past the share of the cell it keeps covering.
    /// With a fine angle, the stops are those of the turned image's box, which follows the mouse.
    /// </summary>
    private void PanBy(int index, Size delta, float resistance, bool free, bool stepwise = false)
    {
        var cells = CellBounds();
        if (index >= cells.Length || delta.IsEmpty)
        {
            return;
        }

        var cell = cells[index];
        var image = _images[index];

        // A text laid out for its zoom always fills its cell: there is nothing to pan, its pages choose what shows.
        if (image.LaysOutZoom)
        {
            return;
        }

        var look = image.Look;
        var size = look.Shown(image.Bitmap.Size);
        var shown = FitCalculator.ComputeTurned(cell, size, look.ZoomIn(cell, size), look.Focus, look.FineAngle);
        var bounds = shown.Bounds;
        var stops = FitCalculator.Stops(cell, bounds.Size);
        var (heldX, heldY) = (_panX.Held, _panY.Held);
        float x = _panX.Move(bounds.X, delta.Width, stops.Left, stops.Right, cell.X + (cell.Width - bounds.Width) / 2, resistance, free, stepwise);
        float y = _panY.Move(bounds.Y, delta.Height, stops.Top, stops.Bottom, cell.Y + (cell.Height - bounds.Height) / 2, resistance, free, stepwise);
        this._panMoved = true;
        this._panFree = free;

        // Stored where it is drawn, so a drag past the covered share does not pile up out of sight.
        var focus = shown.FocusAt(cell, new PointF(x + bounds.Width / 2, y + bounds.Height / 2));
        var moved = FitCalculator.ComputeTurned(cell, size, look.ZoomIn(cell, size), focus, look.FineAngle);
        var middle = new PointF(moved.Bounds.X + moved.Bounds.Width / 2, moved.Bounds.Y + moved.Bounds.Height / 2);
        SetLook(index, look.WithFocus(moved.FocusAt(cell, middle)));

        // A guide can appear or go while the image stays put, and a gap bar may run far beyond the cell,
        // to the center of an image much larger than it.
        if (this.GuideApproach > 0 && !free || _panX.Held != heldX || _panY.Held != heldY)
        {
            this.Invalidate();
        }
    }

    private void Select(int index)
    {
        if (index == _selected)
        {
            return;
        }

        this.CancelEyedropper();

        // A cell selected clears the layer's: the two are exclusive.
        if (index >= 0 && this.SelectedLayer is { } layer)
        {
            this._selectedLayer = -1;
            this.InvalidateLayerOutline(layer);
        }

        _selected = index;
        _hoveredBar = null;
        this._hoveredCorner = null;
        this._keyPan = null;
        Invalidate();
        SelectedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The bars cell <paramref name="index"/> shows: only the selected one, while the blur or the crop is
    /// the selected effect and is on, and the grid is not locked.
    /// </summary>
    private Bars? ShownBars(int index)
    {
        var cells = CellBounds();
        if (_barsEffect is null || _locked || index < 0 || index != _selected || index >= _images.Count || index >= cells.Length)
        {
            return null;
        }

        var cell = cells[index];
        var image = _images[index];
        switch (_barsEffect)
        {
            case ImageEffect.Blur when image.Look.Blur is { } blur:
                return new Bars(blur.Area(cell), cell);
            case ImageEffect.Crop when image.Look.Crop is { } crop:
                var span = Rectangle.Round(Compositor.UncroppedBounds(image.Look.Oriented(image.Bitmap.Size), cell));
                var seen = crop.Seen(image.Look);
                return new Bars(
                    Rectangle.FromLTRB(
                        span.X + (int)Math.Round(seen.Left * span.Width),
                        span.Y + (int)Math.Round(seen.Top * span.Height),
                        span.X + (int)Math.Round(seen.Right * span.Width),
                        span.Y + (int)Math.Round(seen.Bottom * span.Height)),
                    span);
            default:
                return null;
        }
    }

    /// <summary>The bar within reach of <paramref name="location"/>, the nearest one when several are.</summary>
    private BarSide? BarAt(Rectangle cell, Bars bars, Point location)
    {
        if (!cell.Contains(location))
        {
            return null;
        }

        var area = bars.Area;
        int reach = LogicalToDeviceUnits(BarReach);
        BarSide? nearest = null;
        int best = int.MaxValue;
        foreach (var (side, distance) in new[]
        {
            (BarSide.Left, Math.Abs(location.X - area.Left)),
            (BarSide.Right, Math.Abs(location.X - area.Right)),
            (BarSide.Top, Math.Abs(location.Y - area.Top)),
            (BarSide.Bottom, Math.Abs(location.Y - area.Bottom)),
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

    private static Cursor BarCursor(BarSide side) => side is BarSide.Left or BarSide.Right ? Cursors.SizeWE : Cursors.SizeNS;

    /// <summary>
    /// Moves the bar being dragged along its own axis. Within <see cref="BarSnap"/> of its edge — of the
    /// cell for the blur, of the image for the crop — it lands exactly on it, so no strip of a pixel or
    /// two is left there.
    /// </summary>
    private void DragBar(BarSide side, Point location)
    {
        if (ShownBars(_selected) is not { } bars)
        {
            return;
        }

        bool vertical = side is BarSide.Left or BarSide.Right;
        var (fraction, gap) = this.BarFraction(side, (vertical ? location.X : location.Y) + _barGrab, bars.Span);
        Cursor = BarCursor(side);
        var image = _images[_selected];
        var look = image.Look;
        if (_barsEffect == ImageEffect.Crop && look.Crop is { } crop)
        {
            SetLook(_selected, look.WithCrop(crop.WithSeenSide(side, fraction, gap, look, image.Bitmap.Size)));
        }
        else if (look.Blur is { } blur)
        {
            SetLook(_selected, look.WithBlur(blur.WithSide(side, fraction, gap)));
        }
    }

    /// <summary>
    /// Where a bar of <paramref name="side"/> dragged to <paramref name="position"/> — on its own axis, its
    /// grab offset included — lies in fractions of <paramref name="span"/>: within <see cref="BarSnap"/> of
    /// its edge, exactly on it, so no strip of a pixel or two is left there. With the minimum gap between
    /// it and the bar across, in the same fractions.
    /// </summary>
    private (double Fraction, double Gap) BarFraction(BarSide side, int position, Rectangle span)
    {
        bool vertical = side is BarSide.Left or BarSide.Right;
        int length = Math.Max(1, vertical ? span.Width : span.Height);
        int offset = position - (vertical ? span.X : span.Y);
        int snap = this.LogicalToDeviceUnits(BarSnap);
        double fraction = side is BarSide.Left or BarSide.Top
            ? (offset <= snap ? 0 : offset / (double)length)
            : (length - offset <= snap ? 1 : offset / (double)length);
        return (fraction, this.LogicalToDeviceUnits(BarMinGap) / (double)length);
    }

    /// <summary>The corner of the bars within reach of <paramref name="location"/> on both axes, the nearest one when several are.</summary>
    private BarCorner? CornerAt(Rectangle cell, Bars bars, Point location)
    {
        if (!cell.Contains(location))
        {
            return null;
        }

        int reach = this.LogicalToDeviceUnits(BarReach);
        BarCorner? nearest = null;
        int best = int.MaxValue;
        foreach (var corner in Corners)
        {
            int dx = Math.Abs(location.X - SideOf(bars.Area, corner.Vertical));
            int dy = Math.Abs(location.Y - SideOf(bars.Area, corner.Horizontal));
            if (dx <= reach && dy <= reach && dx + dy < best)
            {
                nearest = corner;
                best = dx + dy;
            }
        }

        return nearest;
    }

    private static readonly BarCorner[] Corners =
    [
        new(BarSide.Left, BarSide.Top),
        new(BarSide.Right, BarSide.Top),
        new(BarSide.Left, BarSide.Bottom),
        new(BarSide.Right, BarSide.Bottom),
    ];

    /// <summary>Where <paramref name="side"/> of <paramref name="area"/> lies, on its own axis.</summary>
    private static int SideOf(Rectangle area, BarSide side) => side switch
    {
        BarSide.Left => area.Left,
        BarSide.Right => area.Right,
        BarSide.Top => area.Top,
        _ => area.Bottom,
    };

    private static Cursor CornerCursor(BarCorner corner) =>
        (corner.Vertical == BarSide.Left) == (corner.Horizontal == BarSide.Top) ? Cursors.SizeNWSE : Cursors.SizeNESW;

    /// <summary>
    /// Moves the corner being dragged with the mouse: the two bars meeting there together, each snapped
    /// onto its edge like a bar. Under the crop's ratio, the opposite corner stays fixed and the ratio holds.
    /// </summary>
    private void DragCorner(BarCorner corner, Point location)
    {
        if (this.ShownBars(this._selected) is not { } bars)
        {
            return;
        }

        var (x, gapX) = this.BarFraction(corner.Vertical, location.X + this._cornerGrab.Width, bars.Span);
        var (y, gapY) = this.BarFraction(corner.Horizontal, location.Y + this._cornerGrab.Height, bars.Span);
        this.Cursor = CornerCursor(corner);
        var image = this._images[this._selected];
        var look = image.Look;
        if (this._barsEffect == ImageEffect.Crop && look.Crop is { } crop)
        {
            this.SetLook(this._selected, look.WithCrop(crop.WithSeenCorner(corner, x, y, gapX, gapY, look, image.Bitmap.Size)));
        }
        else if (look.Blur is { } blur)
        {
            this.SetLook(this._selected, look.WithBlur(blur.WithSide(corner.Vertical, x, gapX).WithSide(corner.Horizontal, y, gapY)));
        }
    }

    /// <summary>
    /// Scales the zone of the <paramref name="bars"/> shown on the selected cell by <paramref name="notches"/>
    /// of the wheel (up grows it), its ratio kept, around its center — around the point under
    /// <paramref name="location"/> with Control held — no side closer than <see cref="BarMinGap"/> to the
    /// one across (RULES.md § On-Cell Handles).
    /// </summary>
    private void ScaleZone(Bars bars, Point location, int notches, bool atCursor)
    {
        var span = bars.Span;
        if (span.Width < 1 || span.Height < 1)
        {
            return;
        }

        double gap = this.LogicalToDeviceUnits(BarMinGap);
        double minWidth = gap / span.Width;
        double minHeight = gap / span.Height;
        PointF? anchor = atCursor ? new PointF((location.X - span.X) / (float)span.Width, (location.Y - span.Y) / (float)span.Height) : null;
        var look = this._images[this._selected].Look;
        if (this._barsEffect == ImageEffect.Crop && look.Crop is { } crop)
        {
            this._wheelHoldsRatio = true;
            this.SetLook(this._selected, look.WithCrop(crop.ScaledSeen(notches, anchor, minWidth, minHeight, look)));
        }
        else if (look.Blur is { } blur)
        {
            this.SetLook(this._selected, look.WithBlur(blur.Scaled(notches, anchor, minWidth, minHeight)));
        }
    }

    /// <summary>
    /// The point of the image under <paramref name="location"/> in the crop's edit view, in fractions of
    /// the kept part — the image shown once cropped — brought into it when over the part cut off.
    /// </summary>
    private static PointF KeptPartPoint(Bars bars, Point location)
    {
        var kept = bars.Area;
        return new PointF(
            kept.Width < 1 ? 0.5f : Math.Clamp((location.X - kept.X) / (float)kept.Width, 0, 1),
            kept.Height < 1 ? 0.5f : Math.Clamp((location.Y - kept.Y) / (float)kept.Height, 0, 1));
    }

    /// <summary>Whether cell <paramref name="index"/> shows the crop's edit view: its pan then does nothing, its wheel scales the kept part.</summary>
    private bool EditsCrop(int index) => _barsEffect == ImageEffect.Crop && ShownBars(index) is not null;

    /// <summary>Whether <paramref name="location"/> is inside the crop's kept part, in its edit view on cell <paramref name="index"/>, off its handle.</summary>
    private bool KeptPartAt(int index, Point location) =>
        _barsEffect == ImageEffect.Crop && ShownBars(index) is { } bars && bars.Area.Contains(location)
        && !this.HandleOf(index).Contains(location);

    /// <summary>Moves the crop's kept part whole with the mouse, from where the drag started: its size and ratio kept, stopped at the image's edges.</summary>
    private void MoveCrop(CropEffect crop, Point location)
    {
        if (ShownBars(_selected) is not { } bars || bars.Span.Width < 1 || bars.Span.Height < 1)
        {
            return;
        }

        Cursor = Cursors.SizeAll;
        var look = _images[_selected].Look;
        double dx = (location.X - _moveFrom.X) / (double)bars.Span.Width;
        double dy = (location.Y - _moveFrom.Y) / (double)bars.Span.Height;
        SetLook(_selected, look.WithCrop(crop.MovedSeen(dx, dy, look)));
    }

    /// <summary>
    /// The separator whose band, <see cref="SeparatorReach"/> on each side of it, holds
    /// <paramref name="location"/> — the nearest one when several do; none while the grid is locked.
    /// </summary>
    private Separator? SeparatorAt(Point location)
    {
        var canvas = CanvasBounds();
        if (_layout is null || _locked || canvas.IsEmpty || _images.Count == 0)
        {
            return null;
        }

        int reach = LogicalToDeviceUnits(SeparatorReach);
        Separator? nearest = null;
        int best = int.MaxValue;
        foreach (var separator in _layout.Separators())
        {
            int line, across, from, to, along;
            if (separator.Vertical)
            {
                (line, across, along) = (canvas.X + GridLayout.Boundary(canvas.Width, separator.Position), location.X, location.Y);
                (from, to) = (canvas.Y + GridLayout.Boundary(canvas.Height, separator.Start), canvas.Y + GridLayout.Boundary(canvas.Height, separator.End));
            }
            else
            {
                (line, across, along) = (canvas.Y + GridLayout.Boundary(canvas.Height, separator.Position), location.Y, location.X);
                (from, to) = (canvas.X + GridLayout.Boundary(canvas.Width, separator.Start), canvas.X + GridLayout.Boundary(canvas.Width, separator.End));
            }

            int distance = Math.Abs(across - line);
            if (along >= from && along < to && distance <= reach && distance < best)
            {
                nearest = separator;
                best = distance;
            }
        }

        return nearest;
    }

    private static Cursor SeparatorCursor(Separator separator) => separator.Vertical ? Cursors.SizeWE : Cursors.SizeNS;

    /// <summary>
    /// Moves the separator being dragged along its axis, the cells on both of its sides following it.
    /// Within <see cref="SeparatorSnap"/> of its place in the layout's own proportions, or of a
    /// parallel separator, it lands exactly on it. The cells it moves keep covering the same area:
    /// only they are drawn again, fast, until it is released.
    /// </summary>
    private void DragSeparator(Separator separator, Point location)
    {
        var canvas = CanvasBounds();
        if (_layout is null || canvas.IsEmpty)
        {
            return;
        }

        bool vertical = separator.Vertical;
        int length = vertical ? canvas.Width : canvas.Height;
        int offset = (vertical ? location.X - canvas.X : location.Y - canvas.Y) + _separatorGrab;
        double position = offset / (double)length;
        int best = LogicalToDeviceUnits(SeparatorSnap) + 1;
        var targets = _layout.Separators()
            .Where(s => s.Vertical == vertical && !s.Before.Intersect(separator.Before).Any())
            .Select(s => s.Position)
            .Prepend(_layout.DefaultPosition(separator));
        foreach (double target in targets)
        {
            int distance = Math.Abs(offset - GridLayout.Boundary(length, target));
            if (distance < best)
            {
                best = distance;
                position = target;
            }
        }

        Cursor = SeparatorCursor(separator);
        var layout = _layout.WithSeparator(separator, position);
        if (layout == _layout)
        {
            return;
        }

        _layout = layout;
        _separatorMoved = true;
        RedrawCells([.. separator.Before, .. separator.After]);
    }

    /// <summary>The separator is released: once it moved, the grid is laid out again for its new cells.</summary>
    private void EndSeparatorDrag()
    {
        if (_draggedSeparator is null)
        {
            return;
        }

        _draggedSeparator = null;
        if (_separatorMoved)
        {
            _separatorMoved = false;
            OnSizesChanged();
        }
    }

    /// <summary>Gives the grid new cell sizes, from the same layout.</summary>
    private void ApplySizes(GridLayout layout)
    {
        if (layout != _layout)
        {
            _layout = layout;
            OnSizesChanged();
        }
    }

    /// <summary>
    /// The cells changed size: the grid is drawn again in full, the text pages take the shape of their
    /// new cell, and the frames are decoded at its size.
    /// </summary>
    private void OnSizesChanged()
    {
        _cache?.Dispose();
        _cache = null;
        FitPagesToCells();
        UpdateDisplaySizes();
        Invalidate();
        ContentVersion++;
        UpdateRatio();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gives the canvas the ratio of the format, the free one computed from the grid as it stands, then
    /// lays the grid out again on it. Held while a separator or a crop bar is dragged, or the wheel scales
    /// the crop's kept part, so the canvas does not change shape under the mouse: the release, or the end
    /// of the wheel's burst, computes it again.
    /// </summary>
    private void UpdateRatio()
    {
        if (_draggedSeparator is not null || _draggedBar is not null || this._draggedCorner is not null || _movedCrop is not null
            || this._wheelHoldsRatio)
        {
            return;
        }

        // In a layer's edit view, the canvas takes the layer's shape (workfiles/20261029-layers.md § Edit View).
        double ratio = this._edit is not null ? this.EditRatio() : OutputFormats.Ratio(_format) ?? FreeRatio;

        // A video's frames, scaled to their cell, must not nudge the free ratio back and forth.
        if (Math.Abs(ratio / _ratio - 1) < 0.002)
        {
            return;
        }

        _ratio = ratio;
        _cache?.Dispose();
        _cache = null;
        FitPagesToCells();
        UpdateDisplaySizes();
        Invalidate();
        ContentVersion++;
        RatioChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Draws some cells again into the cached preview, fast, and paints them at once.</summary>
    private void RedrawCells(int[] indices)
    {
        var canvas = CanvasBounds();
        if (_layout is null || _cache is null || _cache.Size != canvas.Size)
        {
            Invalidate();
            return;
        }

        var area = this.DrawIntoCache(indices, canvas.Size, fast: true);
        area.Offset(canvas.Location);
        Invalidate(area);
        Update();
    }

    /// <summary>
    /// The crop's edit view over the selected cell, in place of its cropped image: the whole image on the
    /// background the cropped one gets, live, and the part cut off dimmed — interaction feedback, not a
    /// helper indicator.
    /// </summary>
    private void PaintCropEdit(Graphics g, Rectangle canvas, Rectangle cell, Bars bars)
    {
        var image = _images[_selected];
        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        PaintCheckerboard(g, canvas);
        g.TranslateTransform(canvas.X, canvas.Y);
        var local = cell;
        local.Offset(-canvas.X, -canvas.Y);
        Compositor.DrawUncropped(g, new Frame(image.Bitmap, image.BandColor, image.DrawnLook), local);
        DrawBordersOver(g, local, canvas.Size);
        g.Restore(state);

        state = g.Save();
        g.SetClip(Rectangle.Intersect(bars.Span, cell));
        g.SetClip(bars.Area, CombineMode.Exclude);
        using var dim = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        g.FillRectangle(dim, bars.Span);
        g.Restore(state);
    }

    /// <summary>
    /// The four bars as guides across the cell for the blur, across the whole image for the crop,
    /// fluorescent green and outlined so they show on any image: solid along the sides of the rectangle
    /// they frame, dashed beyond it as the guides are, with a grip at the middle of each side; the grip
    /// turns white while hovered or dragged. Shown by an undo, they fade out at
    /// <paramref name="opacity"/>, without <paramref name="grips"/>: not handles.
    /// </summary>
    private void PaintBars(Graphics g, Rectangle cell, Bars bars, double opacity = 1, bool grips = true)
    {
        var area = bars.Area;
        var span = bars.Span;

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
        using (var outline = new Pen(Color.FromArgb((int)(HelperHalo.A * opacity), HelperHalo), LogicalToDeviceUnits(4)))
        using (var line = new Pen(Color.FromArgb((int)(255 * opacity), HelperColor), LogicalToDeviceUnits(2)))
        using (var dashed = this.GuidePen(opacity))
        {
            g.DrawLine(outline, left, span.Top, left, span.Bottom);
            g.DrawLine(outline, right, span.Top, right, span.Bottom);
            g.DrawLine(outline, span.Left, top, span.Right, top);
            g.DrawLine(outline, span.Left, bottom, span.Right, bottom);

            g.DrawLine(line, left, top, left, bottom);
            g.DrawLine(line, right, top, right, bottom);
            g.DrawLine(line, left, top, right, top);
            g.DrawLine(line, left, bottom, right, bottom);

            // Each overhang from the rectangle's corner outward, so a dash starts right at the corner;
            // none where a side lies on the span's edge.
            foreach (int x in new[] { left, right })
            {
                if (top > span.Top)
                {
                    g.DrawLine(dashed, x, top, x, span.Top);
                }

                if (bottom < span.Bottom - 1)
                {
                    g.DrawLine(dashed, x, bottom, x, span.Bottom);
                }
            }

            foreach (int y in new[] { top, bottom })
            {
                if (left > span.Left)
                {
                    g.DrawLine(dashed, left, y, span.Left, y);
                }

                if (right < span.Right - 1)
                {
                    g.DrawLine(dashed, right, y, span.Right, y);
                }
            }
        }

        if (!grips)
        {
            g.Restore(state);
            return;
        }

        int length = LogicalToDeviceUnits(BarGripLength);
        int width = LogicalToDeviceUnits(BarGripWidth);
        PaintBarGrip(g, BarSide.Left, new Rectangle(left - width / 2, midY - length / 2, width, length));
        PaintBarGrip(g, BarSide.Right, new Rectangle(right - width / 2, midY - length / 2, width, length));
        PaintBarGrip(g, BarSide.Top, new Rectangle(midX - length / 2, top - width / 2, length, width));
        PaintBarGrip(g, BarSide.Bottom, new Rectangle(midX - length / 2, bottom - width / 2, length, width));
        this.PaintCorners(g, left, top, right, bottom);
        g.Restore(state);
    }

    /// <summary>
    /// The L-bracket at each corner of the rectangle the bars frame — <paramref name="left"/> to
    /// <paramref name="bottom"/>, as the bars are drawn — its arms along the two bars meeting there,
    /// thicker than them, never longer than half the rectangle: green over the black halo, white while
    /// hovered or dragged.
    /// </summary>
    private void PaintCorners(Graphics g, int left, int top, int right, int bottom)
    {
        int half = this.LogicalToDeviceUnits(CornerWidth) / 2;
        int armX = Math.Max(half, Math.Min(this.LogicalToDeviceUnits(CornerArm), (right - left) / 2));
        int armY = Math.Max(half, Math.Min(this.LogicalToDeviceUnits(CornerArm), (bottom - top) / 2));
        using var pen = new Pen(HelperHalo, this.LogicalToDeviceUnits(1));
        foreach (var corner in Corners)
        {
            int x = corner.Vertical == BarSide.Left ? left : right;
            int y = corner.Horizontal == BarSide.Top ? top : bottom;

            // Inward from the corner: right and down from the top-left one.
            int dx = corner.Vertical == BarSide.Left ? 1 : -1;
            int dy = corner.Horizontal == BarSide.Top ? 1 : -1;
            Point[] bracket =
            [
                new(x - dx * half, y - dy * half),
                new(x + dx * armX, y - dy * half),
                new(x + dx * armX, y + dy * half),
                new(x + dx * half, y + dy * half),
                new(x + dx * half, y + dy * armY),
                new(x - dx * half, y + dy * armY),
            ];
            bool hot = this._hoveredCorner == corner || this._draggedCorner == corner;
            using var brush = new SolidBrush(hot ? Color.White : HelperColor);
            g.FillPolygon(brush, bracket);
            g.DrawPolygon(pen, bracket);
        }
    }

    /// <summary>
    /// The progress line of every playing cell, a helper indicator: green over the black halo along the
    /// bottom edge, just inside the selection outline, from the left edge to the share of the loop
    /// played — read from the player's clock at each paint, so it glides whatever the content's step.
    /// None on a still, a frozen image, or the cell whose blur bars show, which take its place.
    /// </summary>
    private void PaintProgressLines(Graphics g, Rectangle[] cells)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.None;
        using var outline = new Pen(HelperHalo, LogicalToDeviceUnits(ProgressHaloWidth));
        using var line = new Pen(HelperColor, LogicalToDeviceUnits(ProgressLineWidth));
        for (int i = 0; i < cells.Length && i < _images.Count; i++)
        {
            if (!_dragging && ShownBars(i) is not null)
            {
                continue;
            }

            if (_player.ProgressOf(_images[i]) is not { } progress)
            {
                continue;
            }

            var strip = ProgressStrip(cells[i]);
            int end = strip.Left + (int)Math.Round(progress * strip.Width);
            if (end <= strip.Left)
            {
                continue;
            }

            float y = strip.Top + strip.Height / 2f;
            g.DrawLine(outline, strip.Left, y, end, y);
            g.DrawLine(line, strip.Left, y, end, y);
        }

        g.Restore(state);
    }

    /// <summary>
    /// Guides of the magnetic stops of a moved image, dashed, in the green of the helper indicators: along
    /// the cell edge the image edge is aligned on, or through the center of the cell. A stop holding the
    /// image shows at full opacity; the nearest stop it comes within <see cref="GuideApproach"/> of shows
    /// fainter the farther it is, with a gap bar through the image's center measuring what is left — once
    /// the drag has moved the image when <paramref name="dragged"/>, at once after an arrow key. See
    /// workfiles/20260926-guides-approach-gap-bar.md.
    /// </summary>
    private void PaintPanGuides(Graphics g, Rectangle cell, SourceImage image, bool dragged)
    {
        var look = image.Look;
        var size = look.Shown(image.Bitmap.Size);
        var bounds = FitCalculator.ComputeTurned(cell, size, look.ZoomIn(cell, size), look.Focus, look.FineAngle).Bounds;
        var stops = FitCalculator.Stops(cell, bounds.Size);
        bool approaching = this.GuideApproach > 0 && !this._panFree && (!dragged || this._panMoved);
        float reachX = cell.Width * this.GuideApproach / 100f;
        float reachY = cell.Height * this.GuideApproach / 100f;
        var along = Point.Round(new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        int inset = LogicalToDeviceUnits(2);
        int left = cell.Left + inset;
        int right = cell.Right - 1 - inset;
        int top = cell.Top + inset;
        int bottom = cell.Bottom - 1 - inset;
        int midX = cell.X + cell.Width / 2;
        int midY = cell.Y + cell.Height / 2;

        // The edge guide sits where the image would leave the cell: an image covering the cell uncovers
        // the side it moves away from, a smaller one crosses the side it moves toward.
        int GuideX(PanStop kind, int outward) =>
            kind == PanStop.Center ? midX : (outward > 0) == (bounds.Width >= cell.Width - 0.5f) ? left : right;
        int GuideY(PanStop kind, int outward) =>
            kind == PanStop.Center ? midY : (outward > 0) == (bounds.Height >= cell.Height - 0.5f) ? top : bottom;

        // A gap bar links the anchor to the image: from the point of the image that will snap onto the stop —
        // its edge for an edge stop, its center line for the center one — to the guide, beyond the cell when
        // an overflowing image has that edge outside it.
        var guides = new List<(Point From, Point To, double Opacity)>();
        var bars = new List<(Point From, Point To)>();
        if (_panX.Held != PanStop.None)
        {
            int x = GuideX(_panX.Held, _panX.Outward);
            guides.Add((new Point(x, cell.Top), new Point(x, cell.Bottom), 1));
        }
        else if (approaching && PanMagnet.Approach(bounds.X, stops.Left, stops.Right, cell.X + (cell.Width - bounds.Width) / 2, reachX) is { } near)
        {
            int x = GuideX(near.Kind, near.Outward);
            guides.Add((new Point(x, cell.Top), new Point(x, cell.Bottom), ApproachOpacity(near.Gap, reachX)));
            int y = this.GapBarAt(along.Y, cell.Top, cell.Bottom);
            bars.Add((new Point(this.GapBarStart(bounds.X, bounds.Width, x), y), new Point(x, y)));
        }

        if (_panY.Held != PanStop.None)
        {
            int y = GuideY(_panY.Held, _panY.Outward);
            guides.Add((new Point(cell.Left, y), new Point(cell.Right, y), 1));
        }
        else if (approaching && PanMagnet.Approach(bounds.Y, stops.Top, stops.Bottom, cell.Y + (cell.Height - bounds.Height) / 2, reachY) is { } near)
        {
            int y = GuideY(near.Kind, near.Outward);
            guides.Add((new Point(cell.Left, y), new Point(cell.Right, y), ApproachOpacity(near.Gap, reachY)));
            int x = this.GapBarAt(along.X, cell.Left, cell.Right);
            bars.Add((new Point(x, this.GapBarStart(bounds.Y, bounds.Height, y)), new Point(x, y)));
        }

        foreach (var (from, to, opacity) in guides)
        {
            this.PaintGuideLines(g, cell, [(from, to)], opacity);
        }

        this.PaintGapBars(g, bars);
    }

    /// <summary>
    /// Where a gap bar starts along its axis, for an image at <paramref name="position"/>,
    /// <paramref name="length"/> long, the bar ending on the guide at <paramref name="guide"/>: the same point
    /// of the image for every axis and every stop — its center line, or with <see cref="GapBarsFromEdges"/>
    /// its edge nearest the guide.
    /// </summary>
    private int GapBarStart(float position, float length, int guide)
    {
        if (!this.GapBarsFromEdges)
        {
            return (int)Math.Round(position + length / 2);
        }

        float low = position;
        float high = position + length;
        return (int)Math.Round(Math.Abs(low - guide) <= Math.Abs(high - guide) ? low : high);
    }

    /// <summary>Opacity of a guide <paramref name="gap"/> pixels from its stop, shown from <paramref name="reach"/> pixels: from the minimum at the reach to full at the stop.</summary>
    private static double ApproachOpacity(float gap, float reach) =>
        1 - (1 - ApproachMinOpacity) * Math.Clamp(gap / reach, 0, 1);

    /// <summary>Where a gap bar crosses its guide: at <paramref name="at"/>, kept inside the cell, between <paramref name="low"/> and <paramref name="high"/>, its halo included.</summary>
    private int GapBarAt(int at, int low, int high)
    {
        int margin = this.LogicalToDeviceUnits(2);
        return low + margin >= high - 1 - margin ? (low + high) / 2 : Math.Clamp(at, low + margin, high - 1 - margin);
    }

    /// <summary>
    /// Gap bars, solid in the green of the helper indicators over the black halo, not clipped to their cell:
    /// one may run beyond it to an overflowing image's edge. A bar shorter than a pixel is not drawn.
    /// </summary>
    private void PaintGapBars(Graphics g, List<(Point From, Point To)> bars)
    {
        var lines = bars.Where(b => b.From != b.To).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.None;
        using (var outline = new Pen(HelperHalo, this.LogicalToDeviceUnits(4)))
        {
            foreach (var (from, to) in lines)
            {
                g.DrawLine(outline, from, to);
            }
        }

        using (var line = new Pen(HelperColor, this.LogicalToDeviceUnits(2)))
        {
            foreach (var (from, to) in lines)
            {
                g.DrawLine(line, from, to);
            }
        }

        g.Restore(state);
    }

    /// <summary>Guide lines over a cell, dashed green over the black halo, at <paramref name="opacity"/>.</summary>
    private void PaintGuideLines(Graphics g, Rectangle cell, List<(Point From, Point To)> lines, double opacity)
    {
        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        g.SmoothingMode = SmoothingMode.None;
        using var outline = new Pen(Color.FromArgb((int)(HelperHalo.A * opacity), HelperHalo), this.LogicalToDeviceUnits(4));
        using var dashed = this.GuidePen(opacity);
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

    /// <summary>The dashed green pen of the guides, and of the bars beyond their rectangle, at <paramref name="opacity"/>.</summary>
    private Pen GuidePen(double opacity) =>
        new(Color.FromArgb((int)(255 * opacity), HelperColor), this.LogicalToDeviceUnits(2)) { DashPattern = [4, 3] };

    /// <summary>The rectangle the bars frame, and the one they run across: the cell for the blur, the whole image of the edit view for the crop.</summary>
    private readonly record struct Bars(Rectangle Area, Rectangle Span);

    private void PaintBarGrip(Graphics g, BarSide side, Rectangle bounds)
    {
        bool hot = _hoveredBar == side || _draggedBar == side;
        using var brush = new SolidBrush(hot ? Color.White : HelperColor);
        using var pen = new Pen(HelperHalo, LogicalToDeviceUnits(1));
        g.FillRectangle(brush, bounds);
        g.DrawRectangle(pen, bounds);
    }

    /// <summary>
    /// The file name of the selected cell's image at the bottom left of the cell, and the folder
    /// icon before it (none without a file): the text as a path, its bounds and the icon's. <c>null</c>
    /// without a selected image, during a swap, and in a cell too narrow for it.
    /// </summary>
    private GraphicsPath? SourceNamePath(out Rectangle cell, out Rectangle textBounds, out Rectangle icon)
    {
        cell = textBounds = icon = Rectangle.Empty;
        var cells = CellBounds();
        if (_dragging || SelectedCellImage is not { } image || _selected >= cells.Length)
        {
            return null;
        }

        cell = cells[_selected];
        int inset = LogicalToDeviceUnits(ButtonInset);
        int gap = LogicalToDeviceUnits(ButtonGap);
        int iconSize = image.FilePath is null ? 0 : LogicalToDeviceUnits(SourceIconSize);
        float width = cell.Width - 2 * inset - (iconSize > 0 ? iconSize + gap : 0);
        if (width <= 0)
        {
            return null;
        }

        // Placed on the line of the font, not on the glyphs, so that a name never moves with its letters.
        float size = LogicalToDeviceUnits(SourceNameTextSize);
        var family = Font.FontFamily;
        float line = size * family.GetLineSpacing(FontStyle.Bold) / family.GetEmHeight(FontStyle.Bold);
        float top = cell.Bottom - inset - line;
        if (iconSize > 0)
        {
            icon = new Rectangle(cell.X + inset, (int)(top + (line - iconSize) / 2), iconSize, iconSize);
        }

        var origin = new PointF(iconSize > 0 ? icon.Right + gap : cell.X + inset, top);
        var path = new GraphicsPath();
        path.AddString(FittedSourceName(image, width, size), family, (int)FontStyle.Bold, size, origin, StringFormat.GenericTypographic);
        textBounds = Rectangle.Ceiling(path.GetBounds());
        return path;
    }

    /// <summary>The name the selected cell shows: its file's, else how it arrived.</summary>
    private static string SourceName(SourceImage image) =>
        image.FilePath is { } path ? Path.GetFileName(path)
        : image.Pages is TextPages ? (image.Dropped ? "Dropped text" : "Pasted text")
        : "Pasted image";

    /// <summary>The source name within <paramref name="width"/>, measured once per image and width.</summary>
    private string FittedSourceName(SourceImage image, float width, float size)
    {
        if (_fittedName is { } fitted && fitted.Image == image && fitted.Width == (int)width && fitted.Size == size)
        {
            return fitted.Text;
        }

        string text = FitMiddle(SourceName(image), t => TextWidth(t, size), width);
        _fittedName = (image, (int)width, size, text);
        return text;
    }

    private float TextWidth(string text, float size)
    {
        using var path = new GraphicsPath();
        path.AddString(text, Font.FontFamily, (int)FontStyle.Bold, size, PointF.Empty, StringFormat.GenericTypographic);
        return path.GetBounds().Width;
    }

    /// <summary>
    /// A name too wide shortened by an ellipsis in its middle, its start and its extension kept
    /// (<c>vacances-ete-2…plage.jpg</c>); only the ellipsis and the extension when nothing more fits.
    /// </summary>
    private static string FitMiddle(string name, Func<string, float> width, float max)
    {
        if (width(name) <= max)
        {
            return name;
        }

        string extension = Path.GetExtension(name);
        string stem = name[..^extension.Length];
        string best = "…" + extension;
        int low = 1;
        int high = stem.Length - 1;
        while (low <= high)
        {
            int keep = (low + high) / 2;
            string candidate = stem[..(keep - keep / 2)] + "…" + stem[^(keep / 2)..] + extension;
            if (width(candidate) <= max)
            {
                best = candidate;
                low = keep + 1;
            }
            else
            {
                high = keep - 1;
            }
        }

        return best;
    }

    /// <summary>
    /// The source name, a helper indicator: green over the black halo, clipped to its cell; the
    /// folder icon before it turns white while hovered.
    /// </summary>
    private void PaintSourceName(Graphics g)
    {
        using var path = SourceNamePath(out var cell, out _, out var icon);
        if (path is null)
        {
            return;
        }

        var state = g.Save();
        g.SetClip(cell, CombineMode.Intersect);
        using var outline = new Pen(HelperHalo, LogicalToDeviceUnits(4)) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(HelperColor);
        g.DrawPath(outline, path);
        g.FillPath(fill, path);
        if (!icon.IsEmpty)
        {
            using var folder = FolderPath(icon);
            using var iconFill = new SolidBrush(_hoveringSourceIcon ? Color.White : HelperColor);
            g.DrawPath(outline, folder);
            g.FillPath(iconFill, folder);
        }

        g.Restore(state);
    }

    /// <summary>A folder, its tab up left, filling the lower part of <paramref name="bounds"/>.</summary>
    private static GraphicsPath FolderPath(Rectangle bounds)
    {
        float x = bounds.X;
        float w = bounds.Width;
        float top = bounds.Y + bounds.Height * 0.15f;
        float body = bounds.Y + bounds.Height * 0.3f;
        float bottom = bounds.Y + bounds.Height * 0.9f;
        var path = new GraphicsPath();
        path.AddPolygon(
        [
            new PointF(x, top), new PointF(x + w * 0.4f, top), new PointF(x + w * 0.5f, body),
            new PointF(x + w, body), new PointF(x + w, bottom), new PointF(x, bottom),
        ]);
        return path;
    }

    /// <summary>
    /// Whether <paramref name="location"/> is on the selected cell's file name (its tooltip) or on its
    /// folder icon (opens Explorer). Neither without a file, nor where a blur bar can be grabbed.
    /// </summary>
    private (bool Name, bool Icon) SourceHitAt(Point location)
    {
        using var path = SourceNamePath(out var cell, out var text, out var icon);
        if (path is null || SelectedCellImage?.FilePath is null || !cell.Contains(location)
            || ShownBars(_selected) is { } bars && BarAt(cell, bars, location) is not null)
        {
            return (false, false);
        }

        int halo = LogicalToDeviceUnits(4);
        return icon.Contains(location) ? (false, true) : (Rectangle.Inflate(text, halo, halo).Contains(location), false);
    }

    /// <summary>The hover of the file name and its icon; the full path shows in a tooltip over either.</summary>
    private void SetSourceHover((bool Name, bool Icon) hover)
    {
        bool shown = _hoveringSourceName || _hoveringSourceIcon;
        _hoveringSourceName = hover.Name;
        _hoveringSourceIcon = hover.Icon;
        if (hover.Name || hover.Icon)
        {
            if (!shown)
            {
                _toolTip.SetToolTip(this, SelectedCellImage?.FilePath);
            }
        }
        else if (shown)
        {
            _toolTip.SetToolTip(this, null);
        }
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
        PaintAddPrompt(g, zone, "Add images", _hoveringDropZone, TextFormatFlags.EndEllipsis);
    }

    /// <summary>The empty canvas opens the picker too, so it is drawn like the drop zone.</summary>
    private void PaintEmptyState(Graphics g, Rectangle canvas) =>
        PaintAddPrompt(
            g,
            canvas,
            "Click to pick 1 to 4 images, drop them here, or paste them with Ctrl+V",
            _hoveringCanvas,
            TextFormatFlags.WordBreak);

    /// <summary>Dashed border, then a "+" above the label, centered; white while hovered.</summary>
    private void PaintAddPrompt(Graphics g, Rectangle bounds, string label, bool hovered, TextFormatFlags wrap)
    {
        var color = hovered ? Color.White : ForeColor;
        using (var pen = new Pen(color, LogicalToDeviceUnits(EmptyBorderWidth)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(pen, bounds);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | wrap;
        int plus = Math.Min(bounds.Width * 2 / 5, LogicalToDeviceUnits(32));
        int labelHeight = TextRenderer.MeasureText(label, Font, new Size(bounds.Width, 0), flags).Height;
        int gap = LogicalToDeviceUnits(8);
        int top = bounds.Y + (bounds.Height - plus - gap - labelHeight) / 2;
        int centerX = bounds.X + bounds.Width / 2;
        using (var pen = new Pen(color, LogicalToDeviceUnits(3)))
        {
            g.DrawLine(pen, centerX, top, centerX, top + plus);
            g.DrawLine(pen, centerX - plus / 2, top + plus / 2, centerX + plus / 2, top + plus / 2);
        }

        TextRenderer.DrawText(g, label, Font, new Rectangle(bounds.X, top + plus + gap, bounds.Width, labelHeight), color, flags);
    }
}
