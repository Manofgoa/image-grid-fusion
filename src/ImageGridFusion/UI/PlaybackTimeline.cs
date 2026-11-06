using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>One of the timeline's two range handles.</summary>
internal enum TimelineHandle
{
    Start,
    End,
}

/// <summary>A range handle dragged to <see cref="Time"/>, before the range's own limits are applied.</summary>
internal sealed class TimelineHandleEventArgs(TimelineHandle handle, TimeSpan time) : EventArgs
{
    public TimelineHandle Handle { get; } = handle;

    public TimeSpan Time { get; } = time;
}

/// <summary>
/// The transport's timeline: a track spanning <see cref="Length"/> — the grid's loop, or more when the range ends
/// past it — its played part filled up to a thumb at the grid's time, a tick across it where each turn of the
/// Cascade begins, another where the grid's loop ends. Two <b>range handles</b> mark where the video starts and
/// ends, the track dimmed outside them; dragged, they move the range — the end past the track's right edge
/// too, the track lengthened once released (workfiles/20261106-video-range.md § Timeline). Pressed elsewhere, it
/// seeks there, within the range; dragged, it scrubs (workfiles/20261009-playback-controls.md). Never takes the focus.
/// </summary>
internal sealed class PlaybackTimeline : Control
{
    private const int TrackHeight = 4;
    private const int ThumbRadius = 6;
    private const int TickHeight = 14;
    private const int HandleWidth = 3;
    private const int HandleHeight = 20;
    private const int HandleFlag = 4;

    // How near a handle a press grabs it, and how near a tick a dragged handle snaps onto it, in logical px.
    private const int HandleReach = 6;
    private const int SnapReach = 6;

    private TimeSpan _length;
    private TimeSpan _position;
    private TimeSpan _loop;
    private TimeSpan _rangeStart;
    private TimeSpan _rangeEnd;
    private IReadOnlyList<TimeSpan> _marks = [];
    private bool _dragging;

    // The handle dragged, and the track's span at its press: kept while it is dragged, so the handle stays under the pointer.
    private TimelineHandle? _handleDrag;
    private TimeSpan _dragLength;

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

    /// <summary>Raised when a range handle is pressed, before the first <see cref="HandleMoved"/>.</summary>
    public event EventHandler? HandleDragStarted;

    /// <summary>Raised with the time under the pointer while a range handle is pressed or dragged.</summary>
    public event EventHandler<TimelineHandleEventArgs>? HandleMoved;

    /// <summary>Raised when a range handle is released.</summary>
    public event EventHandler? HandleDragEnded;

    /// <summary>What the track spans: the grid's loop, or more when the range ends past it; zero draws an empty track.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Length
    {
        get => this._length;
        set => this.Set(ref this._length, value);
    }

    /// <summary>The grid's time, where the thumb stands.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Position
    {
        get => this._position;
        set => this.Set(ref this._position, value);
    }

    /// <summary>Where the grid's loop ends: a tick of its own while the track spans past it, a stop for the handles.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Loop
    {
        get => this._loop;
        set => this.Set(ref this._loop, value);
    }

    /// <summary>Where the start handle stands: the video's first instant on the grid's timeline.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan RangeStart
    {
        get => this._rangeStart;
        set => this.Set(ref this._rangeStart, value);
    }

    /// <summary>Where the end handle stands: the video's end on the grid's timeline.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan RangeEnd
    {
        get => this._rangeEnd;
        set => this.Set(ref this._rangeEnd, value);
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

    /// <summary>Whether a range handle is being dragged.</summary>
    public bool DraggingHandle => this._handleDrag is not null;

    protected override Size DefaultSize => new(200, 24);

    // The span the drawing and the pointer map onto: frozen while a handle is dragged.
    private TimeSpan MapLength => this._handleDrag is not null ? this._dragLength : this._length;

    /// <summary>As tall as the thumb, the ticks and the handles need, whatever height the row it stands in offers.</summary>
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
        if (this.HandleAt(e.X) is { } handle)
        {
            this._handleDrag = handle;
            this._dragLength = this._length;
            this.HandleDragStarted?.Invoke(this, EventArgs.Empty);
            this.MoveHandle(e.X);
            return;
        }

        this.SeekStarted?.Invoke(this, EventArgs.Empty);
        this.SeekTo(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (this._dragging)
        {
            if (this._handleDrag is not null)
            {
                this.MoveHandle(e.X);
            }
            else
            {
                this.SeekTo(e.X);
            }

            return;
        }

        this.Cursor = this.Enabled && this._length > TimeSpan.Zero && this.HandleAt(e.X) is not null ? Cursors.SizeWE : Cursors.Default;
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
        int top = middle - trackHeight / 2;

        if (this.MapLength <= TimeSpan.Zero)
        {
            using var empty = new SolidBrush(SystemColors.ControlDark);
            g.FillRectangle(empty, track.Left, top, track.Width, trackHeight);
            return;
        }

        // Outside the range, the track is dimmed: what the video leaves out.
        int start = this.XOf(this._rangeStart, track);
        int end = this.XOf(this._rangeEnd, track);
        using (var outside = new SolidBrush(SystemColors.ControlLight))
        using (var inside = new SolidBrush(SystemColors.ControlDark))
        {
            g.FillRectangle(outside, track.Left, top, track.Width, trackHeight);
            g.FillRectangle(inside, start, top, Math.Max(1, end - start), trackHeight);
        }

        using (var tick = new Pen(SystemColors.ControlText, this.LogicalToDeviceUnits(1)))
        {
            int half = this.LogicalToDeviceUnits(TickHeight) / 2;
            foreach (var mark in this._marks)
            {
                int x = this.XOf(mark, track);
                g.DrawLine(tick, x, middle - half, x, middle + half);
            }

            // The grid's loop ending before the track does: a dashed tick where its contents start over.
            if (this._loop > TimeSpan.Zero && this._loop < this.MapLength)
            {
                using var loopTick = new Pen(SystemColors.GrayText, this.LogicalToDeviceUnits(1)) { DashStyle = DashStyle.Dash };
                int x = this.XOf(this._loop, track);
                g.DrawLine(loopTick, x, middle - half, x, middle + half);
            }
        }

        int thumb = this.XOf(this._position, track);
        using (var fill = new SolidBrush(played))
        {
            if (thumb > start)
            {
                g.FillRectangle(fill, start, top, thumb - start, trackHeight);
            }

            this.PaintHandle(g, fill, start, middle, TimelineHandle.Start);
            this.PaintHandle(g, fill, end, middle, TimelineHandle.End);
            if (this.Enabled)
            {
                int radius = this.LogicalToDeviceUnits(ThumbRadius);
                g.FillEllipse(fill, thumb - radius, middle - radius, 2 * radius, 2 * radius);
            }
        }
    }

    /// <summary>A handle: a bar across the track, a flag at its top turned into the range — a bracket opening or closing it.</summary>
    private void PaintHandle(Graphics g, Brush brush, int x, int middle, TimelineHandle handle)
    {
        int width = this.LogicalToDeviceUnits(HandleWidth);
        int half = this.LogicalToDeviceUnits(HandleHeight) / 2;
        int flag = this.LogicalToDeviceUnits(HandleFlag);
        int left = handle == TimelineHandle.Start ? x - width / 2 : x - width + width / 2;
        g.FillRectangle(brush, left, middle - half, width, 2 * half);
        int flagLeft = handle == TimelineHandle.Start ? left : left - flag + width;
        g.FillRectangle(brush, flagLeft, middle - half, flag, width);
        g.FillRectangle(brush, flagLeft, middle + half - width, flag, width);
    }

    /// <summary>The span the track covers: the width, less the thumb's radius at each end, so the thumb stays whole.</summary>
    private Rectangle Track()
    {
        int radius = this.LogicalToDeviceUnits(ThumbRadius);
        return new Rectangle(radius, 0, Math.Max(1, this.ClientSize.Width - 2 * radius), this.ClientSize.Height);
    }

    private int XOf(TimeSpan time, Rectangle track) => XOf(time, track, this.MapLength);

    private TimeSpan TimeAt(int x, Rectangle track) => TimeAt(x, track, this.MapLength);

    /// <summary>Where <paramref name="time"/> stands on a <paramref name="track"/> spanning <paramref name="length"/>, kept on it.</summary>
    internal static int XOf(TimeSpan time, Rectangle track, TimeSpan length) =>
        track.Left + (int)Math.Round(track.Width * Math.Clamp(time.Ticks / (double)length.Ticks, 0, 1));

    /// <summary>
    /// The time under <paramref name="x"/> on a <paramref name="track"/> spanning <paramref name="length"/>, past the
    /// track's ends when it lies beyond them: the end handle dragged past the right edge goes past the track's span.
    /// </summary>
    internal static TimeSpan TimeAt(int x, Rectangle track, TimeSpan length) =>
        TimeSpan.FromTicks((long)(length.Ticks * ((x - track.Left) / (double)track.Width)));

    /// <summary>The handle within reach of <paramref name="x"/>, the nearer one when both are; <c>null</c> for none.</summary>
    private TimelineHandle? HandleAt(int x)
    {
        var track = this.Track();
        int reach = this.LogicalToDeviceUnits(HandleReach);
        int start = Math.Abs(x - this.XOf(this._rangeStart, track));
        int end = Math.Abs(x - this.XOf(this._rangeEnd, track));
        if (Math.Min(start, end) > reach)
        {
            return null;
        }

        // Both on one spot: the side of the pointer chooses — the end to its right, so a range can be widened.
        return end < start || (end == start && x >= this.XOf(this._rangeEnd, track)) ? TimelineHandle.End : TimelineHandle.Start;
    }

    /// <summary>
    /// Moves the dragged handle to the time under <paramref name="x"/> — not before 0, the end past the track's right
    /// edge as the pointer goes there — snapped onto the grid's loop end, a Cascade tick or the grid's start within reach.
    /// </summary>
    private void MoveHandle(int x)
    {
        var track = this.Track();
        var time = this.TimeAt(x, track);
        if (this._handleDrag == TimelineHandle.Start && time > this.MapLength)
        {
            time = this.MapLength;
        }

        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        int snap = this.LogicalToDeviceUnits(SnapReach);
        foreach (var stop in this._marks.Append(this._loop).Append(TimeSpan.Zero))
        {
            if (stop >= TimeSpan.Zero && stop <= this.MapLength && Math.Abs(this.XOf(stop, track) - x) <= snap)
            {
                time = stop;
                break;
            }
        }

        this.HandleMoved?.Invoke(this, new TimelineHandleEventArgs(this._handleDrag!.Value, time));
    }

    /// <summary>Seeks to the time under <paramref name="x"/>, within the range: its end stands for its start again.</summary>
    private void SeekTo(int x)
    {
        var track = this.Track();
        var time = this.TimeAt(x, track);
        var last = this._rangeEnd > this._rangeStart ? this._rangeEnd - TimeSpan.FromTicks(1) : this._length - TimeSpan.FromTicks(1);
        time = time < this._rangeStart ? this._rangeStart : time > last ? last : time;
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
        if (this._handleDrag is not null)
        {
            this._handleDrag = null;
            this.Invalidate();
            this.HandleDragEnded?.Invoke(this, EventArgs.Empty);
            return;
        }

        this.SeekEnded?.Invoke(this, EventArgs.Empty);
    }

    private void Set(ref TimeSpan field, TimeSpan value)
    {
        if (field != value)
        {
            field = value;
            this.Invalidate();
        }
    }
}
