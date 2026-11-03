namespace ImageGridFusion.Explorer;

/// <summary>
/// The face engine, never shipped: two ONNX models of the OpenCV Zoo — YuNet, finding the faces, and
/// SFace, telling them apart — installed from the Component updates window into <see cref="FolderName"/> next to
/// the exe, each from a pinned URL and checked against its SHA-256 (<see cref="Components.ComponentRegistry"/>). They
/// run on ONNX Runtime's native DLL, the OCR engine's (<see cref="OcrEngineFiles"/>). See
/// workfiles/20261027-face-detection.md § Face Engine.
/// </summary>
internal static class FaceEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "face-engine";

    public const string Detection = "face_detection_yunet_2023mar.onnx";
    public const string Recognition = "face_recognition_sface_2021dec.onnx";

    // The models' licenses: YuNet MIT, SFace Apache 2.0 — the InsightFace models are non-commercial. Each pinned to a
    // revision, its pinned version (workfiles/20261102-component-update-verification.md § Components Manifest).
    public static readonly EngineFile DetectionFile =
        new(Detection, "https://huggingface.co/opencv/face_detection_yunet/resolve/3cc26e7f1014a5ee5d74a42acee58bafc9d0a310/" + Detection, "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4", 232_589);

    public static readonly EngineFile RecognitionFile =
        new(Recognition, "https://huggingface.co/opencv/face_recognition_sface/resolve/3d7082438a6e4551e840c9b2bb60b71e8da4b524/" + Recognition, "0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79", 38_696_353);

    private static readonly EngineFile[] Files = [DetectionFile, RecognitionFile];

    /// <summary>The models' folder: <c>face-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether the engine can run: both models there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, Files) && OcrEngineFiles.HasRuntime;

    public static string PathOf(string name) => Path.Combine(Folder, name);
}
