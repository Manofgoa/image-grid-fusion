using System.ComponentModel;

namespace ImageGridFusion.UI;

/// <summary>
/// A color option's buttons: the eyedropper — Windows' own split button, pressed while it is armed, its arrow
/// opening the sample menu — then the Color… button touching it, opening the standard dialog, its swatch before
/// its text (shared RULES.md § Colour Items). Each with its own hover and tooltip
/// (workfiles/20261029-eyedropper-ergonomics.md § Attached Split Button, § Sample Menu).
/// </summary>
internal sealed class ColorSplitButton : Panel
{
    // How far the Color… button lies over the eyedropper's right edge, in logical px: the system buttons keep a
    // margin inside their bounds, so that only one border shows between the two.
    private const int Overlap = 2;

    private readonly ContextMenuStrip _sampleMenu = new();
    private ToolTip? _toolTip;
    private string _target = "";

    public ColorSplitButton()
    {
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Anchor = AnchorStyles.Left;
        this.Margin = new Padding(3);
        this.Eyedropper.Menu = this._sampleMenu;
        this._sampleMenu.Items.Add(new ToolStripMenuItem("Eyedropper sample") { Enabled = false });
        for (int pixels = GridPreview.MinEyedropperSample; pixels <= GridPreview.MaxEyedropperSample; pixels++)
        {
            int size = pixels;
            var item = new ToolStripMenuItem($"{size} px") { Checked = size == this.Sample, Tag = size };
            item.Click += (_, _) => this.ChooseSample(size);
            this._sampleMenu.Items.Add(item);
        }

        this.Controls.AddRange([this.Eyedropper, this.Dialog]);
    }

    /// <summary>
    /// The eyedropper: an icon only, held pressed while the eyedropper is armed — by its owner only — its arrow
    /// opening the sample menu. Its <see cref="Control.Click"/> arms it, a size chosen in the menu too.
    /// </summary>
    public SplitButton Eyedropper { get; } = new()
    {
        AutoSize = false,
        Margin = Padding.Empty,
        MenuDirection = ToolStripDropDownDirection.BelowRight,
    };

    /// <summary>The Color… part: the swatch of the color in use, then its text, its left border over the eyedropper's.</summary>
    public Button Dialog { get; } = new()
    {
        Text = "Color…",
        AutoSize = true,
        TextImageRelation = TextImageRelation.ImageBeforeText,
        Margin = Padding.Empty,
    };

    /// <summary>
    /// The side of the square of preview pixels the eyedropper averages, 1 to 5 px: one per color option, 1 px at
    /// each launch, out of the undo history and of every Reset (RULES.md § Color Options).
    /// </summary>
    public int Sample { get; private set; } = GridPreview.MinEyedropperSample;

    /// <summary>Whether the eyedropper is drawn pressed: set by its owner while it is armed.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Armed
    {
        get => this.Eyedropper.Held;
        set => this.Eyedropper.Held = value;
    }

    /// <summary>
    /// The tooltips of both parts, saying whose color they set — <paramref name="target"/>, e.g. "the borders'
    /// color" — the eyedropper's naming the sample in use; <paramref name="dialogNote"/> ends the Color… part's.
    /// </summary>
    public void Describe(ToolTip toolTip, string target, string dialogNote = "")
    {
        (this._toolTip, this._target) = (toolTip, target);
        toolTip.SetToolTip(this.Dialog, $"Chooses {target} in the color dialog, following it live — Cancel gives the previous one back{dialogNote}");
        this.DescribeEyedropper();
    }

    /// <summary>The eyedropper's icon, at its size in device pixels; the button keeps its own copy.</summary>
    public void SetIcon(Bitmap icon) => this.Eyedropper.SetIcon(icon);

    /// <summary>
    /// The eyedropper at its ideal width — its icon and its arrow — as tall as the Color… part, which lies over its
    /// right edge.
    /// </summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        int height = this.Dialog.Height;
        int width = this.Eyedropper.IdealWidth is > 0 and var ideal ? ideal : height + this.LogicalToDeviceUnits(16);
        var eyedropper = new Rectangle(0, 0, width, height);
        if (this.Eyedropper.Bounds != eyedropper)
        {
            this.Eyedropper.Bounds = eyedropper;
        }

        var dialog = new Point(width - this.LogicalToDeviceUnits(Overlap), 0);
        if (this.Dialog.Location != dialog)
        {
            this.Dialog.Location = dialog;
        }

        base.OnLayout(levent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._sampleMenu.Dispose();
            this.Dialog.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>A size chosen in the menu: taken, then the eyedropper armed with it, as a click on it would.</summary>
    private void ChooseSample(int size)
    {
        this.Sample = size;
        foreach (var item in this._sampleMenu.Items.OfType<ToolStripMenuItem>())
        {
            item.Checked = item.Tag is int pixels && pixels == size;
        }

        this.DescribeEyedropper();
        this.Eyedropper.PerformClick();
    }

    private void DescribeEyedropper() =>
        this._toolTip?.SetToolTip(
            this.Eyedropper,
            $"Takes {this._target} from the preview (sample: {this.Sample} px): click a cell; Escape or a right click cancels. "
            + "The arrow chooses the sample, the side of the square of pixels averaged");
}
