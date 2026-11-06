using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>The video range on the grid's timeline (workfiles/20261106-video-range.md § Video Range).</summary>
[TestClass]
public sealed class VideoRangeTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [TestMethod]
    public void EndMayLiePastTheGridLoop()
    {
        var range = VideoRange.Of(S(2), S(30));

        Assert.AreEqual(S(2), range.Start);
        Assert.AreEqual(S(30), range.End);
        Assert.AreEqual(S(28), range.Length);
    }

    [TestMethod]
    public void StartNeverBeforeZeroAndEndPushedOneFrameAfterIt()
    {
        var range = VideoRange.Of(S(-1), S(-5));

        Assert.AreEqual(TimeSpan.Zero, range.Start);
        Assert.AreEqual(VideoRange.MinLength, range.End);
    }

    [TestMethod]
    public void MovingOneHandleKeepsTheOther()
    {
        var range = VideoRange.Of(S(1), S(5));

        Assert.AreEqual(VideoRange.Of(S(3), S(5)), range.WithStart(S(3)));
        Assert.AreEqual(VideoRange.Of(S(5) - VideoRange.MinLength, S(5)), range.WithStart(S(9)));
        Assert.AreEqual(VideoRange.Of(S(1), S(12)), range.WithEnd(S(12)));
        Assert.AreEqual(VideoRange.Of(S(1), S(1) + VideoRange.MinLength), range.WithEnd(S(0)));
    }

    [TestMethod]
    public void KeptRangeStaysWhileItsStartIsInTheLoop()
    {
        var range = VideoRange.Of(S(2), S(30));

        Assert.AreSame(range, range.KeptIn(S(10)));
    }

    [TestMethod]
    public void KeptRangeHasItsStartBroughtIntoAShorterLoop()
    {
        var kept = VideoRange.Of(S(8), S(30)).KeptIn(S(4));

        Assert.AreEqual(S(4) - VideoRange.MinLength, kept.Start);
        Assert.AreEqual(S(30), kept.End);
    }

    [TestMethod]
    public void WholeLoopIsTold()
    {
        Assert.IsTrue(VideoRange.Whole(S(6)).IsWhole(S(6)));
        Assert.IsFalse(VideoRange.Of(S(1), S(6)).IsWhole(S(6)));
        Assert.IsFalse(VideoRange.Whole(S(6)).IsWhole(S(7)));
    }

    [TestMethod]
    public void FitToSoundtrackPlaysItsPartOnce()
    {
        var soundtrack = new Soundtrack("music.mp3", S(180)).WithTrim(S(20), S(80));

        Assert.AreEqual(VideoRange.Of(TimeSpan.Zero, S(60)), VideoRange.FitTo(soundtrack));
    }

    [TestMethod]
    public void FitToSoundtrackMayBeShorterThanTheGridLoop()
    {
        var soundtrack = new Soundtrack("jingle.mp3", S(3));

        Assert.AreEqual(S(3), VideoRange.FitTo(soundtrack).Length);
        Assert.AreEqual(S(3), Animation.VideoLength(S(10), soundtrack, VideoRange.FitTo(soundtrack)));
    }

    [TestMethod]
    public void TrackSpansTheLoopOrTheEndPlusOneLoop()
    {
        Assert.AreEqual(S(10), VideoRange.Extent(S(10), null));
        Assert.AreEqual(S(10), VideoRange.Extent(S(10), VideoRange.Of(S(2), S(6))));
        Assert.AreEqual(S(10), VideoRange.Extent(S(10), VideoRange.Of(S(2), S(10))));
        Assert.AreEqual(S(35), VideoRange.Extent(S(10), VideoRange.Of(S(2), S(25))));
    }
}
