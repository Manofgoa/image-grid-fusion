using System.Text.RegularExpressions;
using ImageGridFusion.Resources;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A group of the folder view gathering files a machine named: <paramref name="Id"/> keeps it turned off in
/// the settings, <paramref name="Title"/> heads its section, <paramref name="IsSource"/> marks a known source —
/// a downloader's or an app's prefix — listed in the ⚙ menu's Sources submenu. See
/// workfiles/20261103-file-explorer-groups.md § File Name Patterns.
/// </summary>
internal sealed record NameGroup(string Id, string Name, Func<string, bool> Matches, bool IsSource = false)
{
    /// <summary>The section's title in the UI language; a source's is its own name.</summary>
    public string Title => this.Id switch
    {
        "Camera" => Strings.ExplorerGroupCamera,
        "Screenshots" => Strings.ExplorerGroupScreenshots,
        "Messaging" => Strings.ExplorerGroupMessaging,
        "AI" => Strings.ExplorerGroupAi,
        "GenericNames" => Strings.ExplorerGroupGenericNames,
        "Identifiers" => Strings.ExplorerGroupIdentifiers,
        "ShortCodes" => Strings.ExplorerGroupShortCodes,
        "DateStamps" => Strings.ExplorerGroupDateStamps,
        "Timestamps" => Strings.ExplorerGroupTimestamps,
        "Numbers" => Strings.ExplorerGroupNumbers,
        _ => this.Name,
    };
}

/// <summary>
/// Which <see cref="NameGroup"/> a file name falls into, on its name without its extension: the first of
/// <see cref="Groups"/> still on that matches it — their order is the fallback order when one is turned off —
/// then a <see cref="DetectSources">detected source</see>, then a <see cref="Series">series</see>. A new pattern
/// is a group of <see cref="Groups"/>, its icon and tooltip in the ⚙ menu's File explorer groups; a new known
/// source, a row of <see cref="KnownSources"/>.
/// </summary>
internal static class NamePatterns
{
    /// <summary>The id turning the series off (<see cref="Series"/>).</summary>
    public const string SeriesId = "Series";

    /// <summary>The id turning every detected source off (<see cref="DetectSources"/>).</summary>
    public const string DetectedSourcesId = "DetectedSources";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // The camera's stamp, a WhatsApp file's (IMG-20220321-WA0001) left to Messaging.
    private static readonly Regex CameraName = new(
        @"^(?!.*-WA\d)(?:(?:IMG|VID|PXL|MVIMG|PANO|BURST\d*|DSC[NF]?|DCIM|GOPR|GX\d\d|PICT|CIMG|SAM|WP|MOV)[_\- ]?\d{3,}|P\d{7}(?:$|[\s_(-]))",
        Options);

    // A screenshot's or a screen recording's word, in a few languages, followed by its date or number.
    private static readonly Regex ScreenshotName = new(
        @"^(?:screenshot|screen[ _]?shot|screen[ _]?recording|screenrecorder|capture[\s_\-]?(?:d.?)?[ée]cran|enregistrement de l.[ée]cran|capture|captura de pantalla|bildschirmfoto|schermafbeelding|snapshot)[\s_\-(]*\d",
        Options);

    private static readonly Regex MessagingName = new(
        @"^(?:whatsapp (?:image|video|audio|ptt)|(?:img|vid|aud|ptt|doc)-\d{8}-wa\d+|photo_\d{4}-\d\d-\d\d_|video_\d{4}-\d\d-\d\d_|signal-\d{4}-\d\d-\d\d|fb_img_\d+|received_\d+|snapchat-\d+)",
        Options);

    // An AI generator's stamp: Bing Image Creator, Grok, ChatGPT, the High_Performance videos, DALL·E, Firefly.
    private static readonly Regex AiName = new(
        @"^(?:OIG\d*(?:$|[\s_.(-])|grok[_-](?:video|image)|chatgpt image|high_performance_|dall[·-]?e[\s_]|firefly[\s_])",
        Options);

    // A browser's or a site's default name, with its numbers: téléchargement (1), image-w856, videoplayback (12),
    // YouTube's thumbnails (maxresdefault, hq720).
    private static readonly Regex GenericName = new(
        @"^(?:t[ée]l[ée]chargement|downloads?|images?|videoplayback|maxresdefault|hqdefault|sddefault|hq\d+|full|unnamed|untitled|sans[ _]titre)(?:[\s_\-]*(?:\(\d+\)|\d+|w\d+))*$",
        Options);

    private static readonly Regex Guid = new(@"^\{?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\}?$", Options);

    private static readonly Regex Hash = new(@"^(?=.*\d)(?=.*[a-f])[0-9a-f]{16,}$", Options);

    // An X media id: 15 characters, upper and lower case mixed (GEhjfvEWQAE7EPO, GFGxk-FWMAEPZym).
    private static readonly Regex XMediaId = new(@"^(?=(?:.*[A-Z]){2})(?=(?:.*[a-z]){2})[A-Za-z0-9_-]{15}$", RegexOptions.CultureInvariant);

    // A date, then maybe a time and a counter, digits and separators only: 20230809_212344, 2022-07-18 13.22.09.
    private static readonly Regex DateStamp = new(
        @"^(?:19|20)\d\d[-_.]?(?:0[1-9]|1[0-2])[-_.]?(?:0[1-9]|[12]\d|3[01])(?:[\s_\-.T]?\d\d[-_.:]?\d\d(?:[-_.:]?\d\d)?)?(?:[\s_\-.(]*\d+\)?)*$",
        Options);

    // A Unix time in seconds or milliseconds, 2001 to 2033, maybe followed by a counter.
    private static readonly Regex Timestamp = new(@"^1\d{9}(?:\d{3})?(?:[\s_\-(]*\d+\)?)*$", Options);

    private static readonly Regex Digits = new(@"^(?=.*\d)[\d\s_\-.()]+$", Options);

    // A detected source's word — every letter of it, dots inside — and the stamp right after it: 4 digits or
    // more, or a code of 5 letters and digits or more holding a digit — never a lone counter (jaune 1, Make My
    // Day (2)). The word is atomic: never shortened to find a stamp inside it.
    private static readonly Regex SourceWord = new(@"^((?>[A-Za-z](?:[A-Za-z]|\.(?=[A-Za-z]))*))(?=[\s_\-.]*(?:\d{4}|(?=[A-Za-z0-9]{5})[A-Za-z]*\d))", RegexOptions.CultureInvariant);

    // The shortest random token: shorter names mixing letters and digits are often a person's (A320neo).
    private const int MinTokenLength = 10;

    // The shortest series: two files sharing a word are a coincidence more often than a series.
    private const int MinSeries = 3;

    // The words a series never stands on alone: articles and small words, French and English.
    private static readonly HashSet<string> SmallWords = new(StringComparer.Ordinal)
    {
        "a", "à", "au", "aux", "ce", "ces", "d", "de", "des", "du", "en", "et", "il", "elle", "j", "je", "l", "la", "le",
        "les", "ma", "mes", "mon", "on", "ou", "par", "pour", "sa", "se", "ses", "son", "sur", "un", "une", "y",
        "an", "and", "at", "for", "i", "in", "is", "it", "my", "of", "the", "to", "with",
    };

    /// <summary>The known sources, one group each, in the ⚙ menu's Sources submenu.</summary>
    public static IReadOnlyList<NameGroup> KnownSources { get; } =
    [
        Source("ssstwitter.com", @"^ssstwitter\.com"),
        Source("snapinsta.app", @"^snapinsta\.app"),
        Source("twittervid.com", @"^twittervid\.com"),
        Source("FaceApp", @"^faceapp_"),
        Source("Videoleap", @"^videoleap_"),
        Source("VideoEditor", @"^videoeditor_"),
        Source("VideoCapture", @"^videocapture_"),
        Source("sketch", @"^sketch\d{10,}"),
        Source("Clipped_image", @"^clipped_image_"),
    ];

    /// <summary>Every group, in the order they are tried: the known sources between AI and Generic names.</summary>
    public static IReadOnlyList<NameGroup> Groups { get; } =
    [
        new("Camera", "Camera", CameraName.IsMatch),
        new("Screenshots", "Screenshots", ScreenshotName.IsMatch),
        new("Messaging", "Messaging", MessagingName.IsMatch),
        new("AI", "AI", AiName.IsMatch),
        .. KnownSources,
        new("GenericNames", "Generic names", GenericName.IsMatch),
        new("Identifiers", "Identifiers", stem => Guid.IsMatch(stem) || Hash.IsMatch(stem) || XMediaId.IsMatch(stem) || IsToken(stem)),
        new("ShortCodes", "Short codes", IsShortCode),
        new("DateStamps", "Date stamps", DateStamp.IsMatch),
        new("Timestamps", "Timestamps", Timestamp.IsMatch),
        new("Numbers", "Numbers", Digits.IsMatch),
    ];

    /// <summary>
    /// The first group still on — none of its id in <paramref name="off"/> — that <paramref name="fileName"/>,
    /// with its extension, matches; null for a name a person gave, or a machine's whose every group is off.
    /// </summary>
    public static NameGroup? Of(string fileName, IReadOnlySet<string> off)
    {
        string stem = Stem(fileName);
        foreach (var group in Groups)
        {
            if (!off.Contains(group.Id) && group.Matches(stem))
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>
    /// The sources found among <paramref name="fileNames"/> — names no group took — by themselves: a leading
    /// word, dots allowed (<c>y2mate.com</c>), followed by a stamp, shared by <see cref="MinSeries"/> files or
    /// more, ignoring case; titled by that word as the first of them writes it. By name; a file of no source
    /// is left out.
    /// </summary>
    public static Dictionary<string, string> DetectSources(IEnumerable<string> fileNames)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byWord = fileNames
            .Select(name => (Name: name, Word: SourceWord.Match(Stem(name))))
            .Where(file => file.Word.Success && !SmallWords.Contains(file.Word.Groups[1].Value.ToLowerInvariant()))
            .GroupBy(file => file.Word.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byWord)
        {
            var members = group.ToList();
            if (members.Count >= MinSeries)
            {
                string title = members[0].Word.Groups[1].Value;
                foreach (var (name, _) in members)
                {
                    sources[name] = title;
                }
            }
        }

        return sources;
    }

    /// <summary>
    /// The series among <paramref name="fileNames"/> — names no group nor source took: <see cref="MinSeries"/>
    /// files or more sharing their first word, ignoring case and accents, titled by the longest run of leading
    /// words all of them share, as the first of them writes it — <c>marlène schiappa</c> — small words left off
    /// its end; a run of small words alone (<c>les</c>, <c>the</c>) makes none. By name; a file of no series is left out. See
    /// workfiles/20261103-file-explorer-groups.md § File Series.
    /// </summary>
    public static Dictionary<string, string> Series(IEnumerable<string> fileNames)
    {
        var series = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byFirstWord = fileNames
            .Select(name => (Name: name, Stem: Stem(name)))
            .Select(file => (file.Name, file.Stem, Words: Words(file.Stem)))
            .Where(file => file.Words.Count > 0)
            .GroupBy(file => file.Words[0].Folded, StringComparer.Ordinal);
        foreach (var group in byFirstWord)
        {
            var members = group.ToList();
            if (members.Count < MinSeries)
            {
                continue;
            }

            var (_, firstStem, firstWords) = members[0];
            int shared = firstWords.Count;
            foreach (var (_, _, words) in members.Skip(1))
            {
                int common = 0;
                while (common < shared && common < words.Count && words[common].Folded == firstWords[common].Folded)
                {
                    common++;
                }

                shared = common;
            }

            // Small words never end a title (Taxez les crabes, Taxez les riches → Taxez), nor make one alone.
            while (shared > 0 && SmallWords.Contains(firstWords[shared - 1].Folded))
            {
                shared--;
            }

            if (shared == 0)
            {
                continue;
            }

            string title = firstStem[..firstWords[shared - 1].End];
            foreach (var (name, _, _) in members)
            {
                series[name] = title;
            }
        }

        return series;
    }

    private static NameGroup Source(string title, string pattern)
    {
        var regex = new Regex(pattern, Options);
        return new NameGroup(title, title, regex.IsMatch, IsSource: true);
    }

    private static string Stem(string fileName) => Path.GetFileNameWithoutExtension(fileName).Trim();

    // A name's words, folded, with where each ends: the runs between spaces, -, _ and the dots.
    private static List<(string Folded, int End)> Words(string stem)
    {
        var words = new List<(string Folded, int End)>();
        int start = -1;
        for (int i = 0; i <= stem.Length; i++)
        {
            bool separator = i == stem.Length || stem[i] is ' ' or '-' or '_' or '.';
            if (separator && start >= 0)
            {
                words.Add((FileSearch.Fold(stem[start..i]), i));
                start = -1;
            }
            else if (!separator && start < 0)
            {
                start = i;
            }
        }

        return words;
    }

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

    /// <summary>
    /// A meme site's short code: after a leading <c>-</c>, 5 to 8 lower-case letters and digits
    /// (<c>-oong5f</c>, <c>-htaykg</c>); else 5 to 8 of them holding a letter and a digit before the last
    /// character (<c>d9omli</c>, <c>qy6xu8</c>) — never a word with its counter (<c>breton2</c>).
    /// </summary>
    private static bool IsShortCode(string stem)
    {
        bool dashed = stem.StartsWith('-');
        string code = dashed ? stem[1..] : stem;
        if (code.Length is < 5 or > 8 || !code.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)))
        {
            return false;
        }

        return dashed || (code.Any(char.IsAsciiLetterLower) && code[..^1].Any(char.IsAsciiDigit));
    }
}
