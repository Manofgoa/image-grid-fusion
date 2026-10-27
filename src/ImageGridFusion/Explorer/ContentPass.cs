namespace ImageGridFusion.Explorer;

/// <summary>
/// The indexing passes, run in this order over the indexed files: Windows OCR, the faces, the Vision pass,
/// then RapidOCR, then Windows OCR again on the colours inverted, then on the ring unrolled, and the
/// captions last. The OCR passes keep the words of an image or a PDF's first page, each in its own slots,
/// the search reading them all; the face pass keeps the faces in an index of its own; the Vision pass the
/// images' embeddings in its own, and their tags in a slot; the Caption pass a sentence per image in a
/// slot. See workfiles/20261013-complementary-ocr-passes.md § Indexing Passes,
/// workfiles/20261027-face-detection.md § Face Pass, workfiles/20261027-image-content-analysis.md § Passes.
/// </summary>
internal enum ContentPass
{
    Windows,
    Faces,
    Vision,
    Rapid,
    Inverted,
    Ring,
    Caption,
}

/// <summary>
/// Where a file's words are kept, one slot per source: each OCR pass fills its own, RapidOCR two — its sure
/// words and its unsure ones — then the Vision pass its tags and the Caption pass its sentence. A slot is
/// <b>weak</b> when its words are less reliable — read on an odd shape, or with a low confidence — and
/// ranks after the others; the tags and the caption — <b>seen</b>, not read — rank after every word read
/// (§ Ranking).
/// </summary>
internal enum ContentSlot
{
    Windows,
    Rapid,
    RapidUnsure,
    Inverted,
    Ring,
    Tags,
    Caption,
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

    /// <summary>In the tags or the caption only, as a whole word.</summary>
    Seen,

    /// <summary>In the tags or the caption only, inside a longer word.</summary>
    SeenInside,
}

internal static class ContentPasses
{
    /// <summary>Every pass, in the order they run.</summary>
    public static readonly ContentPass[] All =
        [ContentPass.Windows, ContentPass.Faces, ContentPass.Vision, ContentPass.Rapid, ContentPass.Inverted, ContentPass.Ring, ContentPass.Caption];

    /// <summary>The OCR passes, in the order they run.</summary>
    public static readonly ContentPass[] Ocr = [ContentPass.Windows, ContentPass.Rapid, ContentPass.Inverted, ContentPass.Ring];

    /// <summary>How many slots a file's content has.</summary>
    public const int SlotCount = 7;

    /// <summary>How many slots a version-3 file has: the OCR ones.</summary>
    public const int OcrSlotCount = 5;

    /// <summary>Whether the pass reads the words of the files.</summary>
    public static bool IsOcr(ContentPass pass) => pass is ContentPass.Windows or ContentPass.Rapid or ContentPass.Inverted or ContentPass.Ring;

    /// <summary>The slots a pass fills, in the order its results come; none for the face pass.</summary>
    public static ContentSlot[] SlotsOf(ContentPass pass) => pass switch
    {
        ContentPass.Windows => [ContentSlot.Windows],
        ContentPass.Rapid => [ContentSlot.Rapid, ContentSlot.RapidUnsure],
        ContentPass.Inverted => [ContentSlot.Inverted],
        ContentPass.Ring => [ContentSlot.Ring],
        ContentPass.Vision => [ContentSlot.Tags],
        ContentPass.Caption => [ContentSlot.Caption],
        _ => [],
    };

    /// <summary>Whether a slot's words rank after the others: read on an odd shape, or with a low confidence.</summary>
    public static bool IsWeak(ContentSlot slot) => slot is ContentSlot.RapidUnsure or ContentSlot.Ring;

    /// <summary>Whether a slot holds what an image shows — its tags, its caption — rather than words read in it.</summary>
    public static bool IsSeen(ContentSlot slot) => slot is ContentSlot.Tags or ContentSlot.Caption;

    /// <summary>The pass's name, for the status line and the bars' tooltip.</summary>
    public static string NameOf(ContentPass pass) => pass switch
    {
        ContentPass.Windows => "Windows OCR",
        ContentPass.Faces => "Faces",
        ContentPass.Vision => "Visual analysis",
        ContentPass.Rapid => "RapidOCR",
        ContentPass.Inverted => "Inverted",
        ContentPass.Ring => "Ring",
        _ => "Captions",
    };
}
