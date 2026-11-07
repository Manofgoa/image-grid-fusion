using ImageGridFusion.Web;

namespace ImageGridFusion.Tests.Web;

// Save… on a downloaded content (workfiles/20261107-save-downloaded-content.md).
[TestClass]
public sealed class DownloadedContentTests
{
    private static readonly string[] Folders = [@"C:\Temp\ImageGridFusion\url-downloads", @"C:\Temp\ImageGridFusion\giphy", @"C:\App\giphy-favorites\"];

    [TestMethod]
    public void AFileDirectlyInADownloadFolderIsDownloadedContent()
    {
        Assert.IsTrue(DownloadedContent.Holds(@"C:\Temp\ImageGridFusion\url-downloads\clip-720p.mp4", Folders));
        Assert.IsTrue(DownloadedContent.Holds(@"C:\Temp\ImageGridFusion\giphy\dance-giphy-abc.mp4", Folders));
        Assert.IsTrue(DownloadedContent.Holds(@"c:\app\GIPHY-FAVORITES\dance-giphy-abc.gif", Folders));
    }

    [TestMethod]
    public void AFileElsewhereIsNot()
    {
        Assert.IsFalse(DownloadedContent.Holds(@"D:\Pictures\clip.mp4", Folders));
        Assert.IsFalse(DownloadedContent.Holds(@"C:\Temp\ImageGridFusion\url-downloads\sub\clip.mp4", Folders));
        Assert.IsFalse(DownloadedContent.Holds(@"C:\Temp\ImageGridFusion\url-downloads-old\clip.mp4", Folders));
        Assert.IsFalse(DownloadedContent.Holds(null, Folders));
        Assert.IsFalse(DownloadedContent.Holds("", Folders));
    }

    [TestMethod]
    public void TheInitialFolderIsTheBrowsedOne()
    {
        string folder = Directory.CreateTempSubdirectory("igf-save-").FullName;
        try
        {
            Assert.AreEqual(folder, DownloadedContent.InitialFolder(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void AFolderGoneGivesWayToItsNearestParentStillThere()
    {
        string folder = Directory.CreateTempSubdirectory("igf-save-").FullName;
        try
        {
            Assert.AreEqual(folder, DownloadedContent.InitialFolder(Path.Combine(folder, "gone", "deeper")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void NoBrowsedFolderGivesThePictures()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Assert.AreEqual(pictures, DownloadedContent.InitialFolder(null));
        Assert.AreEqual(pictures, DownloadedContent.InitialFolder(""));
    }

    [TestMethod]
    public void TheFilterOffersTheFilesTypeFirst()
    {
        string filter = DownloadedContent.Filter(@"C:\Temp\clip.mp4");
        StringAssert.StartsWith(filter.Split('|')[1], "*.mp4");
        Assert.AreEqual("*.*", filter.Split('|')[^1]);
    }
}
