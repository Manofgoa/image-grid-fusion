namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer's indexing progress, in the 3 px row under its search box: a blue bar on a
/// transparent track, as long as the job has gone — the scan, the content extraction, any indexing job —
/// or a short segment sweeping the track while the job's total is not known yet; hidden while no job
/// runs, its row kept so nothing moves. See workfiles/20260926-ocr-search.md § Progress Bar.
/// </summary>
internal sealed class IndexingBar : Control
{
    /// <summary>The row's height, in logical pixels.</summary>
    public const int Thickness = 3;

    private const int SweepInterval = 30;

    // The sweeping segment's length, and how far it moves per tick, as fractions of the track.
    private const double SweepLength = 0.25;
    private const double SweepStep = 0.02;

    private static readonly Color BarColor = Color.FromArgb(0, 120, 215);

    private readonly System.Windows.Forms.Timer _sweep = new() { Interval = SweepInterval };
    private double? _fraction;
    private double _sweepAt;

    public IndexingBar()
    {
        this.SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw,
            true);
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = Color.Transparent;
        this.Margin = Padding.Empty;
        this.TabStop = false;
        this.Visible = false;
        this._sweep.Tick += (_, _) =>
        {
            this._sweepAt = (this._sweepAt + SweepStep) % 1;
            this.Invalidate();
        };
    }

    /// <summary>A job gone <paramref name="fraction"/> of its way, 0 to 1.</summary>
    public void ShowProgress(double fraction)
    {
        this._sweep.Stop();
        this._fraction = Math.Clamp(fraction, 0, 1);
        this.Visible = true;
        this.Invalidate();
    }

    /// <summary>A job whose total is not known yet: the sweeping segment.</summary>
    public void ShowUnknown()
    {
        if (this._fraction is null && this._sweep.Enabled)
        {
            return;
        }

        this._fraction = null;
        this._sweepAt = 0;
        this._sweep.Start();
        this.Visible = true;
        this.Invalidate();
    }

    /// <summary>No job runs: the bar hidden, its row left empty.</summary>
    public void HideProgress()
    {
        this._sweep.Stop();
        this.Visible = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int width = this.ClientSize.Width;
        Rectangle bar;
        if (this._fraction is { } fraction)
        {
            bar = new Rectangle(0, 0, (int)Math.Round(width * fraction), this.ClientSize.Height);
        }
        else
        {
            // From just off the left edge to just off the right one: the segment enters and leaves.
            int length = (int)Math.Round(width * SweepLength);
            int x = (int)Math.Round(this._sweepAt * (width + length)) - length;
            bar = new Rectangle(x, 0, length, this.ClientSize.Height);
        }

        using var brush = new SolidBrush(BarColor);
        e.Graphics.FillRectangle(brush, bar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._sweep.Dispose();
        }

        base.Dispose(disposing);
    }
}
