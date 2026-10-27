namespace ImageGridFusion.Explorer;

/// <summary>
/// The indexing passes, run in this order over the indexed files: Windows OCR, the faces, then RapidOCR,
/// then Windows OCR again on the colours inverted, then on the ring unrolled. The OCR passes keep the
/// words of an image or a PDF's first page, each in its own slots, the search reading them all; the face
/// pass keeps the faces in an index of its own. See workfiles/20261013-complementary-ocr-passes.md
/// § Indexing Passes, workfiles/20261027-face-detection.md § Face Pass.
/// </summary>
internal enum ContentPass
{
    Windows,
    Faces,
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

internal static class ContentPasses
{
    /// <summary>Every pass, in the order they run.</summary>
    public static readonly ContentPass[] All = [ContentPass.Windows, ContentPass.Faces, ContentPass.Rapid, ContentPass.Inverted, ContentPass.Ring];

    /// <summary>The OCR passes, in the order they run: the ones filling the slots of <c>files.content</c>.</summary>
    public static readonly ContentPass[] Ocr = [ContentPass.Windows, ContentPass.Rapid, ContentPass.Inverted, ContentPass.Ring];

    /// <summary>How many slots a file's content has.</summary>
    public const int SlotCount = 5;

    /// <summary>Whether the pass reads the words of the files.</summary>
    public static bool IsOcr(ContentPass pass) => pass != ContentPass.Faces;

    /// <summary>The slots a pass fills, in the order its results come; none for the face pass.</summary>
    public static ContentSlot[] SlotsOf(ContentPass pass) => pass switch
    {
        ContentPass.Windows => [ContentSlot.Windows],
        ContentPass.Rapid => [ContentSlot.Rapid, ContentSlot.RapidUnsure],
        ContentPass.Inverted => [ContentSlot.Inverted],
        ContentPass.Ring => [ContentSlot.Ring],
        _ => [],
    };

    /// <summary>Whether a slot's words rank after the others: read on an odd shape, or with a low confidence.</summary>
    public static bool IsWeak(ContentSlot slot) => slot is ContentSlot.RapidUnsure or ContentSlot.Ring;

    /// <summary>The pass's name, for the status line and the bars' tooltip.</summary>
    public static string NameOf(ContentPass pass) => pass switch
    {
        ContentPass.Windows => "Windows OCR",
        ContentPass.Faces => "Faces",
        ContentPass.Rapid => "RapidOCR",
        ContentPass.Inverted => "Inverted",
        _ => "Ring",
    };
}
