using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The shared fill's options (workfiles/20260926-forced-background.md): the Grid background global effect —
/// one fill under the whole canvas, turning off the cells' Background while it is on — and the gradient
/// options it shares with the cell's Background.
/// </summary>
internal sealed partial class MainForm
{
    // The Grid background's options: its first color, its opacity, then the shared fill's.
    private readonly ColorSplitButton _gridBackgroundColor = new();
    private readonly PictureBox _gridBackgroundOpacityIcon = OpacityIcon();
    private readonly StepSlider _gridBackgroundOpacity = OptionSlider(0, 100, 10);
    private readonly Label _gridBackgroundOpacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private FillControls? _gridFill;

    // The cell Background's gradient options, beside its color.
    private FillControls? _backgroundStyle;

    // The Grid background's settings, kept while it is off; back to their initial state, off, with its Resets and Clear all.
    private GridBackground _gridBackground = GridBackground.Initial;
    private bool _gridBackgroundOn;

    /// <summary>The Grid background, <c>null</c> while it is off.</summary>
    private GridBackground? ActiveGridBackground => this._gridBackgroundOn ? this._gridBackground : null;

    /// <summary>Whether the Grid background is in its initial state: off, its color following the first cell, solid.</summary>
    private bool GridBackgroundInitial => !this._gridBackgroundOn && this._gridBackground == GridBackground.Initial;

    /// <summary>The Grid background's first color as it stands: the first cell's automatic one while it follows it.</summary>
    private Color GridBackgroundFirst => this._gridBackground.FirstColor(this._preview.GridAutomaticColors.First);

    /// <summary>The Grid background's options in its row of the global options toolbar, and their events.</summary>
    private void BuildGridBackgroundOptions()
    {
        this._gridFill = new FillControls(offersAuto: true, this.GridSecondSwatch);
        var firstLine = OptionLine();
        firstLine.Controls.AddRange([this._gridBackgroundColor, this._gridBackgroundOpacityIcon, this._gridBackgroundOpacity, this._gridBackgroundOpacityLabel, .. this._gridFill.AngleControls]);
        var lines = OptionLine();
        lines.FlowDirection = FlowDirection.TopDown;
        lines.Controls.AddRange([firstLine, this._gridFill.SecondLine]);
        this._globalOptions[GlobalEffect.GridBackground].Controls.AddRange([this._gridFill.Kind, lines]);
        this.BuildBackgroundImageOptions();

        this._gridBackgroundColor.Describe(this._toolTip, "the grid background's color", ". Until the Grid background is first turned on, it follows the first cell's automatic color");
        this._gridBackgroundColor.Dialog.Click += (_, _) => this.PickColor(this.GridBackgroundRoute());
        this._gridBackgroundColor.Eyedropper.Click += (_, _) => this.ArmEyedropper(this._gridBackgroundColor, this.GridBackgroundRoute());
        this._gridBackgroundOpacity.ValueChanged += (_, _) =>
        {
            this._gridBackgroundOpacityLabel.Text = OpacityText(this._gridBackgroundOpacity.Value);
            this.ChangeGridBackground(background => background.WithOpacity(this._gridBackgroundOpacity.Value / 100.0));
        };
        this._gridFill.Describe(this._toolTip, "the grid");
        this._gridFill.Changed += change => this.ChangeGridBackground(background => background.WithStyle(change(background.Style)));
        this._gridFill.SecondButton.Dialog.Click += (_, _) => this.PickColor(this.GridSecondRoute());
        this._gridFill.SecondButton.Eyedropper.Click += (_, _) => this.ArmEyedropper(this._gridFill.SecondButton, this.GridSecondRoute());
    }

    /// <summary>
    /// The cell Background's options, one feature per line (workfiles/20261104-options-toolbar-layout.md): the fill
    /// modes, the gradient's kinds and angle, the color, the gradient's second color, the extended edges.
    /// </summary>
    private void BuildBackgroundOptions(FeatureLinesPanel lines)
    {
        this._backgroundStyle = new FillControls(offersAuto: false, this.BackgroundSecondSwatch);
        var style = this._backgroundStyle;
        lines.AddLine("Fill", this._backgroundFill);
        lines.AddLine("Gradient", style.Kind, new SliderBox(style.Angle, style.AngleLabel));
        lines.AddLine(this._backgroundAutomatic, this._backgroundColor, new SliderBox(this._backgroundOpacity, this._backgroundOpacityLabel, this._backgroundOpacityIcon));
        lines.AddLine(style.SecondCaption, style.Second, style.SecondButton, new SliderBox(style.SecondOpacity, style.SecondOpacityLabel, style.SecondOpacityIcon));
        lines.AddLine(
            "Edges",
            new SliderBox(this._backgroundEdgeOpacity, this._backgroundEdgeOpacityLabel),
            new SliderBox(this._backgroundBlend, this._backgroundBlendLabel),
            this._backgroundSoften);
        this._backgroundStyle.Describe(this._toolTip, "the cell");
        this._backgroundStyle.Changed += change =>
            this.ChangeLook(ImageEffect.Background, look => look.WithBackground(look.Background!.WithStyle(change(look.Background.Style))));
        this._backgroundStyle.SecondButton.Dialog.Click += (_, _) => this.PickColor(this.BackgroundSecondRoute());
        this._backgroundStyle.SecondButton.Eyedropper.Click += (_, _) => this.ArmEyedropper(this._backgroundStyle.SecondButton, this.BackgroundSecondRoute());
    }

    /// <summary>The icons of the shared fill's options at the monitor's DPI: the opacity's and the eyedroppers'.</summary>
    private void UpdateFillIcons(int size, Bitmap eyedropper)
    {
        foreach (var icon in new[] { this._gridBackgroundOpacityIcon, this._gridFill?.SecondOpacityIcon, this._backgroundStyle?.SecondOpacityIcon })
        {
            if (icon is not null)
            {
                var previous = icon.Image;
                icon.Image = EffectIcons.Opacity(size);
                icon.Size = new Size(size, size);
                previous?.Dispose();
            }
        }

        foreach (var button in new[] { this._gridBackgroundColor, this._gridFill?.SecondButton, this._backgroundStyle?.SecondButton })
        {
            button?.SetIcon(eyedropper);
        }

        using var opacity = EffectIcons.Opacity(size);
        this.UpdateBackgroundImageIcons(size, opacity);
    }

    /// <summary>The Grid background's checkbox: on or off, its settings kept.</summary>
    private void ToggleGridBackground()
    {
        if (this.IsExporting)
        {
            return;
        }

        this.SetGridBackground(this._gridBackground, !this._gridBackgroundOn);
    }

    /// <summary>
    /// An option of the Grid background changed: applied to its settings, turning it on from the settings it
    /// kept (RULES.md), unless the row is being synced.
    /// </summary>
    private void ChangeGridBackground(Func<GridBackground, GridBackground> change)
    {
        if (this._syncingEffects || this.IsExporting)
        {
            return;
        }

        this.SetGridBackground(change(this.FrozenGridBackground()), on: true);
    }

    /// <summary>The settings as turning on takes them: the color followed so far, the first cell's, made its own.</summary>
    private GridBackground FrozenGridBackground() =>
        this._gridBackgroundOn ? this._gridBackground : this._gridBackground.Frozen(this.GridBackgroundFirst);

    /// <summary>
    /// The Grid background set, on or off: turned on, it takes the color it followed as its own and turns off
    /// every cell's Background; turned off, it turns back on the ones it turned off (workfiles/20260926-forced-background.md).
    /// </summary>
    private void SetGridBackground(GridBackground background, bool on)
    {
        var gone = this._gridBackground.Image?.Image;
        this._gridBackground = on && !this._gridBackgroundOn ? background.Frozen(this.GridBackgroundFirst) : background;
        this._gridBackgroundOn = on;
        this.ApplyGridBackground();
        this.ReleaseGoneBackgroundImage(gone);
    }

    /// <summary>
    /// The Grid background's image <paramref name="gone"/> let go once the settings no longer hold it — replaced,
    /// removed, reset, undone —, through the preview's <see cref="GridPreview.ReleaseImage"/>: the undo history
    /// keeps it while a step holds it.
    /// </summary>
    private void ReleaseGoneBackgroundImage(SourceImage? gone)
    {
        if (gone is not null && gone != this._gridBackground.Image?.Image)
        {
            this._preview.ReleaseImage(gone);
        }
    }

    /// <summary>The Grid background as it stands, to the preview — the cells' Background with it — then to the rows and the output buttons.</summary>
    private void ApplyGridBackground()
    {
        this._preview.GridBackground = this.ActiveGridBackground;
        if (this._preview.BackgroundsForcedOff != this._gridBackgroundOn)
        {
            this._preview.ForceBackgrounds(this._gridBackgroundOn);
        }

        this.UpdateButtons();
    }

    /// <summary>The Grid background's options, from its settings: the kept ones while it is off.</summary>
    private void ShowGridBackgroundOptions()
    {
        if (this._gridFill is null)
        {
            return;
        }

        bool syncing = this._syncingEffects;
        this._syncingEffects = true;
        this._gridBackgroundOpacity.Value = (int)Math.Round(this._gridBackground.Opacity * 100);
        this._gridFill.Show(this._gridBackground.Style);
        this._syncingEffects = syncing;
        this._gridBackgroundOpacityLabel.Text = OpacityText(this._gridBackgroundOpacity.Value);
        this.ShowBackgroundImageOptions();

        // The swatches only while its tab shows: the automatic colors are read from the cells.
        if (this._selectedGlobalEffect == GlobalEffect.GridBackground)
        {
            var first = this.GridBackgroundFirst;
            this.SetSwatch(this._gridBackgroundColor.Dialog, first);
            this.SetSwatch(this._gridFill.SecondButton.Dialog, this._gridBackground.Style.SecondFor(first, this._preview.GridAutomaticColors.Last));
            this._gridFill.Second.Invalidate();
        }
    }

    /// <summary>The cell Background's gradient options, from its settings, and its second color's swatch while its tab shows.</summary>
    private void ShowBackgroundStyle(BackgroundEffect background)
    {
        if (this._backgroundStyle is null)
        {
            return;
        }

        this._backgroundStyle.Show(background.Style);
        if (this._selectedEffect == ImageEffect.Background)
        {
            this.SetSwatch(this._backgroundStyle.SecondButton.Dialog, background.Style.SecondFor(this.BackgroundShown(background), null));
            this._backgroundStyle.Second.Invalidate();
        }
    }

    /// <summary>The swatch of a Grid background's second color choice: the color it gives now.</summary>
    private Color GridSecondSwatch(SecondColor choice) =>
        this._gridBackground.Style.WithSecond(choice).SecondFor(this.GridBackgroundFirst, this._preview.GridAutomaticColors.Last);

    /// <summary>The swatch of a cell Background's second color choice: the color it gives the selected cell now.</summary>
    private Color BackgroundSecondSwatch(SecondColor choice) =>
        this._preview.SelectedImage?.Look.TurnOn(ImageEffect.Background).Background is { } background
            ? background.Style.WithSecond(choice).SecondFor(this.BackgroundShown(background), null)
            : FillStyle.Cell.WithSecond(choice).SecondFor(Color.White, null);

    /// <summary>
    /// The Grid background's color routes: the fill follows the color shown, turned on meanwhile — the cells'
    /// Background with it —; the color taken turns it on, a cancel gives it back as it was.
    /// </summary>
    private ColorRoute? GridBackgroundRoute()
    {
        if (this.IsExporting)
        {
            return null;
        }

        var (background, on) = (this._gridBackground, this._gridBackgroundOn);
        return new ColorRoute(
            this.GridBackgroundFirst,
            color => this.SetGridBackground(background.WithColor(color), on: true),
            () => this.SetGridBackground(background, on),
            color => this.SetGridBackground(background.WithColor(color), on: true));
    }

    /// <summary>The Grid background's second color routes, as its first color's: the color taken is the Chosen one.</summary>
    private ColorRoute? GridSecondRoute()
    {
        if (this.IsExporting)
        {
            return null;
        }

        var (background, on) = (this._gridBackground, this._gridBackgroundOn);
        var frozen = this.FrozenGridBackground();
        return new ColorRoute(
            frozen.Style.SecondFor(frozen.FirstColor(null), this._preview.GridAutomaticColors.Last),
            color => this.SetGridBackground(frozen.WithStyle(frozen.Style.WithSecondChosen(color)), on: true),
            () => this.SetGridBackground(background, on),
            color => this.SetGridBackground(frozen.WithStyle(frozen.Style.WithSecondChosen(color)), on: true));
    }

    /// <summary>
    /// The cell Background's second color routes: the selected cell follows the color shown, its Background on
    /// meanwhile; the color taken is the Chosen one, a cancel gives the cell its look back.
    /// </summary>
    private ColorRoute? BackgroundSecondRoute()
    {
        if (this.IsExporting || this._preview.SelectedImage is not { } image)
        {
            return null;
        }

        var original = image.Look;
        var on = original.TurnOn(ImageEffect.Background);
        var background = on.Background!;
        return new ColorRoute(
            background.Style.SecondFor(this.BackgroundShown(background), null),
            color => this._preview.SetSelectedLook(on.WithBackground(background.WithStyle(background.Style.WithSecondChosen(color)))),
            () =>
            {
                if (this._preview.SelectedImage == image)
                {
                    this._preview.SetSelectedLook(original);
                }
            },
            color => this.ChangeLook(ImageEffect.Background, look => look.WithBackground(look.Background!.WithStyle(look.Background.Style.WithSecondChosen(color)))));
    }

    private static string GradientAngleText(int degrees) => $"Angle: {degrees}°";

    /// <summary>
    /// The shared fill's options, one set per user — the cell's Background, the Grid background: its kinds as
    /// thumbnails, the linear gradient's angle, the second color's source, its eyedropper + Color… and its opacity.
    /// </summary>
    private sealed class FillControls
    {
        private readonly SecondColor[] _choices;
        private readonly Func<SecondColor, Color> _swatchOf;

        public FillControls(bool offersAuto, Func<SecondColor, Color> swatchOf)
        {
            this._choices = offersAuto ? Enum.GetValues<SecondColor>() : [.. Enum.GetValues<SecondColor>().Where(c => c != Composition.SecondColor.Auto)];
            this._swatchOf = swatchOf;
            this.Angle.Width = 120;
            this.SecondOpacity.Width = 110;
            this.Second.Items.AddRange([.. this._choices.Select(ChoiceText)]);
            this.Second.DropDownWidth = 150;
            this.Second.DrawItem += (_, e) => this.DrawChoice(e);
            this.AngleControls = [this.Angle, this.AngleLabel];
            this.SecondControls = [this.SecondCaption, this.Second, this.SecondButton, this.SecondOpacityIcon, this.SecondOpacity, this.SecondOpacityLabel];
            this.SecondLine = OptionLine();
            this.SecondLine.Controls.AddRange(this.SecondControls);
            this.Kind.Picked += (_, kind) => this.Changed?.Invoke(style => style.WithKind(kind));
            this.Angle.ValueChanged += (_, _) =>
            {
                this.AngleLabel.Text = GradientAngleText(this.Angle.Value);
                this.Changed?.Invoke(style => style.WithAngle(this.Angle.Value));
            };
            this.Second.SelectionChangeCommitted += (_, _) =>
            {
                if (this.Second.SelectedIndex >= 0)
                {
                    var choice = this._choices[this.Second.SelectedIndex];
                    this.Changed?.Invoke(style => style.WithSecond(choice));
                }
            };
            this.SecondOpacity.ValueChanged += (_, _) =>
            {
                this.SecondOpacityLabel.Text = OpacityText(this.SecondOpacity.Value);
                this.Changed?.Invoke(style => style.WithSecondOpacity(this.SecondOpacity.Value / 100.0));
            };
        }

        /// <summary>A change of the style, applied by the user of the fill to its settings.</summary>
        public event Action<Func<FillStyle, FillStyle>>? Changed;

        public FillKindStrip Kind { get; } = new() { Anchor = AnchorStyles.Left };

        public StepSlider Angle { get; } = OptionSlider(0, 359, 15, controlStep: 5);

        public SliderCaption AngleLabel { get; } = new();

        public Label SecondCaption { get; } = new() { Text = "To", AutoSize = true, Anchor = AnchorStyles.Left };

        public ComboBox Second { get; } = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            Width = 120,
            Anchor = AnchorStyles.Left,
        };

        public ColorSplitButton SecondButton { get; } = new();

        public PictureBox SecondOpacityIcon { get; } = OpacityIcon();

        public StepSlider SecondOpacity { get; } = OptionSlider(0, 100, 10);

        public SliderCaption SecondOpacityLabel { get; } = new();

        /// <summary>The angle's slider and its label, laid after the first color.</summary>
        public Control[] AngleControls { get; }

        /// <summary>The second color's line: its source, its eyedropper + Color…, its opacity — the Grid background's.</summary>
        public FlowLayoutPanel SecondLine { get; }

        /// <summary>The second color's controls, wherever they are laid: enabled for a gradient only.</summary>
        public Control[] SecondControls { get; }

        public void Describe(ToolTip toolTip, string target)
        {
            toolTip.SetToolTip(this.Angle, $"The direction the linear gradient goes across {target}: 0° from left to right, 90° from top to bottom");
            toolTip.SetToolTip(this.Second, "Where the gradient's second color comes from");
            toolTip.SetToolTip(this.SecondOpacity, "The second color's opacity");
            this.SecondButton.Describe(toolTip, "the gradient's second color", ". A color chosen sets the second color to Chosen");
        }

        /// <summary>Shows <paramref name="style"/>: the kept settings while its effect is off; the gradient's options enabled for a gradient only.</summary>
        public void Show(FillStyle style)
        {
            this.Kind.Selected = style.Kind;
            this.Angle.Value = Math.Clamp(style.Angle, this.Angle.Minimum, this.Angle.Maximum);
            int index = Array.IndexOf(this._choices, style.Second);
            this.Second.SelectedIndex = index;
            this.SecondOpacity.Value = (int)Math.Round(style.SecondOpacity * 100);
            this.AngleLabel.Text = GradientAngleText(this.Angle.Value);
            this.SecondOpacityLabel.Text = OpacityText(this.SecondOpacity.Value);

            // Their values kept, the gradient's options rest while the fill is solid; the angle is the linear's only.
            this.Angle.Enabled = this.AngleLabel.Enabled = style.Kind == FillKind.Linear;
            foreach (var control in this.SecondControls)
            {
                control.Enabled = style.IsGradient;
            }
        }

        private static string ChoiceText(SecondColor choice) => choice switch
        {
            Composition.SecondColor.Auto => "Auto, last cell",
            _ => choice.ToString(),
        };

        /// <summary>A choice of the drop-down: the swatch of the color it gives now, then its name.</summary>
        private void DrawChoice(DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0)
            {
                return;
            }

            var choice = this._choices[e.Index];
            bool disabled = (e.State & DrawItemState.Disabled) != 0 || !this.Second.Enabled;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            int size = this.Second.LogicalToDeviceUnits(14), gap = this.Second.LogicalToDeviceUnits(3);
            var corner = new Size(this.Second.LogicalToDeviceUnits(5), this.Second.LogicalToDeviceUnits(5));
            var swatch = new Rectangle(e.Bounds.X + gap, e.Bounds.Y + (e.Bounds.Height - size) / 2, size - 1, size - 1);
            var g = e.Graphics;
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(Color.FromArgb(disabled ? 96 : 255, this._swatchOf(choice))))
            {
                g.FillRoundedRectangle(fill, swatch, corner);
            }

            g.DrawRoundedRectangle(SystemPens.ControlDark, swatch, corner);
            g.SmoothingMode = smoothing;
            int x = swatch.Right + 1 + gap;
            var color = disabled ? SystemColors.GrayText : selected ? SystemColors.HighlightText : e.ForeColor;
            TextRenderer.DrawText(g, ChoiceText(choice), e.Font ?? this.Second.Font, new Rectangle(x, e.Bounds.Y, Math.Max(0, e.Bounds.Right - x), e.Bounds.Height), color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }
    }
}
