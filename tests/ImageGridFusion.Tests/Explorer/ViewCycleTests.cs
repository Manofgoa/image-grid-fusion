using ImageGridFusion.Explorer;

namespace ImageGridFusion.Tests.Explorer;

// The Ctrl+F view cycle (workfiles/20261110-ctrl-f-cycle-starts-at-folders.md).
[TestClass]
public sealed class ViewCycleTests
{
    private static bool All(ExplorerMode mode) => true;

    [TestMethod]
    public void ACycleStartsAtTheFolderViewFromEveryView()
    {
        foreach (var current in Enum.GetValues<ExplorerMode>())
        {
            Assert.AreEqual(ExplorerMode.Folders, ViewCycle.Next(current, 1, cycling: false, All), current.ToString());
        }
    }

    [TestMethod]
    public void ACycleStartingOnTheFolderViewStaysThere()
    {
        Assert.AreEqual(ExplorerMode.Folders, ViewCycle.Next(ExplorerMode.Folders, 1, cycling: false, All));
    }

    [TestMethod]
    public void CtrlShiftFStartsACycleAtTheFolderViewToo()
    {
        Assert.AreEqual(ExplorerMode.Folders, ViewCycle.Next(ExplorerMode.Favorites, -1, cycling: false, All));
    }

    [TestMethod]
    public void ARunningCycleGoesOnInTheViewsOrder()
    {
        Assert.AreEqual(ExplorerMode.People, ViewCycle.Next(ExplorerMode.Folders, 1, cycling: true, All));
        Assert.AreEqual(ExplorerMode.Favorites, ViewCycle.Next(ExplorerMode.People, 1, cycling: true, All));
        Assert.AreEqual(ExplorerMode.Folders, ViewCycle.Next(ExplorerMode.GiphyStickers, 1, cycling: true, All));
    }

    [TestMethod]
    public void ARunningCycleGoesBackWithCtrlShiftF()
    {
        Assert.AreEqual(ExplorerMode.GiphyStickers, ViewCycle.Next(ExplorerMode.Folders, -1, cycling: true, All));
        Assert.AreEqual(ExplorerMode.People, ViewCycle.Next(ExplorerMode.Favorites, -1, cycling: true, All));
    }

    [TestMethod]
    public void ARunningCycleSkipsTheViewsNotAvailable()
    {
        static bool NoPeopleNoGiphy(ExplorerMode mode) => mode is ExplorerMode.Folders or ExplorerMode.Favorites;

        Assert.AreEqual(ExplorerMode.Favorites, ViewCycle.Next(ExplorerMode.Folders, 1, cycling: true, NoPeopleNoGiphy));
        Assert.AreEqual(ExplorerMode.Folders, ViewCycle.Next(ExplorerMode.Favorites, 1, cycling: true, NoPeopleNoGiphy));
    }
}
