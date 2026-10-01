using System.ComponentModel;
using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Format tab's options: one thumbnail per output format, in a row — the current layout, its
/// separators' sizes included, drawn schematically at the format's ratio, its name below. The free
/// format is drawn at the ratio it would give the grid, outlined dashed. The active format is
/// highlighted like the active layout of the layout strip; a click on another one picks it.
/// </summary>
internal sealed class FormatStrip : Control
{
    // In logical pixels: the box every thumbnail fits in, the padding around an item, between items.
    private const int BoxWidth = 72;
    private const int BoxHeight = 40;
    private const int Pad = 4;
    private const int LabelGap = 2;
    private const int Spacing = 4;

    private readonly OutputFormat[] _formats = Enum.GetValues<OutputFormat>();
    private readonly ToolTip _toolTip = new();
    private GridLayout? _layout;
    private double _freeRatio = OutputFormats.TwitterRatio;
    private OutputFormat _selected = OutputFormat.Twitter;
    private int _hovered = -1;

    public FormatStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
        Margin = Padding.Empty;
        Size = GetPreferredSize(Size.Empty);
    }

    /// <summary>A thumbnail other than the active one was clicked.</summary>
    public event EventHandler<OutputFormat>? FormatPicked;

    /// <summary>The format shown as active.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public OutputFormat Selected
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                _selected = value;
                Invalidate();
            }
        }
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

    public override Size GetPreferredSize(Size proposedSize)
    {
        var items = Items();
        return new Size(items[^1].Right, LogicalToDeviceUnits(Pad + BoxHeight + LabelGap + Pad) + Font.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        SetHovered(-1);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var items = Items();
        for (int i = 0; i < _formats.Length; i++)
        {
            PaintItem(g, _formats[i], items[i], i == _hovered);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHovered(Enabled ? Items().FindIndex(item => item.Contains(e.Location)) : -1);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(-1);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = Items().FindIndex(item => item.Contains(e.Location));
        if (e.Button == MouseButtons.Left && Enabled && index >= 0 && _formats[index] != _selected)
        {
            FormatPicked?.Invoke(this, _formats[index]);
        }
    }

    private void SetHovered(int index)
    {
        if (index == _hovered)
        {
            return;
        }

        _hovered = index;
        Cursor = index >= 0 && _formats[index] != _selected ? Cursors.Hand : Cursors.Default;
        _toolTip.SetToolTip(this, index < 0 ? null : Tip(_formats[index]));
        Invalidate();
    }

    private string Tip(OutputFormat format) => format switch
    {
        OutputFormat.Free => $"The ratio that loses the least of the images: {_freeRatio:0.00}:1 for this grid",
        OutputFormat.Twitter => "Twitter / X in-feed image, 1200:628",
        OutputFormat.Square => "Instagram / Facebook feed",
        OutputFormat.Portrait => "Instagram / Facebook portrait feed",
        OutputFormat.Story => "Stories, Reels, TikTok, Shorts",
        _ => "YouTube, LinkedIn video, screens",
    };

    /// <summary>The items left to right, each as wide as its thumbnail box or its name.</summary>
    private List<Rectangle> Items()
    {
        int pad = LogicalToDeviceUnits(Pad);
        int box = LogicalToDeviceUnits(BoxWidth);
        int height = LogicalToDeviceUnits(Pad + BoxHeight + LabelGap + Pad) + Font.Height;
        var items = new List<Rectangle>(_formats.Length);
        int x = 0;
        foreach (var format in _formats)
        {
            int label = TextRenderer.MeasureText(OutputFormats.Name(format), Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            var item = new Rectangle(x, 0, Math.Max(box, label) + 2 * pad, height);
            items.Add(item);
            x = item.Right + LogicalToDeviceUnits(Spacing);
        }

        return items;
    }

    private void PaintItem(Graphics g, OutputFormat format, Rectangle item, bool hot)
    {
        bool active = format == _selected;
        if (active)
        {
            using var fill = new SolidBrush(Color.FromArgb(Enabled ? 60 : 30, SystemColors.Highlight));
            g.FillRectangle(fill, item);
            using var pen = new Pen(Enabled ? SystemColors.Highlight : SystemColors.ControlDark, LogicalToDeviceUnits(2)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, item);
        }
        else if (hot)
        {
            using var fill = new SolidBrush(SystemColors.ControlLight);
            g.FillRectangle(fill, item);
        }

        // The thumbnail: the largest rectangle at the format's ratio in the box, centered.
        double ratio = OutputFormats.Ratio(format) ?? _freeRatio;
        int pad = LogicalToDeviceUnits(Pad);
        int boxWidth = LogicalToDeviceUnits(BoxWidth);
        int boxHeight = LogicalToDeviceUnits(BoxHeight);
        int width = Math.Min(boxWidth, GridLayout.WidthFor(boxHeight, ratio));
        int height = Math.Min(boxHeight, GridLayout.HeightFor(width, ratio));
        var thumbnail = new Rectangle(item.X + (item.Width - width) / 2, item.Y + pad + (boxHeight - height) / 2, width, height);

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

        var label = new Rectangle(item.X, item.Y + pad + boxHeight + LogicalToDeviceUnits(LabelGap), item.Width, Font.Height);
        TextRenderer.DrawText(
            g,
            OutputFormats.Name(format),
            Font,
            label,
            Enabled ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
