namespace ImageGridFusion.Web;

/// <summary>
/// The media behind a URL, downloaded to a file a cell takes like any other: the resolvers tried in
/// order — X / Twitter, a direct link, yt-dlp — the first that recognises the URL answering. A new site
/// adds its resolver here. See workfiles/20261009-url-content.md § Resolution.
/// </summary>
internal static class UrlResolver
{
    /// <summary>
    /// The web address typed or pasted, <c>https://</c> assumed when it names none; null when it is
    /// not an <c>http</c> or <c>https</c> address.
    /// </summary>
    public static Uri? Parse(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && url.Host.Contains('.')
            ? url
            : null;
    }

    /// <summary>
    /// The path of the downloaded media. Throws a <see cref="UrlLoadException"/> saying why when none
    /// can be had — a <see cref="YtDlpMissingException"/> when only yt-dlp could, and it is missing.
    /// </summary>
    public static async Task<string> LoadAsync(Uri url, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        progress.Report(new ResolvingStep());
        return await XPostResolver.TryLoadAsync(url, progress, token)
            ?? await DirectMediaResolver.TryLoadAsync(url, progress, token)
            ?? await YtDlp.LoadAsync(url, progress, token);
    }
}
