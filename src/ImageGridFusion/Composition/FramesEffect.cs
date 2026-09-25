namespace ImageGridFusion.Composition;

/// <summary>
/// The frames effect of an animated image (video, animated GIF, PDF of several pages, long text):
/// where it starts playing, or, frozen, the frame it shows — in the preview and in the exports.
/// </summary>
public sealed record FramesEffect
{
    /// <summary>From the beginning, playing.</summary>
    public static readonly FramesEffect Default = new();

    /// <summary>
    /// Place along the frames, from 0 (the first) to 1 (the last): a share rather than a page, so it
    /// stays about where it was when a text is laid out again on more or fewer pages.
    /// </summary>
    public double Position { get; private init; }

    /// <summary>Shows the frame at <see cref="Position"/>, still, instead of playing from it.</summary>
    public bool Frozen { get; private init; }

    /// <summary>The frame at <see cref="Position"/> among <paramref name="count"/>.</summary>
    public int PageOf(int count) => count <= 1 ? 0 : (int)Math.Round(Position * (count - 1));

    /// <summary>At frame <paramref name="page"/> of <paramref name="count"/>.</summary>
    public FramesEffect AtPage(int page, int count) => this with { Position = count <= 1 ? 0 : Math.Clamp(page / (double)(count - 1), 0, 1) };

    public FramesEffect WithFrozen(bool frozen) => this with { Frozen = frozen };
}
