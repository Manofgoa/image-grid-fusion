using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ImageGridFusion.Web;

namespace ImageGridFusion.Components;

/// <summary>
/// Where a component's versions come from: its latest version, read when the Component updates window checks, and
/// for a winget tool the version installed. See workfiles/20261102-component-update-verification.md § Versions by Source.
/// </summary>
internal abstract class ComponentSource
{
    /// <summary>The upstream as the tooltip names it.</summary>
    public abstract string Upstream { get; }

    /// <summary>
    /// The latest version, for <paramref name="component"/>'s files. Throws a <see cref="UrlLoadException"/> saying why
    /// when it cannot be read.
    /// </summary>
    public abstract Task<LatestVersion> LatestAsync(Component component, CancellationToken token);

    /// <summary>The version installed as the component itself says it, when it can: a winget tool's. Null otherwise.</summary>
    public virtual Task<string?> InstalledLabelAsync(Component component, CancellationToken token) => Task.FromResult<string?>(null);

    /// <summary>The JSON <paramref name="url"/> answers. Throws a <see cref="UrlLoadException"/> when it cannot be read.</summary>
    protected static async Task<JsonNode> GetJsonAsync(Uri url, CancellationToken token)
    {
        using var response = await Downloader.GetAsync(url, token);
        Downloader.EnsureSuccess(response);
        try
        {
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(token)) ?? throw new UrlLoadException($"{url.Host} answered nothing");
        }
        catch (JsonException e)
        {
            throw new UrlLoadException($"{url.Host} answered something unreadable", e);
        }
    }

    protected static DateTimeOffset? DateOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text)
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    protected static string? TextOf(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
}

/// <summary>
/// A Hugging Face repository: its latest commit on <c>main</c>, each file's LFS SHA-256 or Git blob id given beforehand,
/// so a commit touching other files offers no update. <paramref name="files"/>: each file's name in the component's
/// folder, and its path in the repository.
/// </summary>
internal sealed class HuggingFaceSource(string repository, IReadOnlyDictionary<string, string> files) : ComponentSource
{
    public override string Upstream => $"huggingface.co/{repository}";

    /// <summary>
    /// The source of files pinned as <c>https://huggingface.co/&lt;repo&gt;/resolve/&lt;revision&gt;/&lt;path&gt;</c>, all of one
    /// repository; their names in the folder kept.
    /// </summary>
    public static HuggingFaceSource Of(IEnumerable<ComponentFile> pinned)
    {
        string? repository = null;
        var paths = new Dictionary<string, string>();
        foreach (var file in pinned)
        {
            var match = Regex.Match(file.Url, "^https://huggingface\\.co/(?<repo>[^/]+/[^/]+)/resolve/[^/]+/(?<path>.+)$");
            if (!match.Success || (repository is not null && repository != match.Groups["repo"].Value))
            {
                throw new ArgumentException($"{file.Url} is not a file of one Hugging Face repository");
            }

            repository = match.Groups["repo"].Value;
            paths[file.Name] = match.Groups["path"].Value;
        }

        return new HuggingFaceSource(repository ?? throw new ArgumentException("No file"), paths);
    }

    public override async Task<LatestVersion> LatestAsync(Component component, CancellationToken token)
    {
        var json = await GetJsonAsync(new Uri($"https://huggingface.co/api/models/{repository}/revision/main?blobs=true"), token);
        string commit = TextOf(json["sha"]) ?? throw new UrlLoadException("huggingface.co gave no commit");
        var siblings = (json["siblings"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(s => TextOf(s["rfilename"]) ?? "", s => s);
        var latest = new List<ComponentFile>();
        var fingerprints = new Dictionary<string, string>();
        foreach (var pinned in component.Pinned.Files)
        {
            string path = files[pinned.Name];
            if (!siblings.TryGetValue(path, out var sibling))
            {
                throw new UrlLoadException($"{path} is no longer in {repository}");
            }

            string? sha256 = TextOf(sibling["lfs"]?["sha256"]);
            long size = sibling["size"] is JsonValue s && s.TryGetValue(out long bytes) ? bytes : 0;
            latest.Add(new ComponentFile(pinned.Name, $"https://huggingface.co/{repository}/resolve/{commit}/{path}", sha256, size));
            if (sha256 is not null)
            {
                fingerprints[pinned.Name] = sha256;
            }
            else if (TextOf(sibling["blobId"]) is { } blob)
            {
                fingerprints[pinned.Name] = "git:" + blob;
            }
        }

        return new LatestVersion(new ComponentVersion(commit[..Math.Min(8, commit.Length)], latest), DateOf(json["lastModified"]), fingerprints);
    }
}

/// <summary>
/// A file of a GitHub release: an asset of it (<paramref name="asset"/>), else a file of the repository at the release's
/// tag (<paramref name="path"/>). The latest release, or the release of a fixed tag (<paramref name="tag"/>) whose asset may
/// change — rembg's <c>v0.0.0</c> holds its models.
/// </summary>
internal sealed class GitHubReleaseSource(string repository, string? tag, string? asset, string? path) : ComponentSource
{
    public override string Upstream => $"github.com/{repository}";

    public override async Task<LatestVersion> LatestAsync(Component component, CancellationToken token)
    {
        var release = await GetJsonAsync(new Uri($"https://api.github.com/repos/{repository}/releases/{(tag is null ? "latest" : "tags/" + Uri.EscapeDataString(tag))}"), token);
        string label = TextOf(release["tag_name"]) ?? throw new UrlLoadException("github.com gave no release tag");
        string name = component.Pinned.Files[0].Name;
        if (asset is not null)
        {
            var found = (release["assets"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(a => TextOf(a["name"]) == asset)
                ?? throw new UrlLoadException($"no {asset} in its release {label}");
            string url = TextOf(found["browser_download_url"]) ?? throw new UrlLoadException($"no link to {asset}");
            string? sha256 = TextOf(found["digest"]) is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : null;
            long size = found["size"] is JsonValue s && s.TryGetValue(out long bytes) ? bytes : 0;
            var fingerprints = sha256 is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [name] = sha256 };
            return new LatestVersion(
                new ComponentVersion(label, [new ComponentFile(name, url, sha256, size)]),
                tag is null ? DateOf(release["published_at"]) : DateOf(found["updated_at"]),
                fingerprints);
        }

        return new LatestVersion(
            new ComponentVersion(label, [new ComponentFile(name, $"https://raw.githubusercontent.com/{repository}/{Uri.EscapeDataString(label)}/{path}", null, 0)]),
            DateOf(release["published_at"]),
            new Dictionary<string, string>());
    }
}

/// <summary>
/// Entries of a NuGet package (<paramref name="package"/>), taken out of its zip by HTTP ranges: its latest stable version,
/// its date from the registration. <paramref name="entries"/>: each file's name in the folder, and its entry in the package.
/// </summary>
internal sealed class NuGetSource(string package, IReadOnlyDictionary<string, string> entries) : ComponentSource
{
    public override string Upstream => $"nuget.org/packages/{package}";

    /// <summary>The URL of <paramref name="version"/>'s package.</summary>
    public string PackageUrl(string version) => $"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg";

    public override async Task<LatestVersion> LatestAsync(Component component, CancellationToken token)
    {
        var index = await GetJsonAsync(new Uri($"https://api.nuget.org/v3-flatcontainer/{package}/index.json"), token);
        string version = (index["versions"] as JsonArray ?? []).Select(TextOf).OfType<string>().LastOrDefault(v => !v.Contains('-'))
            ?? throw new UrlLoadException($"nuget.org lists no stable version of {package}");
        DateTimeOffset? released = null;
        try
        {
            released = DateOf((await GetJsonAsync(new Uri($"https://api.nuget.org/v3/registration5-semver1/{package}/{version}.json"), token))["published"]);
        }
        catch (UrlLoadException)
        {
            // The date is a detail: the version is known without it.
        }

        return new LatestVersion(
            new ComponentVersion(version, [.. component.Pinned.Files.Select(f => new ComponentFile(f.Name, this.PackageUrl(version), null, 0, entries[f.Name]))]),
            released,
            new Dictionary<string, string>());
    }
}

/// <summary>
/// A winget package, installed by winget into the tool's folder (<see cref="WingetTool"/>): its latest version from
/// <c>winget show --versions</c>, its date from the manifest's release date when it has one, the version installed from
/// the tool itself. winget's labels are in the user's language: only the values are read.
/// </summary>
internal sealed partial class WingetSource(WingetTool tool) : ComponentSource
{
    public WingetTool Tool { get; } = tool;

    public override string Upstream => $"winget {this.Tool.PackageId}";

    public override async Task<LatestVersion> LatestAsync(Component component, CancellationToken token)
    {
        var lines = await Winget(["show", "--id", this.Tool.PackageId, "--exact", "--versions", "--disable-interactivity", "--accept-source-agreements"], token);
        int rule = lines.FindIndex(l => l.Trim() is { Length: >= 3 } t && t.All(c => c == '-'));
        string version = rule >= 0 && lines.Skip(rule + 1).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) is { } first
            ? first
            : throw new UrlLoadException($"winget lists no version of {this.Tool.PackageId}");
        DateTimeOffset? released = null;
        var details = await Winget(["show", "--id", this.Tool.PackageId, "--exact", "--version", version, "--disable-interactivity", "--accept-source-agreements"], token);
        if (details.Select(l => DateLine().Match(l)).FirstOrDefault(m => m.Success) is { } date
            && DateTimeOffset.TryParse(date.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var day))
        {
            released = day;
        }

        return new LatestVersion(new ComponentVersion(version, []), released, new Dictionary<string, string>());
    }

    /// <summary><c>yt-dlp --version</c>, <c>deno --version</c>, <c>ffmpeg -version</c>: the version on their first line.</summary>
    public override async Task<string?> InstalledLabelAsync(Component component, CancellationToken token)
    {
        if (this.Tool.Find() is not { } exe)
        {
            return null;
        }

        try
        {
            bool ffmpeg = this.Tool.ExeName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase);
            var lines = await WingetTool.ReadLinesAsync(exe, [ffmpeg ? "-version" : "--version"], token);
            string[] words = lines.FirstOrDefault(l => l.Trim().Length > 0)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            return words.Length switch
            {
                0 => null,
                1 => words[0],
                _ => ffmpeg && words.Length > 2 ? words[2] : words[1],
            };
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static async Task<List<string>> Winget(string[] arguments, CancellationToken token)
    {
        try
        {
            return [.. await WingetTool.ReadLinesAsync("winget.exe", arguments, token)];
        }
        catch (Win32Exception e)
        {
            throw new UrlLoadException("winget isn't available", e);
        }
    }

    // A line ending with a date, whatever its label's language: "Release Date: 2026-10-26", "Date de version : 2026-10-26".
    [GeneratedRegex(@":\s*(\d{4}-\d{2}-\d{2})\s*$")]
    private static partial Regex DateLine();
}
