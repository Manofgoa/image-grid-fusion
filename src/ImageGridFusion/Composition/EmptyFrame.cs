using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Composition;

/// <summary>
/// Tells a frame that shows nothing — fully black, white or any flat color, like a video's intro — from
/// one worth exporting as a still.
/// </summary>
public static class EmptyFrame
{
    private const int SampleSide = 32;

    /// <summary>Largest difference, per channel, from the average color for a sample to count as flat.</summary>
    private const int Tolerance = 24;

    /// <summary>Share of flat samples from which the frame is empty.</summary>
    private const double FlatShare = 0.98;

    public static bool IsEmpty(Bitmap frame)
    {
        var data = frame.LockBits(new Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var samples = new List<(int R, int G, int B)>(SampleSide * SampleSide);
        try
        {
            var row = new byte[4];
            for (int sy = 0; sy < SampleSide; sy++)
            {
                int y = (int)((sy + 0.5) * frame.Height / SampleSide);
                for (int sx = 0; sx < SampleSide; sx++)
                {
                    int x = (int)((sx + 0.5) * frame.Width / SampleSide);
                    Marshal.Copy(data.Scan0 + y * data.Stride + x * 4, row, 0, 4);
                    samples.Add((row[2], row[1], row[0]));
                }
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }

        double r = samples.Average(s => s.R), g = samples.Average(s => s.G), b = samples.Average(s => s.B);
        int flat = samples.Count(s => Math.Abs(s.R - r) <= Tolerance && Math.Abs(s.G - g) <= Tolerance && Math.Abs(s.B - b) <= Tolerance);
        return flat >= FlatShare * samples.Count;
    }
}
