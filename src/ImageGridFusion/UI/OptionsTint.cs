using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;

namespace ImageGridFusion.UI;

/// <summary>
/// The tint shared by the options row and the selected effect tab, so the two read as one block: the
/// Windows accent color, much lightened.
/// </summary>
internal static class OptionsTint
{
    // The share of the accent in the tint: enough to color the row, light enough for black text.
    private const double AccentShare = 0.15;

    // The hover color of Windows buttons, when the accent cannot be read.
    private static readonly Color Fallback = Color.FromArgb(0xE5, 0xF1, 0xFB);

    /// <summary>Read again each time: the accent may change while the app runs.</summary>
    public static Color Current()
    {
        try
        {
            var accent = new UISettings().GetColorValue(UIColorType.Accent);
            return Color.FromArgb(Lighten(accent.R), Lighten(accent.G), Lighten(accent.B));
        }
        catch (Exception ex) when (ex is COMException or PlatformNotSupportedException)
        {
            return Fallback;
        }
    }

    private static int Lighten(byte channel) => (int)Math.Round(255 + (channel - 255) * AccentShare);
}
