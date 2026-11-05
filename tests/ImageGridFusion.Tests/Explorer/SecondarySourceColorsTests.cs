using ImageGridFusion.Explorer;

namespace ImageGridFusion.Tests.Explorer;

// The secondary sources' colors (workfiles/20261105-secondary-file-sources.md § Source Colors).
[TestClass]
public sealed class SecondarySourceColorsTests
{
    [TestMethod]
    public void NewSourcesTakeThePaletteInOrder()
    {
        var colored = SecondarySources.Colored([@"D:\A", @"D:\B", @"D:\C"], [], 5);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, colored.Select(s => s.Color).ToArray());
    }

    [TestMethod]
    public void RemovingASourceKeepsTheOthersColors()
    {
        var before = SecondarySources.Colored([@"D:\A", @"D:\B", @"D:\C"], [], 5);

        var after = SecondarySources.Colored([@"D:\A", @"D:\C"], before, 5);

        CollectionAssert.AreEqual(new[] { 0, 2 }, after.Select(s => s.Color).ToArray());
    }

    [TestMethod]
    public void ANewSourceTakesTheFirstFreeColor()
    {
        var before = SecondarySources.Colored([@"D:\A", @"D:\B", @"D:\C"], [], 5);
        var kept = SecondarySources.Colored([@"D:\A", @"D:\C"], before, 5);

        var added = SecondarySources.Colored([@"D:\A", @"D:\C", @"E:\New"], kept, 5);

        Assert.AreEqual(1, added[2].Color);
    }

    [TestMethod]
    public void PastThePaletteTheLeastHeldColorIsTaken()
    {
        var colored = SecondarySources.Colored([@"D:\1", @"D:\2", @"D:\3", @"D:\4", @"D:\5", @"D:\6", @"D:\7"], [], 5);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 0, 1 }, colored.Select(s => s.Color).ToArray());
    }

    [TestMethod]
    public void AColorOutOfThePaletteIsGivenAnother()
    {
        var colored = SecondarySources.Colored([@"D:\A", @"D:\B"], [new(@"D:\A", 9), new(@"d:\b", 3)], 5);

        Assert.AreEqual(3, colored[1].Color, "matched without case");
        Assert.AreEqual(0, colored[0].Color);
    }
}
