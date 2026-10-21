namespace ImageGridFusion.UI;

/// <summary>
/// A color option's split button: the eyedropper part, pressed while it is armed, then the Color… part
/// opening the standard dialog, its swatch before its text (shared RULES.md § Colour Items). Two native
/// buttons side by side, each with its own hover and tooltip (workfiles/20261021-color-eyedropper.md
/// § Color Routes).
/// </summary>
internal sealed class ColorSplitButton : FlowLayoutPanel
{
    public ColorSplitButton()
    {
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.WrapContents = false;
        this.Anchor = AnchorStyles.Left;
        this.Margin = new Padding(3);
        this.Controls.AddRange([this.Eyedropper, this.Dialog]);
    }

    /// <summary>The eyedropper part: an icon only, checked — drawn pressed — while the eyedropper is armed; checked by its owner only.</summary>
    public CheckBox Eyedropper { get; } = new()
    {
        Appearance = Appearance.Button,
        AutoCheck = false,
        AutoSize = false,
        ImageAlign = ContentAlignment.MiddleCenter,
        Margin = Padding.Empty,
    };

    /// <summary>The Color… part: the swatch of the color in use, then its text.</summary>
    public Button Dialog { get; } = new()
    {
        Text = "Color…",
        AutoSize = true,
        TextImageRelation = TextImageRelation.ImageBeforeText,
        Margin = Padding.Empty,
    };

    /// <summary>The eyedropper part square, as tall as the Color… part beside it.</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        int side = this.Dialog.Height;
        if (this.Eyedropper.Size != new Size(side, side))
        {
            this.Eyedropper.Size = new Size(side, side);
        }

        base.OnLayout(levent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Eyedropper.Image?.Dispose();
            this.Dialog.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
