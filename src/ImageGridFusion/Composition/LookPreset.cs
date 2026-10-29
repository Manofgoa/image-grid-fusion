using System.Globalization;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>
/// The effects an effect preset saves from a cell and puts back on the same file later: the effects
/// turned on, never the settings an effect turned off keeps, and the Background turned off — on by
/// default, turning it off is a choice. Written by hand under named keys, as the settings file is
/// (workfiles/20261026-effect-preset-favorites.md § What a Preset Saves).
/// </summary>
internal static class LookPreset
{
    /// <summary>The format written; a later effect adds its key without changing it.</summary>
    public const int Version = 1;

    /// <summary>
    /// The effects a preset saves from <paramref name="look"/>, in the toolbar's order: those turned on —
    /// the Zoom only once changed since the image arrived, or the image moved meanwhile — and the
    /// Background when it differs from its default state, off included.
    /// </summary>
    public static IReadOnlyList<ImageEffect> Saved(ImageLook look) =>
        Enum.GetValues<ImageEffect>().Where(effect => effect switch
        {
            ImageEffect.Background => look.Background != BackgroundEffect.Default,
            ImageEffect.Zoom => look.IsActive(effect) && (!look.ZoomOnArrival || MovedOnArrival(look)),
            _ => look.IsActive(effect),
        }).ToList();

    /// <summary>Whether <paramref name="look"/> holds anything a preset saves: what makes the ✥ handle released over the file explorer offer one.</summary>
    public static bool HasEffects(ImageLook look) => Saved(look).Count > 0;

    public static JsonObject ToJson(ImageLook look)
    {
        var json = new JsonObject { ["version"] = Version };
        foreach (var effect in Saved(look))
        {
            switch (effect)
            {
                case ImageEffect.Background:
                    json["background"] = look.Background?.ToJson();
                    break;
                case ImageEffect.Crop:
                    json["crop"] = look.Crop!.ToJson();
                    break;
                case ImageEffect.Cutout:
                    json["cutout"] = look.Cutout!.ToJson();
                    break;
                case ImageEffect.Zoom:
                    json["zoom"] = new JsonObject
                    {
                        ["arrival"] = look.ZoomOnArrival,
                        ["zoom"] = look.Zoom,
                        ["fit"] = look.ZoomFit.ToString(),
                        // As doubles: a value built from a float does not read back as one before it is parsed.
                        ["focusX"] = (double)look.Focus.X,
                        ["focusY"] = (double)look.Focus.Y,
                    };
                    break;
                case ImageEffect.Animations:
                    json["animations"] = look.Motion!.ToJson();
                    break;
                case ImageEffect.Rotate:
                    json["rotate"] = new JsonObject { ["rotation"] = look.Rotation, ["fineAngle"] = look.FineAngle };
                    break;
                case ImageEffect.Flip:
                    json["flip"] = new JsonObject { ["x"] = look.FlipX, ["y"] = look.FlipY };
                    break;
                case ImageEffect.Frames:
                    json["frames"] = look.Frames!.ToJson();
                    break;
                case ImageEffect.Colors:
                    json["colors"] = look.Colors!.ToJson();
                    break;
                case ImageEffect.Blur:
                    json["blur"] = look.Blur!.ToJson();
                    break;
                case ImageEffect.Volume:
                    json["volume"] = look.Volume!.ToJson();
                    break;
            }
        }

        return json;
    }

    /// <summary>
    /// The look <see cref="ToJson"/> saved, every effect it does not hold at its default state. The
    /// orientation comes first — a turn or a flip turns the crop and the focus along — then the crop,
    /// then the others, the zoom and its focus last. A zoom saved as the arrival zoom of a moved image
    /// stays flagged so: <c>GridPreview.ShowWhole</c> computes it in the new cell, keeping the focus.
    /// </summary>
    public static ImageLook FromJson(JsonObject json)
    {
        var look = ImageLook.None;
        if (json["rotate"] is JsonObject rotate)
        {
            int quarter = ((PresetJson.Int(rotate, "rotation", 0) / 90 % 4) + 4) % 4;
            look = look.WithRotation(quarter * 90).WithFineAngle(PresetJson.Int(rotate, "fineAngle", 0));
        }

        if (json["flip"] is JsonObject flip)
        {
            look = look.TurnOn(ImageEffect.Flip);
            look = PresetJson.Bool(flip, "x", false) ? look.ToggleFlipX() : look;
            look = PresetJson.Bool(flip, "y", false) ? look.ToggleFlipY() : look;
        }

        if (json["crop"] is JsonObject crop)
        {
            look = look.WithCrop(CropEffect.FromJson(crop));
        }

        look = json["cutout"] is JsonObject cutout ? look.WithCutout(CutoutEffect.FromJson(cutout)) : look;

        if (json.TryGetPropertyValue("background", out var background))
        {
            look = look.WithBackground(background is JsonObject settings ? BackgroundEffect.FromJson(settings) : null);
        }

        look = json["animations"] is JsonObject motion ? look.WithMotion(MotionEffect.FromJson(motion)) : look;
        look = json["frames"] is JsonObject frames ? look.WithFrames(FramesEffect.FromJson(frames)) : look;
        look = json["colors"] is JsonObject colors ? look.WithColors(ColorsEffect.FromJson(colors)) : look;
        look = json["blur"] is JsonObject blur ? look.WithBlur(BlurEffect.FromJson(blur)) : look;
        look = json["volume"] is JsonObject volume ? look.WithVolume(VolumeEffect.FromJson(volume)) : look;
        if (json["zoom"] is JsonObject zoom)
        {
            var focus = new PointF(
                (float)PresetJson.Double(zoom, "focusX", 0.5),
                (float)PresetJson.Double(zoom, "focusY", 0.5));
            if (PresetJson.Bool(zoom, "arrival", false))
            {
                look = look.WithArrivalZoom(1).WithFocus(focus);
            }
            else
            {
                look = look.WithZoom(PresetJson.Double(zoom, "zoom", 1));
                var fit = PresetJson.Enum(zoom, "fit", ZoomFit.None);
                look = (fit == ZoomFit.None ? look : look.WithZoomFit(fit)).WithFocus(focus);
            }
        }

        return look;
    }

    /// <summary>
    /// One line per effect <paramref name="look"/> saves, its key values after its name
    /// (<c>Crop — 16:9</c>): what the preset dialog lists.
    /// </summary>
    public static IReadOnlyList<string> Describe(ImageLook look) =>
        Saved(look).Select(effect => $"{effect} — {Values(look, effect)}").ToList();

    private static string Values(ImageLook look, ImageEffect effect) => effect switch
    {
        ImageEffect.Background => look.Background is not { } background ? "off, no fill"
            : $"{(background.Automatic ? "automatic color" : $"#{background.Color.R:X2}{background.Color.G:X2}{background.Color.B:X2}")}, {Percent(background.Opacity)}, {background.Mode}",
        ImageEffect.Crop => look.Crop!.SeenRatio(look) is { } ratio ? $"ratio {RatioName(ratio)}" : "free",
        ImageEffect.Cutout => look.Cutout!.Describe(),
        ImageEffect.Zoom => look.ZoomOnArrival ? "image moved, whole at arrival"
            : look.ZoomFit != ZoomFit.None ? look.ZoomFit.ToString() : Percent(look.Zoom),
        ImageEffect.Animations => $"{look.Motion!.Kind}, every {look.Motion.Cycle.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s",
        ImageEffect.Rotate => look.FineAngle == 0 ? $"{look.Rotation}°" : $"{look.Rotation}° {look.FineAngle:+0;-0}°",
        ImageEffect.Flip => (look.FlipX, look.FlipY) switch
        {
            (true, true) => "horizontal and vertical",
            (true, false) => "horizontal",
            (false, true) => "vertical",
            _ => "none",
        },
        ImageEffect.Frames => Frames(look.Frames!),
        ImageEffect.Colors => $"saturation {Signed(look.Colors!.Saturation)}, hue {look.Colors.Hue:+0;-0;0}°, brightness {Signed(look.Colors.Brightness)}, contrast {Signed(look.Colors.Contrast)}",
        ImageEffect.Blur => $"{look.Blur!.Kind}, {Percent(look.Blur.Intensity)}",
        ImageEffect.Volume => look.Volume!.IsMuted ? "muted" : Percent(look.Volume.Level),
        _ => "",
    };

    private static string Frames(FramesEffect frames)
    {
        string trim = frames.Last is { } last ? $"frames {frames.First} → {last}"
            : frames.First > 0 ? $"from frame {frames.First}" : "whole";
        return frames.Frozen ? $"{trim}, frozen" : $"{trim}, starting at {Percent(frames.Position)}";
    }

    private static string RatioName(double ratio) =>
        CropEffect.Same(ratio, 1) ? "1:1"
        : CropEffect.Same(ratio, 4 / 3.0) ? "4:3"
        : CropEffect.Same(ratio, 16 / 9.0) ? "16:9"
        : CropEffect.Same(ratio, 9 / 16.0) ? "9:16"
        : $"{ratio.ToString("0.##", CultureInfo.InvariantCulture)}:1";

    private static string Percent(double value) => $"{Math.Round(value * 100)} %";

    private static string Signed(double value) => $"{Math.Round(value * 100):+0;-0;0} %";

    // An image moved while its zoom is still the arrival one: the move is a choice, the zoom is not.
    private static bool MovedOnArrival(ImageLook look) =>
        Math.Abs(look.Focus.X - 0.5) > 0.001 || Math.Abs(look.Focus.Y - 0.5) > 0.001;
}
