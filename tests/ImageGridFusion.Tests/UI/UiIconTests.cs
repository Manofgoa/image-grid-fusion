using System.Drawing.Imaging;
using ImageGridFusion.Imaging;
using ImageGridFusion.UI;

namespace ImageGridFusion.Tests.UI;

/// <summary>The emoji icons drawn at their size (workfiles/20261105-initial-load-speed.md § A).</summary>
[TestClass]
public sealed class UiIconTests
{
    [TestMethod]
    [DataRow("\U0001F50D", 16)]
    [DataRow("\U0001F5BC️", 20)]
    [DataRow("⚙️", 24)]
    [DataRow("\U0001F468‍\U0001F9B0", 32)]
    public void EmojiAtItsSizeFillsItsSquareAsAtTheTextEm(string glyph, int size)
    {
        using var reduced = UiIcon.DrawEmoji(glyph, size);
        using var large = UiIcon.DrawEmoji(glyph, size, TextRaster.EmSize);

        var reducedInk = InkOf(reduced);
        var largeInk = InkOf(large);

        Assert.IsFalse(reducedInk.IsEmpty, "Nothing drawn");
        Assert.IsLessThanOrEqualTo(1, Math.Abs(reducedInk.Left - largeInk.Left), $"{reducedInk} vs {largeInk}");
        Assert.IsLessThanOrEqualTo(1, Math.Abs(reducedInk.Top - largeInk.Top), $"{reducedInk} vs {largeInk}");
        Assert.IsLessThanOrEqualTo(1, Math.Abs(reducedInk.Right - largeInk.Right), $"{reducedInk} vs {largeInk}");
        Assert.IsLessThanOrEqualTo(1, Math.Abs(reducedInk.Bottom - largeInk.Bottom), $"{reducedInk} vs {largeInk}");
    }

    [TestMethod]
    public void EmSizeIsFarBelowTheTextLayersOne()
    {
        Assert.IsLessThanOrEqualTo(TextRaster.EmSize / 2, UiIcon.EmFor(24));
        Assert.IsGreaterThanOrEqualTo(UiIcon.Oversampling * 32f, UiIcon.EmFor(32));
    }

    // The pixels at least half opaque.
    private static Rectangle InkOf(Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new int[bitmap.Width];
            for (int y = 0; y < bitmap.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < row.Length; x++)
                {
                    if ((row[x] >>> 24) >= 128)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
