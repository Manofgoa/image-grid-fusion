using System.Security.Cryptography;
using ImageGridFusion.Web;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The download of an engine's model files from pinned URLs, each checked against its SHA-256 — the face
/// engine's and the vision engine's. ONNX Runtime's native DLL, the OCR engine's, comes first when it is
/// missing. See workfiles/20261027-image-content-analysis.md § Vision Engine.
/// </summary>
internal static class DirectEngineDownload
{
    private const string PartialSuffix = ".~downloading";

    // How often the download's progress is reported, in ms.
    private const int ReportInterval = 100;

    /// <summary>Whether every file is in <paramref name="folder"/>.</summary>
    public static bool AllPresent(string folder, IEnumerable<EngineFile> files) => files.All(f => File.Exists(Path.Combine(folder, f.Name)));

    /// <summary>
    /// Downloads the files missing from <paramref name="folder"/> — and ONNX Runtime into the OCR engine's
    /// folder when it is missing too, as <paramref name="runtimeShare"/> of the whole — reporting the share
    /// done, 0 to 1. A file is written through a partial file and moved into place once its hash is checked.
    /// Throws a <see cref="UrlLoadException"/> when it cannot.
    /// </summary>
    public static async Task DownloadAsync(string folder, IReadOnlyList<EngineFile> files, double runtimeShare, IProgress<double> progress, CancellationToken token)
    {
        double start = 0;
        if (!OcrEngineFiles.HasRuntime)
        {
            await OcrEngineFiles.DownloadAsync(new Progress<double>(share => progress.Report(share * runtimeShare)), token, [OcrEngineFiles.OnnxRuntime]);
            start = runtimeShare;
        }

        await DownloadMissingAsync(folder, files, start, progress, token);
    }

    /// <summary>
    /// Downloads the files missing from <paramref name="folder"/> without ONNX Runtime — files no engine runs, the
    /// emoji fonts (workfiles/20261102-emoji-fonts.md § Shipped Fonts) — reporting the share done, 0 to 1. Throws a
    /// <see cref="UrlLoadException"/> when it cannot.
    /// </summary>
    public static Task DownloadAsync(string folder, IReadOnlyList<EngineFile> files, IProgress<double> progress, CancellationToken token) =>
        DownloadMissingAsync(folder, files, 0, progress, token);

    /// <summary>
    /// The response to <paramref name="url"/> written to <paramref name="path"/>, the share received reported to
    /// <paramref name="report"/>, 0 to 1. Throws what the request or the file throws.
    /// </summary>
    public static async Task SaveAsync(Uri url, string path, Action<double> report, CancellationToken token)
    {
        using var response = await Downloader.GetAsync(url, token);
        Downloader.EnsureSuccess(response);
        long? total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(token);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
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
                report(Math.Min(1, (double)received / total.Value));
            }
        }
    }

    /// <summary>The SHA-256 of the file at <paramref name="path"/>, in hexadecimal.</summary>
    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Deletes the file at <paramref name="path"/>, nothing said when it cannot.</summary>
    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The files missing from <paramref name="folder"/>, their progress reported from <paramref name="start"/> to 1.</summary>
    private static async Task DownloadMissingAsync(string folder, IReadOnlyList<EngineFile> files, double start, IProgress<double> progress, CancellationToken token)
    {
        Directory.CreateDirectory(folder);
        var missing = files.Where(f => !File.Exists(Path.Combine(folder, f.Name))).ToArray();
        long weight = Math.Max(1, missing.Sum(f => f.Size));
        long before = 0;
        foreach (var file in missing)
        {
            string path = Path.Combine(folder, file.Name);
            string partial = path + PartialSuffix;
            double from = start + (1 - start) * before / weight;
            double span = (1 - start) * file.Size / weight;
            before += file.Size;
            try
            {
                await SaveAsync(new Uri(file.Url), partial, share => progress.Report(from + span * share), token);
                if (!string.Equals(Sha256Of(partial), file.Sha256, StringComparison.OrdinalIgnoreCase))
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
}

/// <summary>A model file: its name in its engine's folder, the URL it comes from, its SHA-256, and its size in bytes — the share of the download's progress it weighs.</summary>
internal sealed record EngineFile(string Name, string Url, string Sha256, long Size);
