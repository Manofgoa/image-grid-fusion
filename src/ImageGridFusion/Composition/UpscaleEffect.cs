using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>
/// The Upscale effect of an image: Real-ESRGAN enlarges it ×2 or ×4, the enlarged bitmap drawn by every
/// reader in place of the image as loaded. It is computed off the UI thread and cached on the image
/// (<see cref="SourceImage.DrawnBitmap"/>, <see cref="SourceImage.DrawnPages"/>); this record holds the
/// factor only. See workfiles/20261029-image-upscale.md.
/// </summary>
public sealed record UpscaleEffect
{
    /// <summary>The factors a model exists for.</summary>
    public static readonly IReadOnlyList<int> Factors = [2, 4];

    public static readonly UpscaleEffect Default = new();

    /// <summary>How many times larger the image is drawn from: 2 or 4.</summary>
    public int Factor { get; private init; } = 2;

    public UpscaleEffect WithFactor(int factor) => this with { Factor = Factors.Contains(factor) ? factor : Default.Factor };

    /// <summary>
    /// Whether an image of <paramref name="size"/> enlarged <paramref name="factor"/> times stays within the
    /// PNG's cap on its longer side (<see cref="CanvasSizer.MaxLongSide"/>): past it, it could never be drawn at its size.
    /// </summary>
    public static bool Fits(Size size, int factor) => (long)Math.Max(size.Width, size.Height) * factor <= CanvasSizer.MaxLongSide;

    /// <summary>Its settings as an effect preset saves them (<see cref="LookPreset"/>).</summary>
    internal JsonObject ToJson() => new() { ["factor"] = this.Factor };

    /// <summary>The settings <see cref="ToJson"/> wrote; a missing or foreign factor gives ×2.</summary>
    internal static UpscaleEffect FromJson(JsonObject json) => Default.WithFactor(PresetJson.Int(json, "factor", Default.Factor));

    /// <summary>What the preset dialog lists after its name.</summary>
    internal string Describe() => $"×{this.Factor}";
}
