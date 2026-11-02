using System.Globalization;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The emojis of a text, one definition: the flag sequences the flag font draws, and the text elements a text cell
/// draws in color, two columns wide (workfiles/20261102-text-flag-emojis.md).
/// </summary>
public static class Emojis
{
    private const int RegionalIndicatorA = 0x1F1E6;
    private const int RegionalIndicatorZ = 0x1F1FF;
    private const int BlackFlag = 0x1F3F4;
    private const int TagFirst = 0xE0020;
    private const int CancelTag = 0xE007F;
    private const char ZeroWidthJoiner = '‍';
    private const char Skull = '☠';
    private const char EmojiPresentation = '️';
    private const char Keycap = '⃣';

    // The flag font's own code points for the sequences DirectWrite never joins (tools/make-flag-font.py, JOINED).
    private const char FirstJoined = '';
    private const char LastJoined = '';

    /// <summary>What follows the black flag in each sequence <see cref="Joined"/> replaces, by its code point in the flag font.</summary>
    private static readonly (char Code, string Tail)[] JoinedTails =
    [
        (FirstJoined, Tags("gbeng")),
        ('', Tags("gbsct")),
        ('', Tags("gbwls")),
        (LastJoined, "‍☠️"),
        (LastJoined, "‍☠"),
    ];

    /// <summary>
    /// Unicode's <c>Emoji_Presentation</c> property (Unicode 15.1, emoji-data.txt): the code points drawn as emojis
    /// without VS16, as first and last of each range. .NET has no API for it.
    /// </summary>
    private static readonly int[] PresentationRanges =
    [
        0x231A, 0x231B, 0x23E9, 0x23EC, 0x23F0, 0x23F0, 0x23F3, 0x23F3, 0x25FD, 0x25FE, 0x2614, 0x2615,
        0x2648, 0x2653, 0x267F, 0x267F, 0x2693, 0x2693, 0x26A1, 0x26A1, 0x26AA, 0x26AB, 0x26BD, 0x26BE,
        0x26C4, 0x26C5, 0x26CE, 0x26CE, 0x26D4, 0x26D4, 0x26EA, 0x26EA, 0x26F2, 0x26F3, 0x26F5, 0x26F5,
        0x26FA, 0x26FA, 0x26FD, 0x26FD, 0x2705, 0x2705, 0x270A, 0x270B, 0x2728, 0x2728, 0x274C, 0x274C,
        0x274E, 0x274E, 0x2753, 0x2755, 0x2757, 0x2757, 0x2795, 0x2797, 0x27B0, 0x27B0, 0x27BF, 0x27BF,
        0x2B1B, 0x2B1C, 0x2B50, 0x2B50, 0x2B55, 0x2B55, 0x1F004, 0x1F004, 0x1F0CF, 0x1F0CF, 0x1F18E, 0x1F18E,
        0x1F191, 0x1F19A, 0x1F1E6, 0x1F1FF, 0x1F201, 0x1F201, 0x1F21A, 0x1F21A, 0x1F22F, 0x1F22F, 0x1F232, 0x1F236,
        0x1F238, 0x1F23A, 0x1F250, 0x1F251, 0x1F300, 0x1F320, 0x1F32D, 0x1F335, 0x1F337, 0x1F37C, 0x1F37E, 0x1F393,
        0x1F3A0, 0x1F3CA, 0x1F3CF, 0x1F3D3, 0x1F3E0, 0x1F3F0, 0x1F3F4, 0x1F3F4, 0x1F3F8, 0x1F43E, 0x1F440, 0x1F440,
        0x1F442, 0x1F4FC, 0x1F4FF, 0x1F53D, 0x1F54B, 0x1F54E, 0x1F550, 0x1F567, 0x1F57A, 0x1F57A, 0x1F595, 0x1F596,
        0x1F5A4, 0x1F5A4, 0x1F5FB, 0x1F64F, 0x1F680, 0x1F6C5, 0x1F6CC, 0x1F6CC, 0x1F6D0, 0x1F6D2, 0x1F6D5, 0x1F6D7,
        0x1F6DC, 0x1F6DF, 0x1F6EB, 0x1F6EC, 0x1F6F4, 0x1F6FC, 0x1F7E0, 0x1F7EB, 0x1F7F0, 0x1F7F0, 0x1F90C, 0x1F93A,
        0x1F93C, 0x1F945, 0x1F947, 0x1F9FF, 0x1FA70, 0x1FA7C, 0x1FA80, 0x1FA89, 0x1FA8F, 0x1FAC6, 0x1FACE, 0x1FADC,
        0x1FADF, 0x1FAE9, 0x1FAF0, 0x1FAF8,
    ];

    /// <summary>
    /// The flag sequences of <paramref name="text"/>, as UTF-16 start and length, each drawn whole by the flag font:
    /// a pair of regional indicators (a lone one too, drawn as its letter), the black flag with the tag characters of a
    /// subdivision flag, or 🏴‍☠️ — a run the font cuts would not join into one flag.
    /// </summary>
    public static IEnumerable<(int Start, int Length)> Flags(string text)
    {
        for (int i = 0; i < text.Length;)
        {
            int code = CodeAt(text, i);
            if (code is >= FirstJoined and <= LastJoined)
            {
                yield return (i, 1);
                i++;
            }
            else if (IsRegionalIndicator(code))
            {
                int length = i + 2 < text.Length && IsRegionalIndicator(CodeAt(text, i + 2)) ? 4 : 2;
                yield return (i, length);
                i += length;
            }
            else if (code == BlackFlag)
            {
                int end = i + 2;
                while (end < text.Length && CodeAt(text, end) is >= TagFirst and <= CancelTag)
                {
                    end += 2;
                }

                if (end == i + 2 && end + 1 < text.Length && text[end] == ZeroWidthJoiner && text[end + 1] == Skull)
                {
                    end += end + 2 < text.Length && text[end + 2] == EmojiPresentation ? 3 : 2;
                }

                yield return (i, end - i);
                i = end;
            }
            else
            {
                i += code > 0xFFFF ? 2 : 1;
            }
        }
    }

    /// <summary>
    /// The text as the flag font draws it: England, Scotland, Wales and the pirate flag replaced by the flag font's own
    /// code points — DirectWrite drops their tags and VS16 before the font's ligatures could join them. For drawing
    /// only, with the flag font set on them (<see cref="Flags"/>): the text kept is never changed.
    /// </summary>
    public static string Joined(string text)
    {
        if (!text.Contains(char.ConvertFromUtf32(BlackFlag), StringComparison.Ordinal))
        {
            return text;
        }

        var joined = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length;)
        {
            if (CodeAt(text, i) == BlackFlag
                && JoinedTails.FirstOrDefault(j => string.CompareOrdinal(text, i + 2, j.Tail, 0, j.Tail.Length) == 0) is { Tail: not null } match)
            {
                joined.Append(match.Code);
                i += 2 + match.Tail.Length;
                continue;
            }

            joined.Append(text[i]);
            i++;
        }

        return joined.ToString();
    }

    /// <summary>
    /// Whether the text element of <paramref name="text"/> at <paramref name="start"/> is an emoji: a flag, an element
    /// holding VS16 or a keycap, or one starting with an <c>Emoji_Presentation</c> code point. A lone regional
    /// indicator is none.
    /// </summary>
    public static bool IsEmoji(string text, int start, int length)
    {
        int first = CodeAt(text, start);
        if (IsRegionalIndicator(first))
        {
            return length >= 4;
        }

        var element = text.AsSpan(start, length);
        return element.Contains(EmojiPresentation) || element.Contains(Keycap) || InPresentation(first);
    }

    /// <summary>The text elements of <paramref name="text"/> — extended grapheme clusters — as UTF-16 start and length.</summary>
    public static IEnumerable<(int Start, int Length)> Elements(string text)
    {
        for (int i = 0; i < text.Length;)
        {
            int length = StringInfo.GetNextTextElementLength(text, i);
            yield return (i, length);
            i += length;
        }
    }

    /// <summary>The tag characters spelling <paramref name="region"/>, then the cancel tag: a subdivision flag's tail.</summary>
    private static string Tags(string region) =>
        string.Concat(region.Select(c => char.ConvertFromUtf32(TagFirst - 0x20 + c))) + char.ConvertFromUtf32(CancelTag);

    private static bool IsRegionalIndicator(int code) => code is >= RegionalIndicatorA and <= RegionalIndicatorZ;

    private static int CodeAt(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
            ? char.ConvertToUtf32(text[index], text[index + 1])
            : text[index];

    private static bool InPresentation(int code)
    {
        // A binary search over the sorted, disjoint ranges.
        int low = 0, high = PresentationRanges.Length / 2 - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (code < PresentationRanges[2 * middle])
            {
                high = middle - 1;
            }
            else if (code > PresentationRanges[2 * middle + 1])
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
