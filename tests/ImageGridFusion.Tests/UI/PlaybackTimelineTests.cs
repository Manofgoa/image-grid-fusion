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

    [TestMethod]
    public void EndGrowsOnlyAtTheEdge()
    {
        // The track ends at 210; a zone of 32 px starts at 178.
        Assert.AreEqual(0, PlaybackTimeline.GrowthRate(100, Track, 32));
        Assert.AreEqual(0, PlaybackTimeline.GrowthRate(178, Track, 32));
        Assert.AreEqual(0.025, PlaybackTimeline.GrowthRate(194, Track, 32), 1e-9);
        Assert.AreEqual(0.1, PlaybackTimeline.GrowthRate(210, Track, 32), 1e-9);
    }

    [TestMethod]
    public void EndGrowsFasterPastTheEdgeUpToACap()
    {
        Assert.AreEqual(0.4, PlaybackTimeline.GrowthRate(242, Track, 32), 1e-9);
        Assert.AreEqual(0.9, PlaybackTimeline.GrowthRate(274, Track, 32), 1e-9);
        Assert.AreEqual(0.9, PlaybackTimeline.GrowthRate(900, Track, 32), 1e-9);
    }

    [TestMethod]
    public void EndShrinksOnlyLeftOfTheLoopTick()
    {
        // The loop's tick at 150, zones of 32 px.
        Assert.AreEqual(0, PlaybackTimeline.ShrinkRate(150, 150, 32));
        Assert.AreEqual(0, PlaybackTimeline.ShrinkRate(180, 150, 32));
        Assert.AreEqual(0.1, PlaybackTimeline.ShrinkRate(118, 150, 32), 1e-9);
        Assert.AreEqual(0.9, PlaybackTimeline.ShrinkRate(0, 150, 32), 1e-9);
    }

    [TestMethod]
    public void SpanFollowsTheGrowingEndUnderThePointer()
    {
        var span = TimeSpan.FromSeconds(20);

        // Pointer at the edge: the end on the right edge.
        Assert.AreEqual(TimeSpan.FromSeconds(30), PlaybackTimeline.SpanFor(TimeSpan.FromSeconds(30), 210, Track, span));

        // Past the edge too.
        Assert.AreEqual(TimeSpan.FromSeconds(30), PlaybackTimeline.SpanFor(TimeSpan.FromSeconds(30), 400, Track, span));

        // Halfway, the span doubles the end; never below the span it had.
        Assert.AreEqual(TimeSpan.FromSeconds(60), PlaybackTimeline.SpanFor(TimeSpan.FromSeconds(30), 110, Track, span));
        Assert.AreEqual(span, PlaybackTimeline.SpanFor(TimeSpan.FromSeconds(5), 210, Track, span));
    }
}
