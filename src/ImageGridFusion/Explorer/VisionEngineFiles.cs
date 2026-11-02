namespace ImageGridFusion.Explorer;

/// <summary>
/// The vision engine, never shipped: SigLIP 2 base — a vision model and a text model sharing one space of
/// embeddings, and the text model's tokenizer — installed from the Component updates window into
/// <see cref="FolderName"/> next to the exe, each file from a pinned URL and checked against its SHA-256
/// (<see cref="Components.ComponentRegistry"/>). It runs on ONNX Runtime's native DLL, the OCR engine's
/// (<see cref="OcrEngineFiles"/>). See workfiles/20261027-image-content-analysis.md § Vision Engine.
/// </summary>
internal static class VisionEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "vision-engine";

    public const string VisionModel = "siglip2_vision_model_uint8.onnx";
    public const string TextModel = "siglip2_text_model_int8.onnx";
    public const string Tokenizer = "siglip2_tokenizer.model";

    /// <summary>What the embeddings come from: a change of model makes the stored ones meaningless.</summary>
    public const string EmbeddingModelId = "siglip2-base-patch16-256 uint8";

    // SigLIP 2 is Apache 2.0 (google/siglip2-base-patch16-256); its ONNX export is onnx-community's, pinned to a revision.
    private const string OnnxRevision = "https://huggingface.co/onnx-community/siglip2-base-patch16-256-ONNX/resolve/d1114256522a37ffa257a0a58017348ab0058db2/onnx/";
    private const string BaseRevision = "https://huggingface.co/google/siglip2-base-patch16-256/resolve/3f9f96cb90da5dbc758b01813f2f6f1aee24c1ab/";

    /// <summary>The vision and text models, onnx-community's export: one component.</summary>
    public static readonly EngineFile[] ModelFiles =
    [
        new(VisionModel, OnnxRevision + "vision_model_quantized.onnx", "f2eb8ccfa3dc0b3761d9ea9a39554fe0f2be71b247ad7f68a80720ec88895650", 94_737_653),
        new(TextModel, OnnxRevision + "text_model_int8.onnx", "6f59b39d880c413042314b79302b74d0dd93b273caf8fbfdb1eb2df61a7fefd4", 283_438_275),
    ];

    /// <summary>The tokenizer, from Google's repository: another component.</summary>
    public static readonly EngineFile TokenizerFile =
        new(Tokenizer, BaseRevision + "tokenizer.model", "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2", 4_241_003);

    private static readonly EngineFile[] EmbeddingFiles = [.. ModelFiles, TokenizerFile];

    /// <summary>The models' folder: <c>vision-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether the embeddings can be computed: SigLIP 2's files there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, EmbeddingFiles) && OcrEngineFiles.HasRuntime;

    public static string PathOf(string name) => Path.Combine(Folder, name);
}
