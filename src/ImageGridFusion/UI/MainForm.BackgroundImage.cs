using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The Grid background's image (workfiles/20261104-grid-background-image.md): its options after the fill's — its
/// thumbnail and drop zone, Browse…, Paste, Edit, Remove, its placements and its opacity —, the files dropped onto
/// them, and the background edit view Edit or the thumbnail's double-click opens.
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

    // The image's Zoom effect, under its opacity (workfiles/20261104-grid-background-image.md, Iteration 7): the cell Zoom slider's scale.
    private readonly PictureBox _backgroundImageZoomIcon = OpacityIcon();
    private readonly StepSlider _backgroundImageZoom = OptionSlider((int)Math.Round(Math.Log2(ImageLook.MinZoom) * 100), (int)Math.Round(Math.Log2(ImageLook.MaxZoom) * 100), 10);
    private readonly Label _backgroundImageZoomLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
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
        // Browse… right after Paste, its text beside its icon so it is seen (workfiles/20261104-grid-background-image.md, Iteration 6).
        this._backgroundImageBrowse.Text = Strings.PanelBgImageBrowse;
        this._backgroundImageBrowse.TextImageRelation = TextImageRelation.ImageBeforeText;
        buttons.Controls.AddRange([this._backgroundImagePaste, this._backgroundImageBrowse, this._backgroundImageEdit, this._backgroundImageRemove]);
        var opacity = OptionLine();
        opacity.Controls.AddRange([this._backgroundImageOpacityIcon, this._backgroundImageOpacity, this._backgroundImageOpacityLabel]);
        var zoom = OptionLine();
        zoom.Controls.AddRange([this._backgroundImageZoomIcon, this._backgroundImageZoom, this._backgroundImageZoomLabel]);
        var lines = OptionLine();
        lines.FlowDirection = FlowDirection.TopDown;
        lines.Controls.AddRange([buttons, opacity, zoom]);
        this._globalOptions[GlobalEffect.GridBackground].Controls.AddRange([this._backgroundImageBox, lines, this._backgroundFit]);

        // As tall as the placements' thumbnails, so the row keeps its height; the canvas's usual shape.
        this._backgroundImageBox.Size = new Size(this._backgroundFit.Height * 16 / 10, this._backgroundFit.Height);
        this._backgroundImageOpacity.Width = this._backgroundImageZoom.Width = 110;

        this._toolTip.SetToolTip(this._backgroundImageBrowse, Strings.PanelBgImageBrowseTip);
        this._toolTip.SetToolTip(this._backgroundImagePaste, Strings.PanelBgImagePasteTip);
        this._toolTip.SetToolTip(this._backgroundImageRemove, Strings.PanelBgImageRemoveTip);
        this._toolTip.SetToolTip(this._backgroundImageOpacity, Strings.PanelBgImageOpacityTip);
        this._toolTip.SetToolTip(this._backgroundImageZoom, Strings.PanelBgImageZoomTip);

        // No click on the thumbnail — Browse… chooses a file —; a double-click edits the image (Iteration 8).
        this._backgroundImageBox.DoubleClick += (_, _) => this.ToggleBackgroundEdit();
        this._backgroundImageZoom.ValueChanged += (_, _) =>
        {
            double value = Math.Abs(this._backgroundImageZoom.Value) <= 4 ? 1 : Math.Pow(2, this._backgroundImageZoom.Value / 100.0);
            this._backgroundImageZoomLabel.Text = ZoomText(value);
            this.ChangeBackgroundImage(image => this.Zoomed(image, value));
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
        this._backgroundImageZoom.Enabled = this._backgroundImageZoomLabel.Enabled = enabled && image is not null;
        this._backgroundFit.Pressed = image is null ? null : FitOf(image);

        double zoom = image is null ? 1 : ShownZoom(image, this._preview.CanvasRatio);
        bool syncing = this._syncingEffects;
        this._syncingEffects = true;
        this._backgroundImageOpacity.Value = (int)Math.Round((image?.Opacity ?? 1) * 100);
        this._backgroundImageZoom.Value = Math.Clamp((int)Math.Round(Math.Log2(zoom) * 100), this._backgroundImageZoom.Minimum, this._backgroundImageZoom.Maximum);
        this._syncingEffects = syncing;
        this._backgroundImageOpacityLabel.Text = OpacityText(this._backgroundImageOpacity.Value);
        this._backgroundImageZoomLabel.Text = ZoomText(zoom);

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
            editing ? Strings.PanelEditDoneTip : Strings.PanelBgImageEditTip);
        this._toolTip.SetToolTip(this._backgroundImageBox, image?.Image.FilePath is { } path
            ? Texts.Format(Strings.PanelBgImageBoxTipFile, Path.GetFileName(path))
            : Strings.PanelBgImageBoxTipEmpty);
    }

    private static string ZoomText(double zoom) => Texts.Format(Strings.PanelBgImageZoomText, zoom * 100);

    /// <summary>The cell the image's zoom is read in: the canvas, at <paramref name="ratio"/>, for an image in its cell; a box of its own shape stretched or tiled.</summary>
    private static Rectangle ZoomCell(BackgroundImage image, double ratio) => image.Placement == BackgroundPlacement.Cell
        ? new Rectangle(0, 0, Math.Max(1, (int)Math.Round(1000 * ratio)), 1000)
        : new Rectangle(Point.Empty, image.Shown);

    /// <summary>The zoom the image is drawn at, its fit mode resolved (RULES.md § Rendering): what the Zoom slider shows.</summary>
    private static double ShownZoom(BackgroundImage image, double ratio) => image.Look.ZoomIn(ZoomCell(image, ratio), image.Shown);

    /// <summary>
    /// The image at a free <paramref name="zoom"/> from the Zoom slider: its Zoom effect turned on, the fit mode left,
    /// its center kept where it is (RULES.md § Zoom Keeps the Image in Place).
    /// </summary>
    private BackgroundImage Zoomed(BackgroundImage image, double zoom)
    {
        var shown = image.Shown;
        var cell = ZoomCell(image, this._preview.CanvasRatio);
        var focus = FitCalculator.FocusKeeping(cell, shown, image.Look.ZoomIn(cell, shown), zoom, image.Look.Focus, image.Look.FineAngle, FitCalculator.ImageCenter);
        return image.WithLook(image.Look.WithZoom(zoom).WithFocus(focus));
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

        var previousZoom = this._backgroundImageZoomIcon.Image;
        this._backgroundImageZoomIcon.Image = UiIcons.Zoom.Copy(size);
        this._backgroundImageZoomIcon.Size = new Size(size, size);
        previousZoom?.Dispose();

        this._backgroundIconSize = size;
        this._backgroundEditShowsDone = null;
        this.ShowBackgroundImageOptions();
    }

    /// <summary>The file picker's image, GIF or video as the Grid background's image.</summary>
    private async Task BrowseBackgroundImageAsync()
    {
        if (this.IsExporting)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = Strings.PanelBgImageDialogTitle,
            Filter = PickerFilter(),
            InitialDirectory = ExistingFolder(AppSettings.AddFolder) ?? "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RememberFolder(AppSettings.SaveAddFolder, dialog.FileName, Strings.PanelAddImagesFolder);
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
            this.ShowStatus(Texts.Format(Strings.PanelPasteFailed, ex.Message), error: true);
            return;
        }

        if (files is { Length: > 0 })
        {
            await this.SetBackgroundImageFromFilesAsync(files, look: null);
        }
        else
        {
            this.ShowStatus(Strings.PanelBgImageNothingToPaste);
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
            this.ShowStatus(paths.Length == 1 ? Strings.PanelBgImageNoPreviewOne : Strings.PanelBgImageNoPreviewMany, error: true);
            return;
        }

        this.SetBackgroundImage(image, look);
        int ignored = paths.Length - skipped - 1;
        var messages = new List<string>();
        if (skipped > 0)
        {
            messages.Add(Texts.Format(Strings.PanelFilesSkipped, Files(skipped)));
        }

        if (ignored > 0)
        {
            messages.Add(Texts.Format(Strings.PanelBgImageIgnored, Files(ignored)));
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
