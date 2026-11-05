using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The Format tab's options: one thumbnail per output format, in a row — the current layout, its
/// separators' sizes included, drawn schematically at the format's ratio, its name below. The free
/// format is drawn at the ratio it would give the grid, outlined dashed. The active format is
/// highlighted like the active layout of the layout strip; a click on another one picks it. The Custom
/// format, last, is labelled with its value — <c>3:2</c>, or <c>1080×1350</c> with exact pixels — and drawn at
/// its ratio; unset, labelled <c>Custom</c> and drawn at the ratio its first pick would take
/// (workfiles/20261104-custom-output-format.md).
/// </summary>
internal sealed class FormatStrip : SelectionStrip<OutputFormat>
{
    private GridLayout? _layout;
    private double _freeRatio = OutputFormats.TwitterRatio;
    private CustomFormat? _custom;
    private double _unsetRatio = OutputFormats.TwitterRatio;

    public FormatStrip()
        : base(OutputFormat.Twitter)
    {
        this.FitSize();
    }

    /// <summary>The grid the thumbnails show: its layout as it stands — a single cell without one — and the free format's ratio.</summary>
    public void SetGrid(GridLayout? layout, double freeRatio)
    {
        if (layout != _layout || freeRatio != _freeRatio)
        {
            _layout = layout;
            _freeRatio = freeRatio;
            Invalidate();
        }
    }

    /// <summary>The Custom format as it stands — <c>null</c> while unset — and, unset, the ratio its first pick would take.</summary>
    public void SetCustom(CustomFormat? custom, double unsetRatio)
    {
        if (custom != this._custom || unsetRatio != this._unsetRatio)
        {
            bool relabelled = custom?.Label != this._custom?.Label;
            this._custom = custom;
            this._unsetRatio = unsetRatio;
            if (relabelled)
            {
                // The label's width is the item's when wider than its box.
                this.FitSize();
            }

            this.Invalidate();
        }
    }

    protected override Size Box => new(72, 40);

    protected override string Label(OutputFormat format) =>
        format == OutputFormat.Custom && this._custom is { } custom ? custom.Label : OutputFormats.Name(format);

    protected override string Tip(OutputFormat format) => format switch
    {
        OutputFormat.Free => Texts.Format(Strings.UiFormatFreeTip, _freeRatio),
        OutputFormat.Twitter => Strings.UiFormatTwitterTip,
        OutputFormat.Square => Strings.UiFormatSquareTip,
        OutputFormat.Portrait => Strings.UiFormatPortraitTip,
        OutputFormat.Story => Strings.UiFormatStoryTip,
        OutputFormat.Custom => this._custom switch
        {
            null => Strings.UiFormatCustomNoneTip,
            { Exact: true } custom => Texts.Format(Strings.UiFormatCustomExactTip, custom.Width, custom.Height),
            var custom => Texts.Format(Strings.UiFormatCustomRatioTip, custom.Label),
        },
        _ => Strings.UiFormatLandscapeTip,
    };

    protected override void PaintThumbnail(Graphics g, OutputFormat format, Rectangle box, bool active)
    {
        // The thumbnail: the largest rectangle at the format's ratio in the box, centered.
        double ratio = OutputFormats.Ratio(format)
            ?? (format == OutputFormat.Custom ? this._custom?.Ratio ?? this._unsetRatio : _freeRatio);
        int width = Math.Min(box.Width, GridLayout.WidthFor(box.Height, ratio));
        int height = Math.Min(box.Height, GridLayout.HeightFor(width, ratio));
        var thumbnail = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);

        var color = !Enabled ? SystemColors.ControlLight : active ? SystemColors.ControlDarkDark : SystemColors.ControlDark;
        using (var brush = new SolidBrush(color))
        {
            int gap = LogicalToDeviceUnits(1);
            foreach (var cell in (_layout ?? GridLayout.Default(1)).Cells(thumbnail.Size))
            {
                var bounds = Rectangle.Inflate(cell, -gap, -gap);
                bounds.Offset(thumbnail.Location);
                g.FillRectangle(brush, bounds);
            }
        }

        if (format == OutputFormat.Free)
        {
            using var dashed = new Pen(Enabled ? SystemColors.ControlText : SystemColors.GrayText) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(dashed, thumbnail.X, thumbnail.Y, thumbnail.Width - 1, thumbnail.Height - 1);
        }
    }
}
