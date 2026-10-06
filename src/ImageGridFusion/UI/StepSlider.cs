using System.ComponentModel;

namespace ImageGridFusion.UI;

/// <summary>
/// The slider of every option: a stock <see cref="TrackBar"/>, whose wheel with Control held moves
/// onto the next multiple of <see cref="ControlStep"/> instead of one unit — or does what
/// <see cref="ControlWheel"/> says, for a slider whose value is not the unit it shows — or, for one
/// stepping its own way with Control or not (the zoom), what <see cref="Wheel"/> says. See
/// workfiles/20260926-ctrl-wheel-5-percent-step.md, workfiles/20261006-wheel-zoom-step.md.
/// </summary>
internal sealed class StepSlider : TrackBar
{
    private bool _wheelWithControl;
    private int _wheelRest;

    /// <summary>The Control + wheel step, in the slider's own units.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ControlStep { get; init; } = (int)WheelSteps.Percent;

    /// <summary>Replaces the Control + wheel step, given the notches turned (up when positive).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<int>? ControlWheel { get; set; }

    /// <summary>
    /// Replaces the whole wheel, with Control held or not, given the notches turned (up when positive)
    /// and whether Control is held; wins over <see cref="ControlWheel"/>.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<int, bool>? Wheel { get; set; }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WheelSteps.WM_MOUSEWHEEL)
        {
            _wheelWithControl = WheelSteps.WithControl(m);
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// Without Control, the stock wheel; with it, one step per notch — the deltas of a free-spinning
    /// wheel accumulated. A <see cref="Wheel"/> takes every notch, with Control or not.
    /// </summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_wheelWithControl && this.Wheel is null)
        {
            _wheelRest = 0;
            base.OnMouseWheel(e);
            return;
        }

        // Handled, so neither the stock move nor the native control adds its own unit.
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        int notch = SystemInformation.MouseWheelScrollDelta;
        _wheelRest += e.Delta;
        int notches = _wheelRest / notch;
        _wheelRest -= notches * notch;
        if (notches == 0)
        {
            return;
        }

        if (this.Wheel is { } any)
        {
            any(notches, _wheelWithControl);
            return;
        }

        if (ControlWheel is { } wheel)
        {
            wheel(notches);
            return;
        }

        int target = (int)Math.Round(WheelSteps.Snap(Value, ControlStep, notches));
        Value = WheelSteps.Within(Value, target, notches, Minimum, Maximum);
    }
}
