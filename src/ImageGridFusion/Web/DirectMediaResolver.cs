using System.Net.Http.Headers;

namespace ImageGridFusion.Web;

/// <summary>
/// A link straight to a media: the response's content type decides — an image or a video is
/// downloaded, anything else (an HTML page) passes to the next resolver. The file's extension
/// follows the content type, since a video is only opened with a video extension.
/// See workfiles/20261009-url-content.md § Direct Link.
/// </summary>
internal static class DirectMediaResolver
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/pjpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/bmp"] = ".bmp",
        ["image/tiff"] = ".tif",
        ["image/svg+xml"] = ".svg",
        ["image/avif"] = ".avif",
        ["image/heic"] = ".heic",
        ["image/x-icon"] = ".ico",
        ["image/vnd.microsoft.icon"] = ".ico",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov",
        ["video/x-msvideo"] = ".avi",
        ["video/x-matroska"] = ".mkv",
        ["video/mpeg"] = ".mpg",
        ["video/3gpp"] = ".3gp",
        ["video/3gpp2"] = ".3g2",
        ["video/x-ms-wmv"] = ".wmv",
        ["video/x-ms-asf"] = ".asf",
        ["video/mp2t"] = ".ts",
        ["video/x-m4v"] = ".m4v",
    };

    /// <summary>
    /// The media at <paramref name="url"/>, downloaded — or the file already there; null when the URL
    /// gives something else than a media.
    /// </summary>
    public static async Task<string?> TryLoadAsync(Uri url, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        string urlExtension = Path.GetExtension(url.AbsolutePath).ToLowerInvariant();
        bool mediaLink = Extensions.ContainsValue(urlExtension);
        using var response = await Downloader.GetAsync(url, token);
        if (!response.IsSuccessStatusCode)
        {
            // A link to a media file that fails is that file's error; a page that fails may still be
            // read by yt-dlp, which asks the site its own way.
            if (mediaLink)
            {
                Downloader.EnsureSuccess(response);
            }

            return null;
        }

        if (ExtensionOf(response.Content.Headers.ContentType, urlExtension, mediaLink) is not { } extension)
        {
            return null;
        }

        string stem = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(url.AbsolutePath.TrimEnd('/')));
        string path = UrlDownloads.PathOf($"{(stem.Length == 0 ? url.Host : stem)}-{UrlDownloads.Tag(url)}", extension);
        return File.Exists(path) ? path : await Downloader.SaveAsync(response, path, progress, token);
    }

    /// <summary>The extension of a media response, or null when it is not one.</summary>
    private static string? ExtensionOf(MediaTypeHeaderValue? contentType, string urlExtension, bool mediaLink)
    {
        string type = contentType?.MediaType ?? "";
        if (type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || type.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            if (Extensions.TryGetValue(type, out string? known))
            {
                return known;
            }

            return mediaLink ? urlExtension : "." + type[(type.IndexOf('/') + 1)..].Split('+')[0].ToLowerInvariant();
        }

        // A server giving no useful type for a file whose name says it is a media.
        return mediaLink && type is "" or "application/octet-stream" or "binary/octet-stream" ? urlExtension : null;
    }
}
