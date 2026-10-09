using System.Drawing.Imaging;

namespace ImageGridFusion.Composition;

/// <summary>
/// The Colors effect of an image: saturation, hue, brightness and contrast, each centered on 0, the
/// image unchanged at <see cref="Default"/>. Saturation at −1 is black &amp; white. The four compose one
/// color matrix, applied to the image and to its background's fill alike.
/// </summary>
public sealed record ColorsEffect
{
    /// <summary>The hue turns at most half a turn either way.</summary>
    public const int MaxHue = 180;

    /// <summary>Every setting at 0: the colors as they are.</summary>
    public static readonly ColorsEffect Default = new();

    /// <summary>Luminance weights of the saturation: at −1, each channel is the luminance.</summary>
    private static readonly double[] Luminance = [0.299, 0.587, 0.114];

    /// <summary>From −1 (black &amp; white) through 0 to 1 (the saturation doubled).</summary>
    public double Saturation { get; private init; }

    /// <summary>Turn of the hue, in degrees, within ±<see cref="MaxHue"/>.</summary>
    public int Hue { get; private init; }

    /// <summary>From −1 to 1: an offset of half that on every channel, never flat black nor white.</summary>
    public double Brightness { get; private init; }

    /// <summary>From −1 (flat mid-gray) through 0 to 1 (three times the contrast), around mid-gray.</summary>
    public double Contrast { get; private init; }

    /// <summary>The colors left as they are: nothing to apply.</summary>
    public bool IsNeutral => this == Default;

    public ColorsEffect WithSaturation(double saturation) => this with { Saturation = Math.Clamp(saturation, -1, 1) };

    public ColorsEffect WithHue(int hue) => this with { Hue = Math.Clamp(hue, -MaxHue, MaxHue) };

    public ColorsEffect WithBrightness(double brightness) => this with { Brightness = Math.Clamp(brightness, -1, 1) };

    public ColorsEffect WithContrast(double contrast) => this with { Contrast = Math.Clamp(contrast, -1, 1) };

    /// <summary>The settings as a GDI+ color matrix, for the image's <see cref="ImageAttributes"/>.</summary>
    public ColorMatrix Matrix()
    {
        var (linear, offset) = this.Transform();
        var matrix = new ColorMatrix();

        // GDI+ multiplies a row vector: the output channel is the column, the input the row.
        for (int output = 0; output < 3; output++)
        {
            for (int input = 0; input < 3; input++)
            {
                matrix[input, output] = (float)linear[output, input];
            }

            matrix[4, output] = (float)offset;
        }

        return matrix;
    }

    /// <summary><paramref name="color"/> as the matrix turns it, its alpha kept: the background's fill.</summary>
    public Color Apply(Color color)
    {
        if (this.IsNeutral)
        {
            return color;
        }

        var (linear, offset) = this.Transform();
        double[] channels = [color.R / 255.0, color.G / 255.0, color.B / 255.0];
        int Channel(int output)
        {
            double value = offset;
            for (int input = 0; input < 3; input++)
            {
                value += linear[output, input] * channels[input];
            }

            return (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
        }

        return Color.FromArgb(color.A, Channel(0), Channel(1), Channel(2));
    }

    /// <summary>
    /// Saturation, then hue, then contrast, then brightness, as one affine map on the channels (0…1):
    /// output = linear · input + offset, the same offset on every channel.
    /// </summary>
    private (double[,] Linear, double Offset) Transform()
    {
        // Saturation: each channel mixed with the luminance.
        double keep = 1 + this.Saturation;
        var saturation = new double[3, 3];
        for (int output = 0; output < 3; output++)
        {
            for (int input = 0; input < 3; input++)
            {
                saturation[output, input] = (1 - keep) * Luminance[input] + (output == input ? keep : 0);
            }
        }

        // Hue: the luminance-preserving rotation of SVG's feColorMatrix hueRotate.
        double angle = this.Hue * Math.PI / 180;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double[,] hue =
        {
            { 0.213 + cos * 0.787 - sin * 0.213, 0.715 - cos * 0.715 - sin * 0.715, 0.072 - cos * 0.072 + sin * 0.928 },
            { 0.213 - cos * 0.213 + sin * 0.143, 0.715 + cos * 0.285 + sin * 0.140, 0.072 - cos * 0.072 - sin * 0.283 },
            { 0.213 - cos * 0.213 - sin * 0.787, 0.715 - cos * 0.715 + sin * 0.715, 0.072 + cos * 0.928 + sin * 0.072 },
        };

        // Contrast: scaled around mid-gray, flat at −1, three times at 1.
        double contrast = this.Contrast < 0 ? 1 + this.Contrast : 1 + 2 * this.Contrast;
        var linear = new double[3, 3];
        for (int output = 0; output < 3; output++)
        {
            for (int input = 0; input < 3; input++)
            {
                double sum = 0;
                for (int k = 0; k < 3; k++)
                {
                    sum += hue[output, k] * saturation[k, input];
                }

                linear[output, input] = contrast * sum;
            }
        }

        // Brightness: half its value added to every channel.
        double offset = 0.5 * (1 - contrast) + 0.5 * this.Brightness;
        return (linear, offset);
    }
}
