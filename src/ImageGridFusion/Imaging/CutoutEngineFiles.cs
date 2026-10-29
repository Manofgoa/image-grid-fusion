using ImageGridFusion.Explorer;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The cutout engine, never shipped: IS-Net — the main subject of an image — and MobileSAM — the
/// object under the points clicked, its image encoder and its two mask decoders — downloaded on demand,
/// in one go, into <see cref="FolderName"/> next to the exe, each file from a pinned URL and checked
/// against its SHA-256 (<see cref="DirectEngineDownload"/>). It runs on ONNX Runtime's native DLL, the OCR
/// engine's (<see cref="OcrEngineFiles"/>), fetched with it when missing. See workfiles/20261029-cutout.md § Engine.
/// </summary>
internal static class CutoutEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "cutout-engine";

    public const string SubjectModel = "isnet-general-use.onnx";
    public const string PickEncoder = "mobile_sam_image_encoder.onnx";
    public const string PickSingle = "sam_mask_decoder_single.onnx";
    public const string PickMulti = "sam_mask_decoder_multi.onnx";

    // ONNX Runtime's share of the whole download, when it is fetched too: about 13 MB of 253.
    private const double RuntimeShare = 0.05;

    // IS-Net general use is the DIS authors' (xuebinqin/DIS, Apache 2.0), its ONNX export rembg's release
    // v0.0.0; MobileSAM's ONNX export is Acly's, MIT, pinned to a revision. BiRefNet takes gigabytes on the
    // CPU — the full one does not fit, the lite one runs 12 s at 6 GB: IS-Net, 2 s at 0.7 GB
    // (workfiles/20261029-cutout.md, Iteration 5).
    private const string SubjectUrl = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx";
    private const string PickRevision = "https://huggingface.co/Acly/MobileSAM/resolve/0d3b403339b4674a82493d5e97964dd78089ddc8/";

    private static readonly EngineFile[] Files =
    [
        new(SubjectModel, SubjectUrl, "60920e99c45464f2ba57bee2ad08c919a52bbf852739e96947fbb4358c0d964a", 178_648_008),
        new(PickEncoder, PickRevision + PickEncoder, "580f5fb648ea1062c0aabc26217aed56921985f03f0cbbd852bba81d760cc749", 28_157_093),
        new(PickSingle, PickRevision + PickSingle, "93915fc7c993ab9d59ab8c9ccd3bce37f7509c81ab4150a74abd4d2abbd8570d", 16_501_323),
        new(PickMulti, PickRevision + PickMulti, "8976b90a87ba50a6a72217a5ff994f7d25ce16f2229fcc1ed259e1294c622ffe", 16_496_559),
    ];

    /// <summary>The models' folder: <c>cutout-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether a cutout can be computed: every model there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, Files) && OcrEngineFiles.HasRuntime;

    /// <summary>The download's size in MB, for the offer.</summary>
    public static int DownloadMegabytes => (int)(Files.Sum(f => f.Size) / 1_000_000);

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>
    /// Downloads the models missing from <see cref="Folder"/> — and ONNX Runtime into the OCR engine's
    /// folder when it is missing too — reporting the share done, 0 to 1. Throws a
    /// <see cref="Web.UrlLoadException"/> when it cannot.
    /// </summary>
    public static Task DownloadAsync(IProgress<double> progress, CancellationToken token) =>
        DirectEngineDownload.DownloadAsync(Folder, Files, RuntimeShare, progress, token);
}
