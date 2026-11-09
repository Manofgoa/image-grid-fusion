using ImageGridFusion.Web;

namespace ImageGridFusion.Tests.Web;

// The URL downloads named after their source (workfiles/20261107-content-download-improvements.md § File Names).
[TestClass]
public sealed class UrlDownloadsTests
{
    [TestMethod]
    public void ASafeStemKeepsATitleReadable()
    {
        Assert.AreEqual("Rick Astley - Never Gonna Give You Up [dQw4w9WgXcQ]", UrlDownloads.SafeStem("Rick Astley - Never Gonna Give You Up [dQw4w9WgXcQ]"));
        Assert.AreEqual("a-b-c d", UrlDownloads.SafeStem("a/b:c d?"));
    }

    [TestMethod]
    public void ASafeStemIsCutAtItsMaximum()
    {
        Assert.AreEqual(UrlDownloads.MaxStemLength, UrlDownloads.SafeStem(new string('a', 100)).Length);
    }

    [TestMethod]
    public void ATellingNameHoldsAWord()
    {
        Assert.IsTrue(UrlDownloads.IsTelling("photo-beach"));
        Assert.IsTrue(UrlDownloads.IsTelling("Sunset_2024"));
    }

    [TestMethod]
    public void AnIdIsNoTellingName()
    {
        Assert.IsFalse(UrlDownloads.IsTelling("a3f9c0d1e2"));
        Assert.IsFalse(UrlDownloads.IsTelling("deadbeefcafe"));
        Assert.IsFalse(UrlDownloads.IsTelling("1234567890"));
        Assert.IsFalse(UrlDownloads.IsTelling("IMG_1234"));
        Assert.IsFalse(UrlDownloads.IsTelling("img"));
        Assert.IsFalse(UrlDownloads.IsTelling(""));
    }

    [TestMethod]
    public void WordsAreCutAtASpace()
    {
        Assert.AreEqual("the quick brown", UrlDownloads.Words("the  quick\nbrown fox", 17));
        Assert.AreEqual("short", UrlDownloads.Words(" short ", 10));
        Assert.AreEqual("", UrlDownloads.Words("anything", 0));
    }

    [TestMethod]
    public void AWordTooLongIsCutWithin()
    {
        Assert.AreEqual("abcdefgh", UrlDownloads.Words("abcdefghijkl", 8));
    }
}
