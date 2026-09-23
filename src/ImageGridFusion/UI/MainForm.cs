using System.Drawing.Imaging;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    private readonly string[] _startupFiles;
    private readonly GridPreview _preview = new() { Dock = DockStyle.Fill, AllowDrop = true };
    private readonly Button _copyButton = new() { Text = "Copy", AutoSize = true };
    private readonly Button _saveButton = new() { Text = "Save…", AutoSize = true };

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

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_copyButton);

        // The fill control goes first so the bottom panel is docked before it.
        Controls.Add(_preview);
        Controls.Add(buttons);
        ResumeLayout(performLayout: true);

        _copyButton.Click += (_, _) => CopyToClipboard();
        _saveButton.Click += (_, _) => Save();
        _preview.ImagesChanged += (_, _) => UpdateButtons();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _preview.DragEnter += OnDragEnter;
        _preview.DragDrop += OnDragDrop;
        UpdateButtons();
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

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await AddFilesAsync(paths);
        }
    }

    private async void Paste()
    {
        if (Clipboard.ContainsFileDropList())
        {
            await AddFilesAsync(Clipboard.GetFileDropList().Cast<string>().ToArray());
        }
        else if (Clipboard.ContainsImage())
        {
            using var image = Clipboard.GetImage();
            if (image is not null)
            {
                _preview.Append([ImageLoader.FromImage(image)]);
            }
        }
    }

    /// <summary>Loads files in the given order, off the UI thread, until the free slots are filled.</summary>
    private async Task AddFilesAsync(string[] paths)
    {
        int wanted = _preview.FreeSlots;
        if (wanted <= 0)
        {
            return;
        }

        var images = await Task.Run(() =>
        {
            var loaded = new List<SourceImage>();
            foreach (var path in paths)
            {
                if (loaded.Count == wanted)
                {
                    break;
                }

                if (ImageLoader.TryLoadFile(path) is { } image)
                {
                    loaded.Add(image);
                }
            }

            return loaded;
        });
        _preview.Append(images);
    }

    private void CopyToClipboard()
    {
        if (_preview.Images.Count == 0)
        {
            return;
        }

        Cursor.Current = Cursors.WaitCursor;
        using var result = Compositor.Render(_preview.Images);
        using var png = new MemoryStream();
        result.Save(png, ImageFormat.Png);

        // Standard bitmap for most apps, plus the PNG format that browsers paste more reliably.
        var data = new DataObject();
        data.SetImage(result);
        data.SetData("PNG", png);
        Clipboard.SetDataObject(data, copy: true);
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
        using var result = Compositor.Render(_preview.Images);
        result.Save(dialog.FileName, ImageFormat.Png);
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
}
