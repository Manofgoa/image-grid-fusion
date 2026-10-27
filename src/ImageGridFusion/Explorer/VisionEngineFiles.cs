namespace ImageGridFusion.Explorer;

/// <summary>
/// The vision engine, never shipped: SigLIP 2 base — a vision model and a text model sharing one space of
/// embeddings, and the text model's tokenizer — downloaded on demand into <see cref="FolderName"/> next to
/// the exe, each file from a pinned URL and checked against its SHA-256 (<see cref="DirectEngineDownload"/>).
/// It runs on ONNX Runtime's native DLL, the OCR engine's (<see cref="OcrEngineFiles"/>), fetched with it
/// when missing. See workfiles/20261027-image-content-analysis.md § Vision Engine.
/// </summary>
internal static class VisionEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "vision-engine";

    public const string VisionModel = "siglip2_vision_model_int8.onnx";
    public const string TextModel = "siglip2_text_model_int8.onnx";
    public const string Tokenizer = "siglip2_tokenizer.model";

    /// <summary>What the embeddings come from: a change of model makes the stored ones meaningless.</summary>
    public const string EmbeddingModelId = "siglip2-base-patch16-256 int8";

    // ONNX Runtime's share of the whole download, when it is fetched too: about 13 MB of 395.
    private const double RuntimeShare = 0.03;

    // SigLIP 2 is Apache 2.0 (google/siglip2-base-patch16-256); its ONNX export is onnx-community's, pinned to a revision.
    private const string OnnxRevision = "https://huggingface.co/onnx-community/siglip2-base-patch16-256-ONNX/resolve/d1114256522a37ffa257a0a58017348ab0058db2/onnx/";
    private const string BaseRevision = "https://huggingface.co/google/siglip2-base-patch16-256/resolve/3f9f96cb90da5dbc758b01813f2f6f1aee24c1ab/";

    private static readonly EngineFile[] EmbeddingFiles =
    [
        new(VisionModel, OnnxRevision + "vision_model_int8.onnx", "8243a496e524bf8fa19830ff42a40a4bae2e97acc2e288d4ae97601edf954977", 94_737_653),
        new(TextModel, OnnxRevision + "text_model_int8.onnx", "6f59b39d880c413042314b79302b74d0dd93b273caf8fbfdb1eb2df61a7fefd4", 283_438_275),
        new(Tokenizer, BaseRevision + "tokenizer.model", "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2", 4_241_003),
    ];

    /// <summary>The models' folder: <c>vision-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether the embeddings can be computed: SigLIP 2's files there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, EmbeddingFiles) && OcrEngineFiles.HasRuntime;

    /// <summary>The download's size in MB, for the offer.</summary>
    public static int DownloadMegabytes => (int)(EmbeddingFiles.Sum(f => f.Size) / 1_000_000);

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>
    /// Downloads SigLIP 2's files missing from <see cref="Folder"/> — and ONNX Runtime into the OCR engine's
    /// folder when it is missing too — reporting the share done, 0 to 1. Throws a
    /// <see cref="Web.UrlLoadException"/> when it cannot.
    /// </summary>
    public static Task DownloadAsync(IProgress<double> progress, CancellationToken token) =>
        DirectEngineDownload.DownloadAsync(Folder, EmbeddingFiles, RuntimeShare, progress, token);
}
