using System.Globalization;
using ImageGridFusion.Resources;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A file's size as the Details view writes it: in the UI language's units, base 1024 — <c>812 bytes</c>,
/// <c>412 KB</c>, <c>2.3 MB</c> in English, <c>812 octets</c>, <c>412 Ko</c>, <c>2,3 Mo</c> in French — one
/// decimal below 10 of a unit, none above, the UI language's decimal separator; <c>—</c> when unknown.
/// See workfiles/20261009-explorer-details-view.md § The Size, workfiles/20261105-french-translation.md § Numbers and Units.
/// </summary>
internal static class FileSizes
{
    /// <summary>What an unknown size shows.</summary>
    public const string Unknown = "—";

    public static string Text(long? bytes)
    {
        if (bytes is not { } size || size < 0)
        {
            return Unknown;
        }

        if (size < 1024)
        {
            return Texts.Format(size <= 1 ? Strings.FileSizeByte : Strings.FileSizeBytes, size);
        }

        // Looked up at each call, never cached before Program.Main sets the UI language.
        string[] units = Strings.FileSizeUnits.Split(' ');

        double value = size / 1024d;
        int unit = 0;
        while (true)
        {
            // Rounded as shown: 1023,7 Ko is written 1 Mo, never 1 024 Ko.
            double shown = value < 10 ? Math.Round(value, 1) : Math.Round(value);
            if (shown < 1024 || unit == units.Length - 1)
            {
                return $"{Texts.Number(shown, value < 10 ? "0.#" : "0")} {units[unit]}";
            }

            value /= 1024;
            unit++;
        }
    }
}
