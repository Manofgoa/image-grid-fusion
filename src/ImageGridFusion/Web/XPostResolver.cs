using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageGridFusion.Web;

/// <summary>
/// The first media of an X / Twitter post, read from the public syndication endpoint the embed widget
/// uses — no account: the MP4 of highest bitrate for a video or a GIF, the original for a photo, the
/// media's picture as the poster while it downloads. See workfiles/20261009-url-content.md § X / Twitter.
/// </summary>
internal static partial class XPostResolver
{
    private const string Endpoint = "https://cdn.syndication.twimg.com/tweet-result";
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    // The hosts a post link comes from, a leading "www." or "mobile." aside.
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "x.com", "twitter.com", "fxtwitter.com", "vxtwitter.com", "fixupx.com",
    };

    /// <summary>The post's id when <paramref name="url"/> is the link of an X / Twitter post, else null.</summary>
    public static ulong? PostId(Uri url)
    {
        string host = url.Host;
        foreach (string prefix in (string[])["www.", "mobile."])
        {
            if (host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                host = host[prefix.Length..];
            }
        }

        return Hosts.Contains(host) && StatusPath().Match(url.AbsolutePath) is { Success: true } match
            && ulong.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)
            ? id
            : null;
    }

    /// <summary>
    /// The post's first media, downloaded — or the file already there; null when <paramref name="url"/>
    /// is not a post link. Throws a <see cref="UrlLoadException"/> when the post has no media, or none
    /// can be read.
    /// </summary>
    public static async Task<string?> TryLoadAsync(Uri url, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        if (PostId(url) is not { } id)
        {
            return null;
        }

        using var post = await FetchAsync(id, token);
        var root = post.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "__typename") == "TweetTombstone" || !root.TryGetProperty("id_str", out _))
        {
            throw new UrlLoadException("Post not found or private");
        }

        var (media, poster) = FirstMedia(root) ?? throw new UrlLoadException("No photo or video in this post");
        if (poster is not null)
        {
            progress.Report(new PosterStep(poster));
        }

        string user = root.TryGetProperty("user", out var author) ? Text(author, "screen_name") ?? "post" : "post";
        string path = UrlDownloads.PathOf($"x-{user}-{id}", Extension(media));
        return File.Exists(path) ? path : await Downloader.DownloadAsync(media, path, progress, token);
    }

    /// <summary>
    /// The token the endpoint asks for, computed from the id as the embed widget does:
    /// <c>((id / 1e15) * π).toString(36)</c>, its zeros and its point removed.
    /// </summary>
    public static string Token(ulong id)
    {
        string base36 = ToRadix36(id / 1e15 * Math.PI);
        return base36.Replace("0", "").Replace(".", "");
    }

    private static async Task<JsonDocument> FetchAsync(ulong id, CancellationToken token)
    {
        var url = new Uri($"{Endpoint}?id={id}&token={Token(id)}&lang=en");
        using var response = await Downloader.GetAsync(url, token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UrlLoadException("Post not found or private");
        }

        Downloader.EnsureSuccess(response);
        try
        {
            string body = await response.Content.ReadAsStringAsync(token);
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new UrlLoadException("Post not found or private");
            }

            return JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            throw new UrlLoadException("X answered something unreadable", e);
        }
        catch (HttpRequestException e)
        {
            throw new UrlLoadException($"Couldn't read X's answer: {e.Message}", e);
        }
    }

    /// <summary>The first media's file and its picture, or null when the post has none (only one media taken, Q3).</summary>
    private static (Uri Media, Uri? Poster)? FirstMedia(JsonElement post)
    {
        if (!post.TryGetProperty("mediaDetails", out var details) || details.ValueKind != JsonValueKind.Array || details.GetArrayLength() == 0)
        {
            return null;
        }

        var first = details[0];
        Uri? picture = Uri.TryCreate(Text(first, "media_url_https"), UriKind.Absolute, out var p) ? p : null;
        switch (Text(first, "type"))
        {
            case "video":
            case "animated_gif":
                if (!first.TryGetProperty("video_info", out var info) || !info.TryGetProperty("variants", out var variants)
                    || variants.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                Uri? best = null;
                long bestRate = -1;
                foreach (var variant in variants.EnumerateArray())
                {
                    long rate = variant.TryGetProperty("bitrate", out var b) && b.TryGetInt64(out long r) ? r : 0;
                    if (Text(variant, "content_type") == "video/mp4" && rate > bestRate
                        && Uri.TryCreate(Text(variant, "url"), UriKind.Absolute, out var candidate))
                    {
                        best = candidate;
                        bestRate = rate;
                    }
                }

                return best is null ? null : (best, picture);
            case "photo" when picture is not null:
                return (new Uri(picture.GetLeftPart(UriPartial.Path) + "?name=orig"), new Uri(picture.GetLeftPart(UriPartial.Path) + "?name=small"));
            default:
                return null;
        }
    }

    private static string Extension(Uri media)
    {
        string extension = Path.GetExtension(media.AbsolutePath);
        return extension.Length is > 1 and <= 5 ? extension.ToLowerInvariant() : ".mp4";
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// A positive double written in base 36 as JavaScript's <c>Number.prototype.toString(36)</c> writes
    /// it (V8's DoubleToRadixCString): the fraction's digits stop as soon as they tell the double apart
    /// from its neighbours, the last one rounded.
    /// </summary>
    private static string ToRadix36(double value)
    {
        double integer = Math.Floor(value);
        double fraction = value - integer;
        double delta = Math.Max(0.5 * (Math.BitIncrement(value) - value), Math.BitIncrement(0.0));
        var digits = new StringBuilder();
        if (fraction >= delta)
        {
            do
            {
                fraction *= 36;
                delta *= 36;
                int digit = (int)fraction;
                digits.Append(Digits[digit]);
                fraction -= digit;
                if ((fraction > 0.5 || (fraction == 0.5 && (digit & 1) == 1)) && fraction + delta > 1)
                {
                    // Round up, carrying into the digits before — and into the integer past the first.
                    while (true)
                    {
                        if (digits.Length == 0)
                        {
                            integer += 1;
                            break;
                        }

                        int last = Digits.IndexOf(digits[^1]);
                        digits.Length--;
                        if (last + 1 < 36)
                        {
                            digits.Append(Digits[last + 1]);
                            break;
                        }
                    }

                    break;
                }
            }
            while (fraction >= delta);
        }

        var whole = new StringBuilder();
        for (ulong n = (ulong)integer; n > 0; n /= 36)
        {
            whole.Insert(0, Digits[(int)(n % 36)]);
        }

        if (whole.Length == 0)
        {
            whole.Append('0');
        }

        return digits.Length == 0 ? whole.ToString() : $"{whole}.{digits}";
    }

    // A post's path: /<user>/status/<id> or /i/status/<id>, anything after it ignored.
    [GeneratedRegex(@"^/(?:i|[A-Za-z0-9_]{1,15})/status/(\d{1,20})(?:/|$)")]
    private static partial Regex StatusPath();
}
