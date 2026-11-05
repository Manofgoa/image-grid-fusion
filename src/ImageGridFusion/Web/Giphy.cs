using System.Globalization;
using System.Net;
using System.Text.Json;
using ImageGridFusion.Resources;

namespace ImageGridFusion.Web;

/// <summary>What the GIPHY view searches: GIPHY's GIFs, or its stickers — transparent, made to lie over something.</summary>
internal enum GiphyKind
{
    Gifs,
    Stickers,
}

/// <summary>
/// One result of GIPHY: its id, its title, its kind, the small animated rendition its tile plays, and the
/// rendition brought into the grid — a GIF's MP4, a sticker's GIF, the only one keeping its transparency.
/// </summary>
internal sealed record GiphyItem(string Id, string Title, GiphyKind Kind, Uri Preview, Uri Media, string Extension);

/// <summary>A page of results: the items, and whether GIPHY holds more after them.</summary>
internal sealed record GiphyPage(IReadOnlyList<GiphyItem> Items, bool HasMore);

/// <summary>
/// The GIPHY API, the first online library: a search or the trending, page by page, through the app's shared
/// client — one call a page, the beta key allowing 100 an hour — parsed with <see cref="JsonDocument"/>. Its
/// refusals become a <see cref="UrlLoadException"/> saying why: an invalid key, the hour's calls used up.
/// See workfiles/20261105-online-library-giphy.md § Requests.
/// </summary>
internal static class Giphy
{
    /// <summary>The results a call asks for: GIPHY's largest page (workfiles/20261105-online-library-giphy.md § Loads).</summary>
    public const int PageSize = 50;

    /// <summary>The content ratings the ⚙ menu offers, from the strictest; <see cref="DefaultRating"/> when none was chosen.</summary>
    public static readonly string[] Ratings = ["g", "pg", "pg-13", "r"];

    public const string DefaultRating = "r";

    /// <summary>Where a key is made: GIPHY's developer dashboard.</summary>
    public static readonly Uri DeveloperSite = new("https://developers.giphy.com/dashboard/");

    // GIPHY serves no offset past this one.
    private const int MaxOffset = 4999;

    private static readonly Uri Api = new("https://api.giphy.com/v1/");

    /// <summary>
    /// The page of <paramref name="kind"/> at <paramref name="offset"/>: the matches of <paramref name="query"/>, or the
    /// trending when it is null.
    /// </summary>
    public static async Task<GiphyPage> FetchAsync(string key, GiphyKind kind, string? query, int offset, string rating, CancellationToken token)
    {
        var url = new Uri(Api, RequestPath(key, kind, query, offset, rating, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        using var response = await Downloader.GetAsync(url, token);
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(token);
        }
        catch (HttpRequestException e)
        {
            throw new UrlLoadException(Strings.GiphyBrokeOff, e);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UrlLoadException(Refusal(response.StatusCode, body));
        }

        try
        {
            return Parse(body, kind, offset);
        }
        catch (JsonException e)
        {
            throw new UrlLoadException(Strings.GiphyUnreadable, e);
        }
    }

    /// <summary>The request's path and query, relative to the API's root.</summary>
    internal static string RequestPath(string key, GiphyKind kind, string? query, int offset, string rating, string language)
    {
        string kindPath = kind == GiphyKind.Stickers ? "stickers" : "gifs";
        string common = $"api_key={Uri.EscapeDataString(key)}&limit={PageSize}&offset={offset}&rating={Uri.EscapeDataString(rating)}";
        return query is null
            ? $"{kindPath}/trending?{common}"
            : $"{kindPath}/search?{common}&q={Uri.EscapeDataString(query)}&lang={Uri.EscapeDataString(language)}";
    }

    /// <summary>
    /// A page read from GIPHY's answer: the items with the renditions the app needs — one missing them is
    /// left out — and whether more remain within GIPHY's reach.
    /// </summary>
    internal static GiphyPage Parse(string body, GiphyKind kind, int offset)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var items = new List<GiphyItem>();
        int received = 0;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in data.EnumerateArray())
            {
                received++;
                if (ItemOf(entry, kind) is { } item)
                {
                    items.Add(item);
                }
            }
        }

        int total = root.TryGetProperty("pagination", out var pagination) && pagination.TryGetProperty("total_count", out var count) && count.TryGetInt32(out int value)
            ? value
            : offset + received;
        int next = offset + received;
        return new GiphyPage(items, received > 0 && next < total && next <= MaxOffset);
    }

    private static GiphyItem? ItemOf(JsonElement entry, GiphyKind kind)
    {
        if (Text(entry, "id") is not { } id || !entry.TryGetProperty("images", out var images))
        {
            return null;
        }

        // The tile plays the lightest animated rendition 200 px wide; the grid gets the whole media.
        var preview = Rendition(images, "fixed_width_downsampled", "url") ?? Rendition(images, "fixed_width", "url") ?? Rendition(images, "preview_gif", "url");
        var (media, extension) = kind == GiphyKind.Gifs && Rendition(images, "original", "mp4") is { } mp4
            ? (mp4, ".mp4")
            : (Rendition(images, "original", "url"), ".gif");
        if (preview is null || media is null)
        {
            return null;
        }

        string title = Text(entry, "title")?.Trim() is { Length: > 0 } named ? named : id;
        return new GiphyItem(id, title, kind, preview, media, extension);
    }

    private static Uri? Rendition(JsonElement images, string name, string field) =>
        images.TryGetProperty(name, out var rendition) && Text(rendition, field) is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Why GIPHY refused: the key, the hour's calls, or its own message.</summary>
    private static string Refusal(HttpStatusCode status, string body) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Strings.GiphyKeyRefused,
        HttpStatusCode.TooManyRequests => Strings.GiphyTooManyCalls,
        _ => Texts.Format(Strings.CompWebServerAnswered, (int)status, MessageOf(body) ?? status.ToString()),
    };

    private static string? MessageOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("meta", out var meta) ? Text(meta, "msg") : Text(document.RootElement, "message");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
