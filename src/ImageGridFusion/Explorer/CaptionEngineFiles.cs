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
    private static readonly EngineFile[] Files = [];

    /// <summary>Whether the captions can be written: Florence-2's files there, and ONNX Runtime.</summary>
    public static bool IsAvailable => Files.Length > 0 && DirectEngineDownload.AllPresent(VisionEngineFiles.Folder, Files) && OcrEngineFiles.HasRuntime;

    /// <summary>The download's size in MB, for the offer.</summary>
    public static int DownloadMegabytes => (int)(Files.Sum(f => f.Size) / 1_000_000);

    /// <summary>Downloads Florence-2's files missing — and ONNX Runtime when it is missing too — reporting the share done, 0 to 1.</summary>
    public static Task DownloadAsync(IProgress<double> progress, CancellationToken token) =>
        DirectEngineDownload.DownloadAsync(VisionEngineFiles.Folder, Files, 0.05, progress, token);
}
