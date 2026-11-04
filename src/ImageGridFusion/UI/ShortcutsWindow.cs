using System.Drawing.Drawing2D;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The shortcuts window: every key and mouse gesture of the app and what it does, read from the embedded
/// <c>SHORTCUTS.md</c> (<see cref="ShortcutList"/>). Opened by F1 and the ⚙ menu, modeless, owned by the main
/// window; Escape closes it. See workfiles/20261029-shortcuts-list.md § Shortcuts Window.
/// </summary>
internal sealed class ShortcutsWindow : Form
{
    private readonly Panel _scroller = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly ShortcutsView _view;

    public ShortcutsWindow(IReadOnlyList<ShortcutSection> sections)
    {
        this.Text = Strings.CompShortcutsTitle;
        this.Icon = AppIcon.Load();
        this.ShowInTaskbar = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.Manual;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.BackColor = SystemColors.Window;
        this.MinimumSize = new Size(this.LogicalToDeviceUnits(360), this.LogicalToDeviceUnits(240));

        this._view = new ShortcutsView(sections);
        this._scroller.Controls.Add(this._view);
        this.Controls.Add(this._scroller);
        this._scroller.Resize += (_, _) => this.LayOut();
    }

    /// <summary>Centered on its owner, within the working area of the owner's screen.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = this.Owner?.Bounds ?? Screen.PrimaryScreen!.WorkingArea;
        var screen = Screen.FromRectangle(area).WorkingArea;
        this.Size = new Size(
            Math.Min(this.LogicalToDeviceUnits(640), screen.Width),
            Math.Min(this.LogicalToDeviceUnits(760), screen.Height * 9 / 10));
        this.Location = new Point(
            Math.Clamp(area.Left + (area.Width - this.Width) / 2, screen.Left, Math.Max(screen.Left, screen.Right - this.Width)),
            Math.Clamp(area.Top + (area.Height - this.Height) / 2, screen.Top, Math.Max(screen.Top, screen.Bottom - this.Height)));
        this.LayOut();
    }

    /// <summary>Escape closes; the arrows, the page keys, Home and End scroll the list.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        int line = this.LogicalToDeviceUnits(40);
        int page = Math.Max(line, this._scroller.ClientSize.Height - line);
        int? delta = keyData switch
        {
            Keys.Up => -line,
            Keys.Down => line,
            Keys.PageUp => -page,
            Keys.PageDown => page,
            Keys.Home => int.MinValue / 2,
            Keys.End => int.MaxValue / 2,
            _ => null,
        };
        if (keyData == Keys.Escape)
        {
            this.Close();
            return true;
        }

        if (delta is { } by)
        {
            int top = -this._scroller.AutoScrollPosition.Y;
            this._scroller.AutoScrollPosition = new Point(0, (int)Math.Clamp((long)top + by, 0, int.MaxValue));
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// The list as wide as the scroller, the vertical scroll bar's room always kept so it never makes the list
    /// lay out again, and as tall as its content.
    /// </summary>
    private void LayOut()
    {
        int width = Math.Max(this.LogicalToDeviceUnits(200), this._scroller.Width - SystemInformation.VerticalScrollBarWidth);
        this._view.Size = new Size(width, this._view.HeightFor(width));
    }
}

/// <summary>
/// The shortcuts drawn: per section its heading in bold, per row the keys as key caps — flowing onto more lines
/// in their column when they do not fit — and what they do on the right, wrapped.
/// </summary>
internal sealed class ShortcutsView : Control
{
    private const int Edge = 12;
    private const int SectionGap = 18;
    private const int HeadingGap = 6;
    private const int RowGap = 7;
    private const int ColumnGap = 16;
    private const int KeyPadX = 5;
    private const int KeyGap = 3;
    private const int LineGap = 3;
    private const TextFormatFlags Single = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    private const TextFormatFlags Wrapped = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;

    private readonly IReadOnlyList<ShortcutSection> _sections;
    // The measures, taken once per font: every layout and paint reads them again.
    private readonly Dictionary<ShortcutPart, int> _partWidths = [];
    private int? _spaceWidth;
    private Font _headingFont;

    public ShortcutsView(IReadOnlyList<ShortcutSection> sections)
    {
        this._sections = sections;
        this._headingFont = new Font(this.Font.FontFamily, this.Font.Size * 1.15f, FontStyle.Bold);
        this.BackColor = SystemColors.Window;
        this.ForeColor = SystemColors.ControlText;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        this.SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>The height the list takes at <paramref name="width"/>.</summary>
    public int HeightFor(int width) => this.Flow(null, width);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        this.Flow(e.Graphics, this.Width);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        this._partWidths.Clear();
        this._spaceWidth = null;
        this._headingFont.Dispose();
        this._headingFont = new Font(this.Font.FontFamily, this.Font.Size * 1.15f, FontStyle.Bold);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._headingFont.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Lays the list out at <paramref name="width"/>, drawing it when <paramref name="g"/> is given; its height.</summary>
    private int Flow(Graphics? g, int width)
    {
        int edge = this.LogicalToDeviceUnits(Edge);
        int available = Math.Max(1, width - 2 * edge);
        int lineHeight = this.KeyLineHeight();
        int textHeight = TextRenderer.MeasureText("Ag", this.Font, Size.Empty, Single).Height;
        int rowGap = this.LogicalToDeviceUnits(RowGap);

        using var rule = new Pen(SystemColors.ControlDark);
        using var separator = new Pen(SystemColors.ControlLight);
        int y = 0;
        foreach (var section in this._sections)
        {
            // Each section its own keys' column, so a long row elsewhere does not push every description away.
            int keysWidth = Math.Min(this.WidestKeys(section), available * 45 / 100);
            int doesX = edge + keysWidth + this.LogicalToDeviceUnits(ColumnGap);
            int doesWidth = Math.Max(this.LogicalToDeviceUnits(80), width - edge - doesX);
            y += this.LogicalToDeviceUnits(y == 0 ? Edge : SectionGap);
            var heading = TextRenderer.MeasureText(section.Title, this._headingFont, new Size(available, int.MaxValue), Wrapped);
            if (g is not null)
            {
                TextRenderer.DrawText(g, section.Title, this._headingFont, new Rectangle(edge, y, available, heading.Height), this.ForeColor, Wrapped);
                g.DrawLine(rule, edge, y + heading.Height + 2, edge + available, y + heading.Height + 2);
            }

            y += heading.Height + this.LogicalToDeviceUnits(HeadingGap) + 3;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                int keysHeight = this.FlowKeys(g, row.Keys, new Point(edge, y), keysWidth);
                int doesTop = y + (lineHeight - textHeight) / 2;
                int doesHeight = TextRenderer.MeasureText(row.Does, this.Font, new Size(doesWidth, int.MaxValue), Wrapped).Height;
                if (g is not null)
                {
                    TextRenderer.DrawText(g, row.Does, this.Font, new Rectangle(doesX, doesTop, doesWidth, doesHeight), this.ForeColor, Wrapped);
                }

                y = Math.Max(y + keysHeight, doesTop + doesHeight) + rowGap;
                if (g is not null && i < section.Rows.Count - 1)
                {
                    g.DrawLine(separator, edge, y - rowGap / 2, edge + available, y - rowGap / 2);
                }
            }
        }

        return y + this.LogicalToDeviceUnits(Edge);
    }

    /// <summary>
    /// The keys of a row laid out from <paramref name="at"/> within <paramref name="width"/> — a key cap never cut,
    /// a word or a key going to the next line when it does not fit — drawn when <paramref name="g"/> is given; their height.
    /// </summary>
    private int FlowKeys(Graphics? g, IReadOnlyList<ShortcutPart> parts, Point at, int width)
    {
        int lineHeight = this.KeyLineHeight();
        int x = at.X, y = at.Y;
        foreach (var part in parts)
        {
            int w = this.PartWidth(part);
            int gap = x == at.X ? 0 : this.GapBefore(part);
            if (x > at.X && x + gap + w > at.X + width)
            {
                x = at.X;
                y += lineHeight + this.LogicalToDeviceUnits(LineGap);
                gap = 0;
            }

            if (g is not null)
            {
                this.DrawPart(g, part, new Rectangle(x + gap, y, w, lineHeight));
            }

            x += gap + w;
        }

        return y - at.Y + lineHeight;
    }

    private void DrawPart(Graphics g, ShortcutPart part, Rectangle box)
    {
        if (!part.IsKey)
        {
            TextRenderer.DrawText(g, part.Text, this.Font, box, SystemColors.GrayText, Single | TextFormatFlags.VerticalCenter);
            return;
        }

        var cap = new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1);
        using (var path = RoundedRectangle(cap, this.LogicalToDeviceUnits(3)))
        using (var fill = new SolidBrush(SystemColors.Control))
        using (var border = new Pen(SystemColors.ControlDark))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        TextRenderer.DrawText(g, part.Text, this.Font, box, SystemColors.ControlText,
            Single | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>The widest row of keys of <paramref name="section"/> on one line: its keys' column, unless that takes more than its share.</summary>
    private int WidestKeys(ShortcutSection section)
    {
        int widest = 0;
        foreach (var row in section.Rows)
        {
            int width = 0;
            for (int i = 0; i < row.Keys.Count; i++)
            {
                width += (i == 0 ? 0 : this.GapBefore(row.Keys[i])) + this.PartWidth(row.Keys[i]);
            }

            widest = Math.Max(widest, width);
        }

        return widest;
    }

    private int PartWidth(ShortcutPart part)
    {
        var key = part with { SpaceBefore = false };
        if (!this._partWidths.TryGetValue(key, out int width))
        {
            int text = TextRenderer.MeasureText(part.Text, this.Font, Size.Empty, Single).Width;
            width = part.IsKey ? text + 2 * this.LogicalToDeviceUnits(KeyPadX) : text;
            this._partWidths[key] = width;
        }

        return width;
    }

    /// <summary>A space's width after a space in the file, a narrow gap otherwise (<c>Ctrl</c>+<c>Z</c>).</summary>
    private int GapBefore(ShortcutPart part) => part.SpaceBefore
        ? this._spaceWidth ??= TextRenderer.MeasureText("x x", this.Font, Size.Empty, Single).Width - TextRenderer.MeasureText("xx", this.Font, Size.Empty, Single).Width
        : this.LogicalToDeviceUnits(KeyGap);

    private int KeyLineHeight() => TextRenderer.MeasureText("Ag", this.Font, Size.Empty, Single).Height + this.LogicalToDeviceUnits(6);

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        int d = 2 * radius;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
