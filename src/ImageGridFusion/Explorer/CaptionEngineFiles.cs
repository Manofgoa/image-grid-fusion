namespace ImageGridFusion.Explorer;

/// <summary>
/// The caption engine, never shipped: Florence-2 base — its vision encoder, its token embeddings, its
/// encoder and its decoder, and its tokenizer's vocabulary — downloaded on demand into the vision engine's
/// folder (<see cref="VisionEngineFiles.FolderName"/>), each file from a pinned URL and checked against its
/// SHA-256 (<see cref="DirectEngineDownload"/>), apart from SigLIP 2: the captions are a setting of their
/// own. See workfiles/20261027-image-content-analysis.md § Engine Offer.
/// </summary>
internal static class CaptionEngineFiles
{
    public const string Vocabulary = "florence2_vocab.json";

    /// <summary>The models, in the order the captioner loads them: vision encoder, token embeddings, encoder, decoder.</summary>
    public static readonly string[] Models =
        ["florence2_vision_encoder_uint8.onnx", "florence2_embed_tokens_int8.onnx", "florence2_encoder_model_int8.onnx", "florence2_decoder_model_int8.onnx"];

    // ONNX Runtime's share of the whole download, when it is fetched too: about 13 MB of 288.
    private const double RuntimeShare = 0.05;

    // Florence-2 is MIT; its ONNX export is onnx-community's, pinned to a revision.
    private const string Revision = "https://huggingface.co/onnx-community/Florence-2-base/resolve/d59e079711c57174f29265539fb4cc9f0f335916/";

    private static readonly EngineFile[] Files =
    [
        new(Models[0], Revision + "onnx/vision_encoder_quantized.onnx", "56351a93ecfb61a0384dc63c130cc5bdeea8de1a85f172e109821c7cc956be5a", 93_788_276),
        new(Models[1], Revision + "onnx/embed_tokens_int8.onnx", "8818c58a214e53bf7e22c48bb4674c2fa3112b9539c3ce4075a2eac797b1ef74", 39_390_496),
        new(Models[2], Revision + "onnx/encoder_model_int8.onnx", "a0459867dc116e8e49f53073e368e45c21169d0b0dd7dc350abed06926e0d835", 43_651_493),
        new(Models[3], Revision + "onnx/decoder_model_int8.onnx", "6054c18012c1dd313c0428ce159379840a1e188ab2f9da0d590bf0640bbeaa1c", 97_760_937),
        new(Vocabulary, Revision + "vocab.json", "ed19656ea1707df69134c4af35c8ceda2cc9860bf2c3495026153a133670ab5e", 798_293),
    ];

    /// <summary>Whether the captions can be written: Florence-2's files there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(VisionEngineFiles.Folder, Files) && OcrEngineFiles.HasRuntime;

    /// <summary>The download's size in MB, for the offer.</summary>
    public static int DownloadMegabytes => (int)(Files.Sum(f => f.Size) / 1_000_000);

    /// <summary>Downloads Florence-2's files missing — and ONNX Runtime when it is missing too — reporting the share done, 0 to 1.</summary>
    public static Task DownloadAsync(IProgress<double> progress, CancellationToken token) =>
        DirectEngineDownload.DownloadAsync(VisionEngineFiles.Folder, Files, RuntimeShare, progress, token);
}
