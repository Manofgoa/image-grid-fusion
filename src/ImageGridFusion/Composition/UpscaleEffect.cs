using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>The model the Upscale effect enlarges with.</summary>
public enum UpscaleModel
{
    /// <summary>Real-ESRGAN's RRDBNet, ×2 and ×4: the finest, slow on the CPU.</summary>
    Quality,

    /// <summary>Real-ESRGAN's compact model, ×4 only — its ×2 is its ×4 halved: many times faster, a little less fine.</summary>
    Fast,
}

/// <summary>
/// The Upscale effect of an image: Real-ESRGAN enlarges it ×2 or ×4 with the Quality or the Fast model, the
/// enlarged bitmap drawn by every reader in place of the image as loaded. On the look it is the setting applied —
/// the one an enlargement was computed for, in the Upscale window — cached on the image
/// (<see cref="SourceImage.DrawnBitmap"/>, <see cref="SourceImage.DrawnPages"/>); this record holds the setting only.
/// See workfiles/20261029-image-upscale.md.
/// </summary>
public sealed record UpscaleEffect
{
    /// <summary>The factors offered.</summary>
    public static readonly IReadOnlyList<int> Factors = [2, 4];

    public static readonly UpscaleEffect Default = new();

    /// <summary>How many times larger the image is drawn from: 2 or 4.</summary>
    public int Factor { get; private init; } = 2;

    /// <summary>The model enlarging it.</summary>
    public UpscaleModel Model { get; private init; } = UpscaleModel.Quality;

    public UpscaleEffect WithFactor(int factor) => this with { Factor = Factors.Contains(factor) ? factor : Default.Factor };

    public UpscaleEffect WithModel(UpscaleModel model) => this with { Model = Enum.IsDefined(model) ? model : Default.Model };

    /// <summary>
    /// Whether an image of <paramref name="size"/> enlarged <paramref name="factor"/> times stays within the
    /// PNG's cap on its longer side (<see cref="CanvasSizer.MaxLongSide"/>): past it, it could never be drawn at its size.
    /// </summary>
    public static bool Fits(Size size, int factor) => (long)Math.Max(size.Width, size.Height) * factor <= CanvasSizer.MaxLongSide;

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>).</summary>
    internal JsonObject ToJson() => new() { ["factor"] = this.Factor, ["model"] = this.Model.ToString() };

    /// <summary>The settings <see cref="ToJson"/> wrote; a missing or foreign value gives ×2 Quality.</summary>
    internal static UpscaleEffect FromJson(JsonObject json) =>
        Default.WithFactor(PresetJson.Int(json, "factor", Default.Factor)).WithModel(PresetJson.Enum(json, "model", Default.Model));

    /// <summary>What the preset dialog and the Upscale window name it by.</summary>
    internal string Describe() => $"×{this.Factor} {this.Model}";
}
