using System.ComponentModel;

namespace ImageGridFusion.UI;

/// <summary>
/// How a tab's feature lines are laid out in its options toolbar (RULES.md § Options Toolbar › Feature Lines,
/// workfiles/20261104-options-toolbar-layout.md): fixed per tab, the first of the three whose columns fit the
/// toolbar with the window 1920 px wide.
/// </summary>
internal enum OptionLayout
{
    /// <summary>C — one feature per line, its checkbox or caption leading it, each slider's name and value above it.</summary>
    Lines,

    /// <summary>A — the lines' leading elements right-aligned in a column, each slider on one row, its value after it.</summary>
    Grid,

    /// <summary>B — one framed group per feature, its controls stacked inside, its name at the bottom.</summary>
    Groups,
}

/// <summary>
/// A slider's caption. Inline, a plain label after its slider (<c>Opacity: 100 %</c>); above its slider, its name at
/// the left and its value at the right (<c>Opacity          100 %</c>), as wide as the slider. The text stays the one
/// the code sets — <c>Name: value</c>, or a value alone — and is split when drawn above.
/// </summary>
internal sealed class SliderCaption : Label
{
    private bool _above;
    private string? _caption;

    public SliderCaption()
    {
        this.AutoSize = true;
        this.Anchor = AnchorStyles.Left;
        this.UseMnemonic = false;
    }

    /// <summary>
    /// The name drawn above the slider in place of the one the text holds: <c>""</c> draws the value alone — a line
    /// whose leading caption names it already — <c>null</c> the text's own name.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Caption
    {
        get => this._caption;
        set
        {
            this._caption = value;
            this.Invalidate();
        }
    }

    /// <summary>Whether the caption is drawn above its slider, name and value apart, rather than as a plain label.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Above
    {
        get => this._above;
        set
        {
            if (this._above == value)
            {
                return;
            }

            this._above = value;
            this.AutoSize = !value;
            this.Invalidate();
        }
    }

    /// <summary>The height of the caption above its slider: one line of its font.</summary>
    public int AboveHeight => TextRenderer.MeasureText("Ag", this.Font, Size.Empty, Flags).Height;

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    /// <summary>The name and the value drawn above the slider: the text split at its first <c>": "</c>, a text without one a value alone.</summary>
    public (string Name, string Value) Parts
    {
        get
        {
            string text = this.Text;
            int colon = text.IndexOf(": ", StringComparison.Ordinal);
            // The name trimmed: French writes a space before its colon (Mélange : 40 %).
            var (name, value) = colon < 0 ? ("", text) : (text[..colon].TrimEnd(), text[(colon + 2)..]);
            return (this._caption ?? name, value);
        }
    }

    /// <summary>
    /// Where the value is drawn, in the caption's coordinates: at its right above the slider, after <c>Name: </c>
    /// inline — where a field typing the value lies over it (workfiles/20261105-slider-exact-value.md).
    /// </summary>
    public Rectangle ValueBounds
    {
        get
        {
            var (_, value) = this.Parts;
            int width = TextRenderer.MeasureText(value, this.Font, Size.Empty, Flags).Width;
            if (this._above)
            {
                return new Rectangle(Math.Max(0, this.Width - width), 0, Math.Min(width, this.Width), this.Height);
            }

            string text = this.Text;
            int colon = text.IndexOf(": ", StringComparison.Ordinal);
            int x = this.Padding.Left + (colon < 0 ? 0 : TextRenderer.MeasureText(text[..(colon + 2)], this.Font, Size.Empty, Flags).Width);
            return new Rectangle(x, 0, width, this.Height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!this._above)
        {
            base.OnPaint(e);
            return;
        }

        var (name, value) = this.Parts;
        var color = this.Enabled ? this.ForeColor : SystemColors.GrayText;
        var bounds = this.ClientRectangle;
        TextRenderer.DrawText(e.Graphics, value, this.Font, bounds, color, Flags | TextFormatFlags.Right | TextFormatFlags.Bottom);
        int valueWidth = value.Length == 0 ? 0 : TextRenderer.MeasureText(e.Graphics, value, this.Font, Size.Empty, Flags).Width + this.LogicalToDeviceUnits(6);
        var nameBounds = bounds with { Width = Math.Max(0, bounds.Width - valueWidth) };
        TextRenderer.DrawText(e.Graphics, name, this.Font, nameBounds, color, Flags | TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// A slider of a feature line, one control: its icon at its left when it has one (the opacity's), its caption above
/// it — or after it in the Grid layout — and the slider at one width, compact unless said otherwise.
/// </summary>
internal sealed class SliderBox : Panel
{
    /// <summary>The width of every slider of a feature line, in logical px.</summary>
    public const int CompactWidth = 120;

    /// <summary>The width of a slider aiming at one frame among many — the Frames' trim and starting point — in logical px.</summary>
    public const int LongWidth = 200;

    private readonly TrackBar _slider;
    private readonly SliderCaption? _caption;
    private readonly Control? _icon;
    private readonly int _width;
    private bool _above = true;
    private bool _arranging;

    public SliderBox(TrackBar slider, SliderCaption? caption, Control? icon = null, int width = CompactWidth)
    {
        this._slider = slider;
        this._caption = caption;
        this._icon = icon;
        this._width = width;
        this.Margin = new Padding(3, 0, 3, 0);
        foreach (var child in new Control?[] { icon, caption, slider })
        {
            if (child is not null)
            {
                child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                child.Margin = Padding.Empty;
                this.Controls.Add(child);
                child.SizeChanged += (_, _) => this.Arrange();
            }
        }

        this.Arrange();
    }

    /// <summary>The distance from the box's top to its slider's middle: where a line aligns it with its other controls.</summary>
    public int Axis { get; private set; }

    /// <summary>Whether the caption stands above the slider (Lines, Groups) or after it (Grid).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Above
    {
        get => this._above;
        set
        {
            this._above = value;
            this.Arrange();
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        this.Arrange();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.Arrange();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        this.Arrange();
    }

    /// <summary>Places the icon, the caption and the slider, and fits the box to them.</summary>
    private void Arrange()
    {
        if (this._arranging)
        {
            return;
        }

        this._arranging = true;
        try
        {
            int gap = this.LogicalToDeviceUnits(3);
            int sliderWidth = this.LogicalToDeviceUnits(this._width);
            if (this._slider.Width != sliderWidth)
            {
                this._slider.Width = sliderWidth;
            }

            if (this._caption is not null)
            {
                this._caption.Above = this._above;
            }

            int head = this._caption is not null && this._above ? this._caption.AboveHeight : 0;
            int x = this._icon is null ? 0 : this._icon.Width + gap;
            if (this._icon is not null)
            {
                this._icon.Location = new Point(0, head + ((this._slider.Height - this._icon.Height) / 2));
            }

            this._slider.Location = new Point(x, head);
            int width = x + sliderWidth;
            int height = head + this._slider.Height;
            if (this._caption is not null)
            {
                if (this._above)
                {
                    this._caption.Bounds = new Rectangle(x, 0, sliderWidth, head);
                }
                else
                {
                    var size = this._caption.PreferredSize;
                    this._caption.Location = new Point(width + gap, (this._slider.Height - size.Height) / 2);
                    width = this._caption.Left + Math.Max(size.Width, this._caption.Width);
                }
            }

            this.Axis = head + (this._slider.Height / 2);
            this.Size = new Size(width, height);
        }
        finally
        {
            this._arranging = false;
        }
    }
}

/// <summary>
/// A tab's options as feature lines (RULES.md § Options Toolbar › Feature Lines): its tall blocks first, each the
/// toolbar's height, then its lines, each a feature — its checkbox or caption, then its controls — laid out in the
/// tab's <see cref="OptionLayout"/>, top-down, a new column to the right when the height is full. Scrolled sideways
/// by a scrollbar under them while wider than the toolbar, its room kept whether it shows or not.
/// </summary>
internal sealed class FeatureLinesPanel : Panel
{
    private readonly OptionLayout _layout;
    private readonly Panel _viewport = new() { Margin = Padding.Empty };
    private readonly LinesContent _content = new();
    private readonly HScrollBar _scroll = new() { Visible = false, TabStop = false };
    private readonly List<Control> _blocks = [];
    private readonly List<Line> _lines = [];

    // The controls stretched by the layout, with their own size: a block as tall as the toolbar, a control as wide as its column.
    private readonly Dictionary<Control, Size> _stretched = [];
    private bool _arranging;
    private int _preferredHeight;

    public FeatureLinesPanel(OptionLayout layout)
    {
        this._layout = layout;
        this.Dock = DockStyle.Fill;
        this.Margin = Padding.Empty;
        this.Visible = false;
        this._viewport.Controls.Add(this._content);
        this.Controls.Add(this._viewport);
        this.Controls.Add(this._scroll);
        this._scroll.ValueChanged += (_, _) => this._content.Left = -this._scroll.Value;
    }

    /// <summary>Raised when the height the lines ask for changes: the toolbar holding them is sized again.</summary>
    public event EventHandler? PreferredHeightChanged;

    /// <summary>The room kept for the scrollbar under the options of both toolbars, every tab, at <paramref name="dpi"/>.</summary>
    public static int ScrollRoom(int dpi) => SystemInformation.GetHorizontalScrollBarHeightForDpi(dpi);

    /// <summary>Adds a tall block — the layer list, the text field — before the lines; one anchored at the bottom takes the toolbar's height.</summary>
    public void AddBlock(Control block)
    {
        this._blocks.Add(block);
        if ((block.Anchor & AnchorStyles.Bottom) != 0)
        {
            this._stretched[block] = block.Size;
        }

        this.Adopt(block);
    }

    /// <summary>Adds a feature's line led by a caption.</summary>
    public void AddLine(string caption, params Control[] controls) =>
        this.AddLine(new Label { Text = caption, AutoSize = true, UseMnemonic = false }, controls);

    /// <summary>
    /// Adds a feature's line led by <paramref name="lead"/> — its checkbox, or a caption label — then its controls; a
    /// control anchored at the right takes what is left of its column's width.
    /// </summary>
    public void AddLine(Control lead, params Control[] controls)
    {
        this._lines.Add(new Line(lead, controls));
        this.Adopt(lead);
        foreach (var control in controls)
        {
            if ((control.Anchor & AnchorStyles.Right) != 0)
            {
                this._stretched[control] = control.Size;
            }

            if (control is SliderBox box)
            {
                box.Above = this._layout != OptionLayout.Grid;
            }

            this.Adopt(control);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        // As tall as its tallest block or line, and room for two lines of sliders — the toolbar keeps its height for them.
        int height = this._blocks.Count == 0 ? 0 : this._blocks.Max(block => this.NaturalSize(block).Height + block.Margin.Vertical);
        int sliders = 0;
        foreach (var line in this._lines)
        {
            var (axis, below) = Extent(line);
            height = Math.Max(height, axis + below);
            // A line of sliders: a slider is its tallest control — never a line of thumbnails holding one.
            int tallest = line.Controls.Append(line.Lead).Max(control => control.Height);
            if (line.Controls.OfType<SliderBox>().Any(box => box.Height >= tallest))
            {
                sliders = Math.Max(sliders, axis + below);
            }
        }

        height = Math.Max(height, (2 * sliders) + this.LineGap);
        return new Size(this._content.Width, height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        this.Arrange();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (this.Visible)
        {
            this.Arrange();
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.Arrange();
    }

    // One spacing everywhere, whatever the controls' own margins (workfiles/20261104-options-toolbar-layout.md, Iteration 6).
    private int Gap => this.LogicalToDeviceUnits(6);

    private int LeadGap => this.LogicalToDeviceUnits(8);

    private int LineGap => this.LogicalToDeviceUnits(4);

    private int ColumnGap => this.LogicalToDeviceUnits(24);

    /// <summary>The space between two controls of a line: the gap, but between buttons of one row touching (the text alignments).</summary>
    private int Spacing(Control previous, Control control) =>
        previous is CheckBox { Appearance: Appearance.Button } && control is CheckBox { Appearance: Appearance.Button }
            ? this.LogicalToDeviceUnits(1)
            : this.Gap;

    /// <summary>
    /// The key a control lines up by in its column — a color button or a slider, named by the color buttons and
    /// sliders of its line up to it — or none: the controls of one key stand at one x in every line of the column,
    /// so lines of one shape line up (the Background's two colors) and a slider after thumbnails stays by them.
    /// </summary>
    private static IEnumerable<(Control Control, string? Key)> Keyed(Line line)
    {
        string path = "";
        foreach (var control in line.Controls)
        {
            if (control is ColorSplitButton or SliderBox)
            {
                path += "/" + control.GetType().Name;
                yield return (control, path);
            }
            else
            {
                yield return (control, null);
            }
        }
    }

    /// <summary>Where each keyed control of <paramref name="line"/> stands from the controls' start, and where the line ends, the anchors found so far applied.</summary>
    private (Dictionary<string, int> Offsets, int End) Offsets(Line line, Dictionary<string, int> anchors)
    {
        var offsets = new Dictionary<string, int>();
        int x = 0;
        Control? previous = null;
        foreach (var (control, key) in Keyed(line))
        {
            if (previous is not null)
            {
                x += this.Spacing(previous, control);
            }

            if (key is { } k)
            {
                x = Math.Max(x, anchors.GetValueOrDefault(k));
                offsets[k] = x;
            }

            x += this.NaturalSize(control).Width;
            previous = control;
        }

        return (offsets, x);
    }

    /// <summary>The anchors of a column: for each key, in the order the lines hold them, the farthest place a line puts it.</summary>
    private Dictionary<string, int> Anchors(List<Line> column)
    {
        var anchors = new Dictionary<string, int>();
        var keys = column.SelectMany(line => Keyed(line).Select(keyed => keyed.Key)).OfType<string>().Distinct().ToList();
        foreach (var key in keys)
        {
            anchors[key] = column.Max(line => this.Offsets(line, anchors).Offsets.GetValueOrDefault(key));
        }

        return anchors;
    }

    /// <summary>
    /// Where a line's leading element stands from the line's top: centered on its first row — the sliders' captions,
    /// or a control's height at most — so it reads at the top of its line, never floating at its middle.
    /// </summary>
    private int LeadTop(Line line, int axis, int height)
    {
        if (line.Controls.OfType<SliderBox>().FirstOrDefault(box => box.Above) is { } box)
        {
            // The caption's row: the box's top, as tall as the box above its slider.
            int caption = box.Height - (2 * (box.Height - box.Axis));
            return Math.Max(0, axis - box.Axis + ((caption - line.Lead.Height) / 2));
        }

        return Math.Max(0, (Math.Min(height, this.LogicalToDeviceUnits(24)) - line.Lead.Height) / 2);
    }

    /// <summary>Where a control's middle is, from its top: its slider's middle for a slider box.</summary>
    private static int AxisOf(Control control) => control is SliderBox box ? box.Axis : control.Height / 2;

    /// <summary>A line's extent around its axis — above it, below it — so its controls line up on their middles.</summary>
    private static (int Axis, int Below) Extent(Line line)
    {
        int axis = line.Lead.Height / 2, below = line.Lead.Height - axis;
        foreach (var control in line.Controls)
        {
            int middle = AxisOf(control);
            axis = Math.Max(axis, middle);
            below = Math.Max(below, control.Height - middle);
        }

        return (axis, below);
    }

    private Size NaturalSize(Control control) => this._stretched.TryGetValue(control, out var size) ? size : control.Size;

    /// <summary>Takes a control into the lines: placed by them from now on, laid out again whenever its size changes.</summary>
    private void Adopt(Control control)
    {
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        this._content.Controls.Add(control);
        control.SizeChanged += (_, _) => this.Arrange();
    }

    /// <summary>Lays the blocks and the lines out in the toolbar's height, then shows the scrollbar while they are wider than it.</summary>
    private void Arrange()
    {
        if (this._arranging)
        {
            return;
        }

        this._arranging = true;
        try
        {
            int room = ScrollRoom(this.DeviceDpi);
            var client = this.ClientSize;
            int height = Math.Max(0, client.Height - room);
            this._viewport.Bounds = new Rectangle(0, 0, client.Width, height);
            this._scroll.Bounds = new Rectangle(0, height, client.Width, room);
            this._content.Separators.Clear();
            this._content.Groups.Clear();
            int x = this.PlaceBlocks(height);
            int width = this._layout == OptionLayout.Groups ? this.PlaceGroups(x, height) : this.PlaceLines(x, height);
            this._content.Size = new Size(width, height);            if (width > client.Width && client.Width > 0)
            {
                this._scroll.Minimum = 0;
                this._scroll.Maximum = width - 1;
                this._scroll.LargeChange = client.Width;
                this._scroll.SmallChange = this.LogicalToDeviceUnits(40);
                this._scroll.Value = Math.Min(this._scroll.Value, width - client.Width);
                this._scroll.Visible = true;
            }
            else
            {
                this._scroll.Value = 0;
                this._scroll.Visible = false;
            }

            this._content.Location = new Point(-this._scroll.Value, 0);
            this._content.Invalidate();
        }
        finally
        {
            this._arranging = false;
        }

        // The controls scaled to the DPI after the toolbar was sized, a font changed: the toolbar sizes itself again.
        int preferred = this.GetPreferredSize(Size.Empty).Height;
        if (preferred != this._preferredHeight)
        {
            this._preferredHeight = preferred;
            this.PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The tall blocks side by side, a line after each; gives where the lines start.</summary>
    private int PlaceBlocks(int height)
    {
        int x = 0;
        foreach (var block in this._blocks)
        {
            if (this._stretched.ContainsKey(block))
            {
                block.Height = Math.Max(0, height - block.Margin.Vertical);
            }

            block.Location = new Point(x + block.Margin.Left, block.Margin.Top);
            x = block.Right + block.Margin.Right;
            this._content.Separators.Add(x + (this.ColumnGap / 2));
            x += this.ColumnGap;
        }

        return x;
    }

    /// <summary>
    /// Lines and Grid: the lines top-down from <paramref name="x"/>, a new column when the height is full; in a column,
    /// the leading elements one width — left-aligned, or right-aligned in the Grid — so the controls start aligned.
    /// </summary>
    private int PlaceLines(int x, int height)
    {
        var columns = new List<List<Line>>();
        int y = 0;
        foreach (var line in this._lines)
        {
            var (axis, below) = Extent(line);
            if (columns.Count == 0 || (y > 0 && y + axis + below > height))
            {
                columns.Add([]);
                y = 0;
            }

            columns[^1].Add(line);
            y += axis + below + this.LineGap;
        }

        foreach (var column in columns)
        {
            int leads = column.Max(line => line.Lead.Width);
            var anchors = this.Anchors(column);
            int controls = column.Max(line => this.Offsets(line, anchors).End);
            int start = x + leads + this.LeadGap;
            y = 0;
            foreach (var line in column)
            {
                var (axis, below) = Extent(line);
                int leadX = this._layout == OptionLayout.Grid ? x + leads - line.Lead.Width : x;
                line.Lead.Location = new Point(leadX, y + this.LeadTop(line, axis, axis + below));
                int cx = 0;
                Control? previous = null;
                foreach (var (control, key) in Keyed(line))
                {
                    if (previous is not null)
                    {
                        cx += this.Spacing(previous, control);
                    }

                    if (key is { } k)
                    {
                        cx = Math.Max(cx, anchors.GetValueOrDefault(k));
                    }

                    if (this._stretched.ContainsKey(control) && control is not SliderBox)
                    {
                        // As wide as what its column leaves it, never narrower than its own width.
                        int rest = controls - cx
                            - line.Controls.SkipWhile(c => c != control).Skip(1).Sum(c => this.Gap + this.NaturalSize(c).Width);
                        control.Width = Math.Max(this.NaturalSize(control).Width, rest);
                    }

                    control.Location = new Point(start + cx, y + axis - AxisOf(control));
                    cx += control.Width;
                    previous = control;
                }

                y += axis + below + this.LineGap;
            }

            x = start + controls;
            this._content.Separators.Add(x + (this.ColumnGap / 2));
            x += this.ColumnGap;
        }

        return this.Trimmed(x);
    }

    /// <summary>Groups: each line a framed group from <paramref name="x"/>, its controls stacked inside — a column more when the height is full — its caption at the bottom.</summary>
    private int PlaceGroups(int x, int height)
    {
        int pad = this.LogicalToDeviceUnits(4), stack = this.LogicalToDeviceUnits(2);
        foreach (var line in this._lines)
        {
            // A checkbox leads the stack; a caption names the group at its bottom.
            bool named = line.Lead is not CheckBox;
            var items = named ? line.Controls : [line.Lead, .. line.Controls];
            int bottom = height - pad - (named ? line.Lead.Height + stack : 0);
            int columnX = x + pad, columnWidth = 0, y = pad;
            foreach (var item in items)
            {
                if (y > pad && y + item.Height > bottom)
                {
                    columnX += columnWidth + pad;
                    columnWidth = 0;
                    y = pad;
                }

                item.Location = new Point(columnX + item.Margin.Left, y);
                y += item.Height + stack;
                columnWidth = Math.Max(columnWidth, item.Width + item.Margin.Horizontal);
            }

            int width = Math.Max(columnX + columnWidth + pad - x, named ? line.Lead.Width + (2 * pad) : 0);
            if (named)
            {
                line.Lead.Location = new Point(x + ((width - line.Lead.Width) / 2), height - pad - line.Lead.Height);
            }

            this._content.Groups.Add(new Rectangle(x, 0, width - 1, height - 1));
            x += width + this.Gap;
        }

        return x;
    }

    /// <summary>The content's width without the gap and the line after the last column.</summary>
    private int Trimmed(int x)
    {
        if (this._content.Separators.Count > 0)
        {
            this._content.Separators.RemoveAt(this._content.Separators.Count - 1);
            x -= this.ColumnGap;
        }

        return Math.Max(0, x);
    }

    private sealed record Line(Control Lead, Control[] Controls);

    /// <summary>The lines' surface: the thin lines between the columns, the groups' frames.</summary>
    private sealed class LinesContent : Panel
    {
        public LinesContent()
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.Margin = Padding.Empty;
        }

        public List<int> Separators { get; } = [];

        public List<Rectangle> Groups { get; } = [];

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int inset = this.LogicalToDeviceUnits(4);
            using var pen = new Pen(SystemColors.ControlDark);
            foreach (int x in this.Separators)
            {
                e.Graphics.DrawLine(pen, x, inset, x, this.Height - 1 - inset);
            }

            foreach (var group in this.Groups)
            {
                e.Graphics.DrawRectangle(pen, group);
            }
        }
    }
}
