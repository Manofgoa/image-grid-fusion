namespace ImageGridFusion.UI;

/// <summary>
/// A frame of a video or an animated GIF, typed in text fields (workfiles/20261008-video-trim.md): for
/// a video, its minutes, seconds and frame within the second, at the file's frame rate; for a GIF, its
/// number from 1. ↑ / ↓ and the wheel add or take 1 of a field's unit, 5 with Control held, carrying
/// over between the fields like a clock; a typed value applies on Enter or when the field is left, an
/// invalid one put back. The value stays within <see cref="Minimum"/> and <see cref="Maximum"/>.
/// </summary>
internal sealed class FrameField : FlowLayoutPanel
{
    /// <summary>The step of ↑ / ↓ and of a wheel notch with Control held.</summary>
    public const int ControlStep = 5;

    private const int SecondsPerMinute = 60;

    // A frame's start read back lands in its second despite the rounding of the rate's products.
    private const double Tolerance = 1e-4;

    private readonly TextBox _minutes = Field();
    private readonly TextBox _seconds = Field();
    private readonly TextBox _frame = Field();
    private readonly Label _minutesColon = Colon();
    private readonly Label _secondsColon = Colon();
    private double? _frameRate;
    private bool _laidOut;
    private int _wheelRest;

    public FrameField()
    {
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.WrapContents = false;
        this.Margin = Padding.Empty;
        this.Anchor = AnchorStyles.Left;
        this.Controls.AddRange([this._minutes, this._minutesColon, this._seconds, this._secondsColon, this._frame]);
        foreach (var field in this.Fields)
        {
            field.KeyDown += (_, e) => this.OnFieldKey(field, e);
            field.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
            field.MouseWheel += (_, e) => this.OnFieldWheel(field, e);
            field.Leave += (_, _) => this.Commit();
        }

        this.SetFrameRate(null);
    }

    /// <summary>Raised when the user changes the value, never when it is set by code.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>The frame shown, from 0.</summary>
    public int Value { get; private set; }

    public int Minimum { get; private set; }

    public int Maximum { get; private set; }

    /// <summary>A focused field keeps the editing keys for its own text (RULES.md § Undo History).</summary>
    public bool IsEditing => this.Fields.Any(f => f.Focused);

    private IEnumerable<TextBox> Fields => [this._minutes, this._seconds, this._frame];

    /// <summary>
    /// A video at <paramref name="frameRate"/> frames per second: minutes, seconds and frame; <c>null</c>
    /// for a GIF — or pages — numbered from 1 in one field.
    /// </summary>
    public void SetFrameRate(double? frameRate)
    {
        if (this._laidOut && frameRate == this._frameRate)
        {
            return;
        }

        this._laidOut = true;
        this._frameRate = frameRate;
        bool timed = frameRate is not null;
        this._minutes.Visible = timed;
        this._minutesColon.Visible = timed;
        this._seconds.Visible = timed;
        this._secondsColon.Visible = timed;
        this._minutes.Width = this.FieldWidth("000");
        this._seconds.Width = this.FieldWidth("00");
        this._frame.Width = this.FieldWidth(timed ? (frameRate > 100 ? "000" : "00") : "00000");
        this.Display();
    }

    /// <summary>
    /// Shows <paramref name="value"/> within <paramref name="minimum"/> and <paramref name="maximum"/>,
    /// raising nothing; a text being typed is left alone while the value stays.
    /// </summary>
    public void Set(int value, int minimum, int maximum)
    {
        this.Minimum = minimum;
        this.Maximum = Math.Max(minimum, maximum);
        value = Math.Clamp(value, this.Minimum, this.Maximum);
        bool changed = value != this.Value;
        this.Value = value;
        if (changed || !this.IsEditing)
        {
            this.Display();
        }
    }

    private static TextBox Field() => new()
    {
        TextAlign = HorizontalAlignment.Right,
        Margin = new Padding(0, 3, 0, 3),
        Anchor = AnchorStyles.Left,
    };

    private static Label Colon() => new()
    {
        Text = ":",
        AutoSize = true,
        Margin = Padding.Empty,
        Anchor = AnchorStyles.Left,
    };

    private int FieldWidth(string widest) => TextRenderer.MeasureText(widest, this._frame.Font).Width + this.LogicalToDeviceUnits(10);

    /// <summary>The first frame starting in second <paramref name="second"/>.</summary>
    private int FirstOf(int second) => (int)Math.Ceiling(second * this._frameRate!.Value - Tolerance);

    private int SecondOf(int frame) => (int)Math.Floor((frame + Tolerance) / this._frameRate!.Value);

    /// <summary>The frames starting in second <paramref name="second"/>: the rate, give or take one for a fractional rate.</summary>
    private int FramesIn(int second) => this.FirstOf(second + 1) - this.FirstOf(second);

    private void Display()
    {
        if (this._frameRate is null)
        {
            this._frame.Text = (this.Value + 1).ToString();
            return;
        }

        int second = this.SecondOf(this.Value);
        this._minutes.Text = (second / SecondsPerMinute).ToString();
        this._seconds.Text = (second % SecondsPerMinute).ToString("00");
        this._frame.Text = (this.Value - this.FirstOf(second)).ToString("00");
    }

    /// <summary>The frame the fields' text gives; <c>null</c> when a field is empty or out of its range.</summary>
    private int? Typed()
    {
        if (!int.TryParse(this._frame.Text, out int frame))
        {
            return null;
        }

        if (this._frameRate is null)
        {
            return frame >= 1 ? frame - 1 : null;
        }

        if (!int.TryParse(this._minutes.Text, out int minutes) || !int.TryParse(this._seconds.Text, out int seconds)
            || seconds >= SecondsPerMinute)
        {
            return null;
        }

        int second = minutes * SecondsPerMinute + seconds;
        return frame < this.FramesIn(second) ? this.FirstOf(second) + frame : null;
    }

    /// <summary>The typed value applied, clamped to the range; an invalid one put back.</summary>
    private void Commit()
    {
        if (this.Typed() is { } typed)
        {
            this.Change(typed);
        }

        this.Display();
    }

    /// <summary><paramref name="steps"/> of the unit of <paramref name="field"/>, carrying over into the others.</summary>
    private void Step(TextBox field, int steps)
    {
        this.Commit();
        if (this._frameRate is null || field == this._frame)
        {
            this.Change(this.Value + steps);
        }
        else
        {
            int second = this.SecondOf(this.Value);
            int frame = this.Value - this.FirstOf(second);
            int target = Math.Max(0, second + steps * (field == this._minutes ? SecondsPerMinute : 1));
            this.Change(this.FirstOf(target) + Math.Min(frame, this.FramesIn(target) - 1));
        }

        this.Display();
        field.SelectAll();
    }

    private void Change(int value)
    {
        value = Math.Clamp(value, this.Minimum, this.Maximum);
        if (value != this.Value)
        {
            this.Value = value;
            this.ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFieldKey(TextBox field, KeyEventArgs e)
    {
        int step = e.Control ? ControlStep : 1;
        switch (e.KeyCode)
        {
            case Keys.Up:
                this.Step(field, step);
                break;
            case Keys.Down:
                this.Step(field, -step);
                break;
            case Keys.Enter:
                this.Commit();
                field.SelectAll();
                break;
            case Keys.Escape:
                this.Display();
                field.SelectAll();
                break;
            default:
                return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>The wheel acts as ↑ / ↓, one step per notch — the deltas of a free-spinning wheel accumulated.</summary>
    private void OnFieldWheel(TextBox field, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        int notch = SystemInformation.MouseWheelScrollDelta;
        this._wheelRest += e.Delta;
        int notches = this._wheelRest / notch;
        this._wheelRest -= notches * notch;
        if (notches != 0)
        {
            this.Step(field, notches * ((ModifierKeys & Keys.Control) != 0 ? ControlStep : 1));
        }
    }
}
