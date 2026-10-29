using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A face across sessions: its file's full path and its box rounded — stable while the file is
/// unchanged. Full paths, so the user's decisions survive a change of base folder.
/// </summary>
internal static class FaceKeys
{
    public static string Of(string fullPath, RectangleF bounds) => string.Create(
        CultureInfo.InvariantCulture,
        $"{fullPath}|{bounds.X:F3},{bounds.Y:F3},{bounds.Width:F3},{bounds.Height:F3}");

    /// <summary>The file a face key names.</summary>
    public static string FileOf(string key) => key[..Math.Max(0, key.LastIndexOf('|'))];
}

/// <summary>
/// A person the user acted on — named, hidden, merged, given a face: its <b>pinned</b> faces, by their
/// keys, each with a copy of its embedding so the person survives its files moving or the index being
/// rebuilt; the faces <b>excluded</b> from it, which never join it again; and, merged into another
/// person, that person's id — each keeping its own faces, so un-merging restores it as it was.
/// </summary>
internal sealed class Person(string id)
{
    public string Id { get; } = id;

    public string Name { get; set; } = "";

    public bool Hidden { get; set; }

    /// <summary>The person it was merged into, shown inside it as a sub-group; null for a group of its own.</summary>
    public string? MergedInto { get; set; }

    public Dictionary<string, float[]> Pinned { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Excluded { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A copy the grouping reads off the UI thread.</summary>
    public PersonData Snapshot() => new(this.Id, this.Name, this.Hidden, this.MergedInto, new Dictionary<string, float[]>(this.Pinned, StringComparer.OrdinalIgnoreCase), new HashSet<string>(this.Excluded, StringComparer.OrdinalIgnoreCase));
}

/// <summary>A person as the grouping reads it: never changed once taken.</summary>
internal sealed record PersonData(string Id, string Name, bool Hidden, string? MergedInto, IReadOnlyDictionary<string, float[]> Pinned, IReadOnlySet<string> Excluded);

/// <summary>
/// The people as a step of the undo history holds them (workfiles/20261028-open-people-from-image.md
/// § Undo): a copy of every person, the decision that led to them ("" for none), and their generation —
/// loading the file or forgetting the faces starts another, never undone. Each decision makes a new one:
/// two are the same state only when they are the same object.
/// </summary>
internal sealed class PeopleState(IReadOnlyList<PersonData> people, int generation, string done)
{
    public static readonly PeopleState Empty = new([], 0, "");

    public IReadOnlyList<PersonData> People { get; } = people;

    public int Generation { get; } = generation;

    /// <summary>The decision that led to it, as the status line names it.</summary>
    public string Done { get; } = done;
}

/// <summary>
/// The people the user acted on, kept in <see cref="FileName"/> next to the exe — the decisions only:
/// the groups themselves are computed from the faces and these, never stored. A version and a
/// <c>people</c> array; each change written at once, by the caller's <see cref="Save"/>, a failure
/// keeping the change for the session. An unreadable entry is skipped. See
/// workfiles/20261027-face-detection.md § Decisions.
/// </summary>
internal sealed class People
{
    public const string FileName = "people.json";

    private const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly List<Person> _people = [];

    public People(string path)
    {
        this._path = path;
    }

    /// <summary>Where the people of this exe live: next to it.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public IReadOnlyList<Person> All => this._people;

    public Person? Find(string id) => this._people.FirstOrDefault(person => person.Id == id);

    /// <summary>Copies of every person, for the grouping.</summary>
    public IReadOnlyList<PersonData> Snapshot() => [.. this._people.Select(person => person.Snapshot())];

    /// <summary>Every person replaced by <paramref name="people"/>, as a copy taken earlier held them — an undo or a redo.</summary>
    public void Restore(IReadOnlyList<PersonData> people)
    {
        this._people.Clear();
        foreach (var data in people)
        {
            var person = new Person(data.Id) { Name = data.Name, Hidden = data.Hidden, MergedInto = data.MergedInto };
            foreach (var (key, embedding) in data.Pinned)
            {
                person.Pinned[key] = embedding;
            }

            person.Excluded.UnionWith(data.Excluded);
            this._people.Add(person);
        }
    }

    /// <summary>A new person holding <paramref name="faces"/> pinned — an unnamed group the user acts on for the first time.</summary>
    public Person Create(IEnumerable<(string Key, float[] Embedding)> faces)
    {
        var person = new Person(Guid.NewGuid().ToString("N"));
        foreach (var (key, embedding) in faces)
        {
            person.Pinned[key] = embedding;
        }

        this._people.Add(person);
        return person;
    }

    /// <summary>The face pinned to <paramref name="person"/> only — unpinned from any other — and no longer excluded from it.</summary>
    public void Pin(string key, float[] embedding, Person person)
    {
        foreach (var other in this._people)
        {
            other.Pinned.Remove(key);
        }

        person.Pinned[key] = embedding;
        person.Excluded.Remove(key);
    }

    /// <summary>The face out of <paramref name="person"/> for good: unpinned from it, and excluded.</summary>
    public static void Exclude(string key, Person person)
    {
        person.Pinned.Remove(key);
        person.Excluded.Add(key);
    }

    /// <summary>
    /// <paramref name="source"/> merged into <paramref name="target"/>, with the sub-groups it had: a group
    /// holds sub-groups one level deep. The target keeps its name, takes the source's when it has none.
    /// </summary>
    public void Merge(Person source, Person target)
    {
        foreach (var person in this._people.Where(person => person.MergedInto == source.Id))
        {
            person.MergedInto = target.Id;
        }

        source.MergedInto = target.Id;
        source.Hidden = false;
        if (target.Name.Length == 0)
        {
            target.Name = source.Name;
        }
    }

    /// <summary>
    /// Reads the file; a missing or unreadable one leaves no person, an unreadable entry is skipped, a
    /// merge into a person gone undone.
    /// </summary>
    public void Load()
    {
        this._people.Clear();
        try
        {
            if (File.Exists(this._path) && JsonNode.Parse(File.ReadAllText(this._path)) is JsonObject { } json && json["people"] is JsonArray entries)
            {
                foreach (var node in entries)
                {
                    if (node is JsonObject entry && Read(entry) is { } person)
                    {
                        this._people.Add(person);
                    }
                }
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is JsonException)
        {
        }

        var ids = this._people.Select(person => person.Id).ToHashSet();
        foreach (var person in this._people.Where(person => person.MergedInto is { } target && !ids.Contains(target)))
        {
            person.MergedInto = null;
        }
    }

    /// <summary>Writes the file. Throws an <see cref="FileIndex.IsFileError"/> exception when it cannot, the people kept for the session.</summary>
    public void Save()
    {
        var entries = new JsonArray();
        foreach (var person in this._people)
        {
            var pinned = new JsonArray();
            foreach (var (key, embedding) in person.Pinned)
            {
                pinned.Add(new JsonObject { ["key"] = key, ["embedding"] = Convert.ToBase64String(MemoryMarshal.AsBytes(embedding.AsSpan())) });
            }

            entries.Add(new JsonObject
            {
                ["id"] = person.Id,
                ["name"] = person.Name,
                ["hidden"] = person.Hidden,
                ["mergedInto"] = person.MergedInto,
                ["pinned"] = pinned,
                ["excluded"] = new JsonArray([.. person.Excluded.Select(key => (JsonNode?)key)]),
            });
        }

        var file = new JsonObject { ["version"] = Version, ["people"] = entries };
        string temp = this._path + ".tmp";
        File.WriteAllText(temp, file.ToJsonString(WriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, this._path, overwrite: true);
    }

    /// <summary>Forgets every person, the file deleted. Throws like <see cref="Save"/>, the people forgotten for the session.</summary>
    public void Clear()
    {
        this._people.Clear();
        if (File.Exists(this._path))
        {
            File.Delete(this._path);
        }
    }

    private static Person? Read(JsonObject entry)
    {
        if (entry["id"] is not JsonValue idValue || !idValue.TryGetValue(out string? id) || id.Length == 0)
        {
            return null;
        }

        var person = new Person(id)
        {
            Name = entry["name"] is JsonValue name && name.TryGetValue(out string? text) ? text : "",
            Hidden = entry["hidden"] is JsonValue hidden && hidden.TryGetValue(out bool isHidden) && isHidden,
            MergedInto = entry["mergedInto"] is JsonValue merged && merged.TryGetValue(out string? into) && into.Length > 0 ? into : null,
        };
        if (entry["pinned"] is JsonArray pinned)
        {
            foreach (var node in pinned)
            {
                if (node is JsonObject face && face["key"] is JsonValue key && key.TryGetValue(out string? faceKey)
                    && face["embedding"] is JsonValue encoded && encoded.TryGetValue(out string? base64) && Embedding(base64) is { } embedding)
                {
                    person.Pinned[faceKey] = embedding;
                }
            }
        }

        if (entry["excluded"] is JsonArray excluded)
        {
            foreach (var node in excluded)
            {
                if (node is JsonValue key && key.TryGetValue(out string? faceKey))
                {
                    person.Excluded.Add(faceKey);
                }
            }
        }

        return person;
    }

    private static float[]? Embedding(string base64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return bytes.Length > 0 && bytes.Length % sizeof(float) == 0 ? MemoryMarshal.Cast<byte, float>(bytes).ToArray() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
