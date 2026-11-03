using ImageGridFusion.Explorer;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The upscale engine, never shipped: Real-ESRGAN's RRDBNet ×2 and ×4 (xinntao, BSD-3-Clause), exported to ONNX
/// one for one from the canonical weights by SceneWorks — installed from the Component updates window into
/// <see cref="FolderName"/> next to the exe, each file from a pinned URL and checked against its SHA-256
/// (<see cref="Components.ComponentRegistry"/>). It runs on ONNX Runtime's native DLL, the OCR engine's
/// (<see cref="OcrEngineFiles"/>). See workfiles/20261029-image-upscale.md § Engine.
/// </summary>
internal static class UpscaleEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "upscale-engine";

    public const string Model2 = "real_esrgan_x2.onnx";
    public const string Model4 = "real_esrgan_x4.onnx";

    private const string Revision = "https://huggingface.co/SceneWorks/real-esrgan-onnx/resolve/09f741bac80a246b407da3ee902bf5f3291b602f/";

    /// <summary>Both models, from one Hugging Face repository: one component.</summary>
    public static readonly EngineFile[] Files =
    [
        new(Model2, Revision + Model2, "7115ba92e8a1bfa63d68558ef006ef3d91273a068d321b1439f8bb1c9179002c", 67_073_434),
        new(Model4, Revision + Model4, "5c586662929cbc686c1a5c38d9c060dbdb4ea5863a1f7672b8c0761e6b89c033", 67_051_616),
    ];

    /// <summary>The models' folder: <c>upscale-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether an image can be upscaled: both models there, and ONNX Runtime.</summary>
    public static bool IsAvailable => DirectEngineDownload.AllPresent(Folder, Files) && OcrEngineFiles.HasRuntime;

    /// <summary>The model enlarging <paramref name="factor"/> times.</summary>
    public static string ModelFor(int factor) => factor == 4 ? Model4 : Model2;

    public static string PathOf(string name) => Path.Combine(Folder, name);
}
