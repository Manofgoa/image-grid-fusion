using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// The transport's timeline: a track as long as the grid's loop, its played part filled up to a thumb
/// at the grid's time, a tick across it where each turn of the Cascade begins. Pressed, it seeks
/// there; dragged, it scrubs (workfiles/20261009-playback-controls.md). Never takes the focus.
/// </summary>
internal sealed class PlaybackTimeline : Control
{
    private const int TrackHeight = 4;
    private const int ThumbRadius = 6;
    private const int TickHeight = 14;

    private TimeSpan _length;
    private TimeSpan _position;
    private IReadOnlyList<TimeSpan> _marks = [];
    private bool _dragging;

    public PlaybackTimeline()
    {
        this.SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        this.SetStyle(ControlStyles.Selectable, false);
        this.TabStop = false;
    }

    /// <summary>Raised when the timeline is pressed, before the first <see cref="Seeking"/>.</summary>
    public event EventHandler? SeekStarted;

    /// <summary>Raised with the time under the pointer while the timeline is pressed or dragged.</summary>
    public event EventHandler<TimeSpan>? Seeking;

    /// <summary>Raised when the timeline is released.</summary>
    public event EventHandler? SeekEnded;

    /// <summary>The grid's loop; zero draws an empty track.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Length
    {
        get => this._length;
        set
        {
            if (this._length != value)
            {
                this._length = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>The grid's time, where the thumb stands.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Position
    {
        get => this._position;
        set
        {
            if (this._position != value)
            {
                this._position = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>Where the Cascade's turns begin, each drawn as a tick across the track.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TimeSpan> Marks
    {
        get => this._marks;
        set
        {
            if (!this._marks.SequenceEqual(value))
            {
                this._marks = value;
                this.Invalidate();
            }
        }
    }

    protected override Size DefaultSize => new(200, 24);

    /// <summary>As tall as the thumb and the ticks need, whatever height the row it stands in offers.</summary>
    public override Size GetPreferredSize(Size proposedSize) =>
        new(Math.Max(this.LogicalToDeviceUnits(80), proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : this.Width), this.LogicalToDeviceUnits(24));

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        this.EndDrag();
        this.Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || this._length <= TimeSpan.Zero)
        {
            return;
        }

        this._dragging = true;
        this.Capture = true;
        this.SeekStarted?.Invoke(this, EventArgs.Empty);
        this.SeekTo(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (this._dragging)
        {
            this.SeekTo(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        this.EndDrag();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        this.EndDrag();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = this.Track();
        int middle = this.ClientSize.Height / 2;
        int trackHeight = this.LogicalToDeviceUnits(TrackHeight);
        var played = this.Enabled ? SystemColors.Highlight : SystemColors.ControlDark;

        using (var back = new SolidBrush(SystemColors.ControlDark))
        {
            g.FillRectangle(back, track.Left, middle - trackHeight / 2, track.Width, trackHeight);
        }

        if (this._length <= TimeSpan.Zero)
        {
            return;
        }

        using (var tick = new Pen(SystemColors.ControlText, this.LogicalToDeviceUnits(1)))
        {
            int half = this.LogicalToDeviceUnits(TickHeight) / 2;
            foreach (var mark in this._marks)
            {
                int x = this.XOf(mark, track);
                g.DrawLine(tick, x, middle - half, x, middle + half);
            }
        }

        int thumb = this.XOf(this._position, track);
        using (var fill = new SolidBrush(played))
        {
            g.FillRectangle(fill, track.Left, middle - trackHeight / 2, thumb - track.Left, trackHeight);
            if (this.Enabled)
            {
                int radius = this.LogicalToDeviceUnits(ThumbRadius);
                g.FillEllipse(fill, thumb - radius, middle - radius, 2 * radius, 2 * radius);
            }
        }
    }

    /// <summary>The span the track covers: the width, less the thumb's radius at each end, so the thumb stays whole.</summary>
    private Rectangle Track()
    {
        int radius = this.LogicalToDeviceUnits(ThumbRadius);
        return new Rectangle(radius, 0, Math.Max(1, this.ClientSize.Width - 2 * radius), this.ClientSize.Height);
    }

    private int XOf(TimeSpan time, Rectangle track) =>
        track.Left + (int)Math.Round(track.Width * Math.Clamp(time.Ticks / (double)this._length.Ticks, 0, 1));

    /// <summary>Seeks to the time under <paramref name="x"/>, within the loop: its end stands for its start again.</summary>
    private void SeekTo(int x)
    {
        var track = this.Track();
        double share = Math.Clamp((x - track.Left) / (double)track.Width, 0, 1);
        var time = TimeSpan.FromTicks(Math.Min((long)(this._length.Ticks * share), this._length.Ticks - 1));
        this.Position = time;
        this.Seeking?.Invoke(this, time);
    }

    private void EndDrag()
    {
        if (!this._dragging)
        {
            return;
        }

        this._dragging = false;
        this.Capture = false;
        this.SeekEnded?.Invoke(this, EventArgs.Empty);
    }
}
