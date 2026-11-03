using System.Reflection;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The RapidOCR engine, never shipped: its models and the native DLLs it runs on, installed from the
/// Component updates window into <see cref="FolderName"/> next to the exe — each file taken out of the NuGet
/// package the app references, at the version it references, by HTTP ranges (<see cref="RemoteZip"/>), and
/// checked against its SHA-256 (<see cref="Components.ComponentRegistry"/>) — then loaded from there. See workfiles/20261013-complementary-ocr-passes.md
/// § RapidOCR Download.
/// </summary>
internal static class OcrEngineFiles
{
    /// <summary>The engine's folder next to the exe.</summary>
    public const string FolderName = "ocr-engine";

    public const string Detection = "ch_PP-OCRv5_mobile_det.onnx";
    public const string Classification = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    public const string Recognition = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    public const string Dictionary = "ppocrv5_latin_dict.txt";

    /// <summary>ONNX Runtime's native DLL — the face engine runs on it too (<see cref="FaceEngineFiles"/>).</summary>
    public const string OnnxRuntime = "onnxruntime.dll";
    public const string SkiaSharp = "libSkiaSharp.dll";

    // The packages' versions are the ones the project references (ImageGridFusion.csproj): change both together, the hashes with them.
    public static readonly EngineFile[] Files =
    [
        new(Detection, "rapidocrnet", "4.2.0", "models/v5/" + Detection, "4d97c44a20d30a81aad087d6a396b08f786c4635742afc391f6621f5c6ae78ae"),
        new(Classification, "rapidocrnet", "4.2.0", "models/v5/" + Classification, "54379ae5174d026780215fc748a7f31910dee36818e63d49e17dc598ecc82df7"),
        new(Recognition, "rapidocrnet", "4.2.0", "models/v5/" + Recognition, "b20bd37c168a570f583afbc8cd7925603890efbcdc000a59e22c269d160b5f5a"),
        new(Dictionary, "rapidocrnet", "4.2.0", "models/v5/" + Dictionary, "7629ffd77277fd4093b556769752ee9f7c881754a8544c79a7c438cd9a93d1e6"),
        new(OnnxRuntime, "microsoft.ml.onnxruntime", "1.29.0", "runtimes/win-x64/native/" + OnnxRuntime, "69d8e6d3879a3b4001cdc74c8ed9ccc7e7f799a5b847059738323404519ec471"),
        new(SkiaSharp, "skiasharp.nativeassets.win32", "3.119.1", "runtimes/win-x64/native/" + SkiaSharp, "de22ab3e7b83644614efb55da18ef0965c3976ba4df99017f1025fbdb1893ff1"),
    ];

    private static readonly object Resolving = new();
    private static bool _resolved;

    /// <summary>The engine's folder: <c>ocr-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether every file of the engine is there.</summary>
    public static bool IsAvailable => Files.All(f => File.Exists(PathOf(f.Name)));

    /// <summary>Whether ONNX Runtime's native DLL is there, whatever else is missing.</summary>
    public static bool HasRuntime => File.Exists(PathOf(OnnxRuntime));

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>
    /// Points the ONNX Runtime and SkiaSharp assemblies at their native DLLs in <see cref="Folder"/>,
    /// once, before any of their code runs.
    /// </summary>
    public static void ResolveNativeLibraries()
    {
        lock (Resolving)
        {
            if (_resolved)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly, Resolve);
            NativeLibrary.SetDllImportResolver(typeof(SkiaSharp.SKBitmap).Assembly, Resolve);
            _resolved = true;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string name = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : libraryName + ".dll";
        string path = PathOf(name);
        return Files.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) && NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
    }

    /// <summary>A file of the engine: its name in the folder, and where it comes from — a NuGet package's entry — with its SHA-256.</summary>
    public sealed record EngineFile(string Name, string Package, string Version, string Entry, string Sha256);
}
