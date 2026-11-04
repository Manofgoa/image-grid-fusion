using System.Globalization;

namespace ImageGridFusion.Resources;

/// <summary>
/// The UI language's helpers around <see cref="Strings"/> — <c>Strings.resx</c> in English, <c>Strings.fr.resx</c> in
/// French, the class generated at build time: the culture a language stands for, set once by <c>Program.Main</c>, and
/// the displayed texts and numbers formatted in it. Parsing and serialization never go through here: they stay
/// <see cref="CultureInfo.InvariantCulture"/>. See RULES.md § UI Language, workfiles/20261105-french-translation.md.
/// </summary>
internal static class Texts
{
    /// <summary>The culture of a language of <c>AppSettings.Languages</c>: its texts, its decimal separator.</summary>
    public static CultureInfo CultureOf(string language) =>
        CultureInfo.GetCultureInfo(language == "fr" ? "fr-FR" : "en-US");

    /// <summary>A text with placeholders (<c>{0}</c>, <c>{1:0.0}</c>…) filled in the UI language's culture.</summary>
    public static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentUICulture, format, args);

    /// <summary>A number as the UI shows it, in the UI language's culture: <c>12.5</c> in English, <c>12,5</c> in French.</summary>
    public static string Number(double value, string format) =>
        value.ToString(format, CultureInfo.CurrentUICulture);
}
