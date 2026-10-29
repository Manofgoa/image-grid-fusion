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
/// The thumbnails of the part of the image the Cutout leaves out: Transparent — no treatment — then the
/// treatments, which combine (<see cref="CutoutEffect.Treatments"/>).
/// </summary>
public enum CutoutBackground
{
    Transparent,
    Opacity,
    Color,
    Blur,
    Pixelate,
}

/// <summary>How wide a pick reaches: the model's choice, or one of its masks of a click, from a part to the whole object.</summary>
public enum CutoutReach
{
    Auto,
    Part,
    Object,
    Whole,
}

/// <summary>
/// A point clicked on the image to pick an object, in fractions of the image as loaded — before any
/// rotation, flip or crop — so the pick survives them; <see cref="Included"/> while it marks the object,
/// not while it marks what to leave out.
/// </summary>
public readonly record struct CutoutPoint(float X, float Y, bool Included);

/// <summary>
/// The Cutout effect of an image: a mask computed by a model — the main subject, or the object under
/// the points clicked, as wide as its reach — keeps a part of the image; the rest is made transparent,
/// or kept with treatments — see-through, under a color, blurred, in squares — combined, each its own
/// amount; the edge grown or eaten, softened, the mask inverted at will. The mask is computed off the UI
/// thread on the image as loaded and cached on the image (<see cref="SourceImage.DrawnBitmap"/>); this
/// record holds the settings only. See workfiles/20261029-cutout.md.
/// </summary>
public sealed record CutoutEffect
{
    /// <summary>The farthest the edge grows or is eaten, and the widest it softens: a share of the image's longer side.</summary>
    public const double MaxEdge = 0.02;

    /// <summary>The strongest blur of the part left out — the gaussian's radius — and the side of its largest squares: shares of the image's longer side.</summary>
    public const double MaxBlur = 0.03;
    public const double MaxPixel = 0.08;

    // A treatment's amount until it is set.
    private const double DefaultAmount = 0.5;

    /// <summary>The main subject, its edge as the model gives it, the rest transparent.</summary>
    public static readonly CutoutEffect Default = new();

    // The treatments on, and each one's amount, from 0 to 1, kept while it is off.
    private HashSet<CutoutBackground> TreatmentSet { get; init; } = [];
    private Dictionary<CutoutBackground, double> AmountSet { get; init; } = [];

    public CutoutMode Mode { get; private init; } = CutoutMode.Subject;

    /// <summary>The points clicked, in <see cref="CutoutMode.Picked"/> mode — the first one included; empty in <see cref="CutoutMode.Subject"/> mode.</summary>
    public IReadOnlyList<CutoutPoint> Points { get; private init; } = [];

    /// <summary>How wide a pick reaches; the main subject ignores it.</summary>
    public CutoutReach Reach { get; private init; } = CutoutReach.Auto;

    /// <summary>From -1, the edge eaten by <see cref="MaxEdge"/>, to 1, grown by it; 0 as the model gives it.</summary>
    public double Grow { get; private init; }

    /// <summary>From 0, a sharp edge, to 1, faded over <see cref="MaxEdge"/>.</summary>
    public double Soften { get; private init; }

    /// <summary>Keeps what the mask leaves out instead.</summary>
    public bool Invert { get; private init; }

    /// <summary>The color masking the part left out, under the <see cref="CutoutBackground.Color"/> treatment.</summary>
    public Color Color { get; private init; } = Color.Black;

    /// <summary>
    /// The treatments of the part left out, on, in the order they apply — blur, squares, color, opacity;
    /// none: it is transparent.
    /// </summary>
    public IReadOnlyList<CutoutBackground> Treatments =>
        [.. new[] { CutoutBackground.Blur, CutoutBackground.Pixelate, CutoutBackground.Color, CutoutBackground.Opacity }.Where(this.TreatmentSet.Contains)];

    /// <summary>Whether the part left out is kept, with at least one treatment.</summary>
    public bool KeepsBackground => this.TreatmentSet.Count > 0;

    public bool Has(CutoutBackground treatment) => this.TreatmentSet.Contains(treatment);

    /// <summary>
    /// From 0 to 1, a treatment's amount, kept while it is off: the part left out's opacity, the color's
    /// opacity over it, the blur's strength or the squares' side.
    /// </summary>
    public double AmountOf(CutoutBackground treatment) => this.AmountSet.GetValueOrDefault(treatment, DefaultAmount);

    /// <summary>What the model's mask depends on — the mode, the points, the reach — the other options aside: two effects with the same key share a mask.</summary>
    public string MaskKey => this.Mode == CutoutMode.Subject
        ? "subject"
        : $"{this.Reach}:" + string.Join(";", this.Points.Select(p => FormattableString.Invariant($"{p.X:R},{p.Y:R},{(p.Included ? 1 : 0)}")));

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

    public CutoutEffect WithReach(CutoutReach reach) => this with { Reach = reach };

    public CutoutEffect WithGrow(double grow) => this with { Grow = Math.Clamp(grow, -1, 1) };

    public CutoutEffect WithSoften(double soften) => this with { Soften = Math.Clamp(soften, 0, 1) };

    public CutoutEffect WithInvert(bool invert) => this with { Invert = invert };

    public CutoutEffect WithColor(Color color) => this with { Color = Color.FromArgb(255, color) };

    /// <summary>A treatment turned on or off, its amount kept; <see cref="CutoutBackground.Transparent"/> on turns them all off.</summary>
    public CutoutEffect WithTreatment(CutoutBackground treatment, bool on)
    {
        var treatments = new HashSet<CutoutBackground>(this.TreatmentSet);
        if (treatment == CutoutBackground.Transparent)
        {
            treatments.Clear();
        }
        else if (on)
        {
            treatments.Add(treatment);
        }
        else
        {
            treatments.Remove(treatment);
        }

        return this with { TreatmentSet = treatments };
    }

    public CutoutEffect WithAmount(CutoutBackground treatment, double amount) =>
        this with { AmountSet = new Dictionary<CutoutBackground, double>(this.AmountSet) { [treatment] = Math.Clamp(amount, 0, 1) } };

    // The points, the treatments and the amounts compared by value: a record compares collections by reference.
    public bool Equals(CutoutEffect? other) =>
        other is not null && this.Mode == other.Mode && this.Reach == other.Reach && this.Grow == other.Grow
        && this.Soften == other.Soften && this.Invert == other.Invert && this.Color.ToArgb() == other.Color.ToArgb()
        && this.Points.SequenceEqual(other.Points) && this.TreatmentSet.SetEquals(other.TreatmentSet)
        && Enum.GetValues<CutoutBackground>().All(t => this.AmountOf(t) == other.AmountOf(t));

    public override int GetHashCode() =>
        HashCode.Combine(this.Mode, this.Points.Count, this.Reach, this.Grow, this.Soften, this.Invert, this.TreatmentSet.Count, this.Color.ToArgb());

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>): the points in fractions of the image as loaded.</summary>
    internal JsonObject ToJson()
    {
        var points = new JsonArray();
        foreach (var point in this.Points)
        {
            // As doubles: a value built from a float does not read back as one before it is parsed.
            points.Add(new JsonObject { ["x"] = (double)point.X, ["y"] = (double)point.Y, ["included"] = point.Included });
        }

        var treatments = new JsonArray();
        foreach (var treatment in this.Treatments)
        {
            treatments.Add(treatment.ToString());
        }

        return new JsonObject
        {
            ["mode"] = this.Mode.ToString(),
            ["points"] = points,
            ["reach"] = this.Reach.ToString(),
            ["grow"] = this.Grow,
            ["soften"] = this.Soften,
            ["invert"] = this.Invert,
            ["treatments"] = treatments,
            ["opacity"] = this.AmountOf(CutoutBackground.Opacity),
            ["colorOpacity"] = this.AmountOf(CutoutBackground.Color),
            ["blur"] = this.AmountOf(CutoutBackground.Blur),
            ["pixels"] = this.AmountOf(CutoutBackground.Pixelate),
            ["color"] = $"#{this.Color.R:X2}{this.Color.G:X2}{this.Color.B:X2}",
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
        cutout = cutout
            .WithReach(PresetJson.Enum(json, "reach", Default.Reach))
            .WithGrow(PresetJson.Double(json, "grow", Default.Grow))
            .WithSoften(PresetJson.Double(json, "soften", Default.Soften))
            .WithInvert(PresetJson.Bool(json, "invert", Default.Invert))
            .WithColor(ColorOf(json, "color") ?? Default.Color)
            .WithAmount(CutoutBackground.Opacity, PresetJson.Double(json, "opacity", DefaultAmount))
            .WithAmount(CutoutBackground.Color, PresetJson.Double(json, "colorOpacity", DefaultAmount))
            .WithAmount(CutoutBackground.Blur, PresetJson.Double(json, "blur", DefaultAmount))
            .WithAmount(CutoutBackground.Pixelate, PresetJson.Double(json, "pixels", DefaultAmount));
        if (json["treatments"] is JsonArray treatments)
        {
            foreach (var name in treatments.OfType<JsonValue>())
            {
                if (name.TryGetValue(out string? text) && Enum.TryParse(text, out CutoutBackground treatment) && Enum.IsDefined(treatment))
                {
                    cutout = cutout.WithTreatment(treatment, on: true);
                }
            }
        }
        else if (PresetJson.Enum(json, "background", CutoutBackground.Transparent) is var single and not CutoutBackground.Transparent)
        {
            // A preset of the one-treatment version: its treatment and its single amount.
            cutout = cutout.WithTreatment(single, on: true).WithAmount(single, PresetJson.Double(json, "amount", DefaultAmount));
        }

        return cutout;
    }

    // A color written #RRGGBB; null when missing or malformed.
    private static Color? ColorOf(JsonObject json, string key) =>
        json[key] is JsonValue value && value.TryGetValue(out string? text) && text is ['#', .. var hex] && hex.Length == 6
            && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
            ? Color.FromArgb(255, Color.FromArgb(rgb))
            : null;

    /// <summary>What the preset dialog lists after its name: the mode, then the options not at their defaults.</summary>
    internal string Describe()
    {
        var parts = new List<string>
        {
            this.Mode == CutoutMode.Subject ? "subject"
                : $"picked, {this.Points.Count} point{(this.Points.Count == 1 ? "" : "s")}{(this.Reach == CutoutReach.Auto ? "" : $", reach {this.Reach.ToString().ToLowerInvariant()}")}",
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

        foreach (var treatment in this.Treatments)
        {
            string color = treatment == CutoutBackground.Color ? $" #{this.Color.R:X2}{this.Color.G:X2}{this.Color.B:X2}" : "";
            parts.Add($"{treatment.ToString().ToLowerInvariant()}{color} {Math.Round(this.AmountOf(treatment) * 100)} %");
        }

        return string.Join(", ", parts);
    }
}
