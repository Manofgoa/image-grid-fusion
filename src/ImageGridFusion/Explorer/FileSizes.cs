using System.Globalization;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A file's size as the Details view writes it: in French units, base 1024 — <c>812 octets</c>,
/// <c>412 Ko</c>, <c>2,3 Mo</c>, <c>1,1 Go</c> — one decimal below 10 of a unit, none above, the
/// French decimal comma; <c>—</c> when unknown. French on purpose, the rest of the UI being English:
/// see workfiles/20261009-explorer-details-view.md § The Size.
/// </summary>
internal static class FileSizes
{
    /// <summary>What an unknown size shows.</summary>
    public const string Unknown = "—";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] Units = ["Ko", "Mo", "Go", "To"];

    public static string Text(long? bytes)
    {
        if (bytes is not { } size || size < 0)
        {
            return Unknown;
        }

        if (size < 1024)
        {
            return size <= 1 ? $"{size} octet" : $"{size} octets";
        }

        double value = size / 1024d;
        int unit = 0;
        while (true)
        {
            // Rounded as shown: 1023,7 Ko is written 1 Mo, never 1 024 Ko.
            double shown = value < 10 ? Math.Round(value, 1) : Math.Round(value);
            if (shown < 1024 || unit == Units.Length - 1)
            {
                return $"{shown.ToString(value < 10 ? "0.#" : "0", French)} {Units[unit]}";
            }

            value /= 1024;
            unit++;
        }
    }
}
