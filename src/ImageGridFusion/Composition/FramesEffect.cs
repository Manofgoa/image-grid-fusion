using System.Globalization;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>
/// The frames effect of an animated image (video, animated GIF, PDF of several pages, long text):
/// where it starts playing, or, frozen, the frame it shows — in the preview and in the exports. A
/// video or an animated GIF may also be trimmed: only its frames from <see cref="First"/> to
/// <see cref="Last"/> play (workfiles/20261008-video-trim.md). Every animated image plays at a
/// <see cref="Speed"/>, a multiple of its own pace (workfiles/20260926-frames-speed.md).
/// </summary>
public sealed record FramesEffect
{
    /// <summary>The slowest speed: a quarter of the content's own pace.</summary>
    public const double MinSpeed = 0.25;

    /// <summary>The fastest speed: four times the content's own pace.</summary>
    public const double MaxSpeed = 4;

    /// <summary>The speed's step, from <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>.</summary>
    public const double SpeedStep = 0.25;

    /// <summary>From the beginning, playing, untrimmed, at its own pace.</summary>
    public static readonly FramesEffect Default = new();

    /// <summary>
    /// Place along the frames, from 0 (the first) to 1 (the last): a share rather than a page, so it
    /// stays about where it was when a text is laid out again on more or fewer pages.
    /// </summary>
    public double Position { get; private init; }

    /// <summary>Shows the frame at <see cref="Position"/>, still, instead of playing from it.</summary>
    public bool Frozen { get; private init; }

    /// <summary>First frame of the trim, from 0.</summary>
    public int First { get; private init; }

    /// <summary>Last frame of the trim, played too; <c>null</c> for the last frame of the content.</summary>
    public int? Last { get; private init; }

    /// <summary>
    /// Multiple of the content's own pace it plays at — a video's frame rate, a GIF's delays, the step of
    /// a PDF or a text (<see cref="Animation.StepDuration"/>) — its sound with it: 1 by default.
    /// </summary>
    public double Speed { get; private init; } = 1;

    /// <summary>The frame at <see cref="Position"/> among <paramref name="count"/>.</summary>
    public int PageOf(int count) => count <= 1 ? 0 : (int)Math.Round(Position * (count - 1));

    /// <summary>At frame <paramref name="page"/> of <paramref name="count"/>.</summary>
    public FramesEffect AtPage(int page, int count) => this with { Position = count <= 1 ? 0 : Math.Clamp(page / (double)(count - 1), 0, 1) };

    public FramesEffect WithFrozen(bool frozen) => this with { Frozen = frozen };

    /// <summary>At <paramref name="speed"/>, brought onto a step of the range.</summary>
    public FramesEffect WithSpeed(double speed) => this with { Speed = SpeedOf(speed) };

    /// <summary><paramref name="speed"/> brought onto a step of the range.</summary>
    public static double SpeedOf(double speed) =>
        double.IsFinite(speed) ? Math.Clamp(Math.Round(speed / SpeedStep) * SpeedStep, MinSpeed, MaxSpeed) : Default.Speed;

    /// <summary><paramref name="speed"/> as the options and the presets write it: <c>×1</c>, <c>×0.25</c>, <c>×1.5</c>.</summary>
    public static string SpeedText(double speed) => "×" + speed.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The trim's first frame among <paramref name="count"/>.</summary>
    public int FirstOf(int count) => Math.Clamp(this.First, 0, Math.Max(0, count - 1));

    /// <summary>The trim's last frame among <paramref name="count"/>, never before its first.</summary>
    public int LastOf(int count) => Math.Clamp(this.Last ?? count - 1, this.FirstOf(count), Math.Max(0, count - 1));

    /// <summary>
    /// Trimmed to the frames <paramref name="first"/> to <paramref name="last"/> of <paramref name="count"/>,
    /// one frame at least; the starting point brought inside them when they leave it out.
    /// </summary>
    public FramesEffect WithTrim(int first, int last, int count)
    {
        first = Math.Clamp(first, 0, Math.Max(0, count - 1));
        last = Math.Clamp(last, first, Math.Max(0, count - 1));
        var trimmed = this with { First = first, Last = last >= count - 1 ? null : last };
        int start = this.PageOf(count);
        return start < first || start > last ? trimmed.AtPage(Math.Clamp(start, first, last), count) : trimmed;
    }

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>): the trim in frames, clamped to the file when read.</summary>
    internal JsonObject ToJson() => new()
    {
        ["position"] = this.Position,
        ["frozen"] = this.Frozen,
        ["first"] = this.First,
        ["last"] = this.Last,
        ["speed"] = this.Speed,
    };

    /// <summary>The settings <see cref="ToJson"/> wrote.</summary>
    internal static FramesEffect FromJson(JsonObject json)
    {
        int first = Math.Max(0, PresetJson.Int(json, "first", Default.First));
        return new FramesEffect
        {
            Position = Math.Clamp(PresetJson.Double(json, "position", Default.Position), 0, 1),
            Frozen = PresetJson.Bool(json, "frozen", Default.Frozen),
            First = first,
            Last = PresetJson.NullableInt(json, "last") is { } last ? Math.Max(first, last) : null,
            Speed = SpeedOf(PresetJson.Double(json, "speed", Default.Speed)),
        };
    }
}
