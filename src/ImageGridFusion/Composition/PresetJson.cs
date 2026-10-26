using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>
/// Reads the values of an effect preset's JSON, written by hand under named keys as the settings file
/// is: a key missing, or of another type, gives the effect's own default — a preset written by an
/// older or a newer version reads what it can.
/// </summary>
internal static class PresetJson
{
    public static double Double(JsonObject json, string key, double fallback) =>
        json[key] is JsonValue value && value.TryGetValue(out double result) && double.IsFinite(result) ? result : fallback;

    public static int Int(JsonObject json, string key, int fallback) =>
        json[key] is JsonValue value && value.TryGetValue(out int result) ? result : fallback;

    public static int? NullableInt(JsonObject json, string key) =>
        json[key] is JsonValue value && value.TryGetValue(out int result) ? result : null;

    public static bool Bool(JsonObject json, string key, bool fallback) =>
        json[key] is JsonValue value && value.TryGetValue(out bool result) ? result : fallback;

    public static T Enum<T>(JsonObject json, string key, T fallback)
        where T : struct, System.Enum =>
        json[key] is JsonValue value && value.TryGetValue(out string? name) && System.Enum.TryParse(name, out T result)
            && System.Enum.IsDefined(result) ? result : fallback;
}
