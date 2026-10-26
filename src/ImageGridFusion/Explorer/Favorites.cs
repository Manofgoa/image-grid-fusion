using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Explorer;

/// <summary>
/// An effect preset: the effects turned on in a cell, saved with the file it held and a description,
/// the file arriving with them when the preset's tile is brought into a cell. A file may have several.
/// See workfiles/20261026-effect-preset-favorites.md.
/// </summary>
/// <param name="Id">Its own identity: a file's presets share its path.</param>
/// <param name="Effects">What <c>LookPreset.ToJson</c> wrote; owned by the preset, never attached to another node.</param>
internal sealed record EffectPreset(string Id, string Description, DateTime Saved, JsonObject Effects)
{
    /// <summary>The description folded as the search folds the query (<see cref="FileSearch.Fold"/>).</summary>
    public string Folded => FileSearch.Fold(this.Description);

    /// <summary>The description's first non-blank line, trimmed: what the tile shows under the file's name.</summary>
    public string FirstLine => this.Description.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? "";

    public static EffectPreset New(string description, JsonObject effects) =>
        new(Guid.NewGuid().ToString("N"), description, DateTime.Now, effects);
}

/// <summary>A favorite: a file, or one of its effect presets (<paramref name="Preset"/>).</summary>
internal sealed record Favorite(string FullPath, EffectPreset? Preset = null);

/// <summary>
/// The file explorer's favorites: files by their absolute paths, and effect presets, in the order they
/// were added, kept in a JSON file next to the exe and written at once on every change — absolute, so
/// a favorite survives a rescan and a change of base folder. A file is a favorite once at most; it may
/// have any number of presets. See workfiles/20260926-file-explorer.md § Favorites and
/// workfiles/20261026-effect-preset-favorites.md § Storage.
/// </summary>
internal sealed class Favorites
{
    public const string FileName = "favorites.json";

    /// <summary>Where an older version kept them, one path per line: read when <see cref="FileName"/> is missing, moved into it by <see cref="Migrate"/>.</summary>
    public const string OldFileName = "favorites.txt";

    private const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly List<Favorite> _entries = [];
    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public Favorites(string path)
    {
        _path = path;
    }

    /// <summary>Where the favorites of this exe live: next to it.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static string OldPath(string path) => Path.Combine(Path.GetDirectoryName(path)!, OldFileName);

    public int Count => _entries.Count;

    /// <summary>The favorites, the most recently added first.</summary>
    public IEnumerable<Favorite> Newest
    {
        get
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                yield return _entries[i];
            }
        }
    }

    /// <summary>The effect presets, the most recently added first: what a search goes through beside the index.</summary>
    public IEnumerable<Favorite> Presets => this.Newest.Where(favorite => favorite.Preset is not null);

    /// <summary>Whether the file itself is a favorite — its presets aside.</summary>
    public bool Contains(string fullPath) => _set.Contains(fullPath);

    /// <summary>Whether a favorite or a preset holds the file: a pasted favorite's file is recycled only once none does.</summary>
    public bool Uses(string fullPath) =>
        _entries.Any(entry => string.Equals(entry.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));

    public Favorite? Preset(string id) => _entries.FirstOrDefault(entry => entry.Preset?.Id == id);

    /// <summary>
    /// Moves an older version's <see cref="OldFileName"/> into <see cref="FileName"/>, once, then deletes
    /// it: left to the normal instance (RULES.md § Single Instance), a test instance reading the old file
    /// as it is. A <see cref="FileName"/> already there — written by a test instance, from the old file it
    /// read — holds its favorites: the old file is only deleted. A failure leaves the old file where it
    /// is, tried again at the next start.
    /// </summary>
    public static void Migrate()
    {
        string path = DefaultPath;
        string old = OldPath(path);
        if (!File.Exists(old))
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                var favorites = new Favorites(path);
                favorites.Load();
                favorites.Save();
            }

            File.Delete(old);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }
    }

    /// <summary>
    /// Reads the file — the older text file when it is missing; a missing or unreadable one leaves no
    /// favorite, an unreadable entry is skipped.
    /// </summary>
    public void Load()
    {
        _entries.Clear();
        _set.Clear();
        try
        {
            if (File.Exists(_path))
            {
                this.Read(JsonNode.Parse(File.ReadAllText(_path)) as JsonObject);
            }
            else
            {
                foreach (string line in File.ReadAllLines(OldPath(_path)))
                {
                    this.Append(new Favorite(line));
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is JsonException)
        {
        }
    }

    private void Read(JsonObject? json)
    {
        if (json?["favorites"] is not JsonArray entries)
        {
            return;
        }

        foreach (var node in entries)
        {
            if (node is not JsonObject entry || entry["path"] is not JsonValue path || !path.TryGetValue(out string? fullPath))
            {
                continue;
            }

            EffectPreset? preset = null;
            if (entry["preset"] is JsonObject saved)
            {
                if (saved["effects"] is not JsonObject effects)
                {
                    continue;
                }

                string id = saved["id"] is JsonValue value && value.TryGetValue(out string? text) ? text : Guid.NewGuid().ToString("N");
                string description = saved["description"] is JsonValue words && words.TryGetValue(out string? said) ? said : "";
                var date = saved["saved"] is JsonValue when && when.TryGetValue(out DateTime at) ? at : DateTime.Now;
                preset = new EffectPreset(id, description, date, (JsonObject)effects.DeepClone());
            }

            this.Append(new Favorite(fullPath, preset));
        }
    }

    // A file favorite already there is skipped, as a duplicate line was.
    private void Append(Favorite favorite)
    {
        if (favorite.FullPath.Length > 0 && (favorite.Preset is not null || _set.Add(favorite.FullPath)))
        {
            _entries.Add(favorite);
        }
    }

    /// <summary>
    /// Adds <paramref name="fullPath"/>, or removes it when it is one already, and writes the file;
    /// true when it is now a favorite. Its presets are left alone. Throws an <see cref="FileIndex.IsFileError"/>
    /// exception when the file cannot be written, the change kept for the session.
    /// </summary>
    public bool Toggle(string fullPath)
    {
        bool added = !Drop(fullPath);
        if (added)
        {
            _set.Add(fullPath);
            _entries.Add(new Favorite(fullPath));
        }

        Save();
        return added;
    }

    /// <summary>
    /// Adds <paramref name="fullPaths"/> in their order, the last one ending the newest — one already a
    /// favorite moved there, as if added again, never removed — and writes the file once. Throws like
    /// <see cref="Toggle"/>, the change kept for the session.
    /// </summary>
    public void Add(IEnumerable<string> fullPaths)
    {
        foreach (string fullPath in fullPaths)
        {
            Drop(fullPath);
            _set.Add(fullPath);
            _entries.Add(new Favorite(fullPath));
        }

        Save();
    }

    /// <summary>Adds an effect preset of <paramref name="fullPath"/>, the newest, and writes the file. Throws like <see cref="Toggle"/>.</summary>
    public void AddPreset(string fullPath, EffectPreset preset)
    {
        _entries.Add(new Favorite(fullPath, preset));
        Save();
    }

    /// <summary>Gives a preset a new description, in its place, and writes the file. Throws like <see cref="Toggle"/>.</summary>
    public void Describe(string id, string description)
    {
        int index = _entries.FindIndex(entry => entry.Preset?.Id == id);
        if (index < 0)
        {
            return;
        }

        _entries[index] = _entries[index] with { Preset = _entries[index].Preset! with { Description = description } };
        Save();
    }

    /// <summary>Removes a preset, writing the file; false when there was none. Throws like <see cref="Toggle"/>.</summary>
    public bool RemovePreset(string id)
    {
        if (_entries.RemoveAll(entry => entry.Preset?.Id == id) == 0)
        {
            return false;
        }

        Save();
        return true;
    }

    /// <summary>Removes the file favorite <paramref name="fullPath"/>, writing the file; false when it was none. Its presets stay.</summary>
    public bool Remove(string fullPath)
    {
        if (!Drop(fullPath))
        {
            return false;
        }

        Save();
        return true;
    }

    private bool Drop(string fullPath)
    {
        if (!_set.Remove(fullPath))
        {
            return false;
        }

        _entries.RemoveAll(entry => entry.Preset is null && string.Equals(entry.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private void Save()
    {
        var entries = new JsonArray();
        foreach (var entry in _entries)
        {
            var json = new JsonObject { ["path"] = entry.FullPath };
            if (entry.Preset is { } preset)
            {
                json["preset"] = new JsonObject
                {
                    ["id"] = preset.Id,
                    ["description"] = preset.Description,
                    ["saved"] = preset.Saved,
                    ["effects"] = preset.Effects.DeepClone(),
                };
            }

            entries.Add(json);
        }

        var file = new JsonObject { ["version"] = Version, ["favorites"] = entries };
        string temp = _path + ".tmp";
        File.WriteAllText(temp, file.ToJsonString(WriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, _path, overwrite: true);
    }
}
