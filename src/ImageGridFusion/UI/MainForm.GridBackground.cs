using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;
using ImageGridFusion.Resources;

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

    /// <summary>The cell Background's From: where its first color comes from (workfiles/20261105-cell-background-auto-colors.md).</summary>
    private ColorChoiceBox<FirstColor>? _backgroundFirst;

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
        this._gridFill = new FillControls(Strings.PanelFillSecondAuto, Strings.PanelFillSecondTip, this.GridSecondSwatch);
        var firstLine = OptionLine();
        firstLine.Controls.AddRange([this._gridBackgroundColor, this._gridBackgroundOpacityIcon, this._gridBackgroundOpacity, this._gridBackgroundOpacityLabel, .. this._gridFill.AngleControls]);
        var lines = OptionLine();
        lines.FlowDirection = FlowDirection.TopDown;
        lines.Controls.AddRange([firstLine, this._gridFill.SecondLine]);
        this._globalOptions[GlobalEffect.GridBackground].Controls.AddRange([this._gridFill.Kind, lines]);
        this.BuildBackgroundImageOptions();

        this._gridBackgroundColor.Describe(this._toolTip, Strings.PanelGridBgColorTarget, Strings.PanelGridBgColorNote);
        this._gridBackgroundColor.Dialog.Click += (_, _) => this.PickColor(this.GridBackgroundRoute());
        this._gridBackgroundColor.Eyedropper.Click += (_, _) => this.ArmEyedropper(this._gridBackgroundColor, this.GridBackgroundRoute());
        this._gridBackgroundOpacity.ValueChanged += (_, _) =>
        {
            this._gridBackgroundOpacityLabel.Text = OpacityText(this._gridBackgroundOpacity.Value);
            this.ChangeGridBackground(background => background.WithOpacity(this._gridBackgroundOpacity.Value / 100.0));
        };
        this._gridFill.Describe(this._toolTip, Strings.PanelFillTargetGrid);
        this._gridFill.Changed += change => this.ChangeGridBackground(background => background.WithStyle(change(background.Style)));
        this._gridFill.SecondButton.Dialog.Click += (_, _) => this.PickColor(this.GridSecondRoute());
        this._gridFill.SecondButton.Eyedropper.Click += (_, _) => this.ArmEyedropper(this._gridFill.SecondButton, this.GridSecondRoute());
    }

    /// <summary>
    /// The cell Background's options, one feature per line (workfiles/20261104-options-toolbar-layout.md): the fill
    /// modes, the gradient's kinds and angle, the color — its From —, the gradient's second color, the extended edges.
    /// </summary>
    private void BuildBackgroundOptions(FeatureLinesPanel lines)
    {
        this._backgroundStyle = new FillControls(Strings.PanelFillAutoCell, Strings.PanelFillAutoCellTip, this.BackgroundSecondSwatch);
        var style = this._backgroundStyle;
        this._backgroundFirst = new ColorChoiceBox<FirstColor>(Enum.GetValues<FirstColor>(), FirstColorText, this.BackgroundFirstSwatch)
        {
            Width = 150,
            DropDownWidth = 170,
        };
        this._backgroundFirst.Picked += (_, first) => this.SetBackgroundFirst(first);
        this._toolTip.SetToolTip(this._backgroundFirst, Strings.MainBackgroundFirstTip);
        lines.AddLine(Strings.MainLineFill, this._backgroundFill);
        lines.AddLine(Strings.MainLineGradient, style.Kind, new SliderBox(style.Angle, style.AngleLabel));
        lines.AddLine(this._backgroundFirstCaption, this._backgroundFirst, this._backgroundColor, new SliderBox(this._backgroundOpacity, this._backgroundOpacityLabel, this._backgroundOpacityIcon));
        lines.AddLine(style.SecondCaption, style.Second, style.SecondButton, new SliderBox(style.SecondOpacity, style.SecondOpacityLabel, style.SecondOpacityIcon));
        lines.AddLine(
            Strings.MainLineEdges,
            new SliderBox(this._backgroundEdgeOpacity, this._backgroundEdgeOpacityLabel),
            new SliderBox(this._backgroundBlend, this._backgroundBlendLabel),
            this._backgroundSoften);
        this._backgroundStyle.Describe(this._toolTip, Strings.PanelFillTargetCell);
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
        if (this._backgroundFirst is not null)
        {
            this._backgroundFirst.Choice = background.First;
            this._backgroundFirstCaption.Text = background.Style.IsGradient ? Strings.PanelFillFrom : Strings.MainLineColor;
        }

        if (this._selectedEffect == ImageEffect.Background)
        {
            this.SetSwatch(this._backgroundStyle.SecondButton.Dialog, background.Style.SecondFor(this.BackgroundShown(background), this._preview.SelectedAutomaticBackground));
            this._backgroundStyle.Second.Invalidate();
            this._backgroundFirst?.Invalidate();
        }
    }

    private static string FirstColorText(FirstColor first) => first switch
    {
        FirstColor.Auto => Strings.PanelFillAutoCell,
        FirstColor.Black => Strings.PanelFillSecondBlack,
        FirstColor.White => Strings.PanelFillSecondWhite,
        _ => Strings.PanelFillSecondChosen,
    };

    /// <summary>The swatch of a cell Background's From choice: the color it gives the selected cell now — Chosen, the color it would keep.</summary>
    private Color BackgroundFirstSwatch(FirstColor first) =>
        this._preview.SelectedImage?.Look.TurnOn(ImageEffect.Background).Background is { } background
            ? first == FirstColor.Chosen ? this.BackgroundShown(background) : background.WithFirst(first).FirstFor(this._preview.SelectedAutomaticBackground ?? Color.White)
            : BackgroundEffect.Default.WithFirst(first).FirstFor(Color.White);

    /// <summary>
    /// From picked: Auto, Black or White drop the chosen color; Chosen freezes the color in use at that moment as
    /// the chosen one — what unchecking <i>Automatic color</i> did.
    /// </summary>
    private void SetBackgroundFirst(FirstColor first) =>
        this.ChangeLook(ImageEffect.Background, look => look.WithBackground(first == FirstColor.Chosen
            ? look.Background!.WithColor(this.BackgroundShown(look.Background!))
            : look.Background!.WithFirst(first)));

    /// <summary>The swatch of a Grid background's second color choice: the color it gives now.</summary>
    private Color GridSecondSwatch(SecondColor choice) =>
        this._gridBackground.Style.WithSecond(choice).SecondFor(this.GridBackgroundFirst, this._preview.GridAutomaticColors.Last);

    /// <summary>The swatch of a cell Background's second color choice: the color it gives the selected cell now.</summary>
    private Color BackgroundSecondSwatch(SecondColor choice) =>
        this._preview.SelectedImage?.Look.TurnOn(ImageEffect.Background).Background is { } background
            ? background.Style.WithSecond(choice).SecondFor(this.BackgroundShown(background), this._preview.SelectedAutomaticBackground)
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
            background.Style.SecondFor(this.BackgroundShown(background), this._preview.SelectedAutomaticBackground),
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

    private static string GradientAngleText(int degrees) => Texts.Format(Strings.PanelFillAngle, degrees);

    /// <summary>
    /// The shared fill's options, one set per user — the cell's Background, the Grid background: its kinds as
    /// thumbnails, the linear gradient's angle, the second color's source, its eyedropper + Color… and its opacity.
    /// </summary>
    private sealed class FillControls
    {
        private readonly string _autoTip;

        /// <summary>
        /// The options of a fill whose <see cref="SecondColor.Auto"/> reads <paramref name="autoText"/> — the Grid
        /// background's last cell, a cell's own automatic color — explained by <paramref name="autoTip"/>.
        /// </summary>
        public FillControls(string autoText, string autoTip, Func<SecondColor, Color> swatchOf)
        {
            this._autoTip = autoTip;
            this.Second = new ColorChoiceBox<SecondColor>(Enum.GetValues<SecondColor>(), choice => ChoiceText(choice, autoText), swatchOf)
            {
                Width = 150,
                DropDownWidth = 170,
            };
            this.Angle.Width = 120;
            this.SecondOpacity.Width = 110;
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
            this.Second.Picked += (_, choice) => this.Changed?.Invoke(style => style.WithSecond(choice));
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

        public Label SecondCaption { get; } = new() { Text = Strings.PanelFillTo, AutoSize = true, Anchor = AnchorStyles.Left };

        public ColorChoiceBox<SecondColor> Second { get; }

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
            toolTip.SetToolTip(this.Angle, Texts.Format(Strings.PanelFillAngleTip, target));
            toolTip.SetToolTip(this.Second, this._autoTip);
            toolTip.SetToolTip(this.SecondOpacity, Strings.PanelFillSecondOpacityTip);
            this.SecondButton.Describe(toolTip, Strings.PanelFillSecondTarget, Strings.PanelFillSecondNote);
        }

        /// <summary>Shows <paramref name="style"/>: the kept settings while its effect is off; the gradient's options enabled for a gradient only.</summary>
        public void Show(FillStyle style)
        {
            this.Kind.Selected = style.Kind;
            this.Angle.Value = Math.Clamp(style.Angle, this.Angle.Minimum, this.Angle.Maximum);
            this.Second.Choice = style.Second;
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

        private static string ChoiceText(SecondColor choice, string autoText) => choice switch
        {
            Composition.SecondColor.Auto => autoText,
            Composition.SecondColor.Darker => Strings.PanelFillSecondDarker,
            Composition.SecondColor.Black => Strings.PanelFillSecondBlack,
            Composition.SecondColor.White => Strings.PanelFillSecondWhite,
            Composition.SecondColor.Chosen => Strings.PanelFillSecondChosen,
            _ => choice.ToString(),
        };
    }
}
