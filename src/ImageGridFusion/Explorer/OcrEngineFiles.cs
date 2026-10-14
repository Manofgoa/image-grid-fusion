using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ImageGridFusion.Web;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The RapidOCR engine, never shipped: its models and the native DLLs it runs on, downloaded on demand
/// into <see cref="FolderName"/> next to the exe — each file taken out of the NuGet package the app
/// references, at the version it references, by HTTP ranges (<see cref="RemoteZip"/>), and checked
/// against its SHA-256 — then loaded from there. See workfiles/20261013-complementary-ocr-passes.md
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

    private const string OnnxRuntime = "onnxruntime.dll";
    private const string SkiaSharp = "libSkiaSharp.dll";
    private const string PartialSuffix = ".~downloading";

    // How often the download's progress is reported, in ms.
    private const int ReportInterval = 100;

    // The packages' versions are the ones the project references (ImageGridFusion.csproj): change both together, the hashes with them.
    private static readonly EngineFile[] Files =
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

    /// <summary>
    /// Downloads the files missing from <see cref="Folder"/>, reporting the share done, 0 to 1. A file
    /// is written through a partial file and moved into place once its hash is checked: a failed or
    /// cancelled download leaves nothing half-written. Throws a <see cref="UrlLoadException"/> when it
    /// cannot.
    /// </summary>
    public static async Task DownloadAsync(IProgress<double> progress, CancellationToken token)
    {
        Directory.CreateDirectory(Folder);
        var missing = Files.Where(f => !File.Exists(PathOf(f.Name))).ToArray();
        var packages = new Dictionary<Uri, IReadOnlyDictionary<string, RemoteZipEntry>>();
        var entries = new List<(EngineFile File, Uri Url, RemoteZipEntry Entry)>();
        foreach (var file in missing)
        {
            var url = new Uri($"https://api.nuget.org/v3-flatcontainer/{file.Package}/{file.Version}/{file.Package}.{file.Version}.nupkg");
            if (!packages.TryGetValue(url, out var directory))
            {
                directory = await RemoteZip.ReadDirectoryAsync(url, token);
                packages[url] = directory;
            }

            entries.Add((file, url, directory.TryGetValue(file.Entry, out var entry) ? entry : throw new UrlLoadException($"{file.Package} {file.Version} has no {file.Entry}")));
        }

        long total = Math.Max(1, entries.Sum(e => e.Entry.CompressedSize));
        long before = 0;
        long reported = 0;
        progress.Report(0);
        foreach (var (file, url, entry) in entries)
        {
            string path = PathOf(file.Name);
            string partial = path + PartialSuffix;
            long start = before;
            var received = new SyncProgress(count =>
            {
                long now = Environment.TickCount64;
                if (now - reported >= ReportInterval)
                {
                    reported = now;
                    progress.Report((double)(start + count) / total);
                }
            });
            try
            {
                await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await RemoteZip.ExtractAsync(url, entry, target, received, token);
                }

                if (!HashMatches(partial, file.Sha256))
                {
                    throw new UrlLoadException($"{file.Name} does not match its expected content");
                }

                File.Move(partial, path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or UrlLoadException or OperationCanceledException)
            {
                TryDelete(partial);
                if (e is OperationCanceledException && token.IsCancellationRequested)
                {
                    throw;
                }

                throw e as UrlLoadException ?? new UrlLoadException(e switch
                {
                    HttpRequestException or OperationCanceledException => "The download broke off",
                    InvalidDataException => $"{file.Name} arrived damaged",
                    _ => $"Couldn't save {file.Name}: {e.Message}",
                }, e);
            }

            before += entry.CompressedSize;
        }

        progress.Report(1);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string name = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : libraryName + ".dll";
        string path = PathOf(name);
        return Files.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) && NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
    }

    private static bool HashMatches(string path, string sha256)
    {
        using var stream = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A file of the engine: its name in the folder, and where it comes from — a NuGet package's entry — with its SHA-256.</summary>
    private sealed record EngineFile(string Name, string Package, string Version, string Entry, string Sha256);

    /// <summary>A progress reported on the reporting thread itself, throttled by its handler.</summary>
    private sealed class SyncProgress(Action<long> handler) : IProgress<long>
    {
        public void Report(long value) => handler(value);
    }
}
