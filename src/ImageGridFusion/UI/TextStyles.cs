using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>A text layer's style under a name: one the app ships, or one the user saved.</summary>
internal sealed record NamedTextStyle(string Name, LayerTextStyle Style, bool BuiltIn);

/// <summary>
/// The text styles (workfiles/20261029-text-layer-style-presets.md): the built-in ones, fixed and shipped in
/// the code, then the user's, kept in <see cref="FileName"/> next to the exe with the name of the last style
/// applied — the style of the next new text. Each change written at once by the caller's <see cref="Save"/>,
/// a failure keeping it for the session; an unreadable entry is skipped. Out of the undo history.
/// </summary>
internal sealed class TextStyles
{
    public const string FileName = "text-styles.json";

    private const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly List<NamedTextStyle> _user = [];

    public TextStyles(string path)
    {
        this._path = path;
    }

    /// <summary>The styles the app ships, in their order: the default style first.</summary>
    public static IReadOnlyList<NamedTextStyle> BuiltIn { get; } =
    [
        new("Default", LayerTextStyle.Default, true),
        new("Meme", new LayerTextStyle("Impact", false, Color.White, false, Color.Black, 0.08, false, Color.Black, 0.5)
        {
            Caps = true,
            Shadow = true,
            ShadowKind = ShadowKind.Halo,
            ShadowSize = 0.1,
        }, true),
        new("Subtitle", new LayerTextStyle("Segoe UI", false, Color.White, false, Color.Black, 0.08, true, Color.Black, 0.6), true),
        new("Label", new LayerTextStyle("Segoe UI", true, Color.Black, false, Color.Black, 0.08, true, Color.White, 1), true),
        new("Neon", new LayerTextStyle("Segoe UI", true, Color.FromArgb(255, 235, 0), true, Color.Black, 0.14, false, Color.Black, 0.5), true),
        new("Subtle", new LayerTextStyle("Segoe UI", false, Color.White, true, Color.Black, 0.03, false, Color.Black, 0.5), true),
    ];

    /// <summary>Where the styles of this exe live: next to it.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The built-in styles, then the user's in the order saved.</summary>
    public IEnumerable<NamedTextStyle> All => BuiltIn.Concat(this._user);

    /// <summary>The name of the last style applied; <c>null</c> before any.</summary>
    public string? Last { get; private set; }

    /// <summary>The style a new text takes: the last one applied, <see cref="LayerTextStyle.Default"/> before any or once it is gone.</summary>
    public LayerTextStyle NewTextStyle => this.Find(this.Last)?.Style ?? LayerTextStyle.Default;

    public NamedTextStyle? Find(string? name) =>
        name is null ? null : this.All.FirstOrDefault(style => string.Equals(style.Name, name, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Whether <paramref name="name"/> is a built-in style's, refused to the user's.</summary>
    public static bool IsReserved(string name) => BuiltIn.Any(style => string.Equals(style.Name, name, StringComparison.CurrentCultureIgnoreCase));

    /// <summary><paramref name="style"/> kept under <paramref name="name"/>: the user's style of that name replaced in its place, else added last.</summary>
    public NamedTextStyle Put(string name, LayerTextStyle style)
    {
        var named = new NamedTextStyle(name, style, false);
        int index = this._user.FindIndex(s => string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0)
        {
            this._user[index] = named;
        }
        else
        {
            this._user.Add(named);
        }

        return named;
    }

    /// <summary>The user's <paramref name="style"/> under <paramref name="name"/>, in its place; the last style applied follows it.</summary>
    public void Rename(NamedTextStyle style, string name)
    {
        int index = this._user.IndexOf(style);
        if (index < 0)
        {
            return;
        }

        this._user[index] = style with { Name = name };
        if (string.Equals(this.Last, style.Name, StringComparison.CurrentCultureIgnoreCase))
        {
            this.Last = name;
        }
    }

    /// <summary>The user's <paramref name="style"/> forgotten; a new text then takes the default style if it was the last applied.</summary>
    public void Delete(NamedTextStyle style)
    {
        this._user.Remove(style);
        if (string.Equals(this.Last, style.Name, StringComparison.CurrentCultureIgnoreCase))
        {
            this.Last = null;
        }
    }

    /// <summary><paramref name="style"/> applied: the next new text takes it.</summary>
    public void Applied(NamedTextStyle style) => this.Last = style.Name;

    /// <summary>
    /// Reads the file; a missing one leaves no user style. <c>false</c> when it exists but cannot be read, no user
    /// style kept; an unreadable entry, or one named like a built-in style, is skipped.
    /// </summary>
    public bool Load()
    {
        this._user.Clear();
        this.Last = null;
        try
        {
            if (!File.Exists(this._path))
            {
                return true;
            }

            if (JsonNode.Parse(File.ReadAllText(this._path)) is not JsonObject json)
            {
                return false;
            }

            if (json["styles"] is JsonArray entries)
            {
                foreach (var node in entries)
                {
                    if (node is JsonObject entry && entry["name"] is JsonValue value && value.TryGetValue(out string? name)
                        && name.Trim() is { Length: > 0 } trimmed && !IsReserved(trimmed) && entry["style"] is JsonObject style)
                    {
                        this.Put(trimmed, LayerTextStyle.FromJson(style));
                    }
                }
            }

            this.Last = json["last"] is JsonValue last && last.TryGetValue(out string? lastName) ? lastName : null;
            return true;
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is JsonException or InvalidOperationException)
        {
            this._user.Clear();
            this.Last = null;
            return false;
        }
    }

    /// <summary>Writes the file. Throws an <see cref="FileIndex.IsFileError"/> exception when it cannot, the styles kept for the session.</summary>
    public void Save()
    {
        var entries = new JsonArray();
        foreach (var style in this._user)
        {
            entries.Add(new JsonObject { ["name"] = style.Name, ["style"] = style.Style.ToJson() });
        }

        var file = new JsonObject { ["version"] = Version, ["last"] = this.Last, ["styles"] = entries };
        string temp = this._path + ".tmp";
        File.WriteAllText(temp, file.ToJsonString(WriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, this._path, overwrite: true);
    }
}
