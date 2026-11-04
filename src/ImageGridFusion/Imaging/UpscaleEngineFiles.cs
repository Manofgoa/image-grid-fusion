using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The upscale engine, never shipped: Real-ESRGAN (xinntao, BSD-3-Clause) — its RRDBNet ×2 and ×4, the Quality
/// model, exported to ONNX one for one from the canonical weights by SceneWorks; its compact model
/// realesr-general-x4v3, the Fast one, exported by Tinier — two components installed from the Component updates
/// window into <see cref="FolderName"/> next to the exe, each file from a pinned URL and checked against its SHA-256
/// (<see cref="Components.ComponentRegistry"/>). It runs on ONNX Runtime's native DLL, the OCR engine's
/// (<see cref="OcrEngineFiles"/>). See workfiles/20261029-image-upscale.md § Engine.
/// </summary>
internal static class UpscaleEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "upscale-engine";

    public const string Model2 = "real_esrgan_x2.onnx";
    public const string Model4 = "real_esrgan_x4.onnx";
    public const string FastModel = "realesr-general-x4v3.onnx";

    private const string QualityRevision = "https://huggingface.co/SceneWorks/real-esrgan-onnx/resolve/09f741bac80a246b407da3ee902bf5f3291b602f/";
    private const string FastRevision = "https://huggingface.co/jonathanst29/tinier-upscale-models/resolve/899dc1e4b22bbf1955c2f1739c085edc080cb366/";

    /// <summary>The Quality model's two files, from one Hugging Face repository: one component.</summary>
    public static readonly EngineFile[] QualityFiles =
    [
        new(Model2, QualityRevision + Model2, "7115ba92e8a1bfa63d68558ef006ef3d91273a068d321b1439f8bb1c9179002c", 67_073_434),
        new(Model4, QualityRevision + Model4, "5c586662929cbc686c1a5c38d9c060dbdb4ea5863a1f7672b8c0761e6b89c033", 67_051_616),
    ];

    /// <summary>The Fast model, from another repository: another component.</summary>
    public static readonly EngineFile FastFile =
        new(FastModel, FastRevision + FastModel, "924ebad6532777303582d4ce7811849b88869231a3ae7093e0f21200249df8d5", 4_866_396);

    /// <summary>The models' folder: <c>upscale-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether an image can be enlarged with <paramref name="model"/>: its files there, and ONNX Runtime.</summary>
    public static bool IsAvailable(UpscaleModel model) =>
        DirectEngineDownload.AllPresent(Folder, model == UpscaleModel.Fast ? [FastFile] : QualityFiles) && OcrEngineFiles.HasRuntime;

    /// <summary>The file enlarging with <paramref name="model"/> <paramref name="factor"/> times: the Fast model's ×4 for both factors.</summary>
    public static string ModelFor(UpscaleModel model, int factor) => model == UpscaleModel.Fast ? FastModel : factor == 4 ? Model4 : Model2;

    /// <summary>How many times larger the file's output is: the Fast model's always ×4.</summary>
    public static int ScaleOf(UpscaleModel model, int factor) => model == UpscaleModel.Fast ? 4 : factor;

    public static string PathOf(string name) => Path.Combine(Folder, name);
}
