namespace ImageGridFusion.UI;

/// <summary>
/// The steps of the wheel: every notch moves a value onto the next multiple of a step — the option
/// sliders with Control held (workfiles/20260926-ctrl-wheel-5-percent-step.md), the zoom with or
/// without it (workfiles/20261006-wheel-zoom-step.md).
/// </summary>
internal static class WheelSteps
{
    /// <summary>The Control + wheel step of an option slider read as a percentage: an opacity, a volume…</summary>
    public const double Percent = 5;

    /// <summary>The zoom's wheel step, in percentage points; <see cref="FineZoomStep"/> with Control held.</summary>
    public const double ZoomStep = 5;

    /// <summary>The zoom's wheel step with Control held, in percentage points.</summary>
    public const double FineZoomStep = 1;

    /// <summary>The zoom, in percent, above which its wheel steps are <see cref="CoarseZoomFactor"/> times larger.</summary>
    public const double CoarseZoomFrom = 200;

    /// <summary>How much larger the zoom's wheel steps are above <see cref="CoarseZoomFrom"/>: 25 points, 5 with Control.</summary>
    public const double CoarseZoomFactor = 5;

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
    /// The zoom, in percent, reached from <paramref name="percent"/> after <paramref name="notches"/>
    /// of the wheel (up when positive), <paramref name="fine"/> with Control held: each notch snaps
    /// onto the next multiple of its step — <see cref="ZoomStep"/> or <see cref="FineZoomStep"/>,
    /// <see cref="CoarseZoomFactor"/> times larger when it moves above <see cref="CoarseZoomFrom"/>
    /// (195 → 200 → 225 up, 225 → 200 → 195 down). Not clamped to the zoom's bounds.
    /// </summary>
    public static double Zoom(double percent, int notches, bool fine)
    {
        int direction = Math.Sign(notches);
        for (int notch = 0; notch < Math.Abs(notches); notch++)
        {
            // The range the notch moves into: up from the boundary, or down from above it, is the coarse one.
            bool coarse = direction > 0 ? percent >= CoarseZoomFrom - Tolerance : percent > CoarseZoomFrom + Tolerance;
            double step = (fine ? FineZoomStep : ZoomStep) * (coarse ? CoarseZoomFactor : 1);
            percent = Snap(percent, step, direction);
        }

        return percent;
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
