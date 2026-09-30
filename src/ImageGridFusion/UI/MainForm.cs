using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    private readonly string[] _startupFiles;
    private readonly GridPreview _preview = new() { Dock = DockStyle.Fill, AllowDrop = true };
    private readonly LayoutStrip _layouts = new() { Dock = DockStyle.Left, Width = 80, AllowDrop = true };

    // The file explorer, right of the preview; its base folder is a setting of the ⚙ menu.
    private readonly FileExplorerPanel _explorer = new() { Dock = DockStyle.Right };

    // Between the preview and the explorer: drags the explorer's width, the preview giving way; hidden while the
    // explorer is collapsed. The system's resize arrow, not WinForms' own VSplit bitmap.
    private readonly Splitter _explorerSplitter = new() { Dock = DockStyle.Right, Width = 6, Cursor = Cursors.SizeWE };
    private readonly ToolStripMenuItem _explorerFolder = new("File explorer folder…");
    // How many pages of tiles the explorer loads at a time: a submenu of exclusive choices, remembered between sessions.
    private readonly ToolStripMenuItem _explorerPages = new("File explorer pages per load");
    private readonly Button _clearButton = new() { Text = "Clear all", AutoSize = true };
    private readonly Button _settingsButton = new() { Text = "⚙", Size = new Size(32, 23), AutoSize = true };
    // The length readout: what the exported video would last, always shown, its detail in its tooltip (see RefreshLength).
    private readonly Label _lengthReadout = new() { AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 3, 6, 3) };
    private readonly ContextMenuStrip _settingsMenu = new();
    private readonly ToolStripMenuItem _startWithWindows = new("Start with Windows");
    private readonly ToolTip _toolTip = new();
    private readonly Button _copyButton = SplitMain("Copy");
    private readonly Button _copyArrow = SplitArrow();
    private readonly ContextMenuStrip _copyMenu = new();
    private readonly ToolStripMenuItem _copyGif = new("GIF");
    private readonly ToolStripMenuItem _copyMp4 = new("MP4 Video");
    private readonly ToolStripMenuItem _copyForSharing = new("JPEG for sharing");
    private readonly Button _saveButton = SplitMain("Save…");
    private readonly Button _saveArrow = SplitArrow();
    private readonly ContextMenuStrip _saveMenu = new();
    private readonly ToolStripMenuItem _saveGif = new("GIF");
    private readonly ToolStripMenuItem _saveMp4 = new("MP4 Video");

    // The last video Copy generated, offered back without generating it again (see LastVideo).
    private readonly Button _copyLastButton = new() { Text = "Copy last video", AutoSize = true, Enabled = false };
    private readonly ToolStripMenuItem _copyLast = new("Copy last video") { Enabled = false };
    private readonly ToolStripMenuItem _saveLast = new("Save last video…") { Enabled = false };
    private readonly Button _cancelButton = new() { Text = "Cancel", AutoSize = true, Visible = false };
    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TableLayoutPanel _bottom;
    private readonly FlowLayoutPanel _outputButtons;
    private readonly FlowLayoutPanel _statusLine;

    /// <summary>Set while an export runs: the grid is locked until it ends.</summary>
    private CancellationTokenSource? _export;
    private bool _closeAfterExport;
    private bool _closingForGood;

    // Whether a size was remembered from the last use, and whether the window was opened this session — its size is remembered only then.
    private readonly bool _sizeRemembered;
    private bool _opened;

    /// <summary>The last MP4 or GIF Copy generated in this session; null until one was. Nothing that happens to the grid touches it.</summary>
    private LastVideo? _lastVideo;

    // The options of the selected tab, then the tabs of the effects hanging below them: see RULES.md.
    private readonly TableLayoutPanel _optionsRow = new()
    {
        Dock = DockStyle.Top,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = SystemColors.Window,
        Padding = new Padding(8, 4, 8, 4),
    };
    private readonly Panel _optionsHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Button _effectResetButton = new()
    {
        Text = "Reset",
        AutoSize = true,
        Anchor = AnchorStyles.Right,
        TextImageRelation = TextImageRelation.ImageBeforeText,
        BackColor = SystemColors.Control,
        UseVisualStyleBackColor = true,
    };
    private readonly TableLayoutPanel _tabsRow = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 4,
        RowCount = 1,
        Padding = new Padding(8, 0, 8, 8),
    };
    // The row's title, in bold and pointing at the tabs, so it does not read as one of them.
    private readonly Label _effectsLabel = new() { Text = "Effects →", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly EffectTabs<ImageEffect> _effectTabs = new(EffectTitle) { Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = Padding.Empty };

    // Hangs from the options row like the tabs, and as tall as them.
    private readonly Button _resetButton = new()
    {
        Text = "Reset",
        AutoSize = true,
        Anchor = AnchorStyles.Right | AnchorStyles.Top,
        Margin = new Padding(3, 0, 0, 0),
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };

    // One row of options per effect, in the options row; only the selected tab's shows.
    private readonly Dictionary<ImageEffect, FlowLayoutPanel> _options = Enum.GetValues<ImageEffect>()
        .ToDictionary(e => e, _ => new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Visible = false });

    // A log scale, in hundredths of a doubling: 50 % → 100 % and each doubling take the same length.
    private readonly StepSlider _zoom = OptionSlider((int)Math.Round(Math.Log2(ImageLook.MinZoom) * 100), (int)Math.Round(Math.Log2(ImageLook.MaxZoom) * 100), 10);
    private readonly Label _zoomLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox[] _quarterTurns = [OptionButton("0°"), OptionButton("90°"), OptionButton("180°"), OptionButton("270°")];
    private readonly TrackBar _fineAngle = OptionSlider(-ImageLook.MaxFineAngle, ImageLook.MaxFineAngle, 5);
    private readonly Label _fineAngleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    // Free first, then the listed ratios, each button previewing its format.
    private readonly (CheckBox Button, double? Ratio)[] _cropRatios =
    [
        (OptionButton("Free"), null),
        .. CropEffect.Ratios.Select(ratio => (OptionButton(RatioText(ratio)), (double?)ratio)),
    ];
    private readonly CheckBox _flipX = OptionButton("Horizontal");
    private readonly CheckBox _flipY = OptionButton("Vertical");
    private readonly StepSlider _frames = OptionSlider(0, 1, 10);
    private readonly Label _framesLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _freeze = new() { Text = "Freeze", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly PictureBox _grayscaleIcon = new() { SizeMode = PictureBoxSizeMode.CenterImage, Anchor = AnchorStyles.Left };
    private readonly TrackBar _grayscale = OptionSlider(0, 100, 10);
    private readonly Label _grayscaleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The standard look, as the options: the flat one sizes itself without its image, clipping both.
    private readonly RadioButton _gaussian = new()
    {
        Text = "Gaussian",
        AutoSize = true,
        Appearance = Appearance.Button,
        BackColor = SystemColors.Control,
        Anchor = AnchorStyles.Left,
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
    private readonly RadioButton _pixelate = new()
    {
        Text = "Pixelate",
        AutoSize = true,
        Appearance = Appearance.Button,
        BackColor = SystemColors.Control,
        Anchor = AnchorStyles.Left,
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
    private readonly PictureBox _blurIntensityIcon = new() { SizeMode = PictureBoxSizeMode.CenterImage, Anchor = AnchorStyles.Left };
    private readonly TrackBar _blurIntensity = OptionSlider(0, 100, 10);
    private readonly Label _blurIntensityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _volume = OptionSlider(0, (int)(VolumeEffect.MaxLevel * 100), 10);
    private readonly Label _volumeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _mute = new() { Text = "Mute", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _backgroundAutomatic = new() { Text = "Automatic color", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly PictureBox _backgroundOpacityIcon = new() { SizeMode = PictureBoxSizeMode.CenterImage, Anchor = AnchorStyles.Left };
    private readonly TrackBar _backgroundOpacity = OptionSlider(0, 100, 10);
    private readonly Label _backgroundOpacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // Its face is the swatch: painted with the background color in use, opaque.
    private readonly Button _backgroundColor = new() { Text = "Color…", AutoSize = true, Anchor = AnchorStyles.Left, UseVisualStyleBackColor = false };

    // One dialog for the whole session, so its custom colors stay from one pick to the next.
    private readonly ColorDialog _colorDialog = new() { AnyColor = true };

    // The selected tab belongs to the toolbar: it stays selected on every cell, and with none.
    private ImageEffect? _selectedEffect;
    private bool _syncingEffects;

    // The global effects, the mirror of the cell effects: their tabs standing on their options row, just
    // above the bottom bar (see RULES.md). A file dropped anywhere on either row becomes the soundtrack.
    private readonly TableLayoutPanel _globalTabsRow = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        ColumnCount = 4,
        RowCount = 1,
        Padding = new Padding(8, 8, 8, 0),
        AllowDrop = true,
    };
    private readonly Label _globalLabel = new() { Text = "Global effects →", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly EffectTabs<GlobalEffect> _globalTabs = new(effect => effect.ToString(), standing: true) { Anchor = AnchorStyles.Left | AnchorStyles.Bottom, Margin = Padding.Empty };

    // Stands on the options row like the tabs, and as tall as them.
    private readonly Button _globalResetButton = new()
    {
        Text = "Reset",
        AutoSize = true,
        Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
        Margin = new Padding(3, 0, 0, 0),
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
    private readonly TableLayoutPanel _globalOptionsRow = new()
    {
        Dock = DockStyle.Bottom,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = SystemColors.Window,
        Padding = new Padding(8, 4, 8, 4),
        AllowDrop = true,
    };
    private readonly Panel _globalOptionsHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Button _globalEffectResetButton = new()
    {
        Text = "Reset",
        AutoSize = true,
        Anchor = AnchorStyles.Right,
        TextImageRelation = TextImageRelation.ImageBeforeText,
        BackColor = SystemColors.Control,
        UseVisualStyleBackColor = true,
    };

    // One row of options per global effect, in the global options row; only the selected tab's shows.
    private readonly Dictionary<GlobalEffect, FlowLayoutPanel> _globalOptions = Enum.GetValues<GlobalEffect>()
        .ToDictionary(e => e, _ => new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Visible = false });

    // The selected global tab belongs to its own toolbar, not to the cell effects' one; none at startup.
    private GlobalEffect? _selectedGlobalEffect;
    private readonly Button _soundtrackBrowse = new() { Text = "Browse…", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _soundtrackFile = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _soundtrackVolume = OptionSlider(0, (int)(Soundtrack.MaxLevel * 100), 10);
    private readonly Label _soundtrackVolumeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The soundtrack's file and level, kept while it is off; cleared by its Resets and Clear all.
    private Soundtrack? _soundtrack;
    private bool _soundtrackOn;

    // The level set before any file is chosen, given to the first one.
    private double _soundtrackLevel = 1;

    // The borders' options; their color is a setting of the ⚙ menu, remembered between sessions.
    private readonly ComboBox _bordersStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, Anchor = AnchorStyles.Left };
    // In thousandths of the grid's shorter side: Control + wheel steps by 0.5 %.
    private readonly TrackBar _bordersThickness = OptionSlider((int)Math.Round(GridBorders.MinThickness * 1000), (int)Math.Round(GridBorders.MaxThickness * 1000), 5, controlStep: 5);
    private readonly Label _bordersThicknessLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _bordersOpacity = OptionSlider((int)Math.Round(GridBorders.MinOpacity * 100), 100, 10);
    private readonly Label _bordersOpacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _bordersOuterFrame = new() { Text = "Outer frame", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _bordersRounded = new() { Text = "Twitter corners", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ToolStripMenuItem _borderColor = new("Border color…");
    private readonly ToolStripMenuItem _twitterCornersDefault = new("Twitter corners by default");

    // Whether the borders' initial state has its Twitter corners: a setting of the ⚙ menu, remembered between sessions.
    private bool _roundedByDefault = AppSettings.TwitterCornersByDefault;

    // The borders' settings, kept while they are off; back to their initial state, off, with Clear all.
    private GridBorders _borders = GridBorders.Initial(AppSettings.BorderColor, AppSettings.TwitterCornersByDefault);
    private bool _bordersOn;

    public MainForm(string[] args)
    {
        _startupFiles = args;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Image Grid Fusion";
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen;

        // The size it had at its last use, else the default — logical pixels, scaled with the rest.
        var remembered = AppSettings.WindowClientSize;
        _sizeRemembered = remembered is not null;
        ClientSize = remembered ?? new Size(960, 860);
        MinimumSize = new Size(480, 320);
        AllowDrop = true;

        _outputButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _outputButtons.Controls.Add(_settingsButton);
        // Between the gear and Copy: a wider text only moves the gear, the buttons keep their place against the right edge.
        _outputButtons.Controls.Add(_lengthReadout);
        _outputButtons.Controls.Add(_copyButton);
        _outputButtons.Controls.Add(_copyArrow);
        _outputButtons.Controls.Add(_copyLastButton);
        _outputButtons.Controls.Add(_saveButton);
        _outputButtons.Controls.Add(_saveArrow);

        // Clear button on the left, then the status line, output buttons on the right.
        _bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8),
        };
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _bottom.Controls.Add(_clearButton, 0, 0);
        _statusLine = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left,
        };
        _statusLine.Controls.Add(_status);
        _statusLine.Controls.Add(_cancelButton);
        _bottom.Controls.Add(_statusLine, 1, 0);
        _bottom.Controls.Add(_outputButtons, 2, 0);

        _tabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _tabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tabsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _effectsLabel.Font = new Font(Font, FontStyle.Bold);
        _tabsRow.Controls.Add(_effectsLabel, 0, 0);
        _tabsRow.Controls.Add(_effectTabs, 1, 0);
        _tabsRow.Controls.Add(_resetButton, 3, 0);
        _optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _optionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _optionsRow.Controls.Add(_optionsHost, 0, 0);
        _optionsRow.Controls.Add(_effectResetButton, 1, 0);
        _optionsHost.Controls.AddRange([.. _options.Values]);
        _options[ImageEffect.Background].Controls.AddRange([_backgroundAutomatic, _backgroundOpacityIcon, _backgroundOpacity, _backgroundOpacityLabel, _backgroundColor]);
        _options[ImageEffect.Crop].Controls.AddRange([.. _cropRatios.Select(r => r.Button)]);
        _options[ImageEffect.Zoom].Controls.AddRange([_zoom, _zoomLabel]);
        _options[ImageEffect.Rotate].Controls.AddRange([.. _quarterTurns, _fineAngle, _fineAngleLabel]);
        _options[ImageEffect.Flip].Controls.AddRange([_flipX, _flipY]);
        _options[ImageEffect.Frames].Controls.AddRange([_frames, _framesLabel, _freeze]);
        _options[ImageEffect.BlackAndWhite].Controls.AddRange([_grayscaleIcon, _grayscale, _grayscaleLabel]);
        _options[ImageEffect.Blur].Controls.AddRange([_gaussian, _pixelate, _blurIntensityIcon, _blurIntensity, _blurIntensityLabel]);
        _options[ImageEffect.Volume].Controls.AddRange([_mute, _volume, _volumeLabel]);
        _globalTabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _globalTabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _globalTabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _globalTabsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _globalTabsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _globalLabel.Font = new Font(Font, FontStyle.Bold);
        _globalTabsRow.Controls.Add(_globalLabel, 0, 0);
        _globalTabsRow.Controls.Add(_globalTabs, 1, 0);
        _globalTabsRow.Controls.Add(_globalResetButton, 3, 0);
        _globalOptionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _globalOptionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _globalOptionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _globalOptionsRow.Controls.Add(_globalOptionsHost, 0, 0);
        _globalOptionsRow.Controls.Add(_globalEffectResetButton, 1, 0);
        _globalOptionsHost.Controls.AddRange([.. _globalOptions.Values]);
        _bordersStyle.Items.AddRange(Enum.GetNames<BorderPattern>());
        _globalOptions[GlobalEffect.Soundtrack].Controls.AddRange([_soundtrackBrowse, _soundtrackFile, _soundtrackVolume, _soundtrackVolumeLabel]);
        _globalOptions[GlobalEffect.Borders].Controls.AddRange(
            [_bordersStyle, _bordersThickness, _bordersThicknessLabel, _bordersOpacity, _bordersOpacityLabel, _bordersOuterFrame, _bordersRounded]);

        // Docked in reverse order of addition: the options row and the tabs row, then the bottom bar,
        // the global options row above it and the global tabs row above that, span the whole width; the
        // layout strip takes the left of what remains, the file explorer the right — its splitter docked
        // against it — and the fill control goes first so it gets the rest.
        _explorer.Open = AppSettings.ExplorerPanelOpen;
        _explorer.OpenWidth = AppSettings.ExplorerWidth;
        _explorer.TileSize = AppSettings.ExplorerTileSize;
        _explorer.PagesPerLoad = AppSettings.ExplorerPagesPerLoad;
        _explorerSplitter.Visible = _explorer.Open;
        ApplyExplorerSplitterBounds();
        Controls.Add(_preview);
        Controls.Add(_explorerSplitter);
        Controls.Add(_explorer);
        Controls.Add(_layouts);
        Controls.Add(_globalTabsRow);
        Controls.Add(_globalOptionsRow);
        Controls.Add(_bottom);
        Controls.Add(_tabsRow);
        Controls.Add(_optionsRow);
        ResumeLayout(performLayout: true);

        // A long message wraps within the space left between the buttons, the bottom bar growing taller.
        _bottom.SizeChanged += (_, _) => FitStatusWidth();
        _outputButtons.SizeChanged += (_, _) => FitStatusWidth();
        _cancelButton.VisibleChanged += (_, _) => FitStatusWidth();
        _clearButton.Click += (_, _) => ClearAll();
        _settingsMenu.Items.AddRange([_startWithWindows, _borderColor, _twitterCornersDefault, _explorerFolder, _explorerPages]);
        for (int pages = FileExplorerPanel.MinPagesPerLoad; pages <= FileExplorerPanel.MaxPagesPerLoad; pages++)
        {
            int choice = pages;
            var item = new ToolStripMenuItem(choice.ToString()) { Tag = choice };
            item.Click += (_, _) => SetExplorerPagesPerLoad(choice);
            _explorerPages.DropDownItems.Add(item);
        }

        _toolTip.SetToolTip(_settingsButton, "Settings");
        _settingsButton.Click += (_, _) => ShowSettings();
        _startWithWindows.Click += (_, _) => ToggleStartWithWindows();
        _borderColor.ToolTipText = "The color of the borders, remembered between sessions";
        _borderColor.Click += (_, _) => PickBorderColor();
        _twitterCornersDefault.ToolTipText = "Whether the borders start with their Twitter corners, at start-up and after Clear all; remembered between sessions";
        _twitterCornersDefault.Click += (_, _) => ToggleTwitterCornersDefault();
        _explorerFolder.ToolTipText = "The folder the file explorer searches, with its subfolders; remembered between sessions";
        _explorerFolder.Click += (_, _) => PickExplorerFolder();
        _explorerPages.ToolTipText = "How many screens of tiles the file explorer loads at a time, the next ones as the list scrolls; remembered between sessions";
        _explorer.OpenChanged += (_, _) =>
        {
            _explorerSplitter.Visible = _explorer.Open;
            SaveExplorerPanelOpen();
        };
        _explorerSplitter.SplitterMoved += (_, _) =>
        {
            // The dragged width, in logical pixels — read here, where the DPI is the window's current one.
            _explorer.OpenWidth = DeviceToLogicalUnits(_explorer.Width);
            SaveExplorerWidth();
        };
        _explorer.TileSizeChanged += (_, _) => SaveExplorerTileSize();
        _explorer.ChooseFolderRequested += (_, _) => PickExplorerFolder();

        // A double-clicked row is added like a file from the Add images picker.
        _explorer.FileActivated += async (_, path) => await AddFilesAsync([path]);
        _copyButton.Click += (_, _) => CopyToClipboard();
        _saveButton.Click += (_, _) => Save();
        _copyGif.Click += (_, _) => Copy(GridExport.Format.Gif);
        _copyMp4.Click += (_, _) => Copy(GridExport.Format.Mp4);
        _copyForSharing.Click += (_, _) => CopyForSharing();
        _copyMenu.Items.AddRange([_copyGif, _copyMp4, _copyForSharing, new ToolStripSeparator(), _copyLast]);
        _saveGif.Click += (_, _) => SaveAs(GridExport.Format.Gif);
        _saveMp4.Click += (_, _) => SaveAs(GridExport.Format.Mp4);
        _saveMenu.Items.AddRange([_saveGif, _saveMp4, new ToolStripSeparator(), _saveLast]);

        // The last video's tooltips say whether the grid has changed since: refreshed as they are about to show.
        _copyLastButton.Click += (_, _) => CopyLastVideo();
        _copyLast.Click += (_, _) => CopyLastVideo();
        _saveLast.Click += (_, _) => SaveLastVideo();
        _copyMenu.ShowItemToolTips = true;
        _saveMenu.ShowItemToolTips = true;
        _copyLastButton.MouseEnter += (_, _) => RefreshLastVideoTooltips();
        _copyMenu.Opening += (_, _) => RefreshLastVideoTooltips();
        _saveMenu.Opening += (_, _) => RefreshLastVideoTooltips();
        _copyArrow.Click += (_, _) => _copyMenu.Show(_copyButton, Point.Empty, ToolStripDropDownDirection.AboveRight);
        _saveArrow.Click += (_, _) => _saveMenu.Show(_saveButton, Point.Empty, ToolStripDropDownDirection.AboveRight);
        _cancelButton.Click += (_, _) => _export?.Cancel();
        UpdateEffectIcons();
        FitEffectRows();

        // The Reset button sizes itself to its font and DPI: the tabs follow it.
        _resetButton.SizeChanged += (_, _) => FitEffectRows();
        _tabsRow.Paint += (_, e) => PaintOptionsEdge(e.Graphics, _tabsRow, standing: false);
        _effectTabs.TabClicked += (_, effect) => SelectEffect(effect);
        _effectTabs.CheckClicked += (_, effect) => ToggleEffect(effect);
        _toolTip.SetToolTip(_effectResetButton, "Brings this effect back to its defaults");
        _toolTip.SetToolTip(_resetButton, "Brings every effect of the cell back to its defaults, and the cells to their layout's sizes");
        _effectResetButton.Click += (_, _) => ResetSelectedEffect();
        _resetButton.Click += (_, _) => ResetEffects();
        _zoom.ValueChanged += (_, _) => SetZoom();
        _zoom.ControlWheel = StepZoom;
        for (int i = 0; i < _quarterTurns.Length; i++)
        {
            int degrees = 90 * i;
            _quarterTurns[i].Click += (_, _) => ChangeLook(ImageEffect.Rotate, look => look.WithRotation(degrees));
        }

        _fineAngle.ValueChanged += (_, _) => SetFineAngle();
        foreach (var (button, ratio) in _cropRatios)
        {
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Click += (_, _) => SetCropRatio(ratio);
        }

        _flipX.Click += (_, _) => ChangeLook(ImageEffect.Flip, look => look.ToggleFlipX());
        _flipY.Click += (_, _) => ChangeLook(ImageEffect.Flip, look => look.ToggleFlipY());
        _frames.ValueChanged += (_, _) => ChangeFrames(frames => frames.AtPage(_frames.Value, _frames.Maximum + 1));
        _frames.ControlWheel = StepFrames;
        _freeze.CheckedChanged += (_, _) => ChangeFrames(frames => frames.WithFrozen(_freeze.Checked));
        _grayscale.ValueChanged += (_, _) =>
        {
            _grayscaleLabel.Text = $"Intensity: {_grayscale.Value}%";
            ChangeLook(ImageEffect.BlackAndWhite, look => look.WithGrayscale(_grayscale.Value / 100.0));
        };

        // Click rather than CheckedChanged: choosing the kind already shown still turns the blur on.
        _gaussian.Click += (_, _) => SetBlurKind(BlurKind.Gaussian);
        _pixelate.Click += (_, _) => SetBlurKind(BlurKind.Pixelate);
        _blurIntensity.ValueChanged += (_, _) => SetBlurIntensity();
        _volume.ValueChanged += (_, _) =>
        {
            _volumeLabel.Text = VolumeText(_volume.Value);
            ChangeLook(ImageEffect.Volume, look => look.WithVolume(look.Volume!.WithLevel(_volume.Value / 100.0)));
        };
        _mute.CheckedChanged += (_, _) => ChangeLook(ImageEffect.Volume, look => look.WithVolume(look.Volume!.WithMuted(_mute.Checked)));
        _toolTip.SetToolTip(_backgroundAutomatic, "The color computed from the edges of the part of the image shown");
        _toolTip.SetToolTip(_backgroundColor, "Chooses the background color; Automatic color is then unchecked");
        _backgroundAutomatic.CheckedChanged += (_, _) => SetBackgroundAutomatic();
        _backgroundOpacity.ValueChanged += (_, _) =>
        {
            _backgroundOpacityLabel.Text = OpacityText(_backgroundOpacity.Value);
            ChangeLook(ImageEffect.Background, look => look.WithBackground(look.Background!.WithOpacity(_backgroundOpacity.Value / 100.0)));
        };
        _backgroundColor.Click += (_, _) => PickBackgroundColor();
        _globalResetButton.SizeChanged += (_, _) => FitEffectRows();
        _globalTabsRow.Paint += (_, e) => PaintOptionsEdge(e.Graphics, _globalTabsRow, standing: true);
        _globalTabs.TabClicked += (_, effect) => SelectGlobalEffect(effect);
        _globalTabs.CheckClicked += (_, effect) => ToggleGlobalEffect(effect);
        _toolTip.SetToolTip(_globalEffectResetButton, "Brings this global effect back to its initial state");
        _toolTip.SetToolTip(_globalResetButton, "Brings every global effect back to its initial state; the cells are left alone");
        _globalEffectResetButton.Click += (_, _) =>
        {
            if (_selectedGlobalEffect is { } effect)
            {
                ResetGlobalEffects(effect);
            }
        };
        _globalResetButton.Click += (_, _) => ResetGlobalEffects();
        _toolTip.SetToolTip(_soundtrackBrowse, "Mixes the sound of an audio or video file over the videos, in the preview and the MP4 export; a file can also be dropped on these rows");
        _soundtrackBrowse.Click += (_, _) => BrowseSoundtrack();
        _soundtrackVolume.ValueChanged += (_, _) => SetSoundtrackVolume();
        _toolTip.SetToolTip(_bordersStyle, "Brackets at the grid's corners, or a gap between the cells; the borders' color is in the ⚙ settings");
        _toolTip.SetToolTip(_bordersThickness, "Width of the borders, as a share of the grid's shorter side");
        _toolTip.SetToolTip(_bordersOuterFrame, "Also draws the borders around the grid; the corner brackets already are its frame");
        _bordersStyle.SelectedIndexChanged += (_, _) => ChangeBorders(borders => borders with { Pattern = (BorderPattern)_bordersStyle.SelectedIndex });
        _bordersThickness.ValueChanged += (_, _) =>
        {
            _bordersThicknessLabel.Text = ThicknessText(_bordersThickness.Value);
            ChangeBorders(borders => borders with { Thickness = _bordersThickness.Value / 1000.0 });
        };
        _toolTip.SetToolTip(_bordersOpacity, "Opacity of the corner brackets, drawn over the images");
        _bordersOpacity.ValueChanged += (_, _) =>
        {
            _bordersOpacityLabel.Text = OpacityText(_bordersOpacity.Value);
            ChangeBorders(borders => borders with { Opacity = _bordersOpacity.Value / 100.0 });
        };
        _bordersOuterFrame.CheckedChanged += (_, _) => ChangeBorders(borders => borders with { OuterFrame = _bordersOuterFrame.Checked });
        _toolTip.SetToolTip(_bordersRounded, "Rounds the grid's corners like Twitter / X shows images; the borders follow the curve");
        _bordersRounded.CheckedChanged += (_, _) => ChangeBorders(borders => borders with { Rounded = _bordersRounded.Checked });
        foreach (var row in new Control[] { _globalTabsRow, _globalOptionsRow })
        {
            row.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
            row.DragDrop += async (_, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
                {
                    await LoadSoundtrackAsync(paths[0]);
                }
            };
        }
        // Freezing the last playing content turns the export back into an image.
        _preview.SelectedImageChanged += (_, _) => UpdateButtons();
        _preview.ImagesChanged += (_, _) => UpdateButtons();
        _preview.LayoutChanged += (_, _) =>
        {
            _layouts.ActiveLayout = _preview.ActiveLayout;

            // A text may fit its new cell, or no longer: it stops or starts scrolling.
            UpdateButtons();
        };
        _layouts.LayoutPicked += (_, layout) => _preview.SetLayout(layout);
        _layouts.MirrorToggled += (_, _) => _preview.SetLayout(_preview.ActiveLayout!.Mirrored());
        _layouts.ActiveLayoutClicked += (_, _) => _preview.ResetCellSizes();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _preview.DragEnter += OnDragEnter;
        _preview.DragOver += OnPreviewDragOver;
        _preview.DragLeave += (_, _) => _preview.ShowDropTarget(null);
        _preview.DragDrop += OnDragDrop;
        _preview.AddImagesClicked += (_, _) => PickFiles();
        _preview.ShowInExplorerClicked += (_, path) => ShowInExplorer(path);
        _preview.SwapDragMoved += (_, point) => _explorer.ShowDropFrame(point is { } at && _explorer.ContainsScreenPoint(at));
        _preview.ReleasedOffGrid += (_, released) =>
        {
            if (_explorer.ContainsScreenPoint(released.ScreenPoint))
            {
                AddToFavorites(released.Image);
            }
        };
        _explorer.MessageWhileCollapsed += (_, message) => ShowStatus(message.Text, message.Error);
        _layouts.DragEnter += OnDragEnter;
        _layouts.DragDrop += OnDragDrop;
        _preview.Borders = ActiveBorders;
        UpdateButtons();
        FitStatusWidth();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsMenu.Dispose();
            _copyMenu.Dispose();
            _saveMenu.Dispose();
            _toolTip.Dispose();
            _resetButton.Image?.Dispose();
            _effectResetButton.Image?.Dispose();
            _globalResetButton.Image?.Dispose();
            _globalEffectResetButton.Image?.Dispose();
            _effectsLabel.Font.Dispose();
            _globalLabel.Font.Dispose();
            _grayscaleIcon.Image?.Dispose();
            _gaussian.Image?.Dispose();
            _pixelate.Image?.Dispose();
            _blurIntensityIcon.Image?.Dispose();
            _backgroundOpacityIcon.Image?.Dispose();
            foreach (var (button, _) in _cropRatios)
            {
                button.Image?.Dispose();
            }

            _borderColor.Image?.Dispose();
            _colorDialog.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>The icons are drawn at the monitor's DPI: again when the window moves to another one.</summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyExplorerSplitterBounds();
        UpdateEffectIcons();
        FitEffectRows();
    }

    private void UpdateEffectIcons()
    {
        int size = LogicalToDeviceUnits(16);
        Image?[] previous = [_resetButton.Image, _effectResetButton.Image, _globalResetButton.Image, _globalEffectResetButton.Image, _grayscaleIcon.Image, _gaussian.Image, _pixelate.Image, _blurIntensityIcon.Image, _backgroundOpacityIcon.Image, .. _cropRatios.Select(r => r.Button.Image)];
        foreach (var effect in Enum.GetValues<ImageEffect>())
        {
            _effectTabs.SetIcon(effect, effect switch
            {
                ImageEffect.Background => EffectIcons.Background(size),
                ImageEffect.Crop => EffectIcons.Crop(size),
                ImageEffect.Zoom => EffectIcons.Zoom(size),
                ImageEffect.Rotate => EffectIcons.Rotate(size),
                ImageEffect.Flip => EffectIcons.Flip(size),
                ImageEffect.Frames => EffectIcons.Frames(size),
                ImageEffect.BlackAndWhite => EffectIcons.BlackAndWhite(size),
                ImageEffect.Volume => EffectIcons.Volume(size),
                _ => EffectIcons.Blur(size),
            });
        }

        _globalTabs.SetIcon(GlobalEffect.Soundtrack, EffectIcons.Soundtrack(size));
        _globalTabs.SetIcon(GlobalEffect.Borders, EffectIcons.Borders(size));
        _resetButton.Image = EffectIcons.Reset(size);
        _effectResetButton.Image = EffectIcons.Reset(size);
        _globalResetButton.Image = EffectIcons.Reset(size);
        _globalEffectResetButton.Image = EffectIcons.Reset(size);
        _grayscaleIcon.Image = EffectIcons.Intensity(size);
        _grayscaleIcon.Size = new Size(size, size);
        _gaussian.Image = EffectIcons.Gaussian(size);
        _pixelate.Image = EffectIcons.Pixelate(size);
        foreach (var (button, ratio) in _cropRatios)
        {
            button.Image = EffectIcons.Ratio(size, ratio);
        }

        _blurIntensityIcon.Image = EffectIcons.Intensity(size);
        _blurIntensityIcon.Size = new Size(size, size);
        _backgroundOpacityIcon.Image = EffectIcons.Opacity(size);
        _backgroundOpacityIcon.Size = new Size(size, size);
        foreach (var image in previous)
        {
            image?.Dispose();
        }

        UpdateBorderColorSwatch();
    }

    /// <summary>The ⚙ menu's Border color item shows the color as a swatch, drawn at the monitor's DPI.</summary>
    private void UpdateBorderColorSwatch()
    {
        int size = LogicalToDeviceUnits(16);
        var swatch = new Bitmap(size, size);
        using (var g = Graphics.FromImage(swatch))
        using (var fill = new SolidBrush(_borders.Color))
        {
            g.FillRectangle(fill, 0, 0, size, size);
            g.DrawRectangle(SystemPens.ControlDark, 0, 0, size - 1, size - 1);
        }

        var previous = _borderColor.Image;
        _borderColor.Image = swatch;
        previous?.Dispose();
    }

    /// <summary>
    /// The tabs as tall as the Reset beside them; each options row as tall as its tallest options, so
    /// nothing beyond it moves when another tab is selected — the cell effects' and the global effects'.
    /// </summary>
    private void FitEffectRows()
    {
        _effectTabs.Size = new Size(_effectTabs.GetPreferredSize(Size.Empty).Width, _resetButton.GetPreferredSize(Size.Empty).Height);
        int options = _options.Values.Max(row => row.GetPreferredSize(Size.Empty).Height);
        int reset = _effectResetButton.GetPreferredSize(Size.Empty).Height + _effectResetButton.Margin.Vertical;
        _optionsRow.Height = Math.Max(options, reset) + _optionsRow.Padding.Vertical;
        _globalTabs.Size = new Size(_globalTabs.GetPreferredSize(Size.Empty).Width, _globalResetButton.GetPreferredSize(Size.Empty).Height);
        int globalOptions = _globalOptions.Values.Max(row => row.GetPreferredSize(Size.Empty).Height);
        int globalReset = _globalEffectResetButton.GetPreferredSize(Size.Empty).Height + _globalEffectResetButton.Margin.Vertical;
        _globalOptionsRow.Height = Math.Max(globalOptions, globalReset) + _globalOptionsRow.Padding.Vertical;
    }

    /// <summary>
    /// The edge of the options row across a tabs row: along its top when the tabs hang from the row
    /// above, along its bottom when they stand on the row below; the tabs open it at the selected one.
    /// </summary>
    private static void PaintOptionsEdge(Graphics g, Control tabsRow, bool standing)
    {
        using var border = new Pen(SystemColors.ControlDark);
        int y = standing ? tabsRow.Height - 1 : 0;
        g.DrawLine(border, 0, y, tabsRow.Width, y);
    }

    /// <summary>
    /// The window opens at the size it had at its last use, else at its default size made taller by
    /// the global effects' tabs row, so the preview keeps the size it had when the global effects
    /// sat in a single row; either way within the working area of its screen, and centered again on it.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _opened = true;

        var area = Screen.FromControl(this).WorkingArea;
        int height = _sizeRemembered ? Height : Height + _globalTabsRow.Height;
        Size = new Size(
            Math.Max(MinimumSize.Width, Math.Min(Width, area.Width)),
            Math.Max(MinimumSize.Height, Math.Min(height, area.Height)));
        CenterToScreen();
    }

    /// <summary>Closes the window for real, instead of hiding it; the tray's Quit.</summary>
    public void CloseForGood()
    {
        _closingForGood = true;
        Close();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = Task.Run(CleanTempVideos);

        // Copy and Save start disabled, so the slider would take the focus and move with unaimed keys or wheel.
        _preview.Focus();

        // The cached index first, then the folder rescanned in the background: the window is up already.
        _explorer.Start(AppSettings.ExplorerFolder);

        // Files dropped on the .exe icon; loaded once the window is visible so startup stays fast.
        if (_startupFiles.Length > 0)
        {
            await AddFilesAsync(_startupFiles);
        }
    }

    /// <summary>Hidden in the tray, the preview stops playing; shown again, it plays from the start.</summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        _preview.WindowVisibleChanged();
    }

    /// <summary>
    /// The ×, Alt+F4 and the taskbar's Close window only hide it: the app keeps running in the tray,
    /// grid unchanged. A real close (Quit, logoff, shutdown) during an export cancels it first; the
    /// window closes once it has stopped. Either way, its size is remembered first.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveWindowSize();

        if (e.CloseReason == CloseReason.UserClosing && !_closingForGood)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (_export is not null)
        {
            e.Cancel = true;
            _closeAfterExport = true;
            _export.Cancel();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Typed in the file explorer's search box, these keys edit its text, not the grid.
        if (_explorer.IsEditingText && keyData is (Keys.Control | Keys.V) or (Keys.Control | Keys.C) or Keys.Delete or Keys.Escape)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        switch (keyData)
        {
            case Keys.Control | Keys.V:
                Paste();
                return true;
            case Keys.Control | Keys.C:
                CopyToClipboard();
                return true;
            case Keys.Control | Keys.S:
                Save();
                return true;
            case Keys.Delete when _preview.HasSelection && !IsExporting:
                _preview.RemoveSelected();
                return true;
            case Keys.Escape when _preview.HasSelection:
                _preview.ClearSelection();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data is { } data && (data.GetDataPresent(DataFormats.FileDrop) || TextData.IsIn(data))
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        // WinForms hands the drag to the shell helper only when a drop image type is set, and the
        // following DragOver events keep it: that is what keeps Explorer's thumbnail over the window.
        if (e.Effect == DragDropEffects.Copy)
        {
            e.DropImageType = DropImageType.Copy;
        }
    }

    private void OnPreviewDragOver(object? sender, DragEventArgs e)
    {
        if (e.Effect != DragDropEffects.None && !IsExporting)
        {
            _preview.ShowDropTarget(_preview.PointToClient(new Point(e.X, e.Y)));
        }
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        _preview.ShowDropTarget(null);

        // Onto a cell: replaces it. Onto the drop zone or elsewhere in the window: added like a paste.
        int target = sender == _preview ? DropCell(e) : -1;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await AddFilesAsync(paths, target);
        }
        else if (e.Data is not null && TextData.TryGet(e.Data) is { } text)
        {
            await AddTextAsync(text, target, "Nothing added: the dropped text is empty.", dropped: true);
        }
    }

    /// <summary>
    /// A cell's image dropped by its ✥ handle onto the file explorer: its file becomes a favorite. An
    /// image without one is saved first into favorites-from-pasted — a pasted image as a PNG, a text in
    /// the form it arrived — and adopts that file (see workfiles/20260930-favorites-drag-drop.md).
    /// </summary>
    private void AddToFavorites(SourceImage image)
    {
        if (image.FilePath is null)
        {
            string? saved;
            try
            {
                saved = image.TextOrigin is { } text ? PastedFavorites.SaveText(text.Content, text.Extension)
                    : image.Pages is null ? PastedFavorites.SaveImage(image.Bitmap)
                    : null;
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is ExternalException)
            {
                _explorer.Report($"Not added to favorites: the image could not be saved: {ex.Message}", error: true);
                return;
            }

            if (saved is null)
            {
                _explorer.Report("Not added to favorites: this image has no file and cannot be saved", error: true);
                return;
            }

            image.AdoptFile(saved);
            _preview.RefreshSourceName();
        }

        _explorer.AddFavorites([image.FilePath!]);
    }

    private int DropCell(DragEventArgs e) => _preview.CellAt(_preview.PointToClient(new Point(e.X, e.Y)));

    /// <summary>Files chosen from the picker (drop zone or empty canvas) are added like a drop onto the drop zone.</summary>
    private async void PickFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add images",
            Multiselect = true,
            Filter = PickerFilter(),
            InitialDirectory = ExistingFolder(AppSettings.AddFolder) ?? "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RememberFolder(AppSettings.SaveAddFolder, dialog.FileName, "Add images");
            await AddFilesAsync(dialog.FileNames);
        }
    }

    /// <summary>Videos the pickers offer, mirroring <see cref="VideoFrames"/>.</summary>
    private static readonly string[] VideoExtensions = ["mp4", "m4v", "mov", "avi", "wmv", "asf", "mkv", "webm", "3gp", "3g2", "mpg", "mpeg", "ts", "m2ts", "mts"];

    private static string Patterns(IEnumerable<string> extensions) => string.Join(";", extensions.Select(e => $"*.{e}"));

    /// <summary>
    /// Every supported type at once, then one filter per type, then all files. Videos mirror
    /// <see cref="VideoFrames"/>; text is detected by content, so only its common extensions are listed.
    /// </summary>
    private static string PickerFilter()
    {
        (string Name, string[] Extensions)[] types =
        [
            ("Images", new[] { "png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff", "webp" }),
            ("Videos", VideoExtensions),
            ("PDF", new[] { "pdf" }),
            ("Text", new[] { "txt", "md", "log", "csv", "json", "xml" }),
        ];

        return string.Join(
            "|",
            types.Select(t => $"{t.Name}|{Patterns(t.Extensions)}")
                .Prepend($"Supported files|{Patterns(types.SelectMany(t => t.Extensions))}")
                .Append("All files (*.*)|*.*"));
    }

    private async void Paste()
    {
        if (RefuseWhileExporting())
        {
            return;
        }

        const string Nothing = "Nothing to paste: the clipboard holds no image or text.";
        string[]? files = null;
        TextData? text = null;
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                files = Clipboard.GetFileDropList().Cast<string>().ToArray();
            }
            else if (Clipboard.GetImage() is { } image)
            {
                // An image wins over the text that may come with it: Excel cells paste as a picture.
                using (image)
                {
                    _preview.Add([ImageLoader.FromImage(image)]);
                }

                return;
            }
            else if (Clipboard.GetDataObject() is { } data)
            {
                text = TextData.TryGet(data);
            }
        }
        catch (ExternalException ex)
        {
            ShowStatus($"Paste failed: {ex.Message}", error: true);
            return;
        }

        if (text is not null)
        {
            await AddTextAsync(text, -1, Nothing, dropped: false);
        }
        else if (files is null)
        {
            ShowStatus(Nothing);
        }
        else
        {
            await AddFilesAsync(files);
        }
    }

    /// <summary>
    /// Adds a text as an image rendered like a text file, placed like a file: into the target cell,
    /// else a free slot, else by the replace rule. Parsed and laid out off the UI thread.
    /// <paramref name="dropped"/> tells a dropped text from a pasted one, the name the preview gives it.
    /// </summary>
    private async Task AddTextAsync(TextData text, int targetCell, string blankMessage, bool dropped)
    {
        if (RefuseWhileExporting())
        {
            return;
        }

        var (image, length) = await Task.Run(() =>
        {
            var parsed = text.Parse();
            if (parsed is not { Text: var styled, Origin: var origin } || styled.Text.Length > StyledText.MaxLength)
            {
                return ((SourceImage?)null, parsed?.Text.Text.Length ?? 0);
            }

            var loaded = ImageLoader.FromText(styled);
            loaded?.TextOrigin = origin;
            return (loaded, styled.Text.Length);
        });

        if (length > StyledText.MaxLength)
        {
            ShowStatus($"Text too long: {length:N0} characters, {StyledText.MaxLength:N0} at most.");
        }
        else if (image is null)
        {
            ShowStatus(blankMessage);
        }
        else
        {
            image.Dropped = dropped;
            _preview.Add([image], targetCell);
        }
    }

    /// <summary>
    /// Loads files in the given order, off the UI thread, and only as many as can be placed:
    /// the target cell, the free slots, and one excess file for the replace rule.
    /// </summary>
    private async Task AddFilesAsync(string[] paths, int targetCell = -1)
    {
        if (RefuseWhileExporting())
        {
            return;
        }

        int wanted = (targetCell >= 0 ? 1 : 0) + _preview.FreeSlots + 1;
        var (images, skipped, notLoaded) = await Task.Run(() =>
        {
            var loaded = new List<SourceImage>();
            int unreadable = 0, attempted = 0;
            foreach (var path in paths)
            {
                if (loaded.Count == wanted)
                {
                    break;
                }

                attempted++;
                if (ImageLoader.TryLoadFile(path) is { } image)
                {
                    loaded.Add(image);
                }
                else
                {
                    unreadable++;
                }
            }

            return (loaded, unreadable, paths.Length - attempted);
        });
        int ignored = notLoaded + _preview.Add(images, targetCell);

        var messages = new List<string>();
        if (skipped > 0)
        {
            messages.Add($"{Files(skipped)} skipped: no preview available");
        }

        if (ignored > 0)
        {
            messages.Add($"{Files(ignored)} ignored: the grid holds {GridLayout.MaxImages} images at most");
        }

        if (messages.Count > 0)
        {
            ShowStatus(string.Join(" · ", messages) + ".");
        }
    }

    /// <summary>Removes every image and the global effects: back to the initial state.</summary>
    private void ClearAll()
    {
        if (IsExporting)
        {
            return;
        }

        int count = _preview.Images.Count;
        bool soundtrack = _soundtrack is not null;
        bool borders = !BordersInitial;
        if (count == 0 && SoundtrackInitial && !borders)
        {
            return;
        }

        _preview.Clear();
        ResetGlobalEffects();

        var removed = new List<string>();
        if (count > 0)
        {
            removed.Add(count == 1 ? "1 image" : $"{count} images");
        }

        if (soundtrack)
        {
            removed.Add(count > 0 ? "the soundtrack" : "The soundtrack");
        }

        ShowStatus(removed.Count > 0 ? $"{string.Join(" and ", removed)} removed." : "Borders back to their initial state.");
    }

    /// <summary>Copies the grid in the format its content suits: a PNG, or an MP4 video while a content plays or a soundtrack is on.</summary>
    private void CopyToClipboard() => Copy(AdaptedFormat);

    /// <summary>
    /// Copies the grid: a still image, or — <paramref name="format"/> given — an MP4 video or a GIF,
    /// written to the temp folder and put on the clipboard as a file; a GIF also as its bytes, in the
    /// clipboard's GIF format that some apps paste directly. The video or GIF becomes the last video,
    /// offered back by Copy last and Save last.
    /// </summary>
    private async void Copy(GridExport.Format? format)
    {
        if (_preview.Images.Count == 0 || IsExporting)
        {
            return;
        }

        if (format is { } animated)
        {
            string path = TempExportPath(Extension(animated));
            int gridVersion = _preview.ContentVersion;
            var animationClock = Stopwatch.StartNew();
            if (await ExportAnimationAsync(path, animated) is { } animation)
            {
                var encoding = animationClock.Elapsed;
                LastVideo? last = null;
                try
                {
                    // Kept before the clipboard step: a copy failing there is retried with Copy last.
                    last = new LastVideo(path, animated, DateTime.Now, animation, new FileInfo(path).Length, encoding, gridVersion);
                    _lastVideo = last;
                    UpdateButtons();
                    PutOnClipboard(last);
                    ShowStatus(AnimationSummary($"Copied {last.FileName}", last));
                    NotifyIfAway($"{FormatName(animated)} copied", $"{last.FileName} is on the clipboard. If it gets overwritten, Copy last {FormatName(animated)} brings it back.", error: false);
                }
                catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
                {
                    ShowStatus($"Copy failed: {ex.Message}", error: true);
                    NotifyIfAway("Copy failed", last is null ? ex.Message : $"{ex.Message} Copy last {FormatName(animated)} puts {last.FileName} on the clipboard again.", error: true);
                }
            }

            return;
        }

        var clock = Stopwatch.StartNew();
        using var result = await RenderStillAsync();
        if (result is null)
        {
            return;
        }

        try
        {
            using var png = new MemoryStream();
            result.Save(png, ImageFormat.Png);
            var encoding = clock.Elapsed;

            // Standard bitmap for most apps, flattened on white as it carries no transparency, plus the
            // PNG format that browsers paste more reliably, which keeps it.
            using var flat = Compositor.Flattened(result);
            var data = new DataObject();
            data.SetImage(flat);
            data.SetData("PNG", png);
            Clipboard.SetDataObject(data, copy: true);
            ShowStatus(StillSummary("Copied to the clipboard", "PNG", result.Size, png.Length, encoding));
        }
        catch (ExternalException ex)
        {
            ShowStatus($"Copy failed: {ex.Message}", error: true);
        }
    }

    /// <summary>Long edge of the light copy: chat apps recompress to about 1600 px anyway.</summary>
    /// <summary>Puts a generated MP4 or GIF on the clipboard: its file, plus its bytes in the clipboard's GIF format for a GIF.</summary>
    private static void PutOnClipboard(LastVideo video)
    {
        var data = new DataObject();
        data.SetFileDropList(new StringCollection { video.FilePath });
        if (video.Format == GridExport.Format.Gif)
        {
            data.SetData("GIF", new MemoryStream(File.ReadAllBytes(video.FilePath)));
        }

        Clipboard.SetDataObject(data, copy: true);
    }

    /// <summary>
    /// Puts the last video back on the clipboard, as its copy did — nothing generated: the clipboard
    /// was overwritten before it was pasted. The grid may have changed since, or be empty.
    /// </summary>
    private void CopyLastVideo()
    {
        if (_lastVideo is not { } last || RefuseWhileExporting() || !LastVideoStillThere(last))
        {
            return;
        }

        try
        {
            PutOnClipboard(last);
            ShowStatus(AnimationSummary($"Copied again {last.FileName} (generated at {last.GeneratedAt:HH:mm})", last));
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Copy failed: {ex.Message}", error: true);
        }
    }

    /// <summary>Saves the last video where the user chooses: its file copied there, nothing generated.</summary>
    private void SaveLastVideo()
    {
        if (_lastVideo is not { } last || RefuseWhileExporting() || !LastVideoStillThere(last))
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = SaveFilter(last.Format),
            DefaultExt = Extension(last.Format),
            FileName = last.FileName,
            InitialDirectory = ExportFolder(),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberFolder(AppSettings.SaveExportFolder, dialog.FileName, "Export");

        try
        {
            File.Copy(last.FilePath, dialog.FileName, overwrite: true);
            ShowStatus(AnimationSummary($"Saved {Path.GetFileName(dialog.FileName)} (the last generated {FormatName(last.Format)}, from {last.GeneratedAt:HH:mm})", last));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    /// <summary>
    /// Whether the last video's file is still in the temp folder. Gone — a temp cleaner, a deletion by
    /// hand — it is forgotten, the status line saying so, and the actions go back to their disabled state.
    /// </summary>
    private bool LastVideoStillThere(LastVideo last)
    {
        if (File.Exists(last.FilePath))
        {
            return true;
        }

        ShowStatus($"The last generated {FormatName(last.Format)}, {last.FileName}, is gone from the temp folder.", error: true);
        _lastVideo = null;
        UpdateButtons();
        return false;
    }

    /// <summary>The tooltips of Copy last and Save last: what the last video holds, and whether the grid has changed since it was generated.</summary>
    private void RefreshLastVideoTooltips()
    {
        string text = "";
        if (_lastVideo is { } last)
        {
            text = AnimationSummary($"Generated at {last.GeneratedAt:HH:mm}", last);
            if (last.GridVersion != _preview.ContentVersion)
            {
                text += " — the grid has changed since";
            }
        }

        _toolTip.SetToolTip(_copyLastButton, text);
        _copyLast.ToolTipText = text;
        _saveLast.ToolTipText = text;
    }

    /// <summary>
    /// The length readout: what the MP4 video Copy and Save would produce lasts — the longest playing
    /// loop, or the soundtrack's length over stills (Animation.VideoLength) — "—" while they produce
    /// a PNG. Refreshed with the captions, so the two never disagree; its tooltip names what sets the
    /// length and how many times every other animated content plays.
    /// </summary>
    private void RefreshLength()
    {
        var images = _preview.Images;
        var soundtrack = ActiveSoundtrack;
        var length = images.Count == 0 ? TimeSpan.Zero : Animation.VideoLength(images, soundtrack);
        _lengthReadout.Text = length > TimeSpan.Zero ? $"⏱ {Seconds(length)}" : "⏱ —";
        // As wide as a length under 1000 s: the digits change without moving the gear.
        _lengthReadout.MinimumSize = new Size(TextRenderer.MeasureText("⏱ 000.0 s", _lengthReadout.Font).Width, 0);
        _toolTip.SetToolTip(_lengthReadout, LengthDetail(images, soundtrack, length));
    }

    /// <summary>
    /// The length readout's tooltip: what sets the length, then every other animated content with its
    /// loop and how many times it plays (a frozen one is a still), in cell order, and the soundtrack,
    /// cut or looping, when one is on over playing contents.
    /// </summary>
    private static string LengthDetail(IReadOnlyList<SourceImage> images, Soundtrack? soundtrack, TimeSpan length)
    {
        if (images.Count == 0)
        {
            return "No image";
        }

        string Name(SourceImage image) => image.FilePath is { } path
            ? Path.GetFileName(path)
            : $"cell {Enumerable.Range(0, images.Count).First(n => images[n] == image) + 1}";

        var lines = new List<string>();
        var longest = images.Where(i => i.Plays).OrderByDescending(i => i.Pages!.LoopDuration).FirstOrDefault();
        if (longest is not null)
        {
            lines.Add($"MP4 video of {Seconds(length)}: the longest loop, {Name(longest)}'s");
        }
        else if (soundtrack is not null)
        {
            lines.Add($"MP4 video of {Seconds(length)}: the soundtrack's length, {Path.GetFileName(soundtrack.Path)}'s; the images are stills");
        }
        else
        {
            lines.Add("A PNG: no content plays and the soundtrack is off");
        }

        foreach (var image in images.Where(i => i.IsAnimated && i != longest))
        {
            var loop = image.Pages!.LoopDuration;
            lines.Add(image.IsFrozen
                ? $"{Name(image)} — frozen, a still"
                : $"{Name(image)} — {Seconds(loop)}, plays {Times(length, loop)}");
        }

        if (soundtrack is not null && longest is not null)
        {
            string name = Path.GetFileName(soundtrack.Path);
            lines.Add(soundtrack.Duration > length
                ? $"Soundtrack {name} — {Seconds(soundtrack.Duration)}, cut at {Seconds(length)}"
                : $"Soundtrack {name} — {Seconds(soundtrack.Duration)}, plays {Times(length, soundtrack.Duration)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>How many times a loop of <paramref name="loop"/> plays within <paramref name="length"/>: "once", "3.1 times".</summary>
    private static string Times(TimeSpan length, TimeSpan loop)
    {
        double times = loop > TimeSpan.Zero ? Math.Round(length.Ticks / (double)loop.Ticks, 1) : 0;
        return times == 1 ? "once" : $"{times:0.#} times";
    }

    private const int SharingMaxEdge = 2560;

    private const long SharingJpegQuality = 90;

    /// <summary>
    /// Copies a light JPEG of the still, for chat apps capping image size (WhatsApp: 16 MB): its long
    /// edge at most <see cref="SharingMaxEdge"/> px, flattened on white. Written to the temp folder and
    /// put on the clipboard as a file, plus the same image as a bitmap for apps pasting bitmaps only.
    /// </summary>
    private async void CopyForSharing()
    {
        if (_preview.Images.Count == 0 || IsExporting)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        using var result = await RenderStillAsync();
        if (result is null)
        {
            return;
        }

        try
        {
            using var light = Compositor.Flattened(result, SharingMaxEdge);
            string path = TempExportPath("jpg");
            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, SharingJpegQuality);
                light.Save(path, jpeg, parameters);
            }

            var encoding = clock.Elapsed;
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { path });
            data.SetImage(light);
            Clipboard.SetDataObject(data, copy: true);
            ShowStatus(StillSummary("Copied for sharing", "JPEG", light.Size, new FileInfo(path).Length, encoding));
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Copy failed: {ex.Message}", error: true);
        }
    }

    /// <summary>Saves the grid in the format its content suits: a PNG, or an MP4 video while a content plays or a soundtrack is on.</summary>
    private void Save() => SaveAs(AdaptedFormat);

    /// <summary>Saves the grid as a PNG, or — <paramref name="format"/> given — as an MP4 video or a GIF.</summary>
    private async void SaveAs(GridExport.Format? format)
    {
        if (_preview.Images.Count == 0 || IsExporting)
        {
            return;
        }

        string extension = Extension(format);
        using var dialog = new SaveFileDialog
        {
            Filter = SaveFilter(format),
            DefaultExt = extension,
            FileName = $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
            InitialDirectory = ExportFolder(),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberFolder(AppSettings.SaveExportFolder, dialog.FileName, "Export");

        string saved = $"Saved {Path.GetFileName(dialog.FileName)}";
        var clock = Stopwatch.StartNew();
        if (format is { } animated)
        {
            if (await ExportAnimationAsync(dialog.FileName, animated) is { } result)
            {
                ShowStatus(AnimationSummary(saved, dialog.FileName, animated, result, clock.Elapsed));
                NotifyIfAway($"{FormatName(animated)} saved", Path.GetFileName(dialog.FileName), error: false);
            }

            return;
        }

        using var still = await RenderStillAsync();
        if (still is null)
        {
            return;
        }

        try
        {
            still.Save(dialog.FileName, ImageFormat.Png);
            ShowStatus(StillSummary(saved, "PNG", still.Size, new FileInfo(dialog.FileName).Length, clock.Elapsed));
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    private bool IsExporting => _export is not null;

    /// <summary>Content that plays: a frozen one is exported as a still.</summary>
    private bool HasAnimation => _preview.Images.Any(i => i.Plays);

    /// <summary>The soundtrack mixed into the preview and the MP4 export; <c>null</c> while off.</summary>
    private Soundtrack? ActiveSoundtrack => _soundtrackOn ? _soundtrack : null;

    /// <summary>The borders drawn on the preview and the exports; <c>null</c> while off.</summary>
    private GridBorders? ActiveBorders => _bordersOn ? _borders : null;

    /// <summary>Whether the borders are as the app starts: off, with their initial settings.</summary>
    private bool BordersInitial => !_bordersOn && _borders == GridBorders.Initial(_borders.Color, _roundedByDefault);

    /// <summary>Whether the soundtrack is in its initial state: no file, off, the level at 100 %.</summary>
    private bool SoundtrackInitial => _soundtrack is null && _soundtrackLevel == 1;

    /// <summary>A video to export: content that plays, or a soundtrack over stills.</summary>
    private bool ProducesVideo => HasAnimation || ActiveSoundtrack is not null;

    /// <summary>What Copy and Save produce without a format picked from their menu: an MP4 video while a content plays or a soundtrack is on, else a PNG (null).</summary>
    private GridExport.Format? AdaptedFormat => ProducesVideo ? GridExport.Format.Mp4 : null;

    private static string Extension(GridExport.Format? format) => format switch
    {
        GridExport.Format.Mp4 => "mp4",
        GridExport.Format.Gif => "gif",
        _ => "png",
    };

    private static string SaveFilter(GridExport.Format? format) => format switch
    {
        GridExport.Format.Mp4 => "MP4 video (*.mp4)|*.mp4",
        GridExport.Format.Gif => "GIF image (*.gif)|*.gif",
        _ => "PNG image (*.png)|*.png",
    };

    /// <summary>"MP4" or "GIF", as the buttons and the notifications name the format.</summary>
    private static string FormatName(GridExport.Format format) => format == GridExport.Format.Gif ? "GIF" : "MP4";

    /// <summary>
    /// Renders the still: the images as shown, or, for animated content, the page each one shows (a
    /// frozen one, its frame), at full size — off the UI thread, the grid locked meanwhile. Returns
    /// null on failure.
    /// </summary>
    private async Task<Bitmap?> RenderStillAsync()
    {
        if (!_preview.Images.Any(i => i.IsAnimated))
        {
            Cursor.Current = Cursors.WaitCursor;
            return Compositor.Render(_preview.Images, _preview.ActiveLayout!, ActiveBorders);
        }

        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!, ActiveBorders);
        BeginExport("Rendering the image…", cancellable: false);
        try
        {
            return await Task.Run(() => GridExport.RenderStill(job));
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            ShowStatus($"Export failed: {ex.Message}", error: true);
            return null;
        }
        finally
        {
            EndExport();
        }
    }

    /// <summary>
    /// Writes the MP4 video or the GIF to <paramref name="path"/> off the UI thread, with its progress
    /// and a Cancel button in the status line, the grid locked meanwhile: the contents playing.
    /// Returns null when cancelled or failing.
    /// </summary>
    private async Task<GridExport.Result?> ExportAnimationAsync(string path, GridExport.Format format)
    {
        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!, ActiveBorders, ActiveSoundtrack);
        string what = format == GridExport.Format.Gif ? "GIF" : "video";
        var cancellation = BeginExport($"Exporting the {what}… 0 %", cancellable: true);
        var progress = new Progress<double>(done =>
        {
            // Reports still queued when the export ends are dropped: they would hide its outcome.
            if (IsExporting)
            {
                ShowStatus($"Exporting the {what}… {done:P0}");
            }
        });
        try
        {
            return await Task.Run(() => GridExport.RenderAnimation(job, format, path, progress, cancellation));
        }
        catch (OperationCanceledException)
        {
            ShowStatus(format == GridExport.Format.Gif ? "GIF export cancelled." : "Video export cancelled.");
            return null;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Export failed: {ex.Message}", error: true);
            NotifyIfAway($"{FormatName(format)} export failed", ex.Message, error: true);
            return null;
        }
        finally
        {
            EndExport();
        }
    }

    private CancellationToken BeginExport(string message, bool cancellable)
    {
        _export = new CancellationTokenSource();
        _preview.Locked = true;
        _layouts.Enabled = false;
        _cancelButton.Visible = cancellable;
        UpdateButtons();
        ShowStatus(message);
        return _export.Token;
    }

    private void EndExport()
    {
        _export?.Dispose();
        _export = null;
        _preview.Locked = false;
        _layouts.Enabled = true;
        _cancelButton.Visible = false;
        UpdateButtons();
        if (_closeAfterExport)
        {
            Close();
        }
    }

    /// <summary>
    /// Raised when an animated export ends — in success or failure — while the window is hidden in the
    /// tray and no window of the app is active: what a notification should say.
    /// </summary>
    public event EventHandler<ExportNotice>? ExportEndedHidden;

    /// <summary>What a notification says of an export that ended while the window was hidden.</summary>
    public sealed record ExportNotice(string Title, string Text, bool Error);

    /// <summary>
    /// Draws attention to an animated export that ended while the user was elsewhere: the taskbar
    /// button flashes until the window comes to the front, or, the window hidden in the tray, the tray
    /// icon notifies. Nothing while a window of the app is active, or when the app is quitting.
    /// </summary>
    private void NotifyIfAway(string title, string text, bool error)
    {
        if (Form.ActiveForm is not null || _closingForGood || _closeAfterExport)
        {
            return;
        }

        if (Visible)
        {
            FlashTaskbar();
        }
        else
        {
            ExportEndedHidden?.Invoke(this, new ExportNotice(title, text, error));
        }
    }

    /// <summary>Flashes the taskbar button until the window comes to the foreground.</summary>
    private void FlashTaskbar()
    {
        var flash = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = Handle,
            Flags = FlashTray | FlashUntilForeground,
        };
        FlashWindowEx(ref flash);
    }

    private const uint FlashTray = 0x2;
    private const uint FlashUntilForeground = 0xC;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    /// <summary>What a still copy or save produced, for the status line: one frame, no duration, no sound.</summary>
    private static string StillSummary(string done, string format, Size size, long bytes, TimeSpan encoding) =>
        Summary(done, format, size, bytes, 1, TimeSpan.Zero, sound: null, encoding);

    /// <summary>What a video or GIF copy or save produced, for the status line, <paramref name="path"/> being the file written.</summary>
    private static string AnimationSummary(string done, string path, GridExport.Format format, GridExport.Result video, TimeSpan encoding) =>
        Summary(
            done,
            format == GridExport.Format.Gif ? "GIF" : "MP4 video",
            video.Size,
            new FileInfo(path).Length,
            video.Frames,
            video.Length,
            SoundSummary(video),
            encoding);

    /// <summary>What the last video holds, for the status line and the tooltips — from what was kept, its file not read again.</summary>
    private static string AnimationSummary(string done, LastVideo last) =>
        Summary(
            done,
            last.Format == GridExport.Format.Gif ? "GIF" : "MP4 video",
            last.Video.Size,
            last.Bytes,
            last.Video.Frames,
            last.Video.Length,
            SoundSummary(last.Video),
            last.Encoding);

    /// <summary>The files whose sound is in the video, then the ones Windows could not re-encode.</summary>
    private static string SoundSummary(GridExport.Result video)
    {
        static string Names(IEnumerable<string> paths) => string.Join(" + ", paths.Select(Path.GetFileName));
        string failed = video.FailedSounds.Count == 0 ? ""
            : $"{Names(video.FailedSounds)}: {(video.FailedSounds.Count == 1 ? "its sound" : "their sounds")} cannot be re-encoded";
        return video.MixedSounds.Count == 0
            ? failed.Length == 0 ? "no sound" : $"no sound ({failed})"
            : $"sound: {Names(video.MixedSounds)}{(failed.Length == 0 ? "" : $" ({failed})")}";
    }

    private static string Summary(string done, string format, Size size, long bytes, int frames, TimeSpan length, string? sound, TimeSpan encoding)
    {
        var fields = new List<string>
        {
            done,
            format,
            $"{size.Width} × {size.Height}",
            FileSize(bytes),
            frames == 1 ? "1 frame" : $"{frames} frames",
            Seconds(length),
        };
        if (sound is not null)
        {
            fields.Add(sound);
        }

        fields.Add($"encoded in {Seconds(encoding)}");
        return string.Join(" · ", fields);
    }

    /// <summary>Binary units: whole kilobytes, then one decimal.</summary>
    private static string FileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.0} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.0} GB",
    };

    private static string Seconds(TimeSpan time) => time == TimeSpan.Zero ? "0 s" : $"{time.TotalSeconds:0.0} s";

    /// <summary>Videos, GIFs and light JPEGs copied to the clipboard live here: the clipboard holds their path.</summary>
    private static string TempVideoFolder => Path.Combine(Path.GetTempPath(), "ImageGridFusion");

    private static string TempExportPath(string extension)
    {
        Directory.CreateDirectory(TempVideoFolder);
        string stamp = $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}";
        string path = Path.Combine(TempVideoFolder, $"{stamp}.{extension}");

        // Two exports within a second get distinct files: the clipboard, and the last video, hold the earlier one.
        for (int n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(TempVideoFolder, $"{stamp}-{n}.{extension}");
        }

        return path;
    }

    /// <summary>Removes the videos, GIFs and light JPEGs copied by previous sessions.</summary>
    private static void CleanTempVideos()
    {
        try
        {
            if (!Directory.Exists(TempVideoFolder))
            {
                return;
            }

            foreach (var file in new[] { "*.mp4", "*.gif", "*.jpg" }.SelectMany(pattern => Directory.EnumerateFiles(TempVideoFolder, pattern)))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use, or protected: left for a later session.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The temp folder cannot be listed: nothing to clean.
        }
    }

    private bool RefuseWhileExporting()
    {
        if (IsExporting)
        {
            ShowStatus("The grid is locked until the export ends.");
        }

        return IsExporting;
    }

    /// <summary>Folder of the first image that came from a file, else the user's Pictures folder.</summary>
    private string DefaultSaveFolder()
    {
        string? file = _preview.Images.Select(i => i.FilePath).FirstOrDefault(p => p is not null);
        return Path.GetDirectoryName(file) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    /// <summary>
    /// Where the exports and Save last… open: the folder of the last file they saved — or its nearest parent
    /// still there — and <see cref="DefaultSaveFolder"/> until a first one was saved.
    /// </summary>
    private string ExportFolder() => ExistingFolder(AppSettings.ExportFolder) ?? DefaultSaveFolder();

    /// <summary>A remembered folder, or its nearest parent that still exists (a drive unplugged, a folder deleted); null when none does.</summary>
    private static string? ExistingFolder(string? folder)
    {
        for (string? current = folder; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>A file dialog closed on <paramref name="file"/>: its folder is remembered between sessions, where that dialog opens next.</summary>
    private void RememberFolder(Action<string> save, string file, string dialog)
    {
        if (Path.GetDirectoryName(file) is not { Length: > 0 } folder)
        {
            return;
        }

        try
        {
            save(folder);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"{dialog} folder not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>Opens the settings menu above the ⚙ button, ticked from the settings as they are now.</summary>
    private void ShowSettings()
    {
        _startWithWindows.Checked = StartupRegistration.IsEnabled;
        _twitterCornersDefault.Checked = _roundedByDefault;
        foreach (ToolStripMenuItem item in _explorerPages.DropDownItems)
        {
            item.Checked = (int)item.Tag! == _explorer.PagesPerLoad;
        }

        // Locked like the global effects toolbar: an export keeps the borders it started with.
        _borderColor.Enabled = !IsExporting;
        _settingsMenu.Show(_settingsButton, Point.Empty, ToolStripDropDownDirection.AboveRight);
    }

    private void ToggleStartWithWindows()
    {
        bool enable = !_startWithWindows.Checked;
        try
        {
            StartupRegistration.SetEnabled(enable);
            _startWithWindows.Checked = enable;
        }
        catch (Exception ex) when (StartupRegistration.IsStartupError(ex))
        {
            ShowStatus($"Start with Windows failed: {ex.Message}", error: true);
        }
    }

    private void UpdateButtons()
    {
        bool any = _preview.Images.Count > 0 && !IsExporting;
        _clearButton.Enabled = (_preview.Images.Count > 0 || !SoundtrackInitial || !BordersInitial) && !IsExporting;
        _copyButton.Enabled = any;
        _saveButton.Enabled = any;

        // The main parts name what they produce; the menus force a GIF or a video, pointless when nothing
        // plays — Copy's menu stays open for its light JPEG.
        string format = ProducesVideo ? "MP4" : "PNG";
        _copyButton.Text = $"Copy {format}";
        _saveButton.Text = $"Save {format}…";
        // The last video is offered back whatever the grid, an empty one included: the arrows open for it.
        bool last = _lastVideo is not null && !IsExporting;
        _copyArrow.Enabled = any || last;
        _copyGif.Enabled = HasAnimation;
        _copyMp4.Enabled = HasAnimation;
        _copyForSharing.Enabled = any;
        _saveArrow.Enabled = (any && HasAnimation) || last;
        _saveGif.Enabled = HasAnimation;
        _saveMp4.Enabled = HasAnimation;
        string lastName = _lastVideo is { } video ? FormatName(video.Format) : "video";
        _copyLastButton.Enabled = last;
        _copyLastButton.Text = $"Copy last {lastName}";
        _copyLast.Enabled = last;
        _copyLast.Text = _lastVideo is { } kept ? $"Copy last {lastName} ({kept.GeneratedAt:HH:mm})" : "Copy last video";
        _saveLast.Enabled = last;
        _saveLast.Text = $"Save last {lastName}…";
        RefreshLength();
        RefreshLastVideoTooltips();
        UpdateEffects();
        UpdateGlobalEffects();
    }

    /// <summary>The main part of a split button: its right edge touches its arrow.</summary>
    private static Button SplitMain(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 3, 0, 3),
    };

    /// <summary>The ▾ part of a split button, opening its menu; as tall as the row, as narrow as its glyph.</summary>
    private static Button SplitArrow() => new()
    {
        Text = "▾",
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom,
        Margin = new Padding(0, 3, 3, 3),
    };

    /// <summary>A toggle of an options row, pressed from the look of the selected image.</summary>
    private static CheckBox OptionButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoCheck = false,
        Appearance = Appearance.Button,

        // The normal grey of buttons, not the white of the options row they sit on.
        BackColor = SystemColors.Control,
        Anchor = AnchorStyles.Left,
    };

    /// <summary>A slider of the options; Control + wheel moves it by <paramref name="controlStep"/>, snapped (5 % for a percentage, 5° for an angle).</summary>
    private static StepSlider OptionSlider(int minimum, int maximum, int largeChange, int controlStep = (int)WheelSteps.Percent) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        SmallChange = 1,
        LargeChange = largeChange,
        ControlStep = controlStep,
        TickStyle = TickStyle.None,
        BackColor = SystemColors.Window,

        // Without ticks the thumb sits at the top: a height fitted to it keeps it level with the label.
        AutoSize = false,
        Size = new Size(160, 26),
        Anchor = AnchorStyles.Left,
    };

    private static string EffectTitle(ImageEffect effect) => effect switch
    {
        ImageEffect.BlackAndWhite => "Black & white",
        _ => effect.ToString(),
    };

    /// <summary>A click on a tab shows its options, and activates nothing.</summary>
    private void SelectEffect(ImageEffect effect)
    {
        _selectedEffect = effect;
        UpdateEffects();
    }

    /// <summary>A click on a tab's checkbox turns its effect on or off, keeping its settings, and selects the tab.</summary>
    private void ToggleEffect(ImageEffect effect)
    {
        _selectedEffect = effect;
        if (_preview.SelectedImage?.Look is { } look)
        {
            _preview.SetSelectedLook(look.IsActive(effect) ? look.TurnOff(effect) : look.TurnOn(effect));
        }

        UpdateEffects();
    }

    /// <summary>The options row's Reset: the selected tab's effect back to its default state, off included.</summary>
    private void ResetSelectedEffect()
    {
        if (_selectedEffect is { } effect && ResetLook(effect) is { } look)
        {
            _preview.SetSelectedLook(look);
        }
    }

    /// <summary>
    /// The tabs row's Reset: every effect of the selected image back to its default state, and every
    /// separator of the grid back to its place in the layout; the selected tab stays.
    /// </summary>
    private void ResetEffects()
    {
        if (ResetLook(effect: null) is { } look)
        {
            _preview.SetSelectedLook(look);
        }

        _preview.ResetCellSizes();
        UpdateEffects();
    }

    /// <summary>
    /// The look of the selected image once <paramref name="effect"/> is reset, or every effect when
    /// <c>null</c>: the default state of each, the Volume's included — heard at 100 %, the effect off.
    /// </summary>
    private ImageLook? ResetLook(ImageEffect? effect)
    {
        if (_preview.SelectedImage is not { } image)
        {
            return null;
        }

        return effect is { } one ? image.Look.Reset(one) : ImageLook.None;
    }

    /// <summary>The slider snaps to 100 % near its mark.</summary>
    private void SetZoom() => ApplyZoom(Math.Abs(_zoom.Value) <= 4 ? 1 : Math.Pow(2, _zoom.Value / 100.0));

    /// <summary>
    /// Control + wheel on the zoom slider: the zoom shown moves onto the next multiple of 5 %, as on
    /// the cell — applied exactly, the log scale of the slider being too coarse for it at high zooms.
    /// </summary>
    private void StepZoom(int notches)
    {
        if (_preview.SelectedImage?.Look is not { } look)
        {
            return;
        }

        double current = look.TurnOn(ImageEffect.Zoom).Zoom;
        double zoom = Math.Clamp(WheelSteps.Snap(current * 100, WheelSteps.Percent, notches) / 100, ImageLook.MinZoom, ImageLook.MaxZoom);
        _syncingEffects = true;
        _zoom.Value = Math.Clamp((int)Math.Round(Math.Log2(zoom) * 100), _zoom.Minimum, _zoom.Maximum);
        _syncingEffects = false;
        ApplyZoom(zoom);
    }

    private void ApplyZoom(double zoom)
    {
        _zoomLabel.Text = $"Zoom: {zoom * 100:0} %";
        if (!_syncingEffects && _preview.SelectedImage?.Look is { } look)
        {
            // Acting on an option turns its effect on, from the settings it kept (RULES.md).
            _preview.SetSelectedLook(look.TurnOn(ImageEffect.Zoom));
            _preview.ZoomSelected(zoom);
        }
    }

    /// <summary>
    /// Control + wheel on the frames slider: the starting point moves by 5 % of the frame count,
    /// snapped onto the multiples of 5 % of the animation, at least one frame per notch.
    /// </summary>
    private void StepFrames(int notches)
    {
        int count = _frames.Maximum + 1;
        double at = WheelSteps.Snap(_frames.Value * 100.0 / count, WheelSteps.Percent, notches);
        _frames.Value = WheelSteps.Within(_frames.Value, (int)Math.Round(at * count / 100), notches, _frames.Minimum, _frames.Maximum);
    }

    private void SetFineAngle()
    {
        _fineAngleLabel.Text = AngleText(_fineAngle.Value);
        ChangeLook(ImageEffect.Rotate, look => look.WithFineAngle(_fineAngle.Value));
    }

    private static string AngleText(int degrees) => $"Angle: {degrees:+0;-0;0}°";

    private static string VolumeText(int percent) => $"Volume: {percent} %";

    /// <summary>Not frozen, the slider sets where the content starts playing; frozen, the frame it shows.</summary>
    private void ChangeFrames(Func<FramesEffect, FramesEffect> change)
    {
        UpdateFramesLabel();
        ChangeLook(ImageEffect.Frames, look => look.Frames is { } frames ? look.WithFrames(change(frames)) : look);
    }

    private void UpdateFramesLabel()
    {
        var pages = _preview.SelectedImage?.Pages;
        string at = pages is null ? "" : pages.Label(Math.Clamp(_frames.Value, 0, pages.Count - 1));
        _framesLabel.Text = _freeze.Checked ? $"Frozen on: {at}" : $"Starts at: {at}";
    }

    /// <summary>A ratio button: the kept part reshaped to it around its center, or freed.</summary>
    private void SetCropRatio(double? ratio)
    {
        if (_preview.SelectedImage is { } image)
        {
            ChangeLook(ImageEffect.Crop, look => look.Crop is { } crop ? look.WithCrop(crop.WithRatio(ratio, look, image.Bitmap.Size)) : look);
        }
    }

    private static string RatioText(double ratio) => ratio switch
    {
        1 => "1:1",
        < 1 => $"{Math.Round(16 * ratio)}:16",
        _ when CropEffect.Same(ratio, 4 / 3.0) => "4:3",
        _ => $"{Math.Round(9 * ratio)}:9",
    };

    private void SetBlurKind(BlurKind kind) =>
        ChangeLook(ImageEffect.Blur, look => look.Blur is { } blur ? look.WithBlur(blur.WithKind(kind)) : look);

    private void SetBlurIntensity()
    {
        _blurIntensityLabel.Text = $"Intensity: {_blurIntensity.Value}%";
        ChangeLook(ImageEffect.Blur, look => look.Blur is { } blur ? look.WithBlur(blur.WithIntensity(_blurIntensity.Value / 100.0)) : look);
    }

    private static string OpacityText(int percent) => $"Opacity: {percent} %";

    /// <summary>
    /// Checked, the automatic color again, the chosen one dropped; unchecked, the automatic color of the
    /// moment frozen as the chosen one.
    /// </summary>
    private void SetBackgroundAutomatic() =>
        ChangeLook(ImageEffect.Background, look => look.WithBackground(_backgroundAutomatic.Checked
            ? look.Background!.WithAutomatic()
            : look.Background!.WithColor(_preview.SelectedAutomaticBackground ?? Color.White)));

    /// <summary>The standard color dialog, on the color in use: a color chosen leaves the automatic mode.</summary>
    private void PickBackgroundColor()
    {
        if (IsExporting || _preview.SelectedImage?.Look.TurnOn(ImageEffect.Background).Background is not { } background)
        {
            return;
        }

        _colorDialog.Color = BackgroundShown(background);
        if (_colorDialog.ShowDialog(this) == DialogResult.OK)
        {
            ChangeLook(ImageEffect.Background, look => look.WithBackground(look.Background!.WithColor(_colorDialog.Color)));
        }
    }

    /// <summary>The color in use: the automatic one of the selected cell, or the chosen one.</summary>
    private Color BackgroundShown(BackgroundEffect background) =>
        background.Automatic ? _preview.SelectedAutomaticBackground ?? Color.White : background.Color;

    /// <summary>The color button's face, its text black or white, whichever reads on it.</summary>
    private void ShowBackgroundColor(Color color)
    {
        _backgroundColor.BackColor = Color.FromArgb(255, color);
        _backgroundColor.ForeColor = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 140 ? Color.Black : Color.White;
    }

    /// <summary>
    /// Applies an option of <paramref name="effect"/> to the selected image, turning the effect on from
    /// the settings it kept (RULES.md); not while the options follow the image.
    /// </summary>
    private void ChangeLook(ImageEffect effect, Func<ImageLook, ImageLook> change)
    {
        if (!_syncingEffects && _preview.SelectedImage?.Look is { } look)
        {
            _preview.SetSelectedLook(change(look.TurnOn(effect)));
        }
    }

    /// <summary>
    /// Shows the effects of the selected image: a checkbox checked per effect on, the options of the
    /// selected tab — the kept settings of an effect that is off — and the blur's bars while it is on.
    /// With no cell selected, or during an export, both rows are disabled.
    /// </summary>
    private void UpdateEffects()
    {
        var image = _preview.SelectedImage;
        var look = image?.Look;
        bool enabled = look is not null && !IsExporting;

        _syncingEffects = true;
        foreach (var effect in Enum.GetValues<ImageEffect>())
        {
            _effectTabs.SetState(effect, look?.IsActive(effect) == true, Unavailable(effect, image));
        }

        _effectTabs.Selected = _selectedEffect;
        _effectTabs.Enabled = enabled;
        _resetButton.Enabled = enabled && (ResetLook(effect: null) != look || _preview.ActiveLayout?.IsResized == true);
        if (look is not null)
        {
            // The automatic color is computed only while the tab shows: it changes with every zoom or move.
            if (look.TurnOn(ImageEffect.Background).Background is { } background)
            {
                _backgroundAutomatic.Checked = background.Automatic;
                _backgroundOpacity.Value = (int)Math.Round(background.Opacity * 100);
                if (_selectedEffect == ImageEffect.Background)
                {
                    ShowBackgroundColor(BackgroundShown(background));
                }
            }

            _zoom.Value = Math.Clamp((int)Math.Round(Math.Log2(look.TurnOn(ImageEffect.Zoom).Zoom) * 100), _zoom.Minimum, _zoom.Maximum);

            // A quarter turn is pressed only while the angle falls exactly on it.
            var rotated = look.TurnOn(ImageEffect.Rotate);
            for (int i = 0; i < _quarterTurns.Length; i++)
            {
                _quarterTurns[i].Checked = rotated.Rotation == 90 * i && rotated.FineAngle == 0;
            }

            _fineAngle.Value = rotated.FineAngle;

            if (look.TurnOn(ImageEffect.Crop).Crop is { } crop)
            {
                double? kept = crop.SeenRatio(look);
                foreach (var (button, ratio) in _cropRatios)
                {
                    button.Checked = ratio is { } r ? kept is { } k && CropEffect.Same(r, k) : kept is null;
                }
            }

            var flipped = look.TurnOn(ImageEffect.Flip);
            _flipX.Checked = flipped.FlipX;
            _flipY.Checked = flipped.FlipY;
            if (look.TurnOn(ImageEffect.Frames).Frames is { } frames && image!.Pages is { } pages)
            {
                _frames.Maximum = Math.Max(0, pages.Count - 1);
                _frames.Value = frames.PageOf(pages.Count);
                _freeze.Checked = frames.Frozen;
            }

            if (look.TurnOn(ImageEffect.BlackAndWhite).Grayscale is { } grayscale)
            {
                _grayscale.Value = (int)Math.Round(grayscale * 100);
            }

            if (look.TurnOn(ImageEffect.Blur).Blur is { } blur)
            {
                _gaussian.Checked = blur.Kind == BlurKind.Gaussian;
                _pixelate.Checked = blur.Kind == BlurKind.Pixelate;
                _blurIntensity.Value = (int)Math.Round(blur.Intensity * 100);
            }

            // Muted, the slider keeps its level and stays usable: moving it above 0 brings the sound back.
            if (look.TurnOn(ImageEffect.Volume).Volume is { } volume)
            {
                _volume.Value = (int)Math.Round(volume.Level * 100);
                _mute.Checked = volume.IsMuted;
            }
        }

        _zoomLabel.Text = $"Zoom: {(look?.TurnOn(ImageEffect.Zoom).Zoom ?? 1) * 100:0} %";
        _fineAngleLabel.Text = AngleText(_fineAngle.Value);
        UpdateFramesLabel();
        _grayscaleLabel.Text = $"Intensity: {_grayscale.Value}%";
        _blurIntensityLabel.Text = $"Intensity: {_blurIntensity.Value}%";
        _volumeLabel.Text = VolumeText(_volume.Value);
        _backgroundOpacityLabel.Text = OpacityText(_backgroundOpacity.Value);
        _syncingEffects = false;

        // An effect that does not apply to the image keeps its tab selectable, its options disabled.
        bool usable = enabled && _selectedEffect is { } selected && Unavailable(selected, image) is null;
        foreach (var (effect, row) in _options)
        {
            row.Visible = _selectedEffect == effect;
            row.Enabled = usable;
        }

        _effectResetButton.Visible = _selectedEffect is not null;
        _effectResetButton.Enabled = usable && ResetLook(_selectedEffect) != look;
        _preview.BarsEffect = _selectedEffect is ImageEffect.Blur or ImageEffect.Crop && look?.IsActive(_selectedEffect.Value) == true
            ? _selectedEffect
            : null;
    }

    /// <summary>A click on a global tab shows its options, and activates nothing.</summary>
    private void SelectGlobalEffect(GlobalEffect effect)
    {
        _selectedGlobalEffect = effect;
        UpdateGlobalEffects();
    }

    /// <summary>
    /// A click on a global tab's checkbox turns its effect on or off, keeping its settings, and selects
    /// the tab — shown first, since the soundtrack's may open the file picker.
    /// </summary>
    private void ToggleGlobalEffect(GlobalEffect effect)
    {
        _selectedGlobalEffect = effect;
        UpdateGlobalEffects();
        if (effect == GlobalEffect.Soundtrack)
        {
            ToggleSoundtrack();
        }
        else
        {
            ToggleBorders();
        }
    }

    /// <summary>
    /// <paramref name="effect"/> back to its initial state, the one Clear all restores — every global
    /// effect when <c>null</c>: the soundtrack off, with no file, at 100 %; the borders off, their initial
    /// settings keeping the color and the Twitter corners default of the ⚙ menu. The cells are left alone.
    /// </summary>
    private void ResetGlobalEffects(GlobalEffect? effect = null)
    {
        if (IsExporting)
        {
            return;
        }

        if (effect is null or GlobalEffect.Soundtrack)
        {
            _soundtrack = null;
            _soundtrackOn = false;
            _soundtrackLevel = 1;
            _preview.Soundtrack = ActiveSoundtrack;
        }

        if (effect is null or GlobalEffect.Borders)
        {
            _borders = GridBorders.Initial(_borders.Color, _roundedByDefault);
            _bordersOn = false;
            _preview.Borders = ActiveBorders;
        }

        UpdateButtons();
    }

    /// <summary>
    /// The soundtrack's checkbox: on or off, its file and level kept; with no file yet, it opens the
    /// picker, turned on once a file is chosen.
    /// </summary>
    private void ToggleSoundtrack()
    {
        if (IsExporting)
        {
            return;
        }

        if (_soundtrack is null)
        {
            BrowseSoundtrack();
            return;
        }

        _soundtrackOn = !_soundtrackOn;
        ApplySoundtrack();
    }

    private async void BrowseSoundtrack()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a soundtrack",
            Filter = string.Join(
                "|",
                $"Audio and video files|{Patterns(SoundtrackFile.AudioExtensions.Concat(VideoExtensions))}",
                $"Audio|{Patterns(SoundtrackFile.AudioExtensions)}",
                $"Videos|{Patterns(VideoExtensions)}",
                "All files (*.*)|*.*"),
            InitialDirectory = ExistingFolder(AppSettings.SoundtrackFolder) ?? "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RememberFolder(AppSettings.SaveSoundtrackFolder, dialog.FileName, "Soundtrack");
            await LoadSoundtrackAsync(dialog.FileName);
        }
    }

    /// <summary>
    /// Makes the sound track of <paramref name="path"/> the soundtrack, turned on at the level it
    /// kept; opened off the UI thread. A file without a readable sound track changes nothing.
    /// </summary>
    private async Task LoadSoundtrackAsync(string path)
    {
        if (RefuseWhileExporting())
        {
            return;
        }

        var soundtrack = await Task.Run(() => SoundtrackFile.TryOpen(path));
        if (soundtrack is null)
        {
            ShowStatus($"{Path.GetFileName(path)}: no sound track Windows can read.", error: true);
            return;
        }

        _soundtrack = soundtrack.WithLevel(_soundtrack?.Level ?? _soundtrackLevel);
        _soundtrackOn = true;
        ApplySoundtrack();
        ShowStatus($"Soundtrack: {Path.GetFileName(path)} · {Seconds(soundtrack.Duration)}.");
    }

    /// <summary>
    /// The soundtrack's level; acting on it turns the soundtrack on (RULES.md) — with no file yet, the
    /// level waits for the first one, there being nothing to hear before it.
    /// </summary>
    private void SetSoundtrackVolume()
    {
        _soundtrackVolumeLabel.Text = VolumeText(_soundtrackVolume.Value);
        if (_syncingEffects || IsExporting)
        {
            return;
        }

        double level = _soundtrackVolume.Value / 100.0;
        if (_soundtrack is null)
        {
            _soundtrackLevel = level;
            UpdateButtons();
            return;
        }

        _soundtrack = _soundtrack.WithLevel(level);
        _soundtrackOn = true;
        ApplySoundtrack();
    }

    /// <summary>The soundtrack as it stands, to the preview, then to the row and the output buttons.</summary>
    private void ApplySoundtrack()
    {
        _preview.Soundtrack = ActiveSoundtrack;
        UpdateButtons();
    }

    /// <summary>
    /// Shows the global effects: a checkbox checked per global effect on, the options of the selected
    /// tab — the kept settings of an effect that is off — and the Resets enabled while there is
    /// something to reset. Both rows stay enabled with no cell selected, and are locked while exporting.
    /// </summary>
    private void UpdateGlobalEffects()
    {
        bool enabled = !IsExporting;
        _globalTabs.SetState(GlobalEffect.Soundtrack, ActiveSoundtrack is not null, unavailable: null);
        _globalTabs.SetState(GlobalEffect.Borders, ActiveBorders is not null, unavailable: null);
        _globalTabs.Selected = _selectedGlobalEffect;
        _globalTabs.Enabled = enabled;
        _globalResetButton.Enabled = enabled && !(SoundtrackInitial && BordersInitial);
        foreach (var (effect, row) in _globalOptions)
        {
            row.Visible = _selectedGlobalEffect == effect;
            row.Enabled = enabled;
        }

        _globalEffectResetButton.Visible = _selectedGlobalEffect is not null;
        _globalEffectResetButton.Enabled = enabled && _selectedGlobalEffect switch
        {
            GlobalEffect.Soundtrack => !SoundtrackInitial,
            GlobalEffect.Borders => !BordersInitial,
            _ => false,
        };
        UpdateBorders();

        // A long name is cut, the whole path in its tooltip; the file is kept while the soundtrack is off.
        const int MaxName = 32;
        string? path = _soundtrack?.Path;
        string name = path is null ? "No file" : Path.GetFileName(path);
        _soundtrackFile.Text = name.Length <= MaxName ? name : name[..(MaxName - 1)] + "…";
        _soundtrackFile.ForeColor = path is null ? SystemColors.GrayText : SystemColors.ControlText;
        _toolTip.SetToolTip(_soundtrackFile, path);

        bool syncing = _syncingEffects;
        _syncingEffects = true;
        _soundtrackVolume.Value = (int)Math.Round((_soundtrack?.Level ?? _soundtrackLevel) * 100);
        _syncingEffects = syncing;
        _soundtrackVolumeLabel.Text = VolumeText(_soundtrackVolume.Value);
    }

    /// <summary>The borders' checkbox: on or off, their settings kept.</summary>
    private void ToggleBorders()
    {
        if (IsExporting)
        {
            return;
        }

        _bordersOn = !_bordersOn;
        ApplyBorders();
    }

    /// <summary>
    /// An option of the borders changed: applied to their settings, turning them on from the settings
    /// they kept (RULES.md), unless the row is being synced.
    /// </summary>
    private void ChangeBorders(Func<GridBorders, GridBorders> change)
    {
        if (_syncingEffects || IsExporting)
        {
            return;
        }

        _borders = change(_borders);
        _bordersOn = true;
        ApplyBorders();
    }

    /// <summary>
    /// The ⚙ menu's Border color: the color dialog, preselected on the current color; the one chosen is
    /// applied at once and remembered between sessions.
    /// </summary>
    private void PickBorderColor()
    {
        if (IsExporting)
        {
            return;
        }

        _colorDialog.Color = _borders.Color;
        if (_colorDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _borders = _borders with { Color = _colorDialog.Color };
        UpdateBorderColorSwatch();
        ApplyBorders();
        try
        {
            AppSettings.SaveBorderColor(_borders.Color);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"Border color not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>
    /// The ⚙ menu's Twitter corners by default: whether the borders' initial state has them, at the next
    /// start-up and after Clear all — the borders of the open grid left as they are. Remembered between sessions.
    /// </summary>
    private void ToggleTwitterCornersDefault()
    {
        bool enable = !_twitterCornersDefault.Checked;
        try
        {
            AppSettings.SaveTwitterCornersByDefault(enable);
            _roundedByDefault = enable;
            _twitterCornersDefault.Checked = enable;
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"Twitter corners by default not remembered: {ex.Message}", error: true);
        }

        // The initial state moved: Clear all may now have something to reset, or nothing.
        UpdateButtons();
    }

    /// <summary>
    /// The ⚙ menu's File explorer folder, also the explorer's own Choose folder button: the folder
    /// dialog, preselected on the current folder; the one chosen is indexed at once and remembered
    /// between sessions.
    /// </summary>
    private void PickExplorerFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "The folder the file explorer searches, with its subfolders",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (AppSettings.ExplorerFolder is { } current && Directory.Exists(current))
        {
            dialog.SelectedPath = current;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            AppSettings.SaveExplorerFolder(dialog.SelectedPath);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"File explorer folder not remembered: {ex.Message}", error: true);
        }

        _explorer.SetBaseFolder(dialog.SelectedPath);
    }

    /// <summary>The file explorer's panel was opened or collapsed by the user: remembered between sessions.</summary>
    private void SaveExplorerPanelOpen()
    {
        try
        {
            AppSettings.SaveExplorerPanelOpen(_explorer.Open);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"File explorer panel state not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The file explorer's width was dragged by the user: remembered between sessions.</summary>
    private void SaveExplorerWidth()
    {
        try
        {
            AppSettings.SaveExplorerWidth(_explorer.OpenWidth);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"File explorer width not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The ⚙ menu's File explorer pages per load: applied from the explorer's next load, remembered between sessions.</summary>
    private void SetExplorerPagesPerLoad(int pages)
    {
        _explorer.PagesPerLoad = pages;
        try
        {
            AppSettings.SaveExplorerPagesPerLoad(pages);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"File explorer pages per load not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The file explorer's tile size was changed by the user: remembered between sessions.</summary>
    private void SaveExplorerTileSize()
    {
        try
        {
            AppSettings.SaveExplorerTileSize(_explorer.TileSize);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"File explorer tile size not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The splitter's bounds in device pixels: the explorer's narrowest, and the room the preview keeps.</summary>
    private void ApplyExplorerSplitterBounds()
    {
        _explorerSplitter.MinSize = LogicalToDeviceUnits(FileExplorerPanel.MinOpenWidth);
        _explorerSplitter.MinExtra = LogicalToDeviceUnits(320);
    }

    /// <summary>
    /// The window closes or hides: its normal size — the one before it was maximized or minimized — is
    /// remembered between sessions, in logical pixels, once it was opened in this session (a hidden
    /// start quit from the tray is not a use).
    /// </summary>
    private void SaveWindowSize()
    {
        if (!_opened)
        {
            return;
        }

        // Maximized or minimized, the normal bounds minus the frame give the normal client size.
        var client = WindowState == FormWindowState.Normal ? ClientSize : RestoreBounds.Size - SizeFromClientSize(Size.Empty);
        try
        {
            AppSettings.SaveWindowClientSize(new Size(DeviceToLogicalUnits(client.Width), DeviceToLogicalUnits(client.Height)));
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            ShowStatus($"Window size not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The reverse of <see cref="Control.LogicalToDeviceUnits(int)"/>: device pixels to 96 DPI ones.</summary>
    private int DeviceToLogicalUnits(int value) => (int)Math.Round(value * 96.0 / DeviceDpi);

    /// <summary>The borders as they stand, to the preview, then to the row and the output buttons.</summary>
    private void ApplyBorders()
    {
        _preview.Borders = ActiveBorders;
        UpdateButtons();
    }

    /// <summary>
    /// Shows the borders' options, their kept settings while they are off; the outer frame disabled for
    /// the corner brackets.
    /// </summary>
    private void UpdateBorders()
    {
        bool syncing = _syncingEffects;
        _syncingEffects = true;
        _bordersStyle.SelectedIndex = (int)_borders.Pattern;
        _bordersThickness.Value = (int)Math.Round(_borders.Thickness * 1000);
        _bordersOpacity.Value = (int)Math.Round(_borders.Opacity * 100);
        _bordersOuterFrame.Checked = _borders.OuterFrame;
        _bordersRounded.Checked = _borders.Rounded;
        _syncingEffects = syncing;
        _bordersThicknessLabel.Text = ThicknessText(_bordersThickness.Value);
        _bordersOpacityLabel.Text = OpacityText(_bordersOpacity.Value);
        _bordersOuterFrame.Enabled = _borders.HasGap;

        // Only the corner brackets lie over the images.
        _bordersOpacity.Enabled = !_borders.HasGap;
        _bordersOpacityLabel.Enabled = !_borders.HasGap;
    }

    /// <summary>The borders' width, from thousandths of the grid's shorter side.</summary>
    private static string ThicknessText(int thousandths) => $"Thickness: {thousandths / 10.0:0.0} %";

    /// <summary>Why <paramref name="effect"/> does not apply to <paramref name="image"/>; <c>null</c> when it does.</summary>
    private static string? Unavailable(ImageEffect effect, SourceImage? image) => effect switch
    {
        ImageEffect.Frames when image is { IsAnimated: false } => "Frames only applies to videos, animated GIFs and content of several pages",
        ImageEffect.Volume when image is { HasSound: false } => "Volume only applies to videos with a sound track",
        _ => null,
    };

    /// <summary>Shows a message that stays until the next one replaces it; errors in red.</summary>
    private void ShowStatus(string message, bool error = false)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = message;
    }

    /// <summary>
    /// Opens Explorer on the folder of an image's file, the file selected. A file moved or deleted
    /// since it was loaded opens its folder, if still there, and says so.
    /// </summary>
    private void ShowInExplorer(string path)
    {
        bool exists = File.Exists(path);
        string? folder = Path.GetDirectoryName(path);
        string? arguments = exists ? $"/select,\"{path}\""
            : Directory.Exists(folder) ? $"\"{folder}\""
            : null;
        if (!exists)
        {
            ShowStatus($"File not found: {path}", error: true);
        }

        if (arguments is null)
        {
            return;
        }

        try
        {
            Process.Start("explorer.exe", arguments)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            ShowStatus($"Explorer could not be opened: {ex.Message}", error: true);
        }
    }

    /// <summary>
    /// Caps the status text to the width its column leaves once the buttons around it are laid out,
    /// so that a long message wraps instead of pushing them out of the window.
    /// </summary>
    private void FitStatusWidth()
    {
        int width = _bottom.ClientSize.Width - _bottom.Padding.Horizontal
            - _clearButton.Width - _clearButton.Margin.Horizontal
            - _outputButtons.Width - _outputButtons.Margin.Horizontal
            - _statusLine.Margin.Horizontal - _status.Margin.Horizontal
            - (_cancelButton.Visible ? _cancelButton.Width + _cancelButton.Margin.Horizontal : 0);
        var maximum = new Size(Math.Max(1, width), 0);
        if (_status.MaximumSize != maximum)
        {
            _status.MaximumSize = maximum;
        }
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count} files";
}
