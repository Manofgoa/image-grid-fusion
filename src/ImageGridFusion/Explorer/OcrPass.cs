namespace ImageGridFusion.Explorer;

/// <summary>
/// The indexing passes reading the words of an image or a PDF's first page, run in this order over
/// every such file: Windows OCR, then RapidOCR, then Windows OCR again on the colours inverted, then on
/// the ring unrolled. Each keeps its own words; the search reads them all. See
/// workfiles/20261013-complementary-ocr-passes.md § Indexing Passes.
/// </summary>
internal enum OcrPass
{
    Windows,
    Rapid,
    Inverted,
    Ring,
}

/// <summary>
/// Where a file's words are kept, one slot per source: each pass fills its own, RapidOCR two — its sure
/// words and its unsure ones. A slot is <b>weak</b> when its words are less reliable — read on an odd
/// shape, or with a low confidence — and ranks after the others (§ Ranking).
/// </summary>
internal enum ContentSlot
{
    Windows,
    Rapid,
    RapidUnsure,
    Inverted,
    Ring,
}

/// <summary>How reliably a file's content holds a query word: lower ranks first.</summary>
internal enum ContentGrade
{
    /// <summary>In a sure slot, as one word or inside one.</summary>
    Sure,

    /// <summary>In a sure slot once its spaces are removed — letters the OCR read apart.</summary>
    Joined,

    /// <summary>In a weak slot only, either way.</summary>
    Weak,
}

internal static class OcrPasses
{
    /// <summary>Every pass, in the order they run.</summary>
    public static readonly OcrPass[] All = [OcrPass.Windows, OcrPass.Rapid, OcrPass.Inverted, OcrPass.Ring];

    /// <summary>How many slots a file's content has.</summary>
    public const int SlotCount = 5;

    /// <summary>The slots a pass fills, in the order its results come.</summary>
    public static ContentSlot[] SlotsOf(OcrPass pass) => pass switch
    {
        OcrPass.Windows => [ContentSlot.Windows],
        OcrPass.Rapid => [ContentSlot.Rapid, ContentSlot.RapidUnsure],
        OcrPass.Inverted => [ContentSlot.Inverted],
        _ => [ContentSlot.Ring],
    };

    /// <summary>Whether a slot's words rank after the others: read on an odd shape, or with a low confidence.</summary>
    public static bool IsWeak(ContentSlot slot) => slot is ContentSlot.RapidUnsure or ContentSlot.Ring;

    /// <summary>The pass's name, for the status line and the bars' tooltip.</summary>
    public static string NameOf(OcrPass pass) => pass switch
    {
        OcrPass.Windows => "Windows OCR",
        OcrPass.Rapid => "RapidOCR",
        OcrPass.Inverted => "Inverted",
        _ => "Ring",
    };
}
