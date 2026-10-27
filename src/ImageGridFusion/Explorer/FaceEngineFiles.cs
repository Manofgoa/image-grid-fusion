using System.Security.Cryptography;
using ImageGridFusion.Web;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The face engine, never shipped: two ONNX models of the OpenCV Zoo — YuNet, finding the faces, and
/// SFace, telling them apart — downloaded on demand into <see cref="FolderName"/> next to the exe, each
/// from a pinned URL and checked against its SHA-256. They run on ONNX Runtime's native DLL, the OCR
/// engine's (<see cref="OcrEngineFiles"/>), fetched with them when missing. See
/// workfiles/20261027-face-detection.md § Face Engine.
/// </summary>
internal static class FaceEngineFiles
{
    /// <summary>The models' folder next to the exe.</summary>
    public const string FolderName = "face-engine";

    public const string Detection = "face_detection_yunet_2023mar.onnx";
    public const string Recognition = "face_recognition_sface_2021dec.onnx";

    private const string PartialSuffix = ".~downloading";

    // How often the download's progress is reported, in ms.
    private const int ReportInterval = 100;

    // ONNX Runtime's share of the whole download, when it is fetched too: about 13 MB of 52.
    private const double RuntimeShare = 0.25;

    // The models' licenses: YuNet MIT, SFace Apache 2.0 — the InsightFace models are non-commercial.
    private static readonly EngineFile[] Files =
    [
        new(Detection, "https://huggingface.co/opencv/face_detection_yunet/resolve/main/" + Detection, "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4"),
        new(Recognition, "https://huggingface.co/opencv/face_recognition_sface/resolve/main/" + Recognition, "0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79"),
    ];

    /// <summary>The models' folder: <c>face-engine\</c> next to the exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether the engine can run: both models there, and ONNX Runtime.</summary>
    public static bool IsAvailable => Files.All(f => File.Exists(PathOf(f.Name))) && OcrEngineFiles.HasRuntime;

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>
    /// Downloads the models missing from <see cref="Folder"/> — and ONNX Runtime into the OCR engine's
    /// folder when it is missing too — reporting the share done, 0 to 1. A model is written through a
    /// partial file and moved into place once its hash is checked. Throws a <see cref="UrlLoadException"/>
    /// when it cannot.
    /// </summary>
    public static async Task DownloadAsync(IProgress<double> progress, CancellationToken token)
    {
        double start = 0;
        if (!OcrEngineFiles.HasRuntime)
        {
            await OcrEngineFiles.DownloadAsync(new Progress<double>(share => progress.Report(share * RuntimeShare)), token, [OcrEngineFiles.OnnxRuntime]);
            start = RuntimeShare;
        }

        Directory.CreateDirectory(Folder);
        var missing = Files.Where(f => !File.Exists(PathOf(f.Name))).ToArray();
        for (int i = 0; i < missing.Length; i++)
        {
            var file = missing[i];
            string path = PathOf(file.Name);
            string partial = path + PartialSuffix;
            double from = start + (1 - start) * i / missing.Length;
            double span = (1 - start) / missing.Length;
            try
            {
                using var response = await Downloader.GetAsync(new Uri(file.Url), token);
                Downloader.EnsureSuccess(response);
                long? total = response.Content.Headers.ContentLength;
                await using (var source = await response.Content.ReadAsStreamAsync(token))
                await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    long reported = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, token)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), token);
                        received += read;
                        long now = Environment.TickCount64;
                        if (total is > 0 && now - reported >= ReportInterval)
                        {
                            reported = now;
                            progress.Report(from + span * Math.Min(1, (double)received / total.Value));
                        }
                    }
                }

                if (!HashMatches(partial, file.Sha256))
                {
                    throw new UrlLoadException($"{file.Name} does not match its expected content");
                }

                File.Move(partial, path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or UrlLoadException or OperationCanceledException)
            {
                TryDelete(partial);
                if (e is OperationCanceledException && token.IsCancellationRequested)
                {
                    throw;
                }

                throw e as UrlLoadException ?? new UrlLoadException(e switch
                {
                    HttpRequestException or OperationCanceledException => "The download broke off",
                    _ => $"Couldn't save {file.Name}: {e.Message}",
                }, e);
            }
        }

        progress.Report(1);
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

    /// <summary>A model: its name in the folder, the URL it comes from, its SHA-256.</summary>
    private sealed record EngineFile(string Name, string Url, string Sha256);
}
