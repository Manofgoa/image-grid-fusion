using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// The Grid background's image (workfiles/20261104-grid-background-image.md): its options after the fill's — its
/// thumbnail and drop zone, Browse…, Paste, Edit, Remove, its placements and its opacity —, the files dropped onto
/// them, and the background edit view the thumbnail's double-click opens.
/// </summary>
internal sealed partial class MainForm
{
    private readonly BackgroundImageBox _backgroundImageBox = new() { Anchor = AnchorStyles.Left, Margin = new Padding(6, 1, 3, 1) };
    private readonly Button _backgroundImageBrowse = IconButton();
    private readonly Button _backgroundImagePaste = IconButton();
    private readonly Button _backgroundImageEdit = IconButton();
    private readonly Button _backgroundImageRemove = IconButton();
    private readonly BackgroundFitStrip _backgroundFit = new() { Anchor = AnchorStyles.Left };
    private readonly PictureBox _backgroundImageOpacityIcon = OpacityIcon();
    private readonly StepSlider _backgroundImageOpacity = OptionSlider(0, 100, 10);
    private readonly Label _backgroundImageOpacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    // A click on a thumbnail holding an image waits for the double-click time: a double-click opens the edit view instead of the picker.
    private readonly System.Windows.Forms.Timer _backgroundClickWait = new() { Interval = SystemInformation.DoubleClickTime };
    private int _backgroundIconSize;
    private bool? _backgroundEditShowsDone;

    /// <summary>The Grid background's image while the Grid background is on: a content the grid plays, hears and exports.</summary>
    private SourceImage? ActiveBackgroundImage => this.ActiveGridBackground?.Image?.Image;

    /// <summary>The contents played beside the cells: the shown layers', then the Grid background's image.</summary>
    private IReadOnlyList<SourceImage> ActiveBesideCells =>
        this.ActiveBackgroundImage is { } background ? [.. this.ActiveLayerImages, background] : this.ActiveLayerImages;

    /// <summary>The image's options after the fill's, in the Grid background's row, and their events.</summary>
    private void BuildBackgroundImageOptions()
    {
        var buttons = OptionLine();
        buttons.Controls.AddRange([this._backgroundImageBrowse, this._backgroundImagePaste, this._backgroundImageEdit, this._backgroundImageRemove]);
        var opacity = OptionLine();
        opacity.Controls.AddRange([this._backgroundImageOpacityIcon, this._backgroundImageOpacity, this._backgroundImageOpacityLabel]);
        var lines = OptionLine();
        lines.FlowDirection = FlowDirection.TopDown;
        lines.Controls.AddRange([buttons, opacity]);
        this._globalOptions[GlobalEffect.GridBackground].Controls.AddRange([this._backgroundImageBox, lines, this._backgroundFit]);

        // As tall as the placements' thumbnails, so the row keeps its height; the canvas's usual shape.
        this._backgroundImageBox.Size = new Size(this._backgroundFit.Height * 16 / 10, this._backgroundFit.Height);
        this._backgroundImageOpacity.Width = 110;

        this._toolTip.SetToolTip(this._backgroundImageBrowse, "Browse… — chooses the grid background's image: an image, a GIF or a video");
        this._toolTip.SetToolTip(this._backgroundImagePaste, "Paste — the clipboard's image, or its first file, as the grid background's image");
        this._toolTip.SetToolTip(this._backgroundImageRemove, "Remove — the grid background's image gone, its fill alone");
        this._toolTip.SetToolTip(this._backgroundImageOpacity, "Opacity of the grid background's image, over its fill");

        // A click loads an image, as Browse… does (workfiles/20261104-grid-background-image.md, Iteration 4); a double-click edits it.
        this._backgroundImageBox.MouseClick += async (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (this._gridBackground.Image is null)
            {
                await this.BrowseBackgroundImageAsync();
                return;
            }

            this._backgroundClickWait.Stop();
            this._backgroundClickWait.Start();
        };
        this._backgroundClickWait.Tick += async (_, _) =>
        {
            this._backgroundClickWait.Stop();
            await this.BrowseBackgroundImageAsync();
        };
        this._backgroundImageBox.DoubleClick += (_, _) =>
        {
            this._backgroundClickWait.Stop();
            this.ToggleBackgroundEdit();
        };
        this._backgroundImageBrowse.Click += async (_, _) => await this.BrowseBackgroundImageAsync();
        this._backgroundImagePaste.Click += async (_, _) => await this.PasteBackgroundImageAsync();
        this._backgroundImageEdit.Click += (_, _) => this.ToggleBackgroundEdit();
        this._backgroundImageRemove.Click += (_, _) =>
        {
            this._preview.EndLayerEdit();
            this.ChangeGridBackground(background => background.WithImage(null));
        };
        this._backgroundFit.Picked += (_, fit) => this.ChangeBackgroundImage(image => Placed(image, fit));
        this._backgroundImageOpacity.ValueChanged += (_, _) =>
        {
            this._backgroundImageOpacityLabel.Text = OpacityText(this._backgroundImageOpacity.Value);
            this.ChangeBackgroundImage(image => image.WithOpacity(this._backgroundImageOpacity.Value / 100.0));
        };

        // The background edit view's look changes are the Grid background's: kept here for the undo history and the exports.
        this._preview.GridBackgroundEdited += (_, background) =>
        {
            this._gridBackground = background;
            this.ShowBackgroundImageOptions();
        };
        this._preview.LayerEditChanged += (_, _) => this.ShowBackgroundImageOptions();
    }

    /// <summary>The image placed as <paramref name="fit"/> says: Fill and Contain its Zoom effect's fit modes — recentering it —, Stretch and Tile its own placement.</summary>
    private static BackgroundImage Placed(BackgroundImage image, BackgroundFit fit) => fit switch
    {
        BackgroundFit.Fill => image with { Placement = BackgroundPlacement.Cell, Look = image.Look.WithZoomFit(ZoomFit.Fill).WithFocus(new PointF(0.5f, 0.5f)) },
        BackgroundFit.Contain => image with { Placement = BackgroundPlacement.Cell, Look = image.Look.WithZoomFit(ZoomFit.Contain).WithFocus(new PointF(0.5f, 0.5f)) },
        BackgroundFit.Stretch => image.WithPlacement(BackgroundPlacement.Stretch),
        _ => image.WithPlacement(BackgroundPlacement.Tile),
    };

    /// <summary>The placement shown pressed: the fit mode of an image in its cell, none for a free zoom.</summary>
    private static BackgroundFit? FitOf(BackgroundImage image) => image.Placement switch
    {
        BackgroundPlacement.Stretch => BackgroundFit.Stretch,
        BackgroundPlacement.Tile => BackgroundFit.Tile,
        _ => image.Look.IsActive(ImageEffect.Zoom) ? image.Look.ZoomFit switch
        {
            ZoomFit.Fill => BackgroundFit.Fill,
            ZoomFit.Contain => BackgroundFit.Contain,
            _ => null,
        } : null,
    };

    /// <summary>An option of the image changed: applied to it, turning the Grid background on (RULES.md § Options Toolbar); nothing without an image.</summary>
    private void ChangeBackgroundImage(Func<BackgroundImage, BackgroundImage> change)
    {
        if (this._gridBackground.Image is null)
        {
            return;
        }

        this.ChangeGridBackground(background => background.Image is { } image ? background.WithImage(change(image)) : background);
    }

    /// <summary>The image's options, from the Grid background's settings: the kept ones while it is off.</summary>
    private void ShowBackgroundImageOptions()
    {
        var image = this._gridBackground.Image;
        bool enabled = !this.IsExporting;
        bool editing = this._preview.EditingBackground;
        this._backgroundImageBox.Show(image);
        this._backgroundImageBox.Enabled = enabled;
        this._backgroundImageBrowse.Enabled = this._backgroundImagePaste.Enabled = enabled;
        this._backgroundImageEdit.Enabled = enabled && (editing || image is not null);
        this._backgroundImageRemove.Enabled = enabled && image is not null;
        this._backgroundFit.Enabled = this._backgroundImageOpacity.Enabled = this._backgroundImageOpacityLabel.Enabled = enabled && image is not null;
        this._backgroundFit.Pressed = image is null ? null : FitOf(image);

        bool syncing = this._syncingEffects;
        this._syncingEffects = true;
        this._backgroundImageOpacity.Value = (int)Math.Round((image?.Opacity ?? 1) * 100);
        this._syncingEffects = syncing;
        this._backgroundImageOpacityLabel.Text = OpacityText(this._backgroundImageOpacity.Value);

        // Edit, or Done while the background edit view shows.
        if (this._backgroundIconSize > 0 && this._backgroundEditShowsDone != editing)
        {
            var previous = this._backgroundImageEdit.Image;
            this._backgroundImageEdit.Image = (editing ? UiIcons.Done : UiIcons.Edit).Copy(this._backgroundIconSize);
            previous?.Dispose();
            this._backgroundEditShowsDone = editing;
        }

        this._toolTip.SetToolTip(
            this._backgroundImageEdit,
            editing ? "Done — gives the grid back (Escape)" : "Edit — shows the grid background's image alone, its effects working as on a cell (double-click its thumbnail)");
        this._toolTip.SetToolTip(this._backgroundImageBox, image?.Image.FilePath is { } path
            ? $"{Path.GetFileName(path)} — click to choose another image, or drop one here; double-click to edit its effects"
            : "The grid background's image, drawn over its fill, under the cells. Click to choose an image, a GIF or a video, or drop one here — from the file explorer or Windows Explorer —; double-click to edit its effects");
    }

    /// <summary>The image's buttons drawn at the monitor's DPI; the ones replaced disposed.</summary>
    private void UpdateBackgroundImageIcons(int size, Bitmap opacity)
    {
        foreach (var (button, icon) in new (Button, UiIcon)[]
        {
            (this._backgroundImageBrowse, UiIcons.AddImage), (this._backgroundImagePaste, UiIcons.Copy),
            (this._backgroundImageRemove, UiIcons.DeleteLayer),
        })
        {
            var previous = button.Image;
            button.Image = icon.Copy(size);
            previous?.Dispose();
        }

        var previousOpacity = this._backgroundImageOpacityIcon.Image;
        this._backgroundImageOpacityIcon.Image = (Bitmap)opacity.Clone();
        this._backgroundImageOpacityIcon.Size = new Size(size, size);
        previousOpacity?.Dispose();

        this._backgroundIconSize = size;
        this._backgroundEditShowsDone = null;
        this.ShowBackgroundImageOptions();
    }

    /// <summary>Whether a drop at <paramref name="screen"/> falls on the Grid background's options while its tab is selected.</summary>
    private bool DroppedOnGridBackground(Point screen) =>
        this._selectedGlobalEffect == GlobalEffect.GridBackground
        && this._globalOptionsRow.RectangleToScreen(this._globalOptionsRow.ClientRectangle).Contains(screen);

    /// <summary>The file picker's image, GIF or video as the Grid background's image.</summary>
    private async Task BrowseBackgroundImageAsync()
    {
        if (this.IsExporting)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Grid background image",
            Filter = PickerFilter(),
            InitialDirectory = ExistingFolder(AppSettings.AddFolder) ?? "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RememberFolder(AppSettings.SaveAddFolder, dialog.FileName, "Add images");
            await this.SetBackgroundImageFromFilesAsync([dialog.FileName], look: null);
        }
    }

    /// <summary>The clipboard's image, or its first file that loads, as the Grid background's image.</summary>
    private async Task PasteBackgroundImageAsync()
    {
        if (this.RefuseWhileExporting())
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
            else if (ClipboardImage.Get() is { } image)
            {
                using (image)
                {
                    this.SetBackgroundImage(ImageLoader.FromImage(image), preset: null);
                }

                return;
            }
        }
        catch (ExternalException ex)
        {
            this.ShowStatus($"Paste failed: {ex.Message}", error: true);
            return;
        }

        if (files is { Length: > 0 })
        {
            await this.SetBackgroundImageFromFilesAsync(files, look: null);
        }
        else
        {
            this.ShowStatus("Nothing to paste: the clipboard holds no image or file.");
        }
    }

    /// <summary>
    /// The first of <paramref name="paths"/> that loads — off the UI thread — as the Grid background's image, with
    /// <paramref name="look"/> when an effect preset's tile was dropped; the others said ignored.
    /// </summary>
    private async Task SetBackgroundImageFromFilesAsync(string[] paths, ImageLook? look)
    {
        if (this.RefuseWhileExporting())
        {
            return;
        }

        var (image, skipped) = await Task.Run(() =>
        {
            int failed = 0;
            foreach (var path in paths)
            {
                if (ImageLoader.TryLoadFile(path) is { } loaded)
                {
                    return (loaded, failed);
                }

                failed++;
            }

            return ((SourceImage?)null, failed);
        });

        // Locked meanwhile: what was loaded is let go.
        if (this.IsExporting)
        {
            image?.Dispose();
            return;
        }

        if (image is null)
        {
            this.ShowStatus($"{(paths.Length == 1 ? "No preview available for this file" : "No preview available for these files")}: the grid background's image is unchanged.", error: true);
            return;
        }

        this.SetBackgroundImage(image, look);
        int ignored = paths.Length - skipped - 1;
        var messages = new List<string>();
        if (skipped > 0)
        {
            messages.Add($"{Files(skipped)} skipped: no preview available");
        }

        if (ignored > 0)
        {
            messages.Add($"{Files(ignored)} ignored: the grid background takes one image");
        }

        if (messages.Count > 0)
        {
            this.ShowStatus(string.Join(" · ", messages) + ".");
        }
    }

    /// <summary>
    /// <paramref name="image"/> as the Grid background's image, turning it on: its default look — or an effect
    /// preset's, its Background off unless the preset chose one —, Fill pressed, the opacity kept; the image it
    /// replaces let go; the grid starts over, as for an image arriving in a cell.
    /// </summary>
    private void SetBackgroundImage(SourceImage image, ImageLook? preset)
    {
        // Composition's record: the form's own BackgroundImage is its picture.
        var look = preset is null ? Composition.BackgroundImage.DefaultLookFor(image)
            : preset.Background == BackgroundEffect.Default ? preset.TurnOff(ImageEffect.Background)
            : preset;
        var placed = new BackgroundImage(image, look, BackgroundPlacement.Cell, this._gridBackground.Image?.Opacity ?? 1);
        this._preview.EndLayerEdit();
        this.ChangeGridBackground(background => background.WithImage(placed));
        if (this._gridBackground.Image?.Image != image)
        {
            image.Dispose();
            return;
        }

        this._preview.StartOver();
    }

    /// <summary>The Edit button and the thumbnail's double-click: the background edit view opened, or closed when it shows (Done).</summary>
    private void ToggleBackgroundEdit()
    {
        if (this._preview.EditingBackground)
        {
            this._preview.EndLayerEdit();
            return;
        }

        if (this.IsExporting || this._gridBackground.Image is null)
        {
            return;
        }

        this._preview.EndLayerEdit();
        if (!this._gridBackgroundOn)
        {
            this.SetGridBackground(this._gridBackground, on: true);
        }

        this._preview.BeginBackgroundEdit();
    }
}
