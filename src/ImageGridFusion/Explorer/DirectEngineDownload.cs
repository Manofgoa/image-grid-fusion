using System.Security.Cryptography;
using ImageGridFusion.Web;

namespace ImageGridFusion.Explorer;

/// <summary>
/// What the downloads of the engines' files share — a response saved with its progress, a file's SHA-256 — and the
/// check that an engine's files are there; the downloads themselves are the Component updates window's
/// (<see cref="Components.ComponentInstaller"/>). See workfiles/20261027-image-content-analysis.md § Vision Engine.
/// </summary>
internal static class DirectEngineDownload
{
    // How often the download's progress is reported, in ms.
    private const int ReportInterval = 100;

    /// <summary>Whether every file is in <paramref name="folder"/>.</summary>
    public static bool AllPresent(string folder, IEnumerable<EngineFile> files) => files.All(f => File.Exists(Path.Combine(folder, f.Name)));

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
}

/// <summary>A model file: its name in its engine's folder, the URL it comes from, its SHA-256, and its size in bytes — the share of the download's progress it weighs.</summary>
internal sealed record EngineFile(string Name, string Url, string Sha256, long Size);
