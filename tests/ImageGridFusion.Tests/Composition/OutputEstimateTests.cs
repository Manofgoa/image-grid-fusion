using System.Drawing;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Tests.Composition;

/// <summary>The output readout's worst-case sizes (RULES.md § Output Readout, workfiles/20261107-status-bar-file-size.md § Estimate).</summary>
[TestClass]
public sealed class OutputEstimateTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [TestMethod]
    public void PngBoundHoldsEveryPixelUncompressed()
    {
        var size = new Size(2400, 1256);
        long raw = 1256L * (1 + 4 * 2400);

        long bound = OutputEstimate.PngUpperBound(size);

        Assert.IsTrue(bound > raw, "never below the raw rows");
        // The stored blocks, the chunks and the fixed parts stay a small share of it.
        Assert.IsTrue(bound < raw * 1.01, $"{bound} too far above {raw}");
    }

    [TestMethod]
    public void PngBoundGrowsWithThePixels()
    {
        Assert.IsTrue(OutputEstimate.PngUpperBound(new Size(1200, 628)) < OutputEstimate.PngUpperBound(new Size(2400, 1256)));
    }

    [TestMethod]
    public void NoPixelNoPng()
    {
        Assert.AreEqual(0, OutputEstimate.PngUpperBound(Size.Empty));
    }

    [TestMethod]
    public void Mp4BoundIsTheBitrateOverTheLength()
    {
        var size = new Size(1920, 1004);
        double video = OutputEstimate.VideoBitrate(size) / 8d * 10;

        long bound = OutputEstimate.Mp4UpperBound(size, S(10), sound: false);

        Assert.IsTrue(bound > video, "never below the video track");
        Assert.IsTrue(bound < video * 1.1, $"{bound} too far above {video}");
    }

    [TestMethod]
    public void SoundAddsTheAacTrack()
    {
        var size = new Size(1920, 1004);
        long silent = OutputEstimate.Mp4UpperBound(size, S(10), sound: false);
        long heard = OutputEstimate.Mp4UpperBound(size, S(10), sound: true);

        Assert.IsTrue(heard - silent >= OutputEstimate.AacBytesPerSecond * 10);
    }

    [TestMethod]
    public void NoLengthNoMp4()
    {
        Assert.AreEqual(0, OutputEstimate.Mp4UpperBound(new Size(1920, 1004), TimeSpan.Zero, sound: true));
    }

    [TestMethod]
    public void BitrateKeepsTheEncodersValues()
    {
        // 0.12 bit per pixel and frame at 30 fps, as the encoder always asked for.
        Assert.AreEqual((long)(1920 * 1004d * 30 * 0.12), OutputEstimate.VideoBitrate(new Size(1920, 1004)));
        Assert.AreEqual(OutputEstimate.MinVideoBitrate, OutputEstimate.VideoBitrate(new Size(320, 240)));
        Assert.AreEqual(OutputEstimate.MaxVideoBitrate, OutputEstimate.VideoBitrate(new Size(8192, 8192)));
    }
}
