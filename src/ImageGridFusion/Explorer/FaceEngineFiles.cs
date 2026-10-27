namespace ImageGridFusion.Explorer;

/// <summary>
/// The face engine, never shipped: two ONNX models of the OpenCV Zoo — YuNet, finding the faces, and
/// SFace, telling them apart — downloaded on demand into <see cref="FolderName"/> next to the exe, each
/// from a pinned URL and checked against its SHA-256 (<see cref="DirectEngineDownload"/>). They run on
/// ONNX Runtime's native DLL, the OCR engine's (<see cref="OcrEngineFiles"/>), fetched with them when
/// missing. See workfiles/20261027-face-detection.md § Face Engine.
/// </summary>
internal static class FaceEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "face-engine";

    public const string Detection = "face_detection_yunet_2023mar.onnx";
    public const string Recognition = "face_recognition_sface_2021dec.onnx";

    // ONNX Runtime's share of the whole download, when it is fetched too: about 13 MB of 52.
    private const double RuntimeShare = 0.25;

    // The models' licenses: YuNet MIT, SFace Apache 2.0 — the InsightFace models are non-commercial.
    private static readonly EngineFile[] Files =
    [
        new(Detection, "https://huggingface.co/opencv/face_detection_yunet/resolve/main/" + Detection, "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4", 232_589),
        new(Recognition, "https://huggingface.co/opencv/face_recognition_sface/resolve/main/" + Recognition, "0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79", 38_696_353),
    ];

    /// <summary>The models' folder: <c>face-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether the engine can run: both models there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, Files) && OcrEngineFiles.HasRuntime;

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>
    /// Downloads the models missing from <see cref="Folder"/> — and ONNX Runtime into the OCR engine's
    /// folder when it is missing too — reporting the share done, 0 to 1. Throws a
    /// <see cref="Web.UrlLoadException"/> when it cannot.
    /// </summary>
    public static Task DownloadAsync(IProgress<double> progress, CancellationToken token) =>
        DirectEngineDownload.DownloadAsync(Folder, Files, RuntimeShare, progress, token);
}
