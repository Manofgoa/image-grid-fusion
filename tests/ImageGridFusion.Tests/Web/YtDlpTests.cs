using ImageGridFusion.Web;

namespace ImageGridFusion.Tests.Web;

// yt-dlp's media named after its title (workfiles/20261107-content-download-improvements.md § File Names).
[TestClass]
public sealed class YtDlpTests
{
    [TestMethod]
    public void TheFileNameCarriesTheTitleThenTheIdThenTheHeight()
    {
        string template = YtDlp.OutputTemplate;
        int title = template.IndexOf("%(title)", StringComparison.Ordinal);
        int id = template.IndexOf("%(id)s", StringComparison.Ordinal);
        int height = template.IndexOf("%(height", StringComparison.Ordinal);
        Assert.IsTrue(title == 0 && id > title && height > id, template);
    }
}
