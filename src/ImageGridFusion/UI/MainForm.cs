using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    private readonly string[] _startupFiles;
    private readonly GridPreview _preview = new() { Dock = DockStyle.Fill, AllowDrop = true };
    private readonly LayoutStrip _layouts = new() { Dock = DockStyle.Left, Width = 80, AllowDrop = true };
    private readonly Button _clearButton = new() { Text = "Clear all", AutoSize = true };
    private readonly Button _settingsButton = new() { Text = "⚙", Size = new Size(32, 23), AutoSize = true };
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
    private readonly Button _cancelButton = new() { Text = "Cancel", AutoSize = true, Visible = false };
    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TableLayoutPanel _bottom;
    private readonly FlowLayoutPanel _outputButtons;
    private readonly FlowLayoutPanel _statusLine;

    /// <summary>Set while an export runs: the grid is locked until it ends.</summary>
    private CancellationTokenSource? _export;
    private bool _closeAfterExport;
    private bool _closingForGood;

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
    private readonly EffectTabs _effectTabs = new() { Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = Padding.Empty };

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
    private readonly TrackBar _zoom = OptionSlider((int)Math.Round(Math.Log2(ImageLook.MinZoom) * 100), (int)Math.Round(Math.Log2(ImageLook.MaxZoom) * 100), 10);
    private readonly Label _zoomLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox[] _quarterTurns = [OptionButton("0°"), OptionButton("90°"), OptionButton("180°"), OptionButton("270°")];
    private readonly TrackBar _fineAngle = OptionSlider(-ImageLook.MaxFineAngle, ImageLook.MaxFineAngle, 5);
    private readonly Label _fineAngleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _flipX = OptionButton("Horizontal");
    private readonly CheckBox _flipY = OptionButton("Vertical");
    private readonly TrackBar _frames = OptionSlider(0, 1, 10);
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

    // The Global effects row, just above the bottom bar, each global effect's options beside its toggle,
    // shown while it is on: see RULES.md. A file dropped anywhere on it becomes the soundtrack.
    private readonly FlowLayoutPanel _globalRow = new()
    {
        Dock = DockStyle.Bottom,
        WrapContents = false,
        Padding = new Padding(8, 4, 8, 0),
        AllowDrop = true,
    };
    private readonly Label _globalLabel = new() { Text = "Global effects →", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _soundtrackToggle = OptionButton("♪ Soundtrack");
    private readonly Button _soundtrackBrowse = new() { Text = "Browse…", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _soundtrackFile = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _soundtrackVolume = OptionSlider(0, (int)(Soundtrack.MaxLevel * 100), 10);
    private readonly Label _soundtrackVolumeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The soundtrack's file and level, kept while it is off; cleared by Clear all only.
    private Soundtrack? _soundtrack;
    private bool _soundtrackOn;

    // The borders' toggle and options; their color is a setting of the ⚙ menu, remembered between sessions.
    private readonly CheckBox _bordersToggle = OptionButton("▦ Borders");
    private readonly ComboBox _bordersStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, Anchor = AnchorStyles.Left };
    private readonly TrackBar _bordersThickness = OptionSlider((int)Math.Round(GridBorders.MinThickness * 1000), (int)Math.Round(GridBorders.MaxThickness * 1000), 5);
    private readonly Label _bordersThicknessLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
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
        ClientSize = new Size(960, 860);
        MinimumSize = new Size(480, 320);
        AllowDrop = true;

        _outputButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _outputButtons.Controls.Add(_settingsButton);
        _outputButtons.Controls.Add(_copyButton);
        _outputButtons.Controls.Add(_copyArrow);
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
        _options[ImageEffect.Zoom].Controls.AddRange([_zoom, _zoomLabel]);
        _options[ImageEffect.Rotate].Controls.AddRange([.. _quarterTurns, _fineAngle, _fineAngleLabel]);
        _options[ImageEffect.Flip].Controls.AddRange([_flipX, _flipY]);
        _options[ImageEffect.Frames].Controls.AddRange([_frames, _framesLabel, _freeze]);
        _options[ImageEffect.BlackAndWhite].Controls.AddRange([_grayscaleIcon, _grayscale, _grayscaleLabel]);
        _options[ImageEffect.Blur].Controls.AddRange([_gaussian, _pixelate, _blurIntensityIcon, _blurIntensity, _blurIntensityLabel]);
        _options[ImageEffect.Volume].Controls.AddRange([_mute, _volume, _volumeLabel]);
        _globalLabel.Font = new Font(Font, FontStyle.Bold);
        _soundtrackVolume.BackColor = SystemColors.Control;
        _bordersThickness.BackColor = SystemColors.Control;
        _bordersStyle.Items.AddRange(Enum.GetNames<BorderPattern>());
        _globalRow.Controls.AddRange(
        [
            _globalLabel, _soundtrackToggle, _soundtrackBrowse, _soundtrackFile, _soundtrackVolume, _soundtrackVolumeLabel,
            _bordersToggle, _bordersStyle, _bordersThickness, _bordersThicknessLabel, _bordersOuterFrame, _bordersRounded,
        ]);

        // Docked in reverse order of addition: the options row and the tabs row, then the bottom bar
        // and the Global effects row above it, span the whole width; the layout strip takes the left
        // of what remains, and the fill control goes first so it gets the rest.
        Controls.Add(_preview);
        Controls.Add(_layouts);
        Controls.Add(_globalRow);
        Controls.Add(_bottom);
        Controls.Add(_tabsRow);
        Controls.Add(_optionsRow);
        ResumeLayout(performLayout: true);

        // A long message wraps within the space left between the buttons, the bottom bar growing taller.
        _bottom.SizeChanged += (_, _) => FitStatusWidth();
        _outputButtons.SizeChanged += (_, _) => FitStatusWidth();
        _cancelButton.VisibleChanged += (_, _) => FitStatusWidth();
        _clearButton.Click += (_, _) => ClearAll();
        _settingsMenu.Items.AddRange([_startWithWindows, _borderColor, _twitterCornersDefault]);
        _toolTip.SetToolTip(_settingsButton, "Settings");
        _settingsButton.Click += (_, _) => ShowSettings();
        _startWithWindows.Click += (_, _) => ToggleStartWithWindows();
        _borderColor.ToolTipText = "The color of the borders, remembered between sessions";
        _borderColor.Click += (_, _) => PickBorderColor();
        _twitterCornersDefault.ToolTipText = "Whether the borders start with their Twitter corners, at start-up and after Clear all; remembered between sessions";
        _twitterCornersDefault.Click += (_, _) => ToggleTwitterCornersDefault();
        _copyButton.Click += (_, _) => CopyToClipboard();
        _saveButton.Click += (_, _) => Save();
        _copyGif.Click += (_, _) => Copy(GridExport.Format.Gif);
        _copyMp4.Click += (_, _) => Copy(GridExport.Format.Mp4);
        _copyForSharing.Click += (_, _) => CopyForSharing();
        _copyMenu.Items.AddRange([_copyGif, _copyMp4, _copyForSharing]);
        _saveMenu.Items.Add("GIF", null, (_, _) => SaveAs(GridExport.Format.Gif));
        _saveMenu.Items.Add("MP4 Video", null, (_, _) => SaveAs(GridExport.Format.Mp4));
        _copyArrow.Click += (_, _) => _copyMenu.Show(_copyButton, Point.Empty, ToolStripDropDownDirection.AboveRight);
        _saveArrow.Click += (_, _) => _saveMenu.Show(_saveButton, Point.Empty, ToolStripDropDownDirection.AboveRight);
        _cancelButton.Click += (_, _) => _export?.Cancel();
        UpdateEffectIcons();
        FitEffectRows();

        // The Reset button sizes itself to its font and DPI: the tabs follow it.
        _resetButton.SizeChanged += (_, _) => FitEffectRows();
        _tabsRow.Paint += (_, e) => PaintOptionsEdge(e.Graphics);
        _effectTabs.TabClicked += (_, effect) => SelectEffect(effect);
        _effectTabs.CheckClicked += (_, effect) => ToggleEffect(effect);
        _toolTip.SetToolTip(_effectResetButton, "Brings this effect back to its defaults");
        _toolTip.SetToolTip(_resetButton, "Brings every effect of the cell back to its defaults, and the cells to their layout's sizes");
        _effectResetButton.Click += (_, _) => ResetSelectedEffect();
        _resetButton.Click += (_, _) => ResetEffects();
        _zoom.ValueChanged += (_, _) => SetZoom();
        for (int i = 0; i < _quarterTurns.Length; i++)
        {
            int degrees = 90 * i;
            _quarterTurns[i].Click += (_, _) => ChangeLook(ImageEffect.Rotate, look => look.WithRotation(degrees));
        }

        _fineAngle.ValueChanged += (_, _) => SetFineAngle();
        _flipX.Click += (_, _) => ChangeLook(ImageEffect.Flip, look => look.ToggleFlipX());
        _flipY.Click += (_, _) => ChangeLook(ImageEffect.Flip, look => look.ToggleFlipY());
        _frames.ValueChanged += (_, _) => ChangeFrames(frames => frames.AtPage(_frames.Value, _frames.Maximum + 1));
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
        _toolTip.SetToolTip(_soundtrackToggle, "Mixes the sound of an audio or video file over the videos, in the preview and the MP4 export");
        _toolTip.SetToolTip(_soundtrackBrowse, "Chooses another audio or video file; one can also be dropped on this row");
        _soundtrackToggle.Click += (_, _) => ToggleSoundtrack();
        _soundtrackBrowse.Click += (_, _) => BrowseSoundtrack();
        _soundtrackVolume.ValueChanged += (_, _) => SetSoundtrackVolume();
        _toolTip.SetToolTip(_bordersToggle, "Borders on the grid: brackets at its corners, or a gap between the cells; their color is in the ⚙ settings");
        _toolTip.SetToolTip(_bordersThickness, "Width of the borders, as a share of the grid's shorter side");
        _toolTip.SetToolTip(_bordersOuterFrame, "Also draws the borders around the grid; the corner brackets already are its frame");
        _bordersToggle.Click += (_, _) => ToggleBorders();
        _bordersStyle.SelectedIndexChanged += (_, _) => ChangeBorders(borders => borders with { Pattern = (BorderPattern)_bordersStyle.SelectedIndex });
        _bordersThickness.ValueChanged += (_, _) =>
        {
            _bordersThicknessLabel.Text = ThicknessText(_bordersThickness.Value);
            ChangeBorders(borders => borders with { Thickness = _bordersThickness.Value / 1000.0 });
        };
        _bordersOuterFrame.CheckedChanged += (_, _) => ChangeBorders(borders => borders with { OuterFrame = _bordersOuterFrame.Checked });
        _toolTip.SetToolTip(_bordersRounded, "Rounds the grid's corners like Twitter / X shows images; the borders follow the curve");
        _bordersRounded.CheckedChanged += (_, _) => ChangeBorders(borders => borders with { Rounded = _bordersRounded.Checked });
        _globalRow.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        _globalRow.DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
            {
                await LoadSoundtrackAsync(paths[0]);
            }
        };
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
            _effectsLabel.Font.Dispose();
            _globalLabel.Font.Dispose();
            _grayscaleIcon.Image?.Dispose();
            _gaussian.Image?.Dispose();
            _pixelate.Image?.Dispose();
            _blurIntensityIcon.Image?.Dispose();
            _backgroundOpacityIcon.Image?.Dispose();
            _borderColor.Image?.Dispose();
            _colorDialog.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>The icons are drawn at the monitor's DPI: again when the window moves to another one.</summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateEffectIcons();
        FitEffectRows();
    }

    private void UpdateEffectIcons()
    {
        int size = LogicalToDeviceUnits(16);
        Image?[] previous = [_resetButton.Image, _effectResetButton.Image, _grayscaleIcon.Image, _gaussian.Image, _pixelate.Image, _blurIntensityIcon.Image, _backgroundOpacityIcon.Image];
        foreach (var effect in Enum.GetValues<ImageEffect>())
        {
            _effectTabs.SetIcon(effect, effect switch
            {
                ImageEffect.Background => EffectIcons.Background(size),
                ImageEffect.Zoom => EffectIcons.Zoom(size),
                ImageEffect.Rotate => EffectIcons.Rotate(size),
                ImageEffect.Flip => EffectIcons.Flip(size),
                ImageEffect.Frames => EffectIcons.Frames(size),
                ImageEffect.BlackAndWhite => EffectIcons.BlackAndWhite(size),
                ImageEffect.Volume => EffectIcons.Volume(size),
                _ => EffectIcons.Blur(size),
            });
        }

        _resetButton.Image = EffectIcons.Reset(size);
        _effectResetButton.Image = EffectIcons.Reset(size);
        _grayscaleIcon.Image = EffectIcons.Intensity(size);
        _grayscaleIcon.Size = new Size(size, size);
        _gaussian.Image = EffectIcons.Gaussian(size);
        _pixelate.Image = EffectIcons.Pixelate(size);
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
    /// The tabs as tall as the Reset beside them; the options row as tall as the tallest options, so
    /// nothing below it moves when another tab is selected; the Global effects row as tall as its
    /// tallest control, so the preview does not move when a global effect's options show.
    /// </summary>
    private void FitEffectRows()
    {
        _globalRow.Height = _globalRow.Controls.Cast<Control>().Max(c => c.GetPreferredSize(Size.Empty).Height + c.Margin.Vertical) + _globalRow.Padding.Vertical;
        _effectTabs.Size = new Size(_effectTabs.GetPreferredSize(Size.Empty).Width, _resetButton.GetPreferredSize(Size.Empty).Height);
        int options = _options.Values.Max(row => row.GetPreferredSize(Size.Empty).Height);
        int reset = _effectResetButton.GetPreferredSize(Size.Empty).Height + _effectResetButton.Margin.Vertical;
        _optionsRow.Height = Math.Max(options, reset) + _optionsRow.Padding.Vertical;
    }

    /// <summary>The bottom edge of the options row, across the tabs row; the tabs open it under the selected one.</summary>
    private void PaintOptionsEdge(Graphics g)
    {
        using var border = new Pen(SystemColors.ControlDark);
        g.DrawLine(border, 0, 0, _tabsRow.Width, 0);
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
    /// window closes once it has stopped.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
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

    private int DropCell(DragEventArgs e) => _preview.CellAt(_preview.PointToClient(new Point(e.X, e.Y)));

    /// <summary>Files chosen from the picker (drop zone or empty canvas) are added like a drop onto the drop zone.</summary>
    private async void PickFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add images",
            Multiselect = true,
            Filter = PickerFilter(),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
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
            var styled = text.Parse();
            return styled is null || styled.Text.Length > StyledText.MaxLength
                ? ((SourceImage?)null, styled?.Text.Length ?? 0)
                : (ImageLoader.FromText(styled), styled.Text.Length);
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
        if (count == 0 && !soundtrack && !borders)
        {
            return;
        }

        _preview.Clear();
        _soundtrack = null;
        _soundtrackOn = false;
        ApplySoundtrack();
        _borders = GridBorders.Initial(_borders.Color, _roundedByDefault);
        _bordersOn = false;
        ApplyBorders();

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
    /// clipboard's GIF format that some apps paste directly.
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
            var animationClock = Stopwatch.StartNew();
            if (await ExportAnimationAsync(path, animated) is { } animation)
            {
                var encoding = animationClock.Elapsed;
                try
                {
                    var data = new DataObject();
                    data.SetFileDropList(new StringCollection { path });
                    if (animated == GridExport.Format.Gif)
                    {
                        data.SetData("GIF", new MemoryStream(File.ReadAllBytes(path)));
                    }

                    Clipboard.SetDataObject(data, copy: true);
                    ShowStatus(AnimationSummary($"Copied {Path.GetFileName(path)}", path, animated, animation, encoding));
                }
                catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
                {
                    ShowStatus($"Copy failed: {ex.Message}", error: true);
                }
            }

            return;
        }

        var clock = Stopwatch.StartNew();
        var borders = ActiveBorders;
        using var result = await RenderStillAsync();
        if (result is null)
        {
            return;
        }

        try
        {
            // Standard bitmap for most apps, flattened on white as it carries no transparency — its
            // corners kept, Twitter / X rounding them itself —, plus the PNG format that browsers paste
            // more reliably, which keeps it, the Twitter corners cut out.
            using var flat = Compositor.Flattened(result);
            borders?.CutCorners(result);
            using var png = new MemoryStream();
            result.Save(png, ImageFormat.Png);
            var encoding = clock.Elapsed;
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
            Filter = format switch
            {
                GridExport.Format.Mp4 => "MP4 video (*.mp4)|*.mp4",
                GridExport.Format.Gif => "GIF image (*.gif)|*.gif",
                _ => "PNG image (*.png)|*.png",
            },
            DefaultExt = extension,
            FileName = $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
            InitialDirectory = DefaultSaveFolder(),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string saved = $"Saved {Path.GetFileName(dialog.FileName)}";
        var clock = Stopwatch.StartNew();
        if (format is { } animated)
        {
            if (await ExportAnimationAsync(dialog.FileName, animated) is { } result)
            {
                ShowStatus(AnimationSummary(saved, dialog.FileName, animated, result, clock.Elapsed));
            }

            return;
        }

        var borders = ActiveBorders;
        using var still = await RenderStillAsync();
        if (still is null)
        {
            return;
        }

        try
        {
            // A PNG keeps alpha: the Twitter corners cut out.
            borders?.CutCorners(still);
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
        return Path.Combine(TempVideoFolder, $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
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

    /// <summary>Opens the settings menu above the ⚙ button, ticked from the registry as it is now.</summary>
    private void ShowSettings()
    {
        _startWithWindows.Checked = StartupRegistration.IsEnabled;
        _twitterCornersDefault.Checked = _roundedByDefault;

        // Locked like the Global effects row: an export keeps the borders it started with.
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
        catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
        {
            ShowStatus($"Start with Windows failed: {ex.Message}", error: true);
        }
    }

    private void UpdateButtons()
    {
        bool any = _preview.Images.Count > 0 && !IsExporting;
        _clearButton.Enabled = (_preview.Images.Count > 0 || _soundtrack is not null || !BordersInitial) && !IsExporting;
        _copyButton.Enabled = any;
        _saveButton.Enabled = any;

        // The main parts name what they produce; the menus force a GIF or a video, pointless when nothing
        // plays — Copy's menu stays open for its light JPEG.
        string format = ProducesVideo ? "MP4" : "PNG";
        _copyButton.Text = $"Copy {format}";
        _saveButton.Text = $"Save {format}…";
        _copyArrow.Enabled = any;
        _copyGif.Enabled = HasAnimation;
        _copyMp4.Enabled = HasAnimation;
        _saveArrow.Enabled = any && HasAnimation;
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

    private static TrackBar OptionSlider(int minimum, int maximum, int largeChange) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        SmallChange = 1,
        LargeChange = largeChange,
        TickStyle = TickStyle.None,
        BackColor = SystemColors.Window,

        // Without ticks the thumb sits at the top: a height fitted to it keeps it level with the label.
        AutoSize = false,
        Size = new Size(160, 26),
        Anchor = AnchorStyles.Left,
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
    /// <c>null</c>: the volume's default state is the sound on arrival, from the other cells (RULES.md).
    /// </summary>
    private ImageLook? ResetLook(ImageEffect? effect)
    {
        if (_preview.SelectedImage is not { } image)
        {
            return null;
        }

        var look = effect is { } one ? image.Look.Reset(one) : ImageLook.None;
        return effect is null or ImageEffect.Volume ? Animation.SoundOnArrival(image, look, _preview.Images) : look;
    }

    /// <summary>The slider snaps to 100 % near its mark.</summary>
    private void SetZoom()
    {
        double zoom = Math.Abs(_zoom.Value) <= 4 ? 1 : Math.Pow(2, _zoom.Value / 100.0);
        _zoomLabel.Text = $"Zoom: {zoom * 100:0} %";
        if (!_syncingEffects && _preview.SelectedImage?.Look is { } look)
        {
            // Acting on an option turns its effect on, from the settings it kept (RULES.md).
            _preview.SetSelectedLook(look.TurnOn(ImageEffect.Zoom));
            _preview.ZoomSelected(zoom);
        }
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
        _preview.ShowsBlurBars = _selectedEffect == ImageEffect.Blur && look?.IsActive(ImageEffect.Blur) == true;
    }

    /// <summary>
    /// The soundtrack's toggle: on or off, its file and level kept; with no file yet, it opens the
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
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
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

        _soundtrack = soundtrack.WithLevel(_soundtrack?.Level ?? 1);
        _soundtrackOn = true;
        ApplySoundtrack();
        ShowStatus($"Soundtrack: {Path.GetFileName(path)} · {Seconds(soundtrack.Duration)}.");
    }

    private void SetSoundtrackVolume()
    {
        _soundtrackVolumeLabel.Text = VolumeText(_soundtrackVolume.Value);
        if (!_syncingEffects && _soundtrack is not null)
        {
            _soundtrack = _soundtrack.WithLevel(_soundtrackVolume.Value / 100.0);
            ApplySoundtrack();
        }
    }

    /// <summary>The soundtrack as it stands, to the preview, then to the row and the output buttons.</summary>
    private void ApplySoundtrack()
    {
        _preview.Soundtrack = ActiveSoundtrack;
        UpdateButtons();
    }

    /// <summary>
    /// Shows the global effects: the toggle pressed while the soundtrack is on, its options — file and
    /// level — only then, kept while it is off. The row stays enabled with no cell selected, and is
    /// locked while exporting.
    /// </summary>
    private void UpdateGlobalEffects()
    {
        _globalRow.Enabled = !IsExporting;
        UpdateBorders();
        var soundtrack = ActiveSoundtrack;
        _soundtrackToggle.Checked = soundtrack is not null;
        foreach (var option in new Control[] { _soundtrackBrowse, _soundtrackFile, _soundtrackVolume, _soundtrackVolumeLabel })
        {
            option.Visible = soundtrack is not null;
        }

        if (soundtrack is null)
        {
            return;
        }

        // A long name is cut, the whole path in its tooltip.
        const int MaxName = 32;
        string name = Path.GetFileName(soundtrack.Path);
        _soundtrackFile.Text = name.Length <= MaxName ? name : name[..(MaxName - 1)] + "…";
        _toolTip.SetToolTip(_soundtrackFile, soundtrack.Path);

        _syncingEffects = true;
        _soundtrackVolume.Value = (int)Math.Round(soundtrack.Level * 100);
        _syncingEffects = false;
        _soundtrackVolumeLabel.Text = VolumeText(_soundtrackVolume.Value);
    }

    /// <summary>The borders' toggle: on or off, their settings kept.</summary>
    private void ToggleBorders()
    {
        if (IsExporting)
        {
            return;
        }

        _bordersOn = !_bordersOn;
        ApplyBorders();
    }

    /// <summary>An option of the borders changed: applied to their settings, unless the row is being synced.</summary>
    private void ChangeBorders(Func<GridBorders, GridBorders> change)
    {
        if (_syncingEffects || IsExporting)
        {
            return;
        }

        _borders = change(_borders);
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
        catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
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
        catch (Exception ex) when (StartupRegistration.IsRegistryError(ex))
        {
            ShowStatus($"Twitter corners by default not remembered: {ex.Message}", error: true);
        }

        // The initial state moved: Clear all may now have something to reset, or nothing.
        UpdateButtons();
    }

    /// <summary>The borders as they stand, to the preview, then to the row and the output buttons.</summary>
    private void ApplyBorders()
    {
        _preview.Borders = ActiveBorders;
        UpdateButtons();
    }

    /// <summary>
    /// Shows the borders in the Global effects row: the toggle pressed while they are on, their options
    /// only then, kept while they are off; the outer frame disabled for the corner brackets.
    /// </summary>
    private void UpdateBorders()
    {
        bool on = ActiveBorders is not null;
        _bordersToggle.Checked = on;
        foreach (var option in new Control[] { _bordersStyle, _bordersThickness, _bordersThicknessLabel, _bordersOuterFrame, _bordersRounded })
        {
            option.Visible = on;
        }

        bool syncing = _syncingEffects;
        _syncingEffects = true;
        _bordersStyle.SelectedIndex = (int)_borders.Pattern;
        _bordersThickness.Value = (int)Math.Round(_borders.Thickness * 1000);
        _bordersOuterFrame.Checked = _borders.OuterFrame;
        _bordersRounded.Checked = _borders.Rounded;
        _syncingEffects = syncing;
        _bordersThicknessLabel.Text = ThicknessText(_bordersThickness.Value);
        _bordersOuterFrame.Enabled = _borders.HasGap;
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
