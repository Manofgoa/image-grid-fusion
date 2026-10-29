using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// The Layers global effect (workfiles/20261029-layers.md): its options in the global options toolbar —
/// the layer list and its icon buttons, a text's own settings — and the drop onto its tab that makes
/// layers. Its layers live in the preview, which draws them and takes their gestures; the cell effects of
/// a selected layer are the effects toolbar's, in its edit view too.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>The files a paste or a drop makes layers of at once, the others ignored.</summary>
    private const int MaxLayerFiles = 10;

    /// <summary>How far each further layer of one paste or drop lands from the previous one, in fractions of the grid.</summary>
    private const double LayerCascadeStep = 0.04;

    // The Layers effect on: its layers drawn over the grid, in the preview and the exports.
    private bool _layersOn;

    // The layer list, at the left of the options, and its icon buttons below it (§ Layer List).
    private readonly LayerList _layerList = new() { Width = 240, Anchor = AnchorStyles.Left };
    private readonly Button _layerAddImage = IconButton();
    private readonly Button _layerPaste = IconButton();
    private readonly Button _layerAddText = IconButton();
    private readonly Button _layerEdit = IconButton();
    private readonly Button _layerDelete = IconButton();
    private readonly Button _layerToBack = IconButton();
    private readonly Button _layerLower = IconButton();
    private readonly Button _layerRaise = IconButton();
    private readonly Button _layerToFront = IconButton();

    // The list's thumbnails, one per layer content, made once and dropped with it.
    private readonly Dictionary<SourceImage, Bitmap> _layerThumbnails = [];
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
    private readonly CheckBox _layerItalic = new() { Text = "Italic", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _layerCaps = new() { Text = "ALL CAPS", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox[] _layerAlignment = [.. Enum.GetValues<LayerTextAlignment>().Select(_ => AlignmentButton())];
    private readonly CheckBox _layerBox = new() { Text = "Box", AutoSize = true, Anchor = AnchorStyles.Left };
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
    private readonly CheckBox _layerShadow = new() { Text = "Shadow", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ColorSplitButton _layerShadowColor = new();
    private readonly TrackBar _layerShadowSample = SampleSlider();
    private readonly Label _layerShadowSampleLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ShadowKindStrip _layerShadowKind = new() { Anchor = AnchorStyles.Left };

    // The shadow's size in percent of a line.
    private readonly TrackBar _layerShadowSize = OptionSlider((int)Math.Round(LayerTextStyle.MinShadowSize * 100), (int)Math.Round(LayerTextStyle.MaxShadowSize * 100), 2, controlStep: 1);
    private readonly Label _layerShadowSizeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // The text styles (workfiles/20261029-text-layer-style-presets.md): their strip above the text's settings, scrolled
    // sideways past the width it has; the styles themselves kept in text-styles.json.
    private readonly TextStyles _textStyles = new(TextStyles.DefaultPath);
    private readonly TextStyleStrip _textStyleStrip = new();
    private readonly Panel _textStyleScroll = new() { AutoScroll = true, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly ContextMenuStrip _textStyleMenu = new();

    // Each family of the font drop-down, made once to draw its name in its own face; null when it has no usable style.
    private readonly Dictionary<string, Font?> _layerFontFaces = [];

    // Families drawing symbols rather than letters: their name follows a sample, in the drop-down's own font.
    private static readonly string[] SymbolFonts = ["Wingdings", "Webdings", "Symbol", "Marlett", "MDL2", "Fluent Icons", "Dings", "MT Extra", "Bookshelf"];

    // The colors the three swatches show, so each is drawn again only when its color changes.
    private Color? _layerColorSwatch;
    private Color? _layerOutlineSwatch;
    private Color? _layerBackgroundSwatch;
    private Color? _layerShadowSwatch;

    /// <summary>A button of the layer list: an icon, no text — its name in its tooltip.</summary>
    private static Button IconButton() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(1),
        Padding = new Padding(2),
        BackColor = SystemColors.Control,
        UseVisualStyleBackColor = true,
    };

    /// <summary>
    /// An alignment button of a text layer (workfiles/20261029-text-layer-alignment.md): an icon, pressed while
    /// its alignment is the text's — set by <see cref="UpdateLayerOptions"/>, never by the click itself.
    /// </summary>
    private static CheckBox AlignmentButton() => new()
    {
        Appearance = Appearance.Button,
        AutoCheck = false,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(1),
        Padding = new Padding(2),
    };

    /// <summary>
    /// The Layers options: the layer list over its icon buttons, then a text field and, on two lines, a
    /// text's font, color, outline and background — each color with its eyedropper and sample (RULES.md
    /// § Color Options). The list shows about four layers: the global options toolbar grows to it.
    /// </summary>
    private void BuildLayerOptions()
    {
        this._layerList.Height = this.LogicalToDeviceUnits(4 * 34 + 4);
        var iconLine = OptionLine();
        iconLine.Controls.AddRange([this._layerAddImage, this._layerPaste, this._layerAddText, this._layerEdit, this._layerDelete, this._layerToBack, this._layerLower, this._layerRaise, this._layerToFront]);
        var buttons = OptionLine();
        buttons.FlowDirection = FlowDirection.TopDown;
        buttons.Controls.AddRange([this._layerList, iconLine]);

        // The text styles' strip over the font and outline lines, as wide as they are, scrolled sideways past it.
        this._textStyleScroll.Controls.Add(this._textStyleStrip);
        this._textStyleScroll.Height = this._textStyleStrip.Height + SystemInformation.HorizontalScrollBarHeight;
        this._textStyleScroll.Width = this.LogicalToDeviceUnits(100);
        var fontLine = OptionLine();
        fontLine.Controls.AddRange([this._layerFont, this._layerBold, this._layerItalic, this._layerCaps, .. this._layerAlignment, this._layerBox, this._layerColor,this._layerColorSample, this._layerColorSampleLabel]);
        var outlineLine = OptionLine();
        outlineLine.Controls.AddRange([this._layerOutline, this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, this._layerOutlineWidth, this._layerOutlineWidthLabel]);
        var styleLines = OptionLine();
        styleLines.FlowDirection = FlowDirection.TopDown;
        styleLines.Controls.AddRange([this._textStyleScroll, fontLine, outlineLine]);

        // The box's lines, then the shadow's below them.
        var backgroundLine = OptionLine();
        backgroundLine.Controls.AddRange([this._layerBackground, this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel]);
        var opacityLine = OptionLine();
        opacityLine.Controls.AddRange([this._layerBackgroundOpacity, this._layerBackgroundOpacityLabel]);
        var shadowLine = OptionLine();
        shadowLine.Controls.AddRange([this._layerShadow, this._layerShadowColor, this._layerShadowSample, this._layerShadowSampleLabel]);
        var shadowShapeLine = OptionLine();
        shadowShapeLine.Controls.AddRange([this._layerShadowKind, this._layerShadowSize, this._layerShadowSizeLabel]);
        var backgroundLines = OptionLine();
        backgroundLines.FlowDirection = FlowDirection.TopDown;
        backgroundLines.Controls.AddRange([backgroundLine, opacityLine, shadowLine, shadowShapeLine]);

        this._layerText.Height = this.LogicalToDeviceUnits(4 * 34);
        this._globalOptions[GlobalEffect.Layers].Controls.AddRange([buttons, this._layerText, styleLines, backgroundLines]);

        var fonts = TextRaster.FontFamilies();
        this._layerFont.Items.AddRange([.. fonts]);

        // Each font shown in its own face, in the list and in the closed box (workfiles/20261029-layers.md, Iteration 13).
        this._layerFont.DrawMode = DrawMode.OwnerDrawFixed;
        this._layerFont.ItemHeight = this.LogicalToDeviceUnits(22);
        this._layerFont.MaxDropDownItems = 16;
        this._layerFont.DropDownWidth = this.LogicalToDeviceUnits(280);
        this._layerFont.DrawItem += (_, e) => this.DrawFontItem(e);
        this.Disposed += (_, _) =>
        {
            foreach (var face in this._layerFontFaces.Values)
            {
                face?.Dispose();
            }

            this._layerFontFaces.Clear();
        };

        this._toolTip.SetToolTip(this._layerAddImage, "Add image… — lays images, GIFs or videos over the grid, chosen in the file picker; files dropped onto the Layers tab do the same");
        this._toolTip.SetToolTip(this._layerPaste, "Paste — lays the clipboard's image, files or text over the grid");
        this._toolTip.SetToolTip(this._layerAddText, "Add text — lays a text over the grid, typed in the field beside; Win+. opens the emoji panel");
        this._toolTip.SetToolTip(this._layerDelete, "Delete — removes the selected layer (Delete)");
        this._toolTip.SetToolTip(this._layerToBack, "To back — puts the selected layer under every other");
        this._toolTip.SetToolTip(this._layerLower, "Lower — puts the selected layer one place lower");
        this._toolTip.SetToolTip(this._layerRaise, "Raise — puts the selected layer one place higher");
        this._toolTip.SetToolTip(this._layerToFront, "To front — puts the selected layer over every other");
        this._toolTip.SetToolTip(this._layerText, "The selected text layer's text, several lines allowed; Win+. opens the emoji panel");
        this._toolTip.SetToolTip(this._layerOutline, "Draws an outline around the letters and the emojis, so the text reads on any image");
        this._toolTip.SetToolTip(this._layerOutlineWidth, "Width of the outline, as a share of a line");
        this._toolTip.SetToolTip(this._layerBackground, "Draws a box behind the text");
        this._toolTip.SetToolTip(this._layerBackgroundOpacity, "Opacity of the box behind the text");

        this._layerAddImage.Click += (_, _) => this.PickLayerFiles();
        this._layerPaste.Click += async (_, _) => await this.PasteLayersAsync();
        this._layerAddText.Click += (_, _) => this.AddTextLayer();
        this._layerEdit.Click += (_, _) => this.ToggleLayerEdit();
        this._layerDelete.Click += (_, _) => this._preview.RemoveSelectedLayer();
        this._layerList.LayerPicked += (_, index) => this._preview.SelectLayer(index);
        this._layerList.VisibilityClicked += (_, index) => this.ToggleLayerVisibility(index);
        this._preview.LayerEditChanged += (_, _) =>
        {
            this.UpdateButtons();
            if (this._preview.EditingLayer)
            {
                this.ShowStatus("Editing the layer: its effects work as on a cell. Escape, Done or a double-click outside it gives the grid back.");
            }
        };
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
        this._layerItalic.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Italic = this._layerItalic.Checked });
        this._layerCaps.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Caps = this._layerCaps.Checked });
        foreach (var alignment in Enum.GetValues<LayerTextAlignment>())
        {
            var button = this._layerAlignment[(int)alignment];
            button.Click += (_, _) => this.ChangeLayerStyle(style => style with { Alignment = alignment });
            this._toolTip.SetToolTip(button, alignment switch
            {
                LayerTextAlignment.Left => "Align left — the lines start at the left edge of the widest one",
                LayerTextAlignment.Center => "Center — the lines centered on the widest one",
                LayerTextAlignment.Right => "Align right — the lines end at the right edge of the widest one",
                _ => "Justify — the lines of a paragraph stretched by their spaces to the widest one, or to the box; its last line, and a line of one word, aligned left; a blank line ends a paragraph, in a box each line break",
            });
        }

        this._layerBox.CheckedChanged += (_, _) => this.ChangeLayerBox(this._layerBox.Checked);
        this._toolTip.SetToolTip(this._layerBox, "Box — the text wraps in a box half the grid wide, its width dragged by the handles on its sides; unchecked, the size fits the text");
        this._preview.RenderTextLayer = layer => this.RenderTextLayer(layer.Text!, layer.Style!, layer.Look, layer.Place, layer.TextBox);

        this._layerShadow.CheckedChanged += (_, _) => this.ChangeLayerStyle(style => style with { Shadow = this._layerShadow.Checked });
        this._layerShadowKind.Picked += (_, kind) => this.ChangeLayerStyle(style => style with { Shadow = true, ShadowKind = kind });
        this._layerShadowSize.ValueChanged += (_, _) =>
        {
            this._layerShadowSizeLabel.Text = ShadowSizeText(this._layerShadowSize.Value);
            this.ChangeLayerStyle(style => style with { Shadow = true, ShadowSize = this._layerShadowSize.Value / 100.0 });
        };
        this._toolTip.SetToolTip(this._layerItalic, "Draws the text slanted: the font's italic, else its letters leaned");
        this._toolTip.SetToolTip(this._layerCaps, "Draws the text in capitals; the field keeps the text as typed");
        this._toolTip.SetToolTip(this._layerShadow, "Draws a soft shadow under the letters and their outline");
        this._toolTip.SetToolTip(this._layerShadowSize, "Size of the shadow, as a share of a line: how far a halo reaches, how far a drop falls");

        // The text styles: a click applies one or saves a new one, a right click on one of the user's manages it.
        if (!this._textStyles.Load())
        {
            this.ShowStatus($"The text styles could not be read from {TextStyles.FileName}: only the built-in ones are offered.", error: true);
        }

        this._textStyleStrip.SetStyles(this._textStyles.All);
        this._textStyleStrip.Picked += (_, choice) =>
        {
            if (choice is ApplyStyle apply)
            {
                this.ApplyTextStyle(apply.Style);
            }
            else
            {
                this.SaveTextStyle();
            }
        };
        this._textStyleStrip.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && this._textStyleStrip.Enabled
                && this._textStyleStrip.TryValueAt(e.Location, out var choice) && choice is ApplyStyle { Style.BuiltIn: false } apply)
            {
                this.ShowTextStyleMenu(apply.Style, e.Location);
            }
        };

        // Each color: the dialog's button and the eyedropper; the outline's and the box's turn them on.
        (ColorSplitButton Button, TrackBar Sample, Label Label, string Name, Func<LayerTextStyle, Color> Get, Func<LayerTextStyle, Color, LayerTextStyle> Set)[] colors =
        [
            (this._layerColor, this._layerColorSample, this._layerColorSampleLabel, "the text's", style => style.Color, (style, color) => style with { Color = color }),
            (this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, "the outline's", style => style.OutlineColor, (style, color) => style with { Outline = true, OutlineColor = color }),
            (this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel, "the box's", style => style.BackgroundColor, (style, color) => style with { Background = true, BackgroundColor = color }),
            (this._layerShadowColor, this._layerShadowSample, this._layerShadowSampleLabel, "the shadow's", style => style.ShadowColor, (style, color) => style with { Shadow = true, ShadowColor = color }),
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
        this._layerShadowSizeLabel.Text = ShadowSizeText(this._layerShadowSize.Value);
        this._layerShadowSizeLabel.MinimumSize = new Size(TextRenderer.MeasureText(ShadowSizeText(30), this._layerShadowSizeLabel.Font).Width, 0);

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

    private static string ShadowSizeText(int percent) => $"Size: {percent} %";

    /// <summary>
    /// A row of the font drop-down: the family's name in its own face; for a family of symbols, a sample of
    /// them, then its name in the drop-down's font so it can be read.
    /// </summary>
    private void DrawFontItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= this._layerFont.Items.Count || this._layerFont.Items[e.Index] is not string family)
        {
            return;
        }

        var color = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText
            : this._layerFont.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        var bounds = Rectangle.Inflate(e.Bounds, -this.LogicalToDeviceUnits(3), 0);
        var face = this.FontFace(family, e.Font ?? this._layerFont.Font);
        if (face is null)
        {
            TextRenderer.DrawText(e.Graphics, family, this._layerFont.Font, bounds, color, flags);
            return;
        }

        if (SymbolFonts.Any(symbol => family.Contains(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            const string Sample = "AaBb";
            int sample = TextRenderer.MeasureText(e.Graphics, Sample, face, bounds.Size, flags).Width;
            TextRenderer.DrawText(e.Graphics, Sample, face, bounds, color, flags);
            bounds = Rectangle.FromLTRB(bounds.Left + sample + this.LogicalToDeviceUnits(4), bounds.Top, bounds.Right, bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, family, this._layerFont.Font, bounds, color, flags);
            return;
        }

        TextRenderer.DrawText(e.Graphics, family, face, bounds, color, flags);
    }

    /// <summary>The family's face at the drop-down's size, made once: regular, else bold, else italic; <c>null</c> without any.</summary>
    private Font? FontFace(string family, Font reference)
    {
        if (this._layerFontFaces.TryGetValue(family, out var face))
        {
            return face;
        }

        face = null;
        try
        {
            using var fontFamily = new FontFamily(family);
            foreach (var style in new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic })
            {
                if (fontFamily.IsStyleAvailable(style))
                {
                    face = new Font(fontFamily, reference.SizeInPoints * 1.15f, style, GraphicsUnit.Point);
                    break;
                }
            }
        }
        catch (ArgumentException)
        {
            // A family GDI+ cannot open: its name is drawn in the drop-down's font.
        }

        this._layerFontFaces[family] = face;
        return face;
    }

    /// <summary>The eyedropper icons of the three colors and the list's buttons, drawn at the monitor's DPI; the ones replaced disposed.</summary>
    private void UpdateLayerIcons(int size)
    {
        foreach (var (button, action) in new (Button, EffectIcons.LayerAction)[]
        {
            (this._layerAddImage, EffectIcons.LayerAction.AddImage), (this._layerPaste, EffectIcons.LayerAction.Paste),
            (this._layerAddText, EffectIcons.LayerAction.AddText), (this._layerDelete, EffectIcons.LayerAction.Delete),
            (this._layerToBack, EffectIcons.LayerAction.ToBack), (this._layerLower, EffectIcons.LayerAction.Lower),
            (this._layerRaise, EffectIcons.LayerAction.Raise), (this._layerToFront, EffectIcons.LayerAction.ToFront),
        })
        {
            var previous = button.Image;
            button.Image = EffectIcons.Layer(size, action);
            previous?.Dispose();
        }

        this._layerIconSize = size;
        this._layerEditShowsDone = null;

        foreach (var alignment in Enum.GetValues<LayerTextAlignment>())
        {
            var button = this._layerAlignment[(int)alignment];
            var previous = button.Image;
            button.Image = EffectIcons.Alignment(size, alignment);
            previous?.Dispose();
        }

        foreach (var button in new[] { this._layerColor, this._layerOutlineColor, this._layerBackgroundColor, this._layerShadowColor })
        {
            var previous = button.Eyedropper.Image;
            button.Eyedropper.Image = EffectIcons.Eyedropper(size);
            previous?.Dispose();
        }

        this._layerColorSwatch = this._layerOutlineSwatch = this._layerBackgroundSwatch = this._layerShadowSwatch = null;
        this.UpdateLayerOptions();
    }

    // The size the list's icons were drawn at, and whether the Edit button shows Done — drawn again when it changes.
    private int _layerIconSize = 16;
    private bool? _layerEditShowsDone;

    /// <summary>
    /// Shows the Layers options: the list of the layers, the selected one selected; the adding buttons while
    /// the grid holds an image, the others while they would act on the selected layer; Edit or Done; a
    /// text's settings while a text layer is selected outside its edit view.
    /// </summary>
    private void UpdateLayerOptions()
    {
        bool enabled = !this.IsExporting;
        bool editing = this._preview.EditingLayer;
        var listed = this._preview.ListedLayer;
        var layer = this._preview.SelectedLayer;
        int index = this._preview.SelectedLayerIndex;
        int count = this._preview.Layers.Count;
        this.UpdateLayerList();
        this._layerAddImage.Enabled = this._layerPaste.Enabled = this._layerAddText.Enabled = enabled;
        this._layerEdit.Enabled = enabled && (editing || layer is { Visible: true });
        this._layerDelete.Enabled = enabled && listed is not null;
        this._layerToBack.Enabled = this._layerLower.Enabled = enabled && listed is not null && index > 0;
        this._layerRaise.Enabled = this._layerToFront.Enabled = enabled && listed is not null && index < count - 1;
        if (this._layerEditShowsDone != editing)
        {
            this._layerEditShowsDone = editing;
            var previous = this._layerEdit.Image;
            this._layerEdit.Image = EffectIcons.Layer(this._layerIconSize, editing ? EffectIcons.LayerAction.Done : EffectIcons.LayerAction.Edit);
            previous?.Dispose();
            this._toolTip.SetToolTip(
                this._layerEdit,
                editing ? "Done — gives the grid back (Escape)" : "Edit — shows the selected layer alone, large, its effects working as on a cell (double-click the layer)");
        }

        bool text = enabled && layer is { IsText: true };
        foreach (var control in new Control[]
        {
            this._layerText, this._layerFont, this._layerBold, this._layerItalic, this._layerCaps, this._layerColor, this._layerColorSample, this._layerColorSampleLabel,
            this._layerOutline, this._layerOutlineColor, this._layerOutlineSample, this._layerOutlineSampleLabel, this._layerOutlineWidth, this._layerOutlineWidthLabel,
            this._layerBackground, this._layerBackgroundColor, this._layerBackgroundSample, this._layerBackgroundSampleLabel, this._layerBackgroundOpacity, this._layerBackgroundOpacityLabel,
            this._layerShadow, this._layerShadowColor, this._layerShadowSample, this._layerShadowSampleLabel, this._layerShadowKind, this._layerShadowSize, this._layerShadowSizeLabel,
            this._textStyleStrip,
        }.Concat(this._layerAlignment).Append(this._layerBox))
        {
            control.Enabled = text;
        }

        // The text layer's own settings shown — a new text's style while none is selected; the field left
        // alone while it already holds the text, so its caret stays where the user types.
        var style = layer is { IsText: true } ? layer.Style! : this._textStyles.NewTextStyle;
        this._textStyleStrip.Current = layer is { IsText: true } ? layer.Style : null;
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
        this._layerItalic.Checked = style.Italic;
        this._layerCaps.Checked = style.Caps;
        foreach (var alignment in Enum.GetValues<LayerTextAlignment>())
        {
            this._layerAlignment[(int)alignment].Checked = style.Alignment == alignment;
        }

        this._layerBox.Checked = layer is { IsText: true, TextBox: not null };

        this._layerShadow.Checked = style.Shadow;
        this._layerShadowKind.Selected = style.ShadowKind;
        this._layerShadowSize.Value = Math.Clamp((int)Math.Round(style.ShadowSize * 100), this._layerShadowSize.Minimum, this._layerShadowSize.Maximum);
        this._layerOutline.Checked = style.Outline;
        this._layerOutlineWidth.Value = Math.Clamp((int)Math.Round(style.OutlineWidth * 100), this._layerOutlineWidth.Minimum, this._layerOutlineWidth.Maximum);
        this._layerBackground.Checked = style.Background;
        this._layerBackgroundOpacity.Value = (int)Math.Round(style.BackgroundOpacity * 100);
        this._syncingEffects = syncing;
        this._layerOutlineWidthLabel.Text = OutlineWidthText(this._layerOutlineWidth.Value);
        this._layerBackgroundOpacityLabel.Text = OpacityText(this._layerBackgroundOpacity.Value);
        this._layerShadowSizeLabel.Text = ShadowSizeText(this._layerShadowSize.Value);
        this.ShowLayerSwatch(this._layerShadowColor, ref this._layerShadowSwatch, style.ShadowColor);
        this.ShowLayerSwatch(this._layerColor, ref this._layerColorSwatch, style.Color);
        this.ShowLayerSwatch(this._layerOutlineColor, ref this._layerOutlineSwatch, style.OutlineColor);
        this.ShowLayerSwatch(this._layerBackgroundColor, ref this._layerBackgroundSwatch, style.BackgroundColor);
    }

    /// <summary>The layer list's rows, their thumbnails made for the new contents and dropped for the gone ones.</summary>
    private void UpdateLayerList()
    {
        var layers = this._preview.Layers.Items;
        foreach (var gone in this._layerThumbnails.Keys.Where(image => !layers.Any(l => l.Image == image)).ToList())
        {
            this._layerThumbnails[gone].Dispose();
            this._layerThumbnails.Remove(gone);
        }

        var rows = new List<LayerList.Row>(layers.Count);
        foreach (var layer in layers)
        {
            if (!this._layerThumbnails.TryGetValue(layer.Image, out var thumbnail) && !layer.Image.IsDisposed)
            {
                thumbnail = Thumbnail(layer.Image.Bitmap);
                this._layerThumbnails[layer.Image] = thumbnail;
            }

            rows.Add(new LayerList.Row(LayerName(layer), thumbnail, layer.Visible));
        }

        this._layerList.SetRows(rows, this._preview.SelectedLayerIndex);
    }

    /// <summary>A layer's name in the list: its file's, its text's first line, else what it was pasted as.</summary>
    private static string LayerName(Layer layer) =>
        layer.Text is { } text ? (text.Split('\n')[0] is { Length: > 0 } line ? line : "Text")
        : layer.Image.FilePath is { } path ? Path.GetFileName(path)
        : "Pasted image";

    /// <summary>A small copy of <paramref name="bitmap"/>, for the list: its longer side 80 px.</summary>
    private static Bitmap Thumbnail(Bitmap bitmap)
    {
        double scale = Math.Min(1, 80.0 / Math.Max(bitmap.Width, bitmap.Height));
        var size = new Size(Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale)));
        var thumbnail = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(thumbnail);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(bitmap, new Rectangle(Point.Empty, size));
        return thumbnail;
    }

    /// <summary>A layer's visibility checkbox in the list: shown or hidden, its settings kept; the Layers turned on (RULES.md).</summary>
    private void ToggleLayerVisibility(int index)
    {
        if (this.IsExporting || index < 0 || index >= this._preview.Layers.Count)
        {
            return;
        }

        var layer = this._preview.Layers.Items[index];
        this.TurnLayersOn();
        this._preview.SetLayers(this._preview.Layers.With(index, layer with { Visible = !layer.Visible }));
    }

    /// <summary>The Edit button: the selected layer's edit view opened, or closed when it shows (Done).</summary>
    private void ToggleLayerEdit()
    {
        if (this._preview.EditingLayer)
        {
            this._preview.EndLayerEdit();
        }
        else if (!this.IsExporting)
        {
            this.TurnLayersOn();
            this._preview.BeginLayerEdit();
        }
    }

    /// <summary>Whether a drop at <paramref name="screen"/> falls on the Layers tab, or on the Global options while it is selected.</summary>
    private bool DroppedOnLayers(Point screen)
    {
        var onTabs = this._globalTabs.PointToClient(screen);
        if (this._globalTabs.ClientRectangle.Contains(onTabs))
        {
            return this._globalTabs.EffectAt(onTabs) == GlobalEffect.Layers;
        }

        return this._selectedGlobalEffect == GlobalEffect.Layers
            && this._globalOptionsRow.RectangleToScreen(this._globalOptionsRow.ClientRectangle).Contains(screen);
    }

    /// <summary>Files dropped onto the Layers tab or its options: its tab selected, they are laid over the grid.</summary>
    private async Task DropLayerFilesAsync(string[] paths)
    {
        this.SelectGlobalEffect(GlobalEffect.Layers);
        await this.AddLayerFilesAsync(paths);
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
        if (this.IsExporting)
        {
            return;
        }

        var center = at ?? new PointF(0.5f, 0.5f);
        var place = new LayerPlacement(center.X, center.Y, LayerPlacement.TextSize, 0);
        if (this.RenderTextLayer(text, this._textStyles.NewTextStyle, Layer.DefaultLook, place) is { } layer)
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
    private Layer? RenderTextLayer(string text, LayerTextStyle style, ImageLook look, LayerPlacement place, double? box = null)
    {
        try
        {
            var (bitmap, line) = TextRaster.Render(text, style, box);
            var image = new SourceImage(bitmap, filePath: null) { Look = look };
            return new Layer(image, look, place with { LinePixels = line }, text, style) { TextBox = box };
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

    /// <summary>
    /// The Box checkbox: the selected text layer wrapped in a box half the grid wide, its center kept — or
    /// back to the automatic size (workfiles/20261029-text-layer-alignment.md § Size Mode).
    /// </summary>
    private void ChangeLayerBox(bool box)
    {
        if (this._syncingEffects || this.IsExporting || this._preview.SelectedLayer is not { IsText: true } layer || box == layer.TextBox is not null)
        {
            return;
        }

        double ratio = this._preview.CanvasRatio;
        double half = Math.Clamp(0.5 * ratio / (Math.Min(ratio, 1) * layer.Place.Size), Layer.MinTextBox, Layer.MaxTextBox);
        this.ReplaceTextLayer(layer, layer.Text!, layer.Style!, box ? half : null);
    }

    private void ReplaceTextLayer(Layer layer, string text, LayerTextStyle style) => this.ReplaceTextLayer(layer, text, style, layer.TextBox);

    private void ReplaceTextLayer(Layer layer, string text, LayerTextStyle style, double? box)
    {
        if (this.RenderTextLayer(text, style, layer.Look, layer.Place, box) is { } drawn)
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
        if (this.IsExporting)
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
        if (this.RefuseWhileExporting())
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

    /// <summary>
    /// A style's thumbnail clicked: the selected text layer drawn in it — one step of the undo history, like any
    /// setting — and the next new text given it (workfiles/20261029-text-layer-style-presets.md § Applying a Style).
    /// </summary>
    private void ApplyTextStyle(NamedTextStyle style)
    {
        if (this.IsExporting || this._preview.SelectedLayer is not { IsText: true })
        {
            return;
        }

        this.ChangeLayerStyle(_ => style.Style);
        this._textStyles.Applied(style);
        this.SaveTextStyles(null);
    }

    /// <summary>Save style…: the selected text layer's style kept under a name, one of the user's of that name replaced after a confirmation.</summary>
    private void SaveTextStyle()
    {
        if (this.IsExporting || this._preview.SelectedLayer is not { IsText: true } layer
            || this.AskTextStyleName("Save style", "The style's name:", "", except: null) is not { } name)
        {
            return;
        }

        if (this._textStyles.Find(name) is { } existing)
        {
            if (MessageBox.Show(
                    this,
                    $"Replace the style “{existing.Name}” with the selected text's style?",
                    "Save style",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            name = existing.Name;
        }

        this._textStyles.Put(name, layer.Style!);
        this.SaveTextStyles($"Style “{name}” saved.");
    }

    /// <summary>The context menu of one of the user's styles: rename it, replace it with the selected text's style, delete it.</summary>
    private void ShowTextStyleMenu(NamedTextStyle style, Point location)
    {
        foreach (var item in this._textStyleMenu.Items.Cast<ToolStripItem>().ToList())
        {
            item.Dispose();
        }

        this._textStyleMenu.Items.Clear();
        this._textStyleMenu.Items.Add("Rename…", null, (_, _) => this.RenameTextStyle(style));
        var replace = new ToolStripMenuItem("Replace with current style", null, (_, _) =>
        {
            if (this._preview.SelectedLayer is { IsText: true } layer)
            {
                this._textStyles.Put(style.Name, layer.Style!);
                this.SaveTextStyles($"Style “{style.Name}” replaced.");
            }
        })
        {
            Enabled = this._preview.SelectedLayer is { IsText: true },
        };
        this._textStyleMenu.Items.Add(replace);

        // A decision taking something away goes through a submenu (RULES.md § People).
        var delete = new ToolStripMenuItem("Delete");
        delete.DropDownItems.Add($"Delete {style.Name}", null, (_, _) =>
        {
            this._textStyles.Delete(style);
            this.SaveTextStyles($"Style “{style.Name}” deleted.");
        });
        this._textStyleMenu.Items.Add(delete);
        this._textStyleMenu.Show(this._textStyleStrip, location);
    }

    private void RenameTextStyle(NamedTextStyle style)
    {
        if (this.AskTextStyleName("Rename style", "The style's new name:", style.Name, except: style) is { } name && name != style.Name)
        {
            this._textStyles.Rename(style, name);
            this.SaveTextStyles($"Style “{style.Name}” renamed “{name}”.");
        }
    }

    /// <summary>
    /// A style's name asked until it is given or cancelled: not empty, never a built-in style's; when renaming
    /// <paramref name="except"/>, never another style's either. <c>null</c> when cancelled.
    /// </summary>
    private string? AskTextStyleName(string title, string prompt, string value, NamedTextStyle? except)
    {
        while (true)
        {
            using var dialog = new NameDialog(title, prompt, value);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return null;
            }

            value = dialog.Value;
            string? refused = value.Length == 0 ? "A style needs a name."
                : TextStyles.IsReserved(value) ? $"“{value}” is a built-in style's name: choose another one."
                : except is not null && this._textStyles.Find(value) is { } other && other != except ? $"A style is already named “{other.Name}”: choose another name."
                : null;
            if (refused is null)
            {
                return value;
            }

            MessageBox.Show(this, refused, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>The text styles written to their file and shown again in the strip; <paramref name="done"/> in the status line, or why they could not be written.</summary>
    private void SaveTextStyles(string? done)
    {
        this._textStyleStrip.SetStyles(this._textStyles.All);
        try
        {
            this._textStyles.Save();
            if (done is not null)
            {
                this.ShowStatus(done);
            }
        }
        catch (Exception ex) when (Explorer.FileIndex.IsFileError(ex))
        {
            this.ShowStatus($"The text styles could not be saved to {TextStyles.FileName}: {ErrorLog.Describe(ex)}", error: true);
        }
    }
}
