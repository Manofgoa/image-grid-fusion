namespace ImageGridFusion.Composition;

/// <summary>
/// The frames effect of an animated image (video, animated GIF, PDF of several pages, long text):
/// where it starts playing, or, frozen, the frame it shows — in the preview and in the exports. A
/// video or an animated GIF may also be trimmed: only its frames from <see cref="First"/> to
/// <see cref="Last"/> play (workfiles/20261008-video-trim.md).
/// </summary>
public sealed record FramesEffect
{
    /// <summary>From the beginning, playing, untrimmed.</summary>
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

    /// <summary>The frame at <see cref="Position"/> among <paramref name="count"/>.</summary>
    public int PageOf(int count) => count <= 1 ? 0 : (int)Math.Round(Position * (count - 1));

    /// <summary>At frame <paramref name="page"/> of <paramref name="count"/>.</summary>
    public FramesEffect AtPage(int page, int count) => this with { Position = count <= 1 ? 0 : Math.Clamp(page / (double)(count - 1), 0, 1) };

    public FramesEffect WithFrozen(bool frozen) => this with { Frozen = frozen };

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
}
