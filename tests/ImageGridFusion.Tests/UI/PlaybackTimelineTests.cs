using ImageGridFusion.UI;

namespace ImageGridFusion.Tests.UI;

/// <summary>The timeline's x ↔ time mapping over a span longer than the grid's loop (workfiles/20261106-video-range.md § Timeline).</summary>
[TestClass]
public sealed class PlaybackTimelineTests
{
    private static readonly Rectangle Track = new(10, 0, 200, 24);

    [TestMethod]
    public void TimeMapsLinearlyOverTheSpan()
    {
        var span = TimeSpan.FromSeconds(20);

        Assert.AreEqual(10, PlaybackTimeline.XOf(TimeSpan.Zero, Track, span));
        Assert.AreEqual(110, PlaybackTimeline.XOf(TimeSpan.FromSeconds(10), Track, span));
        Assert.AreEqual(210, PlaybackTimeline.XOf(span, Track, span));
        Assert.AreEqual(TimeSpan.FromSeconds(10), PlaybackTimeline.TimeAt(110, Track, span));
    }

    [TestMethod]
    public void TimesPastTheSpanStayOnTheTrack()
    {
        Assert.AreEqual(210, PlaybackTimeline.XOf(TimeSpan.FromSeconds(50), Track, TimeSpan.FromSeconds(20)));
    }

    [TestMethod]
    public void PointerPastTheRightEdgeGoesPastTheSpan()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), PlaybackTimeline.TimeAt(310, Track, TimeSpan.FromSeconds(20)));
    }
}
