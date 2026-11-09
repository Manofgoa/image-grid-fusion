using System.Drawing;
using System.Globalization;
using ImageGridFusion.UI;

namespace ImageGridFusion.Tests.UI;

// The URL zone's caption of a ready media (workfiles/20261107-content-download-improvements.md § Media Information).
[TestClass]
public sealed class UrlZoneCaptionTests
{
    private const long TwelvePointFourMegabytes = 13_002_342;

    [TestInitialize]
    public void UseEnglish() => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    public void TheDetailsAreTheSizeTheDimensionsAndTheDuration()
    {
        Assert.AreEqual("12.4 MB · 1920×1080 · 3:33", UrlCaption.Details(TwelvePointFourMegabytes, new Size(1920, 1080), TimeSpan.FromSeconds(213)));
    }

    [TestMethod]
    public void AnUnknownPartIsLeftOut()
    {
        Assert.AreEqual("12.4 MB", UrlCaption.Details(TwelvePointFourMegabytes, null, null));
        Assert.AreEqual("12.4 MB · 640×480", UrlCaption.Details(TwelvePointFourMegabytes, new Size(640, 480), TimeSpan.Zero));
        Assert.AreEqual("12.4 MB · 0:04", UrlCaption.Details(TwelvePointFourMegabytes, Size.Empty, TimeSpan.FromSeconds(4)));
    }

    [TestMethod]
    public void ADurationIsWrittenAsAPlayerDoes()
    {
        Assert.AreEqual("0:04", UrlCaption.Duration(TimeSpan.FromSeconds(4)));
        Assert.AreEqual("3:33", UrlCaption.Duration(TimeSpan.FromSeconds(213)));
        Assert.AreEqual("1:02:05", UrlCaption.Duration(TimeSpan.FromSeconds(3725)));
        Assert.AreEqual("0.8 s", UrlCaption.Duration(TimeSpan.FromSeconds(0.8)));
    }

    [TestMethod]
    public void TheNameIsCutTheDetailsNever()
    {
        static string Compose(string name) => $"{name} · 12.4 MB";
        static int Width(string text) => text.Length;

        Assert.AreEqual("A long title · 12.4 MB", UrlCaption.Fit("A long title", Compose, Width, 100));
        string fitted = UrlCaption.Fit("A long title", Compose, Width, 16);
        Assert.AreEqual("A lon… · 12.4 MB", fitted);
        Assert.AreEqual("… · 12.4 MB", UrlCaption.Fit("A long title", Compose, Width, 3));
    }
}
