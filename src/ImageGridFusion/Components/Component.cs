using System.Text.Json.Nodes;
using ImageGridFusion.Web;

namespace ImageGridFusion.Components;

/// <summary>The kind of a downloadable component: the window's group it is listed under, in this order.</summary>
internal enum ComponentGroup
{
    Engines,
    Runtimes,
    WebTools,
    EmojiFonts,
}

/// <summary>
/// A file of a component's version: its name in the component's folder, where it comes from — a URL, or an entry of
/// the zip at that URL (<paramref name="ZipEntry"/>, a NuGet package's) — its SHA-256 when known before the download,
/// and its size in bytes, 0 when unknown.
/// </summary>
internal sealed record ComponentFile(string Name, string Url, string? Sha256, long Size, string? ZipEntry = null)
{
    public JsonObject ToJson()
    {
        var json = new JsonObject { ["name"] = this.Name, ["url"] = this.Url };
        if (this.ZipEntry is { } entry)
        {
            json["zipEntry"] = entry;
        }

        if (this.Sha256 is { } sha)
        {
            json["sha256"] = sha;
        }

        if (this.Size > 0)
        {
            json["size"] = this.Size;
        }

        return json;
    }

    /// <summary>The file written by <see cref="ToJson"/>, or by hand; null when its name or its URL is missing.</summary>
    public static ComponentFile? FromJson(JsonNode? node)
    {
        if (node is not JsonObject json || ComponentJson.String(json, "name") is not { Length: > 0 } name || ComponentJson.String(json, "url") is not { Length: > 0 } url
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return new ComponentFile(name, url, ComponentJson.String(json, "sha256"), ComponentJson.Long(json, "size"), ComponentJson.String(json, "zipEntry"));
    }
}

/// <summary>
/// A version of a component: its label as the window shows it — a release, a package version, a commit — and its
/// files; none for a winget tool, which winget fetches by its version.
/// </summary>
internal sealed record ComponentVersion(string Label, IReadOnlyList<ComponentFile> Files)
{
    public JsonObject ToJson() => new()
    {
        ["version"] = this.Label,
        ["files"] = new JsonArray([.. this.Files.Select(f => (JsonNode)f.ToJson())]),
    };

    /// <summary>The version written by <see cref="ToJson"/>, or by hand; null without a version label.</summary>
    public static ComponentVersion? FromJson(JsonNode? node) =>
        node is JsonObject json && ComponentJson.String(json, "version") is { Length: > 0 } label
            ? new ComponentVersion(label, json["files"] is JsonArray files ? [.. files.Select(ComponentFile.FromJson).OfType<ComponentFile>()] : [])
            : null;

    /// <summary>Whether <paramref name="other"/> has the same label and the same files, hashes included.</summary>
    public bool SameAs(ComponentVersion? other) =>
        other is not null && this.Label == other.Label && this.Files.Count == other.Files.Count
        && this.Files.Zip(other.Files).All(p => p.First.Name == p.Second.Name && p.First.Url == p.Second.Url
            && string.Equals(p.First.Sha256, p.Second.Sha256, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The latest version a source knows: the version, when it came out when the source says, and per file what tells
/// its content apart — its SHA-256, else <c>git:</c> and its Git blob id — to compare with the files installed.
/// </summary>
internal sealed record LatestVersion(ComponentVersion Version, DateTimeOffset? Released, IReadOnlyDictionary<string, string> Fingerprints);

/// <summary>
/// A component the app downloads on demand, never ships: one upstream — a Hugging Face repository, a GitHub release, a
/// NuGet package, a winget package — its files pinned in the code, listed in the Component updates window under its
/// group. See workfiles/20261102-component-update-verification.md § Components Window.
/// </summary>
internal sealed class Component(string id, ComponentGroup group, string name, string purpose, string license, string folder, ComponentVersion pinned, ComponentSource source)
{
    /// <summary>Its key in the components manifest.</summary>
    public string Id { get; } = id;

    public ComponentGroup Group { get; } = group;

    /// <summary>Its name, after its engine's when it is part of one: <c>Cutout engine — IS-Net</c>.</summary>
    public string Name { get; } = name;

    /// <summary>What it does in the app and which features need it, for its ℹ️ tooltip.</summary>
    public string Purpose { get; } = purpose;

    public string License { get; } = license;

    /// <summary>Its folder next to the exe, shared with others for some (<c>vision-engine\</c>, <c>ocr-engine\</c>).</summary>
    public string Folder { get; } = folder;

    /// <summary>The version the code pins: the fallback of every install, and what <c>Back to the pinned version</c> installs.</summary>
    public ComponentVersion Pinned { get; } = pinned;

    public ComponentSource Source { get; } = source;

    /// <summary>A check of a file downloaded, beside its hash: a font must start as a font. Null when none.</summary>
    public Func<string, bool>? Accepts { get; init; }

    /// <summary>The winget tool it is, when it is one: installed by winget, no file of its own to download.</summary>
    public WingetTool? Tool => (this.Source as WingetSource)?.Tool;

    /// <summary>Whether it runs on ONNX Runtime: an engine's models.</summary>
    public bool NeedsRuntime => this.Group == ComponentGroup.Engines;

    /// <summary>Whether it is there: its tool found, or every file of its pinned version in its folder.</summary>
    public bool IsInstalled => this.Tool is { } tool ? tool.Find() is not null : this.Pinned.Files.All(f => File.Exists(this.PathOf(f.Name)));

    /// <summary>Its upstream as the tooltip names it: <c>huggingface.co/opencv/face_detection_yunet</c>.</summary>
    public string Upstream => this.Source.Upstream;

    /// <summary>Its upstream's page, opened by a click on its name in the window.</summary>
    public string PageUrl => this.Source.PageUrl;

    public string PathOf(string fileName) => Path.Combine(this.Folder, fileName);
}

/// <summary>Reads a manifest's values, a missing or foreign one giving its default.</summary>
internal static class ComponentJson
{
    public static string? String(JsonObject json, string key) =>
        json[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    public static long Long(JsonObject json, string key) =>
        json[key] is JsonValue value && (value.TryGetValue(out long number) || (value.TryGetValue(out double real) && (number = (long)real) >= 0)) ? Math.Max(0, number) : 0;

    public static DateTimeOffset? Date(JsonObject json, string key) =>
        String(json, key) is { } text && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date : null;
}
