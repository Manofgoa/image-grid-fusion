using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Format tab's options: one thumbnail per output format, in a row — the current layout, its
/// separators' sizes included, drawn schematically at the format's ratio, its name below. The free
/// format is drawn at the ratio it would give the grid, outlined dashed. The active format is
/// highlighted like the active layout of the layout strip; a click on another one picks it.
/// </summary>
internal sealed class FormatStrip : ThumbnailStrip<OutputFormat>
{
    private GridLayout? _layout;
    private double _freeRatio = OutputFormats.TwitterRatio;

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

    protected override Size Box => new(72, 40);

    protected override string Label(OutputFormat format) => OutputFormats.Name(format);

    protected override string Tip(OutputFormat format) => format switch
    {
        OutputFormat.Free => $"The ratio that loses the least of the images: {_freeRatio:0.00}:1 for this grid",
        OutputFormat.Twitter => "Twitter / X in-feed image, 1200:628",
        OutputFormat.Square => "Instagram / Facebook feed",
        OutputFormat.Portrait => "Instagram / Facebook portrait feed",
        OutputFormat.Story => "Stories, Reels, TikTok, Shorts",
        _ => "YouTube, LinkedIn video, screens",
    };

    protected override void PaintThumbnail(Graphics g, OutputFormat format, Rectangle box, bool active)
    {
        // The thumbnail: the largest rectangle at the format's ratio in the box, centered.
        double ratio = OutputFormats.Ratio(format) ?? _freeRatio;
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
