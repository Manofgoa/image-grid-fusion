using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer's breadcrumb, in its caption line while the folder view shows: the ↑ button, the
/// base folder's name and every folder down to the open one — each but the last opening its folder
/// when clicked — then what the list holds. Too long for its width, it drops its first folders behind
/// an ellipsis, the open folder always shown. See workfiles/20260930-file-explorer-folder-view.md § Layout.
/// </summary>
internal sealed class Breadcrumb : Control
{
    // In logical pixels.
    private const int UpWidth = 20;
    private const int Spacing = 4;
    private const string Separator = " › ";

    // Before a secondary source's name: another environment, not a child of the folder before it.
    private const string SourceSeparator = "  │  ";
    private const string Ellipsis = "…";

    private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

    private readonly ToolTip _toolTip = new();
    private readonly List<(Rectangle Bounds, int Depth)> _links = [];
    private IReadOnlyList<string> _segments = [];

    // The segment of a secondary source's name, drawn in its color after a clear separator; int.MaxValue for none.
    private int _sourceAt = int.MaxValue;
    private Color _sourceColor;
    private string _trailing = "";
    private Rectangle _up;

    // What the mouse is over: -1 the ↑ button, a depth for a folder, int.MinValue nothing.
    private int _hovered = int.MinValue;

    public Breadcrumb()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>The ↑ button: the parent folder is to open.</summary>
    public event EventHandler? UpRequested;

    /// <summary>A folder of the path was clicked: its depth, 0 for the base folder.</summary>
    public event EventHandler<int>? SegmentClicked;

    /// <summary>Whether the ↑ button leads anywhere: the open folder is below the base folder.</summary>
    private bool CanGoUp => _segments.Count > 1;

    /// <summary>
    /// The path shown — the base folder's name first — and the text after it; the segment at
    /// <paramref name="sourceAt"/> is a secondary source's name, drawn in its color (<paramref name="sourceColor"/>)
    /// after a clear separator rather than the child's › (workfiles/20261105-secondary-file-sources.md § In the Folder
    /// View, § Source Colors).
    /// </summary>
    public void SetPath(IReadOnlyList<string> segments, string trailing, int sourceAt = int.MaxValue, Color sourceColor = default)
    {
        _segments = segments;
        this._sourceAt = sourceAt;
        this._sourceColor = sourceColor;
        _trailing = trailing;
        _hovered = int.MinValue;
        _toolTip.SetToolTip(this, null);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        _links.Clear();

        _up = new Rectangle(0, 0, LogicalToDeviceUnits(UpWidth), ClientSize.Height);
        if (CanGoUp && _hovered == -1)
        {
            using var fill = new SolidBrush(SystemColors.ControlLight);
            g.FillRectangle(fill, _up);
            g.DrawRectangle(SystemPens.HotTrack, new Rectangle(_up.X, _up.Y, _up.Width - 1, _up.Height - 1));
        }

        TextRenderer.DrawText(g, "↑", Font, _up, CanGoUp ? SystemColors.ControlText : SystemColors.GrayText, Flags | TextFormatFlags.HorizontalCenter);
        if (_segments.Count == 0)
        {
            return;
        }

        int x = _up.Right + LogicalToDeviceUnits(Spacing);
        string trailing = _trailing.Length == 0 ? "" : "  " + _trailing;
        int trailingWidth = Measure(trailing);
        int available = Math.Max(0, ClientSize.Width - x - trailingWidth);
        int separator = Measure(Separator);
        int[] widths = _segments.Select(Measure).ToArray();

        // The first folder shown: the path from it on, behind an ellipsis once one is dropped, fits.
        int first = 0;
        while (first < _segments.Count - 1)
        {
            int needed = (first > 0 ? Measure(Ellipsis) + separator : 0) + widths[first..].Sum()
                + Enumerable.Range(first + 1, _segments.Count - 1 - first).Sum(next => this.Measure(this.SeparatorBefore(next)));
            if (needed <= available)
            {
                break;
            }

            first++;
        }

        if (first > 0)
        {
            x = Draw(g, Ellipsis, x, SystemColors.GrayText, Font);
            x = Draw(g, Separator, x, SystemColors.GrayText, Font);
        }

        int limit = x + available;
        for (int depth = first; depth < _segments.Count; depth++)
        {
            bool last = depth == _segments.Count - 1;
            if (last)
            {
                // The open folder: cut with an ellipsis rather than dropped.
                var bounds = new Rectangle(x, 0, Math.Max(0, Math.Min(widths[depth], limit - x)), ClientSize.Height);
                TextRenderer.DrawText(g, _segments[depth], Font, bounds, depth == this._sourceAt ? this._sourceColor : SystemColors.ControlText, Flags | TextFormatFlags.EndEllipsis);
                x = bounds.Right;
                break;
            }

            _links.Add((new Rectangle(x, 0, widths[depth], ClientSize.Height), depth));
            using (var font = _hovered == depth ? new Font(Font, FontStyle.Underline) : null)
            {
                x = Draw(g, _segments[depth], x, depth == this._sourceAt ? this._sourceColor : SystemColors.HotTrack, font ?? Font);
            }

            x = Draw(g, this.SeparatorBefore(depth + 1), x, SystemColors.GrayText, Font);
        }

        if (trailing.Length > 0)
        {
            TextRenderer.DrawText(g, trailing, Font, new Rectangle(x, 0, Math.Max(0, ClientSize.Width - x), ClientSize.Height), SystemColors.GrayText, Flags | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Hover(HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Hover(int.MinValue);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int hit = HitTest(e.Location);
        if (hit == -1)
        {
            UpRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (hit >= 0)
        {
            SegmentClicked?.Invoke(this, hit);
        }
    }

    private int HitTest(Point point)
    {
        if (CanGoUp && _up.Contains(point))
        {
            return -1;
        }

        foreach (var (bounds, depth) in _links)
        {
            if (bounds.Contains(point))
            {
                return depth;
            }
        }

        return int.MinValue;
    }

    private void Hover(int hit)
    {
        if (hit == _hovered)
        {
            return;
        }

        _hovered = hit;
        Cursor = hit == int.MinValue ? Cursors.Default : Cursors.Hand;
        _toolTip.SetToolTip(this, hit switch
        {
            -1 => Strings.ExplorerBreadcrumbUp,
            >= 0 => Texts.Format(Strings.ExplorerBreadcrumbOpen, _segments[hit]),
            _ => null,
        });
        Invalidate();
    }

    /// <summary>What separates a segment from the one before it: a clear bar before a secondary source's name, › otherwise.</summary>
    private string SeparatorBefore(int depth) => depth == this._sourceAt ? SourceSeparator : Separator;

    private int Measure(string text) => text.Length == 0 ? 0 : TextRenderer.MeasureText(text, Font, Size.Empty, Flags).Width;

    private int Draw(Graphics g, string text, int x, Color color, Font font)
    {
        int width = TextRenderer.MeasureText(text, font, Size.Empty, Flags).Width;
        TextRenderer.DrawText(g, text, font, new Rectangle(x, 0, width, ClientSize.Height), color, Flags);
        return x + width;
    }
}
