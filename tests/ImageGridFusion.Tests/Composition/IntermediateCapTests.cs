using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>
/// The cap on <see cref="Compositor"/>'s per-cell intermediate bitmaps, for a layer larger than the canvas
/// (workfiles/20261107-layer-size-limits.md § A Large Layer Stays Within Memory).
/// </summary>
[TestClass]
public sealed class IntermediateCapTests
{
    [TestMethod]
    public void CellWithinTheCapIsNotReduced()
    {
        Assert.AreEqual(1.0, Compositor.IntermediateCap(4000, 3000));
        Assert.AreEqual(1.0, Compositor.IntermediateCap(CanvasSizer.MaxLongSide, 10));
    }

    [TestMethod]
    public void LongerSideBroughtToTheCap()
    {
        Assert.AreEqual(0.5, Compositor.IntermediateCap(16384, 1000), 1e-9);
        Assert.AreEqual(0.2, Compositor.IntermediateCap(1000, 40960), 1e-9);
    }
}
