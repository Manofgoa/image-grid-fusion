using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The URL zone's caption of a ready media: its name, then its size, its dimensions and — a content that plays — its
/// duration, each part left out when unknown; the name cut first when the caption is too narrow, the details always
/// shown (workfiles/20261107-content-download-improvements.md § Media Information).
/// </summary>
internal static class UrlCaption
{
    /// <summary>Between two parts of the caption.</summary>
    public const string Separator = " · ";

    private const string Ellipsis = "…";

    /// <summary>A size in megabytes, in the UI language: <c>12.4 MB</c>, <c>12,4 Mo</c>.</summary>
    public static string Megabytes(long bytes) => Texts.Format(Strings.CompUrlMegabytes, Texts.Number(bytes / (1024.0 * 1024), "0.0"));

    /// <summary>
    /// What follows the name: <c>12.4 MB · 1920×1080 · 3:33</c> — the dimensions left out when unknown, the duration
    /// for a content that plays only.
    /// </summary>
    public static string Details(long bytes, Size? size, TimeSpan? duration)
    {
        var parts = new List<string> { Megabytes(bytes) };
        if (size is { Width: > 0, Height: > 0 } known)
        {
            parts.Add($"{known.Width}×{known.Height}");
        }

        if (duration is { } length && length > TimeSpan.Zero)
        {
            parts.Add(Duration(length));
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// A duration as a player writes it: <c>0:04</c>, <c>3:33</c>, <c>1:02:05</c>, rounded to the second; under a
    /// second, <c>0.8 s</c>.
    /// </summary>
    public static string Duration(TimeSpan length)
    {
        if (length < TimeSpan.FromSeconds(1))
        {
            return Texts.Format(Strings.CompUrlSeconds, Texts.Number(length.TotalSeconds, "0.0"));
        }

        long seconds = (long)Math.Round(length.TotalSeconds, MidpointRounding.AwayFromZero);
        long hours = seconds / 3600;
        long minutes = seconds / 60 % 60;
        long rest = seconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{rest:00}" : $"{minutes}:{rest:00}";
    }

    /// <summary>
    /// <paramref name="compose"/> given <paramref name="name"/>, or the longest start of it ended by <c>…</c> whose
    /// text is at most <paramref name="available"/> wide by <paramref name="width"/> — the name cut, never the rest;
    /// down to <c>…</c> alone.
    /// </summary>
    public static string Fit(string name, Func<string, string> compose, Func<string, int> width, int available)
    {
        string whole = compose(name);
        if (width(whole) <= available || name.Length == 0)
        {
            return whole;
        }

        // The longest start that fits, by halves: the width grows with the length kept.
        int low = 0;
        int high = name.Length - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (width(compose(Cut(name, middle))) <= available)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return compose(Cut(name, low));
    }

    private static string Cut(string name, int length)
    {
        // Never between the two halves of a surrogate pair.
        if (length > 0 && char.IsHighSurrogate(name[length - 1]))
        {
            length--;
        }

        return name[..length].TrimEnd() + Ellipsis;
    }
}
