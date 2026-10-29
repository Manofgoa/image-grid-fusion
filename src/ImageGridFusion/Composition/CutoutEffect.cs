using System.Globalization;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>What the Cutout effect keeps: the image's main subject, or the object under the points clicked.</summary>
public enum CutoutMode
{
    Subject,
    Picked,
}

/// <summary>
/// A point clicked on the image to pick an object, in fractions of the image as loaded — before any
/// rotation, flip or crop — so the pick survives them; <see cref="Included"/> while it marks the object,
/// not while it marks what to leave out.
/// </summary>
public readonly record struct CutoutPoint(float X, float Y, bool Included);

/// <summary>
/// The Cutout effect of an image: a mask computed by a model — the main subject, or the object under
/// the points clicked — keeps a part of the image, the rest made transparent; its edge grown or eaten,
/// softened, the mask inverted at will. The mask is computed off the UI thread on the image as loaded and
/// cached on the image (<see cref="SourceImage.DrawnBitmap"/>); this record holds the settings only.
/// See workfiles/20261029-cutout.md.
/// </summary>
public sealed record CutoutEffect
{
    /// <summary>The farthest the edge grows or is eaten, and the widest it softens: a share of the image's longer side.</summary>
    public const double MaxEdge = 0.02;

    /// <summary>The main subject, its edge as the model gives it.</summary>
    public static readonly CutoutEffect Default = new();

    public CutoutMode Mode { get; private init; } = CutoutMode.Subject;

    /// <summary>The points clicked, in <see cref="CutoutMode.Picked"/> mode — the first one included; empty in <see cref="CutoutMode.Subject"/> mode.</summary>
    public IReadOnlyList<CutoutPoint> Points { get; private init; } = [];

    /// <summary>From -1, the edge eaten by <see cref="MaxEdge"/>, to 1, grown by it; 0 as the model gives it.</summary>
    public double Grow { get; private init; }

    /// <summary>From 0, a sharp edge, to 1, faded over <see cref="MaxEdge"/>.</summary>
    public double Soften { get; private init; }

    /// <summary>Keeps what the mask leaves out instead.</summary>
    public bool Invert { get; private init; }

    /// <summary>What the model's mask depends on — the mode and the points — the edge options aside: two effects with the same key share a mask.</summary>
    public string MaskKey => this.Mode == CutoutMode.Subject
        ? "subject"
        : string.Join(";", this.Points.Select(p => FormattableString.Invariant($"{p.X:R},{p.Y:R},{(p.Included ? 1 : 0)}")));

    /// <summary>The main subject: the points cleared.</summary>
    public CutoutEffect WithSubject() => this with { Mode = CutoutMode.Subject, Points = [] };

    /// <summary>
    /// A point clicked: alone, a new pick; <paramref name="add"/>, one more point of the pick — included,
    /// or left out when <paramref name="included"/> is false. A first point is always a new pick.
    /// </summary>
    public CutoutEffect WithPoint(CutoutPoint point, bool add) =>
        add && this.Mode == CutoutMode.Picked && this.Points.Count > 0
            ? this with { Points = [.. this.Points, point] }
            : this with { Mode = CutoutMode.Picked, Points = [point with { Included = true }] };

    public CutoutEffect WithGrow(double grow) => this with { Grow = Math.Clamp(grow, -1, 1) };

    public CutoutEffect WithSoften(double soften) => this with { Soften = Math.Clamp(soften, 0, 1) };

    public CutoutEffect WithInvert(bool invert) => this with { Invert = invert };

    // The points compared by value: a record compares a list by reference.
    public bool Equals(CutoutEffect? other) =>
        other is not null && this.Mode == other.Mode && this.Grow == other.Grow && this.Soften == other.Soften
        && this.Invert == other.Invert && this.Points.SequenceEqual(other.Points);

    public override int GetHashCode() => HashCode.Combine(this.Mode, this.Points.Count, this.Grow, this.Soften, this.Invert);

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>): the points in fractions of the image as loaded.</summary>
    internal JsonObject ToJson()
    {
        var points = new JsonArray();
        foreach (var point in this.Points)
        {
            // As doubles: a value built from a float does not read back as one before it is parsed.
            points.Add(new JsonObject { ["x"] = (double)point.X, ["y"] = (double)point.Y, ["included"] = point.Included });
        }

        return new JsonObject
        {
            ["mode"] = this.Mode.ToString(),
            ["points"] = points,
            ["grow"] = this.Grow,
            ["soften"] = this.Soften,
            ["invert"] = this.Invert,
        };
    }

    /// <summary>The settings <see cref="ToJson"/> wrote; a pick without a point gives the main subject.</summary>
    internal static CutoutEffect FromJson(JsonObject json)
    {
        var points = json["points"] is JsonArray array
            ? array.OfType<JsonObject>().Select(p => new CutoutPoint(
                (float)Math.Clamp(PresetJson.Double(p, "x", 0.5), 0, 1),
                (float)Math.Clamp(PresetJson.Double(p, "y", 0.5), 0, 1),
                PresetJson.Bool(p, "included", true))).ToList()
            : [];
        var mode = PresetJson.Enum(json, "mode", Default.Mode);
        var cutout = mode == CutoutMode.Picked && points.Count > 0
            ? Default with { Mode = CutoutMode.Picked, Points = [points[0] with { Included = true }, .. points.Skip(1)] }
            : Default;
        return cutout
            .WithGrow(PresetJson.Double(json, "grow", Default.Grow))
            .WithSoften(PresetJson.Double(json, "soften", Default.Soften))
            .WithInvert(PresetJson.Bool(json, "invert", Default.Invert));
    }

    /// <summary>What the preset dialog lists after its name: the mode, then the edge options not at their defaults.</summary>
    internal string Describe()
    {
        var parts = new List<string>
        {
            this.Mode == CutoutMode.Subject ? "subject"
                : $"picked, {this.Points.Count} point{(this.Points.Count == 1 ? "" : "s")}",
        };
        if (this.Grow != 0)
        {
            parts.Add($"grow {(this.Grow * MaxEdge * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} %");
        }

        if (this.Soften != 0)
        {
            parts.Add($"soften {(this.Soften * MaxEdge * 100).ToString("0.0", CultureInfo.InvariantCulture)} %");
        }

        if (this.Invert)
        {
            parts.Add("inverted");
        }

        return string.Join(", ", parts);
    }
}
