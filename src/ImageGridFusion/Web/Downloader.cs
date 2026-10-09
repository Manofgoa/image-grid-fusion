using System.Net;

namespace ImageGridFusion.Web;

/// <summary>
/// The app's HTTP: one shared client with a browser's User-Agent, and the download of a response to a
/// file — streamed to a partial file renamed once complete, its progress reported.
/// See workfiles/20261009-url-content.md § Download.
/// </summary>
internal static class Downloader
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    private const string PartialSuffix = ".~downloading";
    private const int BufferSize = 81920;

    // How often the progress is reported, in ms: the URL zone needs no more.
    private const int ReportInterval = 100;

    public static HttpClient Client { get; } = Create();

    /// <summary>The response's headers, its body left to read. Throws a <see cref="UrlLoadException"/> when the server cannot be reached.</summary>
    public static async Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken token)
    {
        try
        {
            return await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException e)
        {
            throw new UrlLoadException($"Couldn't reach {url.Host}: {e.Message}", e);
        }
        catch (TaskCanceledException e) when (!token.IsCancellationRequested)
        {
            throw new UrlLoadException($"{url.Host} didn't answer in time", e);
        }
    }

    /// <summary>Downloads <paramref name="url"/> to <paramref name="path"/>; returns the path.</summary>
    public static async Task<string> DownloadAsync(Uri url, string path, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        using var response = await GetAsync(url, token);
        EnsureSuccess(response);
        return await SaveAsync(response, path, progress, token);
    }

    /// <summary>Throws a <see cref="UrlLoadException"/> naming the status when the server refused.</summary>
    public static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UrlLoadException($"The server answered {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    /// <summary>
    /// Streams the response's body to <paramref name="path"/> through a partial file, renamed once
    /// complete; returns the path. A failed or cancelled download leaves no file behind.
    /// </summary>
    public static async Task<string> SaveAsync(HttpResponseMessage response, string path, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string partial = path + PartialSuffix;
        long? total = response.Content.Headers.ContentLength;
        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                long received = 0;
                long reported = Environment.TickCount64;
                progress.Report(new DownloadingStep(0, total));
                int read;
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    received += read;
                    if (Environment.TickCount64 - reported >= ReportInterval)
                    {
                        reported = Environment.TickCount64;
                        progress.Report(new DownloadingStep(received, total));
                    }
                }

                progress.Report(new DownloadingStep(received, total));
            }

            File.Move(partial, path, overwrite: true);
            return path;
        }
        catch (Exception e) when (e is IOException or HttpRequestException or UnauthorizedAccessException or OperationCanceledException)
        {
            TryDelete(partial);
            if (e is OperationCanceledException && token.IsCancellationRequested)
            {
                throw;
            }

            throw new UrlLoadException(e switch
            {
                HttpRequestException or OperationCanceledException => "The download broke off",
                _ => $"Couldn't save the file: {e.Message}",
            }, e);
        }
    }

    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left to the start-up cleanup.
        }
    }
}
