using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// The Layers global effect (workfiles/20261029-layers.md): its options in the global options toolbar —
/// adding an image or a text, the selected layer's stacking, a text's own settings — and the paste and the
/// drop that make layers while its tab is selected. Its layers live in the preview, which draws them and
/// takes their gestures; the cell effects of a selected layer are the effects toolbar's.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>The files a paste or a drop makes layers of at once, the others ignored.</summary>
    private const int MaxLayerFiles = 10;

    /// <summary>How far each further layer of one paste or drop lands from the previous one, in fractions of the grid.</summary>
    private const double LayerCascadeStep = 0.04;

    // The Layers effect on: its layers drawn over the grid, in the preview and the exports.
    private bool _layersOn;

    private readonly Button _layerAddImage = OptionButton("Add image…");
    private readonly Button _layerPaste = OptionButton("Paste");
    private readonly Button _layerAddText = OptionButton("Add text");
    private readonly Button _layerToBack = OptionButton("To back");
    private readonly Button _layerLower = OptionButton("Lower");
    private readonly Button _layerRaise = OptionButton("Raise");
    private readonly Button _layerToFront = OptionButton("To front");
    private readonly TextBox _layerText = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Vertical,
        Width = 200,
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
    };

    private readonly ComboBox _layerFont = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Anchor = AnchorStyles.Left };
    private readonly CheckBox _layerBold = new() { Text = "Bold", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ColorSplitButton _layerColor = new();
    private readonly TrackBar _layerColorSample = SampleSlider();
    private readonly Label _layerColorSampleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _layerOutline = new() { Text = "Outline", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ColorSplitButton _layerOutlineColor = new();
    private readonly TrackBar _layerOutlineSample = SampleSlider();
    private readonly Label _layerOutlineSampleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The outline's width in percent of a line.
    private readonly TrackBar _layerOutlineWidth = OptionSlider((int)Math.Round(LayerTextStyle.MinOutlineWidth * 100), (int)Math.Round(LayerTextStyle.MaxOutlineWidth * 100), 2, controlStep: 1);
    private readonly Label _layerOutlineWidthLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _layerBackground = new() { Text = "Background", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ColorSplitButton _layerBackgroundColor = new();
    private readonly TrackBar _layerBackgroundSample = SampleSlider();
    private readonly Label _layerBackgroundSampleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _layerBackgroundOpacity = OptionSlider(0, 100, 10);
    private readonly Label _layerBackgroundOpacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The colors the three swatches show, so each is drawn again only when its color changes.
    private Color? _layerColorSwatch;
    private Color? _layerOutlineSwatch;
    private Color? _layerBackgroundSwatch;

    /// <summary>A button of the options, as wide as its text.</summary>
    private static Button OptionButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        BackColor = SystemColors.Control,
        UseVisualStyleBackColor = true,
    };

    /// <summary>
    /// The Layers options, on two lines beside a text field: adding, stacking, then a text's font, color,
    /// outline and background — each color with its eyedropper and sample (RULES.md § Color Options).
    /// </summary>
    private void BuildLayerOptions()
    {
        var addLine = OptionLine();
        addLine.Controls.AddRange([this._layerAddImage, this._layerPaste, this._layerAddText]);
        var stackLine = OptionLine();
        stackLine.Controls.AddRange([this._layerToBack, this._layerLower, this._layerRaise, this._layerToFront]);
        var buttons = OptionLine();
        buttons.FlowDirection = FlowDirection.TopDown;
        buttons.Controls.AddRange([addLine, stackLine]);

        var fontLine = OptionLine();
        fontLine.Controls.AddRange([this._layerFont, this._layerBold, this._layerColor, this._layerColorSample, this._layerColorSampleLabel]);
        var outlineLine = OptionLine();
        outlineLine.Controls.AddRange([this._layerOutline, this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, this._layerOutlineWidth, this._layerOutlineWidthLabel]);
        var styleLines = OptionLine();
        styleLines.FlowDirection = FlowDirection.TopDown;
        styleLines.Controls.AddRange([fontLine, outlineLine]);

        var backgroundLine = OptionLine();
        backgroundLine.Controls.AddRange([this._layerBackground, this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel]);
        var opacityLine = OptionLine();
        opacityLine.Controls.AddRange([this._layerBackgroundOpacity, this._layerBackgroundOpacityLabel]);
        var backgroundLines = OptionLine();
        backgroundLines.FlowDirection = FlowDirection.TopDown;
        backgroundLines.Controls.AddRange([backgroundLine, opacityLine]);

        this._layerText.Height = this.LogicalToDeviceUnits(52);
        this._globalOptions[GlobalEffect.Layers].Controls.AddRange([buttons, this._layerText, styleLines, backgroundLines]);

        var fonts = TextRaster.FontFamilies();
        this._layerFont.Items.AddRange([.. fonts]);

        this._toolTip.SetToolTip(this._layerAddImage, "Lays an image, a GIF or a video over the grid, chosen in the file picker");
        this._toolTip.SetToolTip(this._layerPaste, "Lays the clipboard's image, files or text over the grid (Ctrl+V while this tab is selected)");
        this._toolTip.SetToolTip(this._layerAddText, "Lays a text over the grid, typed in the field beside — Win+. opens the emoji panel");
        this._toolTip.SetToolTip(this._layerToBack, "Puts the selected layer under every other");
        this._toolTip.SetToolTip(this._layerLower, "Puts the selected layer one place lower");
        this._toolTip.SetToolTip(this._layerRaise, "Puts the selected layer one place higher");
        this._toolTip.SetToolTip(this._layerToFront, "Puts the selected layer over every other");
        this._toolTip.SetToolTip(this._layerText, "The selected text layer's text, several lines allowed; Win+. opens the emoji panel");
        this._toolTip.SetToolTip(this._layerOutline, "Draws an outline around the letters and the emojis, so the text reads on any image");
        this._toolTip.SetToolTip(this._layerOutlineWidth, "Width of the outline, as a share of a line");
        this._toolTip.SetToolTip(this._layerBackground, "Draws a box behind the text");
        this._toolTip.SetToolTip(this._layerBackgroundOpacity, "Opacity of the box behind the text");

        this._layerAddImage.Click += (_, _) => this.PickLayerFiles();
        this._layerPaste.Click += async (_, _) => await this.PasteLayersAsync();
        this._layerAddText.Click += (_, _) => this.AddTextLayer();
        this._layerToBack.Click += (_, _) => this.RestackLayer(_ => 0);
        this._layerLower.Click += (_, _) => this.RestackLayer(index => index - 1);
        this._layerRaise.Click += (_, _) => this.RestackLayer(index => index + 1);
        this._layerToFront.Click += (_, _) => this.RestackLayer(_ => int.MaxValue);
        this._layerText.TextChanged += (_, _) => this.ChangeLayerText(this._layerText.Text);
        this._layerFont.SelectedIndexChanged += (_, _) =>
        {
            if (this._layerFont.SelectedItem is string font)
            {
                this.ChangeLayerStyle(style => style with { Font = font });
            }
        };
        this._layerBold.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Bold = this._layerBold.Checked });
        this._layerOutline.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Outline = this._layerOutline.Checked });
        this._layerOutlineWidth.ValueChanged += (_, _) =>
        {
            this._layerOutlineWidthLabel.Text = OutlineWidthText(this._layerOutlineWidth.Value);
            this.ChangeLayerStyle(style => style with { Outline = true, OutlineWidth = this._layerOutlineWidth.Value / 100.0 });
        };
        this._layerBackground.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Background = this._layerBackground.Checked });
        this._layerBackgroundOpacity.ValueChanged += (_, _) =>
        {
            this._layerBackgroundOpacityLabel.Text = OpacityText(this._layerBackgroundOpacity.Value);
            this.ChangeLayerStyle(style => style with { Background = true, BackgroundOpacity = this._layerBackgroundOpacity.Value / 100.0 });
        };

        // Each color: the dialog's button and the eyedropper; the outline's and the box's turn them on.
        (ColorSplitButton Button, TrackBar Sample, Label Label, string Name, Func<LayerTextStyle, Color> Get, Func<LayerTextStyle, Color, LayerTextStyle> Set)[] colors =
        [
            (this._layerColor, this._layerColorSample, this._layerColorSampleLabel, "the text's", style => style.Color, (style, color) => style with { Color = color }),
            (this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, "the outline's", style => style.OutlineColor, (style, color) => style with { Outline = true, OutlineColor = color }),
            (this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel, "the box's", style => style.BackgroundColor, (style, color) => style with { Background = true, BackgroundColor = color }),
        ];
        foreach (var (button, sample, label, name, get, set) in colors)
        {
            this._toolTip.SetToolTip(button.Dialog, $"Chooses {name} color in the color dialog");
            this._toolTip.SetToolTip(button.Eyedropper, $"Takes {name} color from the preview: click a cell; Escape or a right click cancels");
            button.Dialog.Click += (_, _) => this.PickLayerColor(get, set);
            button.Eyedropper.Click += (_, _) => this.ArmLayerEyedropper(button, sample, set);
            label.Text = SampleText(sample.Value);
            label.MinimumSize = new Size(TextRenderer.MeasureText(SampleText(GridPreview.MaxEyedropperSample), label.Font).Width, 0);
            this._toolTip.SetToolTip(sample, "The eyedropper's sample: the side of the square of preview pixels it averages; not remembered");
            sample.ValueChanged += (_, _) => label.Text = SampleText(sample.Value);
        }

        this._layerOutlineWidthLabel.Text = OutlineWidthText(this._layerOutlineWidth.Value);
        this._layerOutlineWidthLabel.MinimumSize = new Size(TextRenderer.MeasureText(OutlineWidthText(20), this._layerOutlineWidthLabel.Font).Width, 0);
        this._layerBackgroundOpacityLabel.Text = OpacityText(this._layerBackgroundOpacity.Value);

        // A move or a turn by a gesture updates the options alone; any other change the buttons and rows too.
        this._preview.LayersChanged += (_, _) =>
        {
            if (this._preview.InGesture)
            {
                this.UpdateLayerOptions();
            }
            else
            {
                this.UpdateButtons();
            }
        };
    }

    private static string OutlineWidthText(int percent) => $"Width: {percent} %";

    /// <summary>The eyedropper icons of the three colors, drawn at the monitor's DPI; the ones replaced disposed.</summary>
    private void UpdateLayerIcons(int size)
    {
        foreach (var button in new[] { this._layerColor, this._layerOutlineColor, this._layerBackgroundColor })
        {
            var previous = button.Eyedropper.Image;
            button.Eyedropper.Image = EffectIcons.Eyedropper(size);
            previous?.Dispose();
        }

        this._layerColorSwatch = this._layerOutlineSwatch = this._layerBackgroundSwatch = null;
        this.UpdateLayerOptions();
    }

    /// <summary>
    /// Shows the Layers options: the adding buttons while the grid holds an image, the stacking ones while
    /// they would move the selected layer, a text's settings while a text layer is selected.
    /// </summary>
    private void UpdateLayerOptions()
    {
        bool enabled = !this.IsExporting && this.LayersUnavailable is null;
        var layer = this._preview.SelectedLayer;
        int index = this._preview.SelectedLayerIndex;
        int count = this._preview.Layers.Count;
        this._layerAddImage.Enabled = this._layerPaste.Enabled = this._layerAddText.Enabled = enabled;
        this._layerToBack.Enabled = this._layerLower.Enabled = enabled && layer is not null && index > 0;
        this._layerRaise.Enabled = this._layerToFront.Enabled = enabled && layer is not null && index < count - 1;

        bool text = enabled && layer is { IsText: true };
        foreach (var control in new Control[]
        {
            this._layerText, this._layerFont, this._layerBold, this._layerColor, this._layerColorSample, this._layerColorSampleLabel,
            this._layerOutline, this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, this._layerOutlineWidth, this._layerOutlineWidthLabel,
            this._layerBackground, this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel, this._layerBackgroundOpacity, this._layerBackgroundOpacityLabel,
        })
        {
            control.Enabled = text;
        }

        // The text layer's own settings shown — the defaults while none is selected; the field left alone
        // while it already holds the text, so its caret stays where the user types.
        var style = layer is { IsText: true } ? layer.Style! : LayerTextStyle.Default;
        string shownText = layer is { IsText: true } ? layer.Text! : "";
        bool syncing = this._syncingEffects;
        this._syncingEffects = true;
        if (this._layerText.Text != shownText)
        {
            this._layerText.Text = shownText;
        }

        if (!this._layerFont.Items.Contains(style.Font))
        {
            this._layerFont.Items.Add(style.Font);
        }

        this._layerFont.SelectedItem = style.Font;
        this._layerBold.Checked = style.Bold;
        this._layerOutline.Checked = style.Outline;
        this._layerOutlineWidth.Value = Math.Clamp((int)Math.Round(style.OutlineWidth * 100), this._layerOutlineWidth.Minimum, this._layerOutlineWidth.Maximum);
        this._layerBackground.Checked = style.Background;
        this._layerBackgroundOpacity.Value = (int)Math.Round(style.BackgroundOpacity * 100);
        this._syncingEffects = syncing;
        this._layerOutlineWidthLabel.Text = OutlineWidthText(this._layerOutlineWidth.Value);
        this._layerBackgroundOpacityLabel.Text = OpacityText(this._layerBackgroundOpacity.Value);
        this.ShowLayerSwatch(this._layerColor, ref this._layerColorSwatch, style.Color);
        this.ShowLayerSwatch(this._layerOutlineColor, ref this._layerOutlineSwatch, style.OutlineColor);
        this.ShowLayerSwatch(this._layerBackgroundColor, ref this._layerBackgroundSwatch, style.BackgroundColor);
    }

    /// <summary>A color button's swatch, drawn again only when its color changes.</summary>
    private void ShowLayerSwatch(ColorSplitButton button, ref Color? shown, Color color)
    {
        color = Color.FromArgb(255, color);
        if (color != shown)
        {
            shown = color;
            this.SetSwatch(button.Dialog, color);
        }
    }

    /// <summary>The Layers checkbox: on or off, its layers kept.</summary>
    private void ToggleLayers()
    {
        if (this.IsExporting)
        {
            return;
        }

        this._layersOn = !this._layersOn;
        this._preview.LayersShown = this._layersOn;
        this.UpdateButtons();
    }

    /// <summary>Acting on the Layers options turns them on, from the layers they kept (RULES.md).</summary>
    private void TurnLayersOn()
    {
        if (!this._layersOn)
        {
            this._layersOn = true;
            this._preview.LayersShown = true;
        }
    }

    /// <summary>The selected layer moved in the stacking order, to the place <paramref name="to"/> gives from its own.</summary>
    private void RestackLayer(Func<int, int> to)
    {
        int index = this._preview.SelectedLayerIndex;
        if (index < 0 || this.IsExporting)
        {
            return;
        }

        this.TurnLayersOn();
        this._preview.RestackSelectedLayer(to(index));
    }

    /// <summary>
    /// Lays <paramref name="images"/> over the grid, each on top, the last one selected: the first at
    /// <paramref name="at"/> — in fractions of the grid —, else at its center, the next ones a little further.
    /// </summary>
    private void AddImageLayers(IReadOnlyList<SourceImage> images, PointF? at = null)
    {
        if (images.Count == 0)
        {
            return;
        }

        this.TurnLayersOn();
        var center = at ?? new PointF(0.5f, 0.5f);
        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            image.Look = Layer.DefaultLook;
            var place = new LayerPlacement(
                Math.Clamp(center.X + i * LayerCascadeStep, 0, 1),
                Math.Clamp(center.Y + i * LayerCascadeStep, 0, 1),
                LayerPlacement.ImageSize,
                0);
            this._preview.AddLayer(new Layer(image, image.Look, place));
        }

        this.ShowStatus(images.Count == 1 ? "Layer added." : $"{images.Count} layers added.");
    }

    /// <summary>
    /// Lays a text over the grid, on top and selected, its field focused to type it: <paramref name="text"/>,
    /// empty by default, in the default style, at <paramref name="at"/> or the grid's center.
    /// </summary>
    private void AddTextLayer(string text = "", PointF? at = null)
    {
        if (this.IsExporting || this.LayersUnavailable is not null)
        {
            return;
        }

        var center = at ?? new PointF(0.5f, 0.5f);
        var place = new LayerPlacement(center.X, center.Y, LayerPlacement.TextSize, 0);
        if (this.RenderTextLayer(text, LayerTextStyle.Default, Layer.DefaultLook, place) is { } layer)
        {
            this.TurnLayersOn();
            this._preview.AddLayer(layer);
            this._layerText.Focus();
            this._layerText.SelectionStart = this._layerText.TextLength;
        }
    }

    /// <summary>
    /// A text layer of <paramref name="text"/> in <paramref name="style"/>, its content drawn anew; <c>null</c>,
    /// the status line saying why, when it cannot be drawn.
    /// </summary>
    private Layer? RenderTextLayer(string text, LayerTextStyle style, ImageLook look, LayerPlacement place)
    {
        try
        {
            var (bitmap, line) = TextRaster.Render(text, style);
            var image = new SourceImage(bitmap, filePath: null) { Look = look };
            return new Layer(image, look, place with { LinePixels = line }, text, style);
        }
        catch (Exception ex) when (ex is ExternalException or OutOfMemoryException or ArgumentException)
        {
            this.ShowStatus($"The text could not be drawn: {ErrorLog.Describe(ex)}", error: true);
            return null;
        }
    }

    /// <summary>The selected text layer takes <paramref name="text"/>, drawn again in its style.</summary>
    private void ChangeLayerText(string text)
    {
        if (this._syncingEffects || this.IsExporting || this._preview.SelectedLayer is not { IsText: true } layer || text == layer.Text)
        {
            return;
        }

        this.ReplaceTextLayer(layer, text, layer.Style!);
    }

    /// <summary>A setting of the selected text layer changed: drawn again in the new style, the Layers turned on (RULES.md).</summary>
    private void ChangeLayerStyle(Func<LayerTextStyle, LayerTextStyle> change)
    {
        if (this._syncingEffects || this.IsExporting || this._preview.SelectedLayer is not { IsText: true } layer)
        {
            return;
        }

        var style = change(layer.Style!);
        if (style != layer.Style)
        {
            this.ReplaceTextLayer(layer, layer.Text!, style);
        }
    }

    private void ReplaceTextLayer(Layer layer, string text, LayerTextStyle style)
    {
        if (this.RenderTextLayer(text, style, layer.Look, layer.Place) is { } drawn)
        {
            this.TurnLayersOn();
            this._preview.ReplaceSelectedLayer(drawn);
        }
    }

    /// <summary>The standard color dialog, on a color of the selected text layer: the color chosen taken.</summary>
    private void PickLayerColor(Func<LayerTextStyle, Color> get, Func<LayerTextStyle, Color, LayerTextStyle> set)
    {
        if (this.IsExporting || this._preview.SelectedLayer is not { IsText: true } layer)
        {
            return;
        }

        if (this._colorDialog.Show(this, get(layer.Style!)) is { } color)
        {
            this.ChangeLayerStyle(style => set(style, color));
        }
    }

    /// <summary>
    /// A text layer's eyedropper: the color follows the one under the cursor, a click takes it, a cancel gives
    /// the layer back as it was (RULES.md § Color Options).
    /// </summary>
    private void ArmLayerEyedropper(ColorSplitButton button, TrackBar sample, Func<LayerTextStyle, Color, LayerTextStyle> set)
    {
        if (this.IsExporting || this._preview.SelectedLayer is not { IsText: true } original)
        {
            return;
        }

        this.ArmEyedropper(
            button,
            sample,
            color => this.ChangeLayerStyle(style => set(style, color)),
            () =>
            {
                if (this._preview.SelectedLayer is { IsText: true } layer && layer.Style != original.Style)
                {
                    this.ReplaceTextLayer(layer, layer.Text!, original.Style!);
                }
            },
            color => this.ChangeLayerStyle(style => set(style, color)));
    }

    /// <summary>The file picker's images, GIFs and videos laid over the grid as layers.</summary>
    private async void PickLayerFiles()
    {
        if (this.IsExporting || this.LayersUnavailable is not null)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Add layers",
            Multiselect = true,
            Filter = PickerFilter(),
            InitialDirectory = ExistingFolder(AppSettings.AddFolder) ?? "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RememberFolder(AppSettings.SaveAddFolder, dialog.FileName, "Add images");
            await this.AddLayerFilesAsync(dialog.FileNames);
        }
    }

    /// <summary>Loads <paramref name="paths"/> off the UI thread — at most <see cref="MaxLayerFiles"/> — and lays them over the grid.</summary>
    private async Task AddLayerFilesAsync(string[] paths, PointF? at = null)
    {
        if (this.RefuseWhileExporting())
        {
            return;
        }

        var (images, unreadable) = await Task.Run(() =>
        {
            var loaded = new List<SourceImage>();
            int failed = 0;
            foreach (var path in paths.Take(MaxLayerFiles))
            {
                if (ImageLoader.TryLoadFile(path) is { } image)
                {
                    loaded.Add(image);
                }
                else
                {
                    failed++;
                }
            }

            return (loaded, failed);
        });

        // Locked meanwhile: what was loaded is let go.
        if (this.IsExporting)
        {
            images.ForEach(i => i.Dispose());
            return;
        }

        this.AddImageLayers(images, at);
        var messages = new List<string>();
        if (unreadable > 0)
        {
            messages.Add($"{Files(unreadable)} skipped: no preview available");
        }

        if (paths.Length > MaxLayerFiles)
        {
            messages.Add($"{Files(paths.Length - MaxLayerFiles)} ignored: {MaxLayerFiles} layers at most at once");
        }

        if (messages.Count > 0)
        {
            this.ShowStatus(string.Join(" · ", messages) + ".");
        }
    }

    /// <summary>The clipboard laid over the grid: its files, its image, or its text as a text layer.</summary>
    private async Task PasteLayersAsync()
    {
        if (this.RefuseWhileExporting() || this.LayersUnavailable is not null)
        {
            return;
        }

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
                using (image)
                {
                    this.AddImageLayers([ImageLoader.FromImage(image)]);
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
            this.ShowStatus($"Paste failed: {ex.Message}", error: true);
            return;
        }

        if (files is not null)
        {
            await this.AddLayerFilesAsync(files);
        }
        else if (text is not null)
        {
            await this.AddTextLayerAsync(text, at: null);
        }
        else
        {
            this.ShowStatus("Nothing to paste: the clipboard holds no image, file or text.");
        }
    }

    /// <summary>A pasted or dropped text laid over the grid as a text layer: its plain text, read off the UI thread.</summary>
    private async Task AddTextLayerAsync(TextData data, PointF? at)
    {
        string? text = data.Plain ?? await Task.Run(() => data.Parse()?.Text.Text);
        if (string.IsNullOrWhiteSpace(text))
        {
            this.ShowStatus("Nothing added: the text is empty.");
            return;
        }

        this.AddTextLayer(text.Replace("\r\n", "\n").TrimEnd('\n'), at);
    }

    /// <summary>A drop on the grid while the Layers tab is selected: its files, or its text, laid over the grid where it fell.</summary>
    private async Task DropLayersAsync(DragEventArgs e)
    {
        var at = this._preview.GridFractionAt(this._preview.PointToClient(new Point(e.X, e.Y)));
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await this.AddLayerFilesAsync(paths, at);
        }
        else if (e.Data is not null && TextData.TryGet(e.Data) is { } text)
        {
            await this.AddTextLayerAsync(text, at);
        }
    }
}
