namespace ImageGridFusion.UI;

/// <summary>
/// The steps of the wheel with Control held: every notch moves a value onto the next multiple of a
/// step — the cell zoom and the option sliders alike. See workfiles/20260926-ctrl-wheel-5-percent-step.md.
/// </summary>
internal static class WheelSteps
{
    /// <summary>The step of a value read as a percentage: the zoom, an opacity, a volume…</summary>
    public const double Percent = 5;

    public const int WM_MOUSEWHEEL = 0x020A;
    private const int MK_CONTROL = 0x0008;

    // A value a hair off a multiple (104.9999 for 105) counts as on it, so a notch never moves it by a hair.
    private const double Tolerance = 1e-3;

    /// <summary>
    /// The multiple of <paramref name="step"/> reached from <paramref name="value"/> after
    /// <paramref name="notches"/> (up when positive): from a multiple, one step per notch; from
    /// between two, the first notch stops on the next one in its direction (103 → 105 up, → 100 down).
    /// </summary>
    public static double Snap(double value, double step, int notches)
    {
        double units = value / step;
        double first = notches > 0 ? Math.Floor(units + Tolerance) : Math.Ceiling(units - Tolerance);
        return (first + notches) * step;
    }

    /// <summary>
    /// <paramref name="target"/> for a control stepping by whole units, moved at least one unit per
    /// notch from <paramref name="value"/> — where the step is finer than a unit — and within its range.
    /// </summary>
    public static int Within(int value, int target, int notches, int minimum, int maximum)
    {
        if (Math.Abs(target - value) < Math.Abs(notches))
        {
            target = value + notches;
        }

        return Math.Clamp(target, minimum, maximum);
    }

    /// <summary>Whether a wheel message came with Control held: the message's own flag, set by Windows with the event.</summary>
    public static bool WithControl(Message m) => ((int)(long)m.WParam & MK_CONTROL) != 0;
}
