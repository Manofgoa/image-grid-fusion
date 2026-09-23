using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    private readonly string[] _startupFiles;
    private readonly GridPreview _preview = new() { Dock = DockStyle.Fill, AllowDrop = true };
    private readonly Button _copyButton = new() { Text = "Copy", AutoSize = true };
    private readonly Button _saveButton = new() { Text = "Save…", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _statusTimer = new();

    public MainForm(string[] args)
    {
        _startupFiles = args;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Image Grid Fusion";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 580);
        MinimumSize = new Size(480, 320);
        AllowDrop = true;

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        buttons.Controls.Add(_copyButton);
        buttons.Controls.Add(_saveButton);

        // Status line on the left, buttons on the right.
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8),
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        // The fill control goes first so the bottom panel is docked before it.
        Controls.Add(_preview);
        Controls.Add(bottom);
        ResumeLayout(performLayout: true);

        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _status.Text = string.Empty;
        };
        _copyButton.Click += (_, _) => CopyToClipboard();
        _saveButton.Click += (_, _) => Save();
        _preview.ImagesChanged += (_, _) => UpdateButtons();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _preview.DragEnter += OnDragEnter;
        _preview.DragOver += OnPreviewDragOver;
        _preview.DragLeave += (_, _) => _preview.ShowDropTarget(-1);
        _preview.DragDrop += OnDragDrop;
        UpdateButtons();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Files dropped on the .exe icon; loaded once the window is visible so startup stays fast.
        if (_startupFiles.Length > 0)
        {
            await AddFilesAsync(_startupFiles);
        }
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
            case Keys.Delete when _preview.HasSelection:
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
    }

    private void OnPreviewDragOver(object? sender, DragEventArgs e)
    {
        if (e.Effect != DragDropEffects.None)
        {
            _preview.ShowDropTarget(DropCell(e));
        }
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        _preview.ShowDropTarget(-1);
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            // Onto a cell: replaces it. Elsewhere in the window: added like a paste.
            int target = sender == _preview ? DropCell(e) : -1;
            await AddFilesAsync(paths, target);
        }
    }

    private int DropCell(DragEventArgs e) => _preview.CellAt(_preview.PointToClient(new Point(e.X, e.Y)));

    private async void Paste()
    {
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
            messages.Add($"{Files(skipped)} skipped: not a readable image");
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

    private void CopyToClipboard()
    {
        if (_preview.Images.Count == 0)
        {
            return;
        }

        Cursor.Current = Cursors.WaitCursor;
        try
        {
            using var result = Compositor.Render(_preview.Images);
            using var png = new MemoryStream();
            result.Save(png, ImageFormat.Png);

            // Standard bitmap for most apps, plus the PNG format that browsers paste more reliably.
            var data = new DataObject();
            data.SetImage(result);
            data.SetData("PNG", png);
            Clipboard.SetDataObject(data, copy: true);
            ShowStatus($"Copied to the clipboard ({result.Width} × {result.Height}).");
        }
        catch (ExternalException ex)
        {
            ShowStatus($"Copy failed: {ex.Message}", error: true);
        }
    }

    private void Save()
    {
        if (_preview.Images.Count == 0)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = "png",
            FileName = $"fusion-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            InitialDirectory = DefaultSaveFolder(),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Cursor.Current = Cursors.WaitCursor;
        try
        {
            using var result = Compositor.Render(_preview.Images);
            result.Save(dialog.FileName, ImageFormat.Png);
            ShowStatus($"Saved {Path.GetFileName(dialog.FileName)} ({result.Width} × {result.Height}).");
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    /// <summary>Folder of the first image that came from a file, else the user's Pictures folder.</summary>
    private string DefaultSaveFolder()
    {
        string? file = _preview.Images.Select(i => i.FilePath).FirstOrDefault(p => p is not null);
        return Path.GetDirectoryName(file) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private void UpdateButtons()
    {
        bool any = _preview.Images.Count > 0;
        _copyButton.Enabled = any;
        _saveButton.Enabled = any;
    }

    /// <summary>Shows a message for a few seconds; errors in red, and a little longer.</summary>
    private void ShowStatus(string message, bool error = false)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = message;
        _statusTimer.Stop();
        _statusTimer.Interval = error ? 8000 : 4000;
        _statusTimer.Start();
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count} files";
}
