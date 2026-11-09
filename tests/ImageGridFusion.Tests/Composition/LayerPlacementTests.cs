using System.Drawing;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>A layer's size bounds and the selected layer's reach (workfiles/20261107-layer-size-limits.md).</summary>
[TestClass]
public sealed class LayerPlacementTests
{
    private static readonly Size Square = new(1000, 1000);

    private static LayerPlacement Centered(double size, double angle = 0) => new(0.5, 0.5, size, angle);

    [TestMethod]
    public void SizeClampedBetweenHalfAPercentAndFiveHundredPercent()
    {
        Assert.AreEqual(0.005, Centered(0.4).WithSize(0.001).Size);
        Assert.AreEqual(5.0, Centered(0.4).WithSize(10).Size);
    }

    [TestMethod]
    public void SizeWithinTheBoundsIsKept()
    {
        Assert.AreEqual(2.5, Centered(0.4).WithSize(2.5).Size);
        Assert.AreEqual(0.01, Centered(0.4).WithSize(0.01).Size);
    }

    [TestMethod]
    public void LargestBoxIsFiveTimesTheCanvasShorterSide()
    {
        var box = Centered(LayerPlacement.MaxSize).BoxSize(new Size(400, 300), new Size(1200, 628));

        Assert.AreEqual(3140f, box.Width, 0.01f);
        Assert.AreEqual(2355f, box.Height, 0.01f);
    }

    [TestMethod]
    public void TinyLayerReachedAroundItsCenter()
    {
        // A 5-px box: missed 10 px off its center, reached within the 24-px reach.
        var place = Centered(LayerPlacement.MinSize);
        var point = new PointF(510, 500);

        Assert.IsFalse(place.Contains(point, new Size(100, 100), Square));
        Assert.IsTrue(place.Reaches(point, new Size(100, 100), Square, 24));
        Assert.IsFalse(place.Reaches(new PointF(513, 500), new Size(100, 100), Square, 24));
    }

    [TestMethod]
    public void TinyLayerReachTurnsWithIt()
    {
        // (10, 10) off the center lies 14 px along the turned box's axis: inside the upright reach, outside the turned one.
        var point = new PointF(510, 510);

        Assert.IsTrue(Centered(LayerPlacement.MinSize).Reaches(point, new Size(100, 100), Square, 24));
        Assert.IsFalse(Centered(LayerPlacement.MinSize, angle: 45).Reaches(point, new Size(100, 100), Square, 24));
    }

    [TestMethod]
    public void LargeLayerReachIsItsBox()
    {
        // A 400-px box: the reach adds nothing.
        var place = Centered(0.4);
        var shown = new Size(100, 100);

        Assert.IsTrue(place.Reaches(new PointF(690, 500), shown, Square, 24));
        Assert.IsFalse(place.Reaches(new PointF(710, 500), shown, Square, 24));
        Assert.AreEqual(place.Contains(new PointF(710, 500), shown, Square), place.Reaches(new PointF(710, 500), shown, Square, 24));
    }
}
