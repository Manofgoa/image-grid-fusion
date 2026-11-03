using System.Text.RegularExpressions;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A kind of file name a machine made up rather than a person, a group of the folder view of its own;
/// the values in the order they are tried, which is the fallback order when one is turned off.
/// See workfiles/20261103-file-explorer-groups.md § File Name Patterns.
/// </summary>
internal enum NamePattern
{
    /// <summary>A camera or device stamp: <c>IMG_20220321_132643</c>, <c>PXL_…</c>, <c>DSC_0042</c>.</summary>
    Camera,

    /// <summary>A screenshot or screen recording stamp: <c>Screenshot_2026-11-03-10-24-09</c>.</summary>
    Screenshots,

    /// <summary>A messaging app's stamp: <c>WhatsApp Image 2026-11-03 at 10.24.09</c>, <c>IMG-20220321-WA0001</c>.</summary>
    Messaging,

    /// <summary>A GUID, a hash, a random token: <c>3f2504e0-4f89-…</c>, <c>F3xKz9aWkAA1b2c</c>.</summary>
    Identifiers,

    /// <summary>Digits and separators only: <c>20220321_132643</c>, <c>001</c>.</summary>
    Numbers,
}

/// <summary>
/// Which <see cref="NamePattern"/> a file name falls into, on its name without its extension. A new
/// pattern is a value of <see cref="NamePattern"/>, its case in <see cref="Matches"/>, its
/// <see cref="Title"/>, and its checkbox in the ⚙ menu's submenu.
/// </summary>
internal static class NamePatterns
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // The camera's stamp, a WhatsApp file's (IMG-20220321-WA0001) left to Messaging.
    private static readonly Regex CameraName = new(
        @"^(?!.*-WA\d)(?:(?:IMG|VID|PXL|MVIMG|PANO|BURST\d*|DSC[NF]?|DCIM|GOPR|GX\d\d|PICT|CIMG|SAM|WP|MOV)[_\- ]?\d{3,}|P\d{7})",
        Options);

    // A screenshot's or a screen recording's word, in a few languages, followed by its date or number.
    private static readonly Regex ScreenshotName = new(
        @"^(?:screenshot|screen[ _]?shot|screen[ _]?recording|screenrecorder|capture d.[ée]cran|enregistrement de l.[ée]cran|capture|captura de pantalla|bildschirmfoto|schermafbeelding|snapshot)[\s_\-(]*\d",
        Options);

    private static readonly Regex MessagingName = new(
        @"^(?:whatsapp (?:image|video|audio|ptt)|(?:img|vid|aud|ptt|doc)-\d{8}-wa\d+|photo_\d{4}-\d\d-\d\d_|video_\d{4}-\d\d-\d\d_|signal-\d{4}-\d\d-\d\d|fb_img_\d+|received_\d+|snapchat-\d+)",
        Options);

    private static readonly Regex Guid = new(@"^\{?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\}?$", Options);

    private static readonly Regex Hash = new(@"^(?=.*\d)(?=.*[a-f])[0-9a-f]{16,}$", Options);

    private static readonly Regex Digits = new(@"^(?=.*\d)[\d\s_\-.()]+$", Options);

    // The shortest random token: shorter names mixing letters and digits are often a person's (A320neo).
    private const int MinTokenLength = 10;

    /// <summary>Every pattern, in the order they are tried.</summary>
    public static IReadOnlyList<NamePattern> All { get; } = Enum.GetValues<NamePattern>();

    /// <summary>A pattern's group title, as its header and its checkbox show it.</summary>
    public static string Title(NamePattern pattern) => pattern switch
    {
        NamePattern.Camera => "Camera",
        NamePattern.Screenshots => "Screenshots",
        NamePattern.Messaging => "Messaging",
        NamePattern.Identifiers => "Identifiers",
        _ => "Numbers",
    };

    /// <summary>
    /// The first pattern still on that <paramref name="fileName"/> — with its extension — matches; null
    /// for a name a person gave, or a machine's whose every pattern is in <paramref name="off"/>.
    /// </summary>
    public static NamePattern? Of(string fileName, IReadOnlySet<NamePattern> off)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        foreach (var pattern in All)
        {
            if (!off.Contains(pattern) && Matches(pattern, stem))
            {
                return pattern;
            }
        }

        return null;
    }

    /// <summary>Whether a name without its extension matches a pattern.</summary>
    public static bool Matches(NamePattern pattern, string stem) => pattern switch
    {
        NamePattern.Camera => CameraName.IsMatch(stem),
        NamePattern.Screenshots => ScreenshotName.IsMatch(stem),
        NamePattern.Messaging => MessagingName.IsMatch(stem),
        NamePattern.Identifiers => Guid.IsMatch(stem) || Hash.IsMatch(stem) || IsToken(stem),
        _ => Digits.IsMatch(stem),
    };

    /// <summary>
    /// A random token — an X media name, a video id: <see cref="MinTokenLength"/> letters and digits or
    /// more, <c>-</c> and <c>_</c> allowed, both letters and digits, switching between lower case, upper
    /// case and digits at least every third character — never a word-like run.
    /// </summary>
    private static bool IsToken(string stem)
    {
        int length = 0;
        int switches = 0;
        bool letter = false;
        bool digit = false;
        int previous = -1;
        foreach (char c in stem)
        {
            if (c is '-' or '_')
            {
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }

            int kind = char.IsAsciiDigit(c) ? 0 : char.IsAsciiLetterUpper(c) ? 1 : 2;
            letter |= kind != 0;
            digit |= kind == 0;
            if (previous >= 0 && kind != previous)
            {
                switches++;
            }

            previous = kind;
            length++;
        }

        return length >= MinTokenLength && letter && digit && switches * 3 >= length;
    }
}
