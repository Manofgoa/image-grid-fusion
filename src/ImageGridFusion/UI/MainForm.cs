using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    private const int ThresholdStepPercent = 1;
    private const int MaxThresholdPercent = 50;

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

    // One slider position per step: the value is a step index, not a percentage.
    private readonly TrackBar _threshold = new()
    {
        Minimum = 0,
        Maximum = MaxThresholdPercent / ThresholdStepPercent,
        Value = (int)Math.Round(FitCalculator.DefaultCropThreshold * 100 / ThresholdStepPercent),
        SmallChange = 1,
        LargeChange = 1,
        TickStyle = TickStyle.None,

        // Without ticks the thumb sits at the top: a height fitted to it keeps it level with the label.
        AutoSize = false,
        Size = new Size(160, 26),
        Anchor = AnchorStyles.Left,
    };
    private readonly Label _thresholdLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _carousel = new() { Text = "Carrousel", AutoSize = true, Anchor = AnchorStyles.Right };

    // Effects of the selected cell, then the options of the selected effect: see RULES.md.
    private readonly FlowLayoutPanel _effects = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(8, 0, 8, 8) };
    private readonly FlowLayoutPanel _blurOptions = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(8, 0, 8, 8), Visible = false };
    // The standard look, as the options: the flat one sizes itself without its image, clipping both.
    private readonly CheckBox _blurButton = new()
    {
        Text = "Blur",
        AutoSize = true,
        AutoCheck = false,
        Appearance = Appearance.Button,
        TextImageRelation = TextImageRelation.ImageBeforeText,
    };
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
    private readonly TrackBar _blurIntensity = new()
    {
        Minimum = 0,
        Maximum = 100,
        SmallChange = 1,
        LargeChange = 10,
        TickStyle = TickStyle.None,
        AutoSize = false,
        Size = new Size(160, 26),
        Anchor = AnchorStyles.Left,
    };
    private readonly Label _blurIntensityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The selected effect belongs to the toolbar: it stays selected on another cell where it is active.
    private bool _blurSelected;
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

        // Settings on the left; the label follows the slider so its changing width never moves it. Modes on the right.
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(_threshold, 0, 0);
        top.Controls.Add(_thresholdLabel, 1, 0);
        top.Controls.Add(_carousel, 2, 0);

        _effects.Controls.Add(_blurButton);
        _blurOptions.Controls.Add(_gaussian);
        _blurOptions.Controls.Add(_pixelate);
        _blurOptions.Controls.Add(_blurIntensityIcon);
        _blurOptions.Controls.Add(_blurIntensity);
        _blurOptions.Controls.Add(_blurIntensityLabel);

        // Docked in reverse order of addition: the top bar, the effects row and the options row, then
        // the bottom bar, span the whole width; the layout strip takes the left of what remains, and the
        // fill control goes first so it gets the rest.
        Controls.Add(_preview);
        Controls.Add(_layouts);
        Controls.Add(_bottom);
        Controls.Add(_blurOptions);
        Controls.Add(_effects);
        Controls.Add(top);
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
        _threshold.ValueChanged += (_, _) => UpdateThreshold();
        _forceImage.CheckedChanged += (_, _) => _preview.ForceStill = _forceImage.Checked;
        _carousel.CheckedChanged += (_, _) => _preview.PlaysCarousel = _carousel.Checked;
        UpdateEffectIcons();
        _blurButton.Click += (_, _) => ToggleBlur();
        _gaussian.CheckedChanged += (_, _) => SetBlurKind(_gaussian, BlurKind.Gaussian);
        _pixelate.CheckedChanged += (_, _) => SetBlurKind(_pixelate, BlurKind.Pixelate);
        _blurIntensity.ValueChanged += (_, _) => SetBlurIntensity();
        _preview.SelectedImageChanged += (_, _) => UpdateEffects();
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
        UpdateThreshold();
        UpdateButtons();
        FitStatusWidth();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsMenu.Dispose();
            _toolTip.Dispose();
            _blurButton.Image?.Dispose();
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
    }

    private void UpdateEffectIcons()
    {
        int size = LogicalToDeviceUnits(16);
        Image?[] previous = [_blurButton.Image, _gaussian.Image, _pixelate.Image, _blurIntensityIcon.Image];
        _blurButton.Image = EffectIcons.Blur(size);
        _gaussian.Image = EffectIcons.Gaussian(size);
        _pixelate.Image = EffectIcons.Pixelate(size);
        _blurIntensityIcon.Image = EffectIcons.Intensity(size);
        _blurIntensityIcon.Size = new Size(size, size);
        foreach (var image in previous)
        {
            image?.Dispose();
        }
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

        bool carousel = ExportsCarousel;
        if (carousel || ExportsVideo)
        {
            string path = TempVideoPath();
            var videoClock = Stopwatch.StartNew();
            if (await ExportVideoAsync(path, carousel) is { } video)
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

        bool carousel = ExportsCarousel;
        bool video = carousel || ExportsVideo;
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
            if (await ExportVideoAsync(dialog.FileName, carousel) is { } result)
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

    private bool HasAnimation => _preview.Images.Any(i => i.IsAnimated);

    /// <summary>Animated content exports as a video, unless "Force as image" is checked.</summary>
    private bool ExportsVideo => HasAnimation && !_forceImage.Checked;

    /// <summary>While "Carrousel" is checked, Copy and Save produce the carousel's video.</summary>
    private bool ExportsCarousel => _carousel.Checked && Carousel.CanPlay(_preview.Images.Count);

    /// <summary>
    /// Renders the still: the images as shown, or, for animated content, the page each one's slider
    /// selects, at full size — off the UI thread, the grid locked meanwhile. Returns null on failure.
    /// </summary>
    private async Task<Bitmap?> RenderStillAsync()
    {
        if (!HasAnimation)
        {
            Cursor.Current = Cursors.WaitCursor;
            return Compositor.Render(_preview.Images, _preview.ActiveLayout!, _preview.CropThreshold);
        }

        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!, _preview.CropThreshold);
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
    /// Cancel button in the status line, the grid locked meanwhile: the contents playing, or the
    /// <paramref name="carousel"/> of the images, its contents playing unless forced to images.
    /// Returns null when cancelled or failing.
    /// </summary>
    private async Task<GridExport.Result?> ExportVideoAsync(string path, bool carousel = false)
    {
        using var job = GridExport.Job.Capture(_preview.Images, _preview.ActiveLayout!, _preview.CropThreshold);
        bool playContents = !_forceImage.Checked;
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
            return await Task.Run(() => carousel
                ? CarouselExport.RenderVideo(job, playContents, path, progress, cancellation)
                : GridExport.RenderVideo(job, path, progress, cancellation));
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

        // The job holds the threshold it started with: moving the slider would only mislead the preview.
        _threshold.Enabled = false;
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
        _threshold.Enabled = true;
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

        // A single image has nowhere to move: the mode turns off with it.
        bool carousel = Carousel.CanPlay(_preview.Images.Count);
        _carousel.Enabled = carousel;
        if (!carousel)
        {
            _carousel.Checked = false;
        }

        UpdateEffects();
    }

    /// <summary>
    /// Inactive on the selected image: activates the blur and selects it. Active: selects it, and
    /// shows its options and bars. Active and selected: deactivates it.
    /// </summary>
    private void ToggleBlur()
    {
        if (_preview.SelectedImage?.Look is not { } look)
        {
            return;
        }

        if (look.Blur is null)
        {
            _blurSelected = true;
            _preview.SetSelectedLook(look.WithBlur(BlurEffect.Default));
        }
        else if (!_blurSelected)
        {
            _blurSelected = true;
        }
        else
        {
            _blurSelected = false;
            _preview.SetSelectedLook(look.WithBlur(null));
        }

        UpdateEffects();
    }

    private void SetBlurKind(RadioButton button, BlurKind kind)
    {
        if (button.Checked)
        {
            ChangeBlur(blur => blur.WithKind(kind));
        }
    }

    private void SetBlurIntensity()
    {
        _blurIntensityLabel.Text = $"Intensity: {_blurIntensity.Value}%";
        ChangeBlur(blur => blur.WithIntensity(_blurIntensity.Value / 100.0));
    }

    /// <summary>Applies an option of the options row to the blur of the selected image; not while the row follows the image.</summary>
    private void ChangeBlur(Func<BlurEffect, BlurEffect> change)
    {
        if (!_syncingEffects && _preview.SelectedImage?.Look is { Blur: { } blur } look)
        {
            _preview.SetSelectedLook(look.WithBlur(change(blur)));
        }
    }

    /// <summary>
    /// Shows the effects of the selected image: a button pressed per active effect, the options and
    /// bars of the selected one. The selection of an effect is dropped where it is inactive; with no
    /// cell selected, or during an export, the toolbar is disabled.
    /// </summary>
    private void UpdateEffects()
    {
        var blur = _preview.SelectedImage?.Look.Blur;
        bool enabled = _preview.SelectedImage is not null && !IsExporting;
        if (blur is null || !enabled)
        {
            _blurSelected = false;
        }

        _syncingEffects = true;
        _blurButton.Enabled = enabled;

        // Pressed: active. Selected: its options show below.
        _blurButton.Checked = blur is not null;
        if (blur is not null)
        {
            _gaussian.Checked = blur.Kind == BlurKind.Gaussian;
            _pixelate.Checked = blur.Kind == BlurKind.Pixelate;
            _blurIntensity.Value = (int)Math.Round(blur.Intensity * 100);
        }

        _blurIntensityLabel.Text = $"Intensity: {_blurIntensity.Value}%";
        _syncingEffects = false;

        _blurOptions.Visible = _blurSelected;
        _preview.ShowsBlurBars = _blurSelected;
    }

    /// <summary>Applies the slider position, live while it is dragged: the preview, then every export, use it.</summary>
    private void UpdateThreshold()
    {
        int percent = _threshold.Value * ThresholdStepPercent;
        _thresholdLabel.Text = $"Crop: {percent}%";
        _preview.CropThreshold = percent / 100.0;
    }

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
