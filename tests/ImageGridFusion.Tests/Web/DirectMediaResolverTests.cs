using ImageGridFusion.Web;

namespace ImageGridFusion.Tests.Web;

// A direct link's media named after its telling name (workfiles/20261107-content-download-improvements.md § File Names).
[TestClass]
public sealed class DirectMediaResolverTests
{
    [TestMethod]
    public void ATellingUrlNameIsKept()
    {
        var url = new Uri("https://example.com/media/beach-sunset.jpg?size=large");
        Assert.AreEqual("beach-sunset-" + UrlDownloads.Tag(url), DirectMediaResolver.StemOf(url, "\"Other name.jpg\""));
    }

    [TestMethod]
    public void TheServersNameComesNext()
    {
        var url = new Uri("https://cdn.example.com/a3f9c0d1e2b4.jpg");
        Assert.AreEqual("Holiday Photo-" + UrlDownloads.Tag(url), DirectMediaResolver.StemOf(url, "\"Holiday Photo.jpg\""));
    }

    [TestMethod]
    public void TheHostComesLast()
    {
        var url = new Uri("https://cdn.example.com/a3f9c0d1e2b4.jpg");
        Assert.AreEqual("cdn.example.com-" + UrlDownloads.Tag(url), DirectMediaResolver.StemOf(url, null));
    }
}
