using ImageGridFusion.Web;

namespace ImageGridFusion.Tests.Web;

// An X post's media named after its author and its text (workfiles/20261107-content-download-improvements.md § File Names).
[TestClass]
public sealed class XPostResolverTests
{
    [TestMethod]
    public void TheStemIsTheAuthorThenTheTextThenTheId()
    {
        Assert.AreEqual(
            "Jean Dupont - Le coucher de soleil ce soir [1234567890123456789]",
            XPostResolver.StemOf("Jean Dupont", "jdupont", "Le coucher de soleil\nce soir https://t.co/AbC123", 1234567890123456789));
    }

    [TestMethod]
    public void WithoutTextTheStemIsTheAuthorAndTheId()
    {
        Assert.AreEqual("Jean Dupont [42]", XPostResolver.StemOf("Jean Dupont", "jdupont", "https://t.co/AbC123", 42));
        Assert.AreEqual("Jean Dupont [42]", XPostResolver.StemOf("Jean Dupont", "jdupont", null, 42));
    }

    [TestMethod]
    public void WithoutANameTheHandleStands()
    {
        Assert.AreEqual("jdupont [42]", XPostResolver.StemOf(" ", "jdupont", null, 42));
        Assert.AreEqual("post [42]", XPostResolver.StemOf(null, null, null, 42));
    }

    [TestMethod]
    public void ALongTextIsCutAtAWordTheIdKept()
    {
        string text = string.Join(' ', Enumerable.Repeat("soleil", 30));
        string stem = XPostResolver.StemOf("Jean Dupont", "jdupont", text, 1234567890123456789);
        Assert.IsTrue(stem.Length <= UrlDownloads.MaxStemLength, stem);
        StringAssert.EndsWith(stem, "soleil [1234567890123456789]");
        StringAssert.StartsWith(stem, "Jean Dupont - soleil soleil");
    }
}
