using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>The soundtrack's part and its loop (workfiles/20261106-video-range.md § Soundtrack Trim).</summary>
[TestClass]
public sealed class SoundtrackTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [TestMethod]
    public void NewSoundtrackPlaysTheWholeFileLooped()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90));

        Assert.AreEqual(TimeSpan.Zero, soundtrack.From);
        Assert.AreEqual(S(90), soundtrack.To);
        Assert.IsTrue(soundtrack.Loops);
        Assert.IsFalse(soundtrack.IsTrimmed);
    }

    [TestMethod]
    public void TrimStaysInTheFileWithAMinimumPart()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90));

        var inside = soundtrack.WithTrim(S(10), S(40));
        Assert.AreEqual(S(10), inside.From);
        Assert.AreEqual(S(40), inside.To);
        Assert.AreEqual(S(30), inside.PlayedLength);

        var outside = soundtrack.WithTrim(S(-3), S(200));
        Assert.AreEqual(TimeSpan.Zero, outside.From);
        Assert.AreEqual(S(90), outside.To);

        var crossed = soundtrack.WithTrim(S(50), S(20));
        Assert.AreEqual(S(50), crossed.From);
        Assert.AreEqual(S(50) + Soundtrack.MinPart, crossed.To);

        var atTheEnd = soundtrack.WithTrim(S(95), S(95));
        Assert.AreEqual(S(90) - Soundtrack.MinPart, atTheEnd.From);
        Assert.AreEqual(S(90), atTheEnd.To);
    }

    [TestMethod]
    public void GridOfStillsLastsThePart()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90)).WithTrim(S(10), S(40));

        Assert.AreEqual(S(30), soundtrack.LoopIn(TimeSpan.Zero));
        Assert.AreEqual(S(6), soundtrack.LoopIn(S(6)));
    }

    [TestMethod]
    public void LoopedPartFollowsTheGridClock()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90)).WithTrim(S(10), S(40));

        Assert.AreEqual(S(10), soundtrack.FileTimeAt(TimeSpan.Zero));
        Assert.AreEqual(S(22), soundtrack.FileTimeAt(S(12)));
        Assert.AreEqual(S(15), soundtrack.FileTimeAt(S(35)));
    }

    [TestMethod]
    public void PartPlayedOnceIsSilentPastIt()
    {
        var soundtrack = new Soundtrack("music.mp3", S(90)).WithTrim(S(10), S(40)).WithLoops(false);

        Assert.AreEqual(S(39), soundtrack.FileTimeAt(S(29)));
        Assert.IsNull(soundtrack.FileTimeAt(S(30)));
        Assert.IsNull(soundtrack.FileTimeAt(S(75)));
    }
}
