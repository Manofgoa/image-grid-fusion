using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Components;

/// <summary>
/// What a component's manifest entry holds: its pinned version — a copy of the code's —, the version the user forces as
/// the safe fallback, the version installed with its files' hashes, the one waiting for the next start, and when it was
/// installed and last replaced.
/// </summary>
internal sealed class ComponentRecord
{
    public required ComponentVersion Pinned { get; set; }

    /// <summary>The user's, written by hand, never by the app.</summary>
    public ComponentVersion? Forced { get; set; }

    public ComponentVersion? Installed { get; set; }

    /// <summary>A version downloaded whose files were in use: swapped at the next start (<see cref="ComponentInstaller.ApplyPending"/>).</summary>
    public ComponentVersion? Pending { get; set; }

    public DateTimeOffset? InstalledOn { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }
}

/// <summary>
/// The components manifest, <see cref="FileName"/> next to the exe: per component its versions and dates
/// (<see cref="ComponentRecord"/>). The pinned versions are rewritten from the code at every load — the code always wins
/// for them —, the forced ones are the user's; missing, the file is recreated, the components already there recorded as
/// their pinned version. Read and written on the UI thread. See workfiles/20261102-component-update-verification.md
/// § Components Manifest.
/// </summary>
internal sealed class ComponentManifest
{
    public const string FileName = "components.json";

    private const string BadSuffix = ".bad";

    private readonly Dictionary<string, ComponentRecord> _records;

    private ComponentManifest(Dictionary<string, ComponentRecord> records, string? loadError)
    {
        this._records = records;
        this.LoadError = loadError;
    }

    /// <summary>The manifest the app runs with, loaded at start-up (<see cref="Load"/>).</summary>
    public static ComponentManifest Current { get; private set; } = new([], null);

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>Why the file could not be read or written at start-up, for the status line; null when it could.</summary>
    public string? LoadError { get; private set; }

    public ComponentRecord this[Component component] => this._records[component.Id];

    /// <summary>
    /// Loads the manifest of <paramref name="components"/> into <see cref="Current"/>, and writes it back when
    /// <paramref name="writes"/> — the normal instance: a <c>--new-instance</c> one keeps it in memory until a user action
    /// writes it. An unreadable file is kept beside as <c>components.json.bad</c> before being recreated.
    /// </summary>
    public static ComponentManifest Load(IReadOnlyList<Component> components, bool writes)
    {
        string path = DefaultPath;
        string? error = null;
        JsonObject? json = null;
        try
        {
            if (File.Exists(path))
            {
                json = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new JsonException("not an object");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            error = $"{FileName} couldn't be read ({e.Message}): recreated";
            if (writes && e is JsonException)
            {
                try
                {
                    File.Copy(path, path + BadSuffix, overwrite: true);
                }
                catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        var entries = json?["components"] as JsonObject;
        var records = new Dictionary<string, ComponentRecord>();
        foreach (var component in components)
        {
            var entry = entries?[component.Id] as JsonObject;
            var record = new ComponentRecord
            {
                Pinned = component.Pinned,
                Forced = ValidForced(component, ComponentVersion.FromJson(entry?["forced"])),
                Installed = ComponentVersion.FromJson(entry?["installed"]),
                Pending = ComponentVersion.FromJson(entry?["pending"]),
                InstalledOn = entry is null ? null : ComponentJson.Date(entry, "installedOn"),
                UpdatedOn = entry is null ? null : ComponentJson.Date(entry, "updatedOn"),
            };
            Reconcile(component, record);
            records[component.Id] = record;
        }

        var manifest = new ComponentManifest(records, error);
        if (writes)
        {
            try
            {
                manifest.Save();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                manifest.LoadError = $"{FileName} couldn't be written: {e.Message}";
            }
        }

        Current = manifest;
        return manifest;
    }

    /// <summary>
    /// The record made to agree with the disk: a component there with no version recorded — installed before the
    /// manifest — is its pinned version, installed when its first file was created; one gone has no version.
    /// </summary>
    public static void Reconcile(Component component, ComponentRecord record)
    {
        if (component.IsInstalled && record.Installed is null)
        {
            record.Installed = component.Pinned;
            record.InstalledOn ??= CreatedOn(component);
        }
        else if (!component.IsInstalled && record.Pending is null)
        {
            record.Installed = null;
            record.InstalledOn = null;
            record.UpdatedOn = null;
        }
    }

    /// <summary>
    /// Writes the whole file at once, through a temporary file. Throws what the file throws. Every write is a user action's
    /// — an install, an update, a swap at the next start — so a <c>--new-instance</c> one writes too.
    /// </summary>
    public void Save()
    {
        var components = new JsonObject();
        foreach (var (id, record) in this._records)
        {
            var entry = new JsonObject { ["pinned"] = record.Pinned.ToJson() };
            if (record.Forced is { } forced)
            {
                entry["forced"] = forced.ToJson();
            }

            if (record.Installed is { } installed)
            {
                entry["installed"] = installed.ToJson();
            }

            if (record.Pending is { } pending)
            {
                entry["pending"] = pending.ToJson();
            }

            if (record.InstalledOn is { } on)
            {
                entry["installedOn"] = on.ToString("O");
            }

            if (record.UpdatedOn is { } updated)
            {
                entry["updatedOn"] = updated.ToString("O");
            }

            components[id] = entry;
        }

        var json = new JsonObject { ["components"] = components };
        string path = DefaultPath;
        string temporary = path + ".~saving";
        File.WriteAllText(temporary, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }

    // A forced version needs files, but for a winget tool, which winget fetches by its version.
    private static ComponentVersion? ValidForced(Component component, ComponentVersion? forced) =>
        forced is not null && (component.Tool is not null || forced.Files.Count > 0) ? forced : null;

    private static DateTimeOffset? CreatedOn(Component component)
    {
        try
        {
            string? path = component.Tool?.Find() ?? (component.Pinned.Files.Count > 0 ? component.PathOf(component.Pinned.Files[0].Name) : null);
            return path is not null && File.Exists(path) ? new DateTimeOffset(File.GetCreationTime(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
