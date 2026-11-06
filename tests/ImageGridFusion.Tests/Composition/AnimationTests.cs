using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>The video length with a range (RULES.md § Video Length, workfiles/20261106-video-range.md).</summary>
[TestClass]
public sealed class AnimationTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [TestMethod]
    public void RangeGivesTheVideoItsLength()
    {
        Assert.AreEqual(S(25), Animation.VideoLength(S(4), null, VideoRange.Of(S(5), S(30))));
    }

    [TestMethod]
    public void WithoutRangeTheGridLoopOrTheSoundtrackPart()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90)).WithTrim(S(10), S(40));

        Assert.AreEqual(S(4), Animation.VideoLength(S(4), null));
        Assert.AreEqual(S(4), Animation.VideoLength(S(4), soundtrack));
        Assert.AreEqual(S(30), Animation.VideoLength(TimeSpan.Zero, soundtrack));
    }

    [TestMethod]
    public void StillGridIgnoresTheRange()
    {
        var range = VideoRange.Of(S(1), S(8));

        Assert.AreEqual(TimeSpan.Zero, Animation.VideoLength(TimeSpan.Zero, null, range));
        Assert.AreEqual(TimeSpan.Zero, Animation.VideoStart(TimeSpan.Zero, range));
    }

    [TestMethod]
    public void VideoStartsAtTheRangeStart()
    {
        Assert.AreEqual(S(3), Animation.VideoStart(S(10), VideoRange.Of(S(3), S(12))));
        Assert.AreEqual(TimeSpan.Zero, Animation.VideoStart(S(10), null));
    }
}
