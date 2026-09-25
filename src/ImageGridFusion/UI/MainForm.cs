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
    private readonly Button _copyButton = new() { Text = "Copy", AutoSize = true };
    private readonly Button _saveButton = new() { Text = "Save…", AutoSize = true };
    private readonly CheckBox _forceImage = new() { Text = "Force as image", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
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
    private readonly Label _effectsLabel = new() { Text = "Effects", AutoSize = true, Anchor = AnchorStyles.Left };
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
        Anchor = AnchorStyles.Left,
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
    private readonly RadioButton _pixelate = new()
    {
        Text = "Pixelate",
        AutoSize = true,
        Appearance = Appearance.Button,
        Anchor = AnchorStyles.Left,
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
    private readonly PictureBox _blurIntensityIcon = new() { SizeMode = PictureBoxSizeMode.CenterImage, Anchor = AnchorStyles.Left };
    private readonly TrackBar _blurIntensity = OptionSlider(0, 100, 10);
    private readonly Label _blurIntensityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The selected tab belongs to the toolbar: it stays selected on every cell, and with none.
    private ImageEffect? _selectedEffect;
    private bool _syncingEffects;

    public MainForm(string[] args)
    {
        _startupFiles = args;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Image Grid Fusion";
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 580);
        MinimumSize = new Size(480, 320);
        AllowDrop = true;

        _outputButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _outputButtons.Controls.Add(_settingsButton);
        _outputButtons.Controls.Add(_forceImage);
        _outputButtons.Controls.Add(_copyButton);
        _outputButtons.Controls.Add(_saveButton);

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
        _tabsRow.Controls.Add(_effectsLabel, 0, 0);
        _tabsRow.Controls.Add(_effectTabs, 1, 0);
        _tabsRow.Controls.Add(_resetButton, 3, 0);
        _optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _optionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _optionsRow.Controls.Add(_optionsHost, 0, 0);
        _optionsRow.Controls.Add(_effectResetButton, 1, 0);
        _optionsHost.Controls.AddRange([.. _options.Values]);
        _options[ImageEffect.Zoom].Controls.AddRange([_zoom, _zoomLabel]);
        _options[ImageEffect.Rotate].Controls.AddRange([.. _quarterTurns, _fineAngle, _fineAngleLabel]);
        _options[ImageEffect.Flip].Controls.AddRange([_flipX, _flipY]);
        _options[ImageEffect.Frames].Controls.AddRange([_frames, _framesLabel, _freeze]);
        _options[ImageEffect.BlackAndWhite].Controls.AddRange([_grayscaleIcon, _grayscale, _grayscaleLabel]);
        _options[ImageEffect.Blur].Controls.AddRange([_gaussian, _pixelate, _blurIntensityIcon, _blurIntensity, _blurIntensityLabel]);

        // Docked in reverse order of addition: the options row and the tabs row, then the bottom bar,
        // span the whole width; the layout strip takes the left of what remains, and the fill control
        // goes first so it gets the rest.
        Controls.Add(_preview);
        Controls.Add(_layouts);
        Controls.Add(_bottom);
        Controls.Add(_tabsRow);
        Controls.Add(_optionsRow);
        ResumeLayout(performLayout: true);

        // A long message wraps within the space left between the buttons, the bottom bar growing taller.
        _bottom.SizeChanged += (_, _) => FitStatusWidth();
        _outputButtons.SizeChanged += (_, _) => FitStatusWidth();
        _cancelButton.VisibleChanged += (_, _) => FitStatusWidth();
        _clearButton.Click += (_, _) => ClearAll();
        _settingsMenu.Items.Add(_startWithWindows);
        _toolTip.SetToolTip(_settingsButton, "Settings");
        _settingsButton.Click += (_, _) => ShowSettings();
        _startWithWindows.Click += (_, _) => ToggleStartWithWindows();
        _copyButton.Click += (_, _) => CopyToClipboard();
        _saveButton.Click += (_, _) => Save();
        _cancelButton.Click += (_, _) => _export?.Cancel();
        _forceImage.CheckedChanged += (_, _) => _preview.ForceStill = _forceImage.Checked;
        UpdateEffectIcons();
        FitEffectRows();

        // The Reset button sizes itself to its font and DPI: the tabs follow it.
        _resetButton.SizeChanged += (_, _) => FitEffectRows();
        _tabsRow.Paint += (_, e) => PaintOptionsEdge(e.Graphics);
        _effectTabs.TabClicked += (_, effect) => SelectEffect(effect);
        _effectTabs.CheckClicked += (_, effect) => ToggleEffect(effect);
        _toolTip.SetToolTip(_effectResetButton, "Brings this effect back to its defaults");
        _toolTip.SetToolTip(_resetButton, "Brings every effect of the cell back to its defaults");
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
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _preview.DragEnter += OnDragEnter;
        _preview.DragOver += OnPreviewDragOver;
        _preview.DragLeave += (_, _) => _preview.ShowDropTarget(null);
        _preview.DragDrop += OnDragDrop;
        _preview.DropZoneClicked += (_, _) => PickFiles();
        _layouts.DragEnter += OnDragEnter;
        _layouts.DragDrop += OnDragDrop;
        UpdateButtons();
        FitStatusWidth();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsMenu.Dispose();
            _toolTip.Dispose();
            _resetButton.Image?.Dispose();
            _effectResetButton.Image?.Dispose();
            _grayscaleIcon.Image?.Dispose();
            _gaussian.Image?.Dispose();
            _pixelate.Image?.Dispose();
            _blurIntensityIcon.Image?.Dispose();
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
        Image?[] previous = [_resetButton.Image, _effectResetButton.Image, _grayscaleIcon.Image, _gaussian.Image, _pixelate.Image, _blurIntensityIcon.Image];
        foreach (var effect in Enum.GetValues<ImageEffect>())
        {
            _effectTabs.SetIcon(effect, effect switch
            {
                ImageEffect.Zoom => EffectIcons.Zoom(size),
                ImageEffect.Rotate => EffectIcons.Rotate(size),
                ImageEffect.Flip => EffectIcons.Flip(size),
                ImageEffect.Frames => EffectIcons.Frames(size),
                ImageEffect.BlackAndWhite => EffectIcons.BlackAndWhite(size),
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
        foreach (var image in previous)
        {
            image?.Dispose();
        }
    }

    /// <summary>
    /// The tabs as tall as the Reset beside them; the options row as tall as the tallest options, so
    /// nothing below it moves when another tab is selected.
    /// </summary>
    private void FitEffectRows()
    {
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
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

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
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            // Onto a cell: replaces it. Onto the drop zone or elsewhere in the window: added like a paste.
            int target = sender == _preview ? DropCell(e) : -1;
            await AddFilesAsync(paths, target);
        }
    }

    private int DropCell(DragEventArgs e) => _preview.CellAt(_preview.PointToClient(new Point(e.X, e.Y)));

    /// <summary>Files chosen from the drop zone's picker are added like a drop onto it.</summary>
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

    /// <summary>
    /// Every supported type at once, then one filter per type, then all files. Videos mirror
    /// <see cref="VideoFrames"/>; text is detected by content, so only its common extensions are listed.
    /// </summary>
    private static string PickerFilter()
    {
        (string Name, string[] Extensions)[] types =
        [
            ("Images", new[] { "png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff", "webp" }),
            ("Videos", new[] { "mp4", "m4v", "mov", "avi", "wmv", "asf", "mkv", "webm", "3gp", "3g2", "mpg", "mpeg", "ts", "m2ts", "mts" }),
            ("PDF", new[] { "pdf" }),
            ("Text", new[] { "txt", "md", "log", "csv", "json", "xml" }),
        ];

        static string Patterns(IEnumerable<string> extensions) => string.Join(";", extensions.Select(e => $"*.{e}"));

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

        string[]? files = null;
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                files = Clipboard.GetFileDropList().Cast<string>().ToArray();
            }
            else if (Clipboard.GetImage() is { } image)
            {
                using (image)
                {
                    _preview.Add([ImageLoader.FromImage(image)]);
                }

                return;
            }
        }
        catch (ExternalException ex)
        {
            ShowStatus($"Paste failed: {ex.Message}", error: true);
            return;
        }

        if (files is null)
        {
            ShowStatus("Nothing to paste: the clipboard holds no image.");
            return;
        }

        await AddFilesAsync(files);
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

    private void ClearAll()
    {
        if (IsExporting)
        {
            return;
        }

        int count = _preview.Images.Count;
        if (count == 0)
        {
            return;
        }

        _preview.Clear();
        ShowStatus(count == 1 ? "1 image removed." : $"{count} images removed.");
    }

    /// <summary>
    /// Copies the grid: a still image, or — with animated content, unless forced to an image — an MP4
    /// video, written to the temp folder and put on the clipboard as a file.
    /// </summary>
    private async void CopyToClipboard()
    {
        if (_preview.Images.Count == 0 || IsExporting)
        {
            return;
        }

        if (ExportsVideo)
        {
            string path = TempVideoPath();
            var videoClock = Stopwatch.StartNew();
            if (await ExportVideoAsync(path) is { } video)
            {
                var encoding = videoClock.Elapsed;
                try
                {
                    Clipboard.SetFileDropList([path]);
                    ShowStatus(VideoSummary($"Copied {Path.GetFileName(path)}", path, video, encoding));
                }
                catch (ExternalException ex)
                {
                    ShowStatus($"Copy failed: {ex.Message}", error: true);
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

            // Standard bitmap for most apps, plus the PNG format that browsers paste more reliably.
            var data = new DataObject();
            data.SetImage(result);
            data.SetData("PNG", png);
            Clipboard.SetDataObject(data, copy: true);
            ShowStatus(StillSummary("Copied to the clipboard", result.Size, png.Length, encoding));
        }
        catch (ExternalException ex)
        {
            ShowStatus($"Copy failed: {ex.Message}", error: true);
        }
    }

    /// <summary>Saves the grid as a PNG, or — with animated content, unless forced to an image — as an MP4 video.</summary>
    private async void Save()
    {
        if (_preview.Images.Count == 0 || IsExporting)
        {
            return;
        }

        bool video = ExportsVideo;
        string extension = video ? "mp4" : "png";
        using var dialog = new SaveFileDialog
        {
            Filter = video ? "MP4 video (*.mp4)|*.mp4" : "PNG image (*.png)|*.png",
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
        if (video)
        {
            if (await ExportVideoAsync(dialog.FileName) is { } result)
            {
                ShowStatus(VideoSummary(saved, dialog.FileName, result, clock.Elapsed));
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
            ShowStatus(StillSummary(saved, still.Size, new FileInfo(dialog.FileName).Length, clock.Elapsed));
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    private bool IsExporting => _export is not null;

    /// <summary>Content that plays: a frozen one is exported as a still.</summary>
    private bool HasAnimation => _preview.Images.Any(i => i.Plays);

    /// <summary>Animated content exports as a video, unless "Force as image" is checked.</summary>
    private bool ExportsVideo => HasAnimation && !_forceImage.Checked;

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
            return Compositor.Render(_preview.Images, _preview.ActiveLayout!);
        }

        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!);
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
    /// Writes the MP4 video to <paramref name="path"/> off the UI thread, with its progress and a
    /// Cancel button in the status line, the grid locked meanwhile: the contents playing. Returns
    /// null when cancelled or failing.
    /// </summary>
    private async Task<GridExport.Result?> ExportVideoAsync(string path)
    {
        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!);
        var cancellation = BeginExport("Exporting the video… 0 %", cancellable: true);
        var progress = new Progress<double>(done =>
        {
            // Reports still queued when the export ends are dropped: they would hide its outcome.
            if (IsExporting)
            {
                ShowStatus($"Exporting the video… {done:P0}");
            }
        });
        try
        {
            return await Task.Run(() => GridExport.RenderVideo(job, path, progress, cancellation));
        }
        catch (OperationCanceledException)
        {
            ShowStatus("Video export cancelled.");
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
    private static string StillSummary(string done, Size size, long bytes, TimeSpan encoding) =>
        Summary(done, "PNG", size, bytes, 1, TimeSpan.Zero, sound: null, encoding);

    /// <summary>What a video copy or save produced, for the status line, <paramref name="path"/> being the file written.</summary>
    private static string VideoSummary(string done, string path, GridExport.Result video, TimeSpan encoding) =>
        Summary(
            done,
            "MP4 video",
            video.Size,
            new FileInfo(path).Length,
            video.Frames,
            video.Length,
            video.SoundProblem ?? (video.SoundPath is null ? "no sound" : $"sound: {Path.GetFileName(video.SoundPath)}"),
            encoding);

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

    /// <summary>Videos copied to the clipboard live here: the clipboard only holds their path.</summary>
    private static string TempVideoFolder => Path.Combine(Path.GetTempPath(), "ImageGridFusion");

    private static string TempVideoPath()
    {
        Directory.CreateDirectory(TempVideoFolder);
        return Path.Combine(TempVideoFolder, $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
    }

    /// <summary>Removes the videos copied by previous sessions.</summary>
    private static void CleanTempVideos()
    {
        try
        {
            if (!Directory.Exists(TempVideoFolder))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(TempVideoFolder, "*.mp4"))
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
        _clearButton.Enabled = any;
        _copyButton.Enabled = any;
        _saveButton.Enabled = any;
        _forceImage.Visible = HasAnimation;
        _forceImage.Enabled = !IsExporting;
        UpdateEffects();
    }

    /// <summary>A toggle of an options row, pressed from the look of the selected image.</summary>
    private static CheckBox OptionButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoCheck = false,
        Appearance = Appearance.Button,
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
        if (_selectedEffect is { } effect && _preview.SelectedImage?.Look is { } look)
        {
            _preview.SetSelectedLook(look.Reset(effect));
        }
    }

    /// <summary>The tabs row's Reset: every effect of the selected image back to its default state; the selected tab stays.</summary>
    private void ResetEffects()
    {
        _preview.SetSelectedLook(ImageLook.None);
        UpdateEffects();
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
        _resetButton.Enabled = enabled && !look!.IsNone;
        if (look is not null)
        {
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
        }

        _zoomLabel.Text = $"Zoom: {(look?.TurnOn(ImageEffect.Zoom).Zoom ?? 1) * 100:0} %";
        _fineAngleLabel.Text = AngleText(_fineAngle.Value);
        UpdateFramesLabel();
        _grayscaleLabel.Text = $"Intensity: {_grayscale.Value}%";
        _blurIntensityLabel.Text = $"Intensity: {_blurIntensity.Value}%";
        _syncingEffects = false;

        // An effect that does not apply to the image keeps its tab selectable, its options disabled.
        bool usable = enabled && _selectedEffect is { } selected && Unavailable(selected, image) is null;
        foreach (var (effect, row) in _options)
        {
            row.Visible = _selectedEffect == effect;
            row.Enabled = usable;
        }

        _effectResetButton.Visible = _selectedEffect is not null;
        _effectResetButton.Enabled = usable && look!.Reset(_selectedEffect!.Value) != look;
        _preview.ShowsBlurBars = _selectedEffect == ImageEffect.Blur && look?.IsActive(ImageEffect.Blur) == true;
    }

    /// <summary>Why <paramref name="effect"/> does not apply to <paramref name="image"/>; <c>null</c> when it does.</summary>
    private static string? Unavailable(ImageEffect effect, SourceImage? image) =>
        effect == ImageEffect.Frames && image is { IsAnimated: false }
            ? "Frames only applies to videos, animated GIFs and content of several pages"
            : null;

    /// <summary>Shows a message that stays until the next one replaces it; errors in red.</summary>
    private void ShowStatus(string message, bool error = false)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = message;
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
