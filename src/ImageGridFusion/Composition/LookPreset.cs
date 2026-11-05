using System.Globalization;
using System.Text.Json.Nodes;
using ImageGridFusion.Resources;

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
    /// Background when it differs from its default state, off included — as the user left it, never turned
    /// off by the Grid background (workfiles/20260926-forced-background.md, Q&amp;A #17).
    /// </summary>
    public static IReadOnlyList<ImageEffect> Saved(ImageLook look) =>
        Enum.GetValues<ImageEffect>().Where(effect => effect switch
        {
            ImageEffect.Background => look.UnforcedBackground().Background != BackgroundEffect.Default,
            ImageEffect.Zoom => look.IsActive(effect) && (!look.ZoomOnArrival || MovedOnArrival(look)),
            _ => look.IsActive(effect),
        }).ToList();

    /// <summary>Whether <paramref name="look"/> holds anything a preset saves: what makes the ✥ handle released over the file explorer offer one.</summary>
    public static bool HasEffects(ImageLook look) => Saved(look).Count > 0;

    public static JsonObject ToJson(ImageLook look)
    {
        look = look.UnforcedBackground();
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
                case ImageEffect.Upscale:
                    json["upscale"] = look.Upscale!.ToJson();
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
        look = json["upscale"] is JsonObject upscale ? look.WithUpscale(UpscaleEffect.FromJson(upscale)) : look;

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
    public static IReadOnlyList<string> Describe(ImageLook look)
    {
        look = look.UnforcedBackground();
        return Saved(look).Select(effect => Texts.Format(Strings.UiDescLine, EffectName(effect), Values(look, effect))).ToList();
    }

    private static string EffectName(ImageEffect effect) => effect switch
    {
        ImageEffect.Background => Strings.UiEffectNameBackground,
        ImageEffect.Crop => Strings.UiEffectNameCrop,
        ImageEffect.Cutout => Strings.UiEffectNameCutout,
        ImageEffect.Upscale => Strings.UiEffectNameUpscale,
        ImageEffect.Zoom => Strings.UiEffectNameZoom,
        ImageEffect.Animations => Strings.UiEffectNameAnimations,
        ImageEffect.Rotate => Strings.UiEffectNameRotate,
        ImageEffect.Flip => Strings.UiEffectNameFlip,
        ImageEffect.Frames => Strings.UiEffectNameFrames,
        ImageEffect.Colors => Strings.UiEffectNameColors,
        ImageEffect.Blur => Strings.UiEffectNameBlur,
        ImageEffect.Volume => Strings.UiEffectNameVolume,
        _ => effect.ToString(),
    };

    private static string FillModeName(BackgroundFill mode) => mode switch
    {
        BackgroundFill.Color => Strings.UiDescFillColor,
        BackgroundFill.CornerPixel => Strings.UiDescFillCornerPixel,
        BackgroundFill.Miter => Strings.UiDescFillMiter,
        _ => Strings.UiDescFillBackgroundCorners,
    };

    private static string Values(ImageLook look, ImageEffect effect) => effect switch
    {
        ImageEffect.Background => look.Background is not { } background ? Strings.UiDescOffNoFill
            : Texts.Format(
                Strings.UiDescBackground,
                background.Automatic ? Strings.UiDescAutomaticColor : $"#{background.Color.R:X2}{background.Color.G:X2}{background.Color.B:X2}",
                Percent(background.Opacity),
                FillModeName(background.Mode),
                !background.Style.IsGradient ? "" : background.Style.Kind == FillKind.Linear ? Strings.UiDescGradientLinear : Strings.UiDescGradientRadial),
        ImageEffect.Crop => look.Crop!.SeenRatio(look) is { } ratio ? Texts.Format(Strings.UiDescRatio, RatioName(ratio)) : Strings.UiDescFree,
        ImageEffect.Cutout => look.Cutout!.Describe(),
        ImageEffect.Upscale => look.Upscale!.Describe(),
        ImageEffect.Zoom => look.ZoomOnArrival ? Strings.UiDescZoomMoved
            : look.ZoomFit != ZoomFit.None ? (look.ZoomFit == ZoomFit.Contain ? Strings.UiFitContain : Strings.UiFitFill) : Percent(look.Zoom),
        ImageEffect.Animations => Texts.Format(Strings.UiDescMotion, Strings.UiDescMotionZoom, Texts.Number(look.Motion!.Cycle.TotalSeconds, "0.#")),
        ImageEffect.Rotate => $"{look.Angle:+0;-0;0}°",
        ImageEffect.Flip => (look.FlipX, look.FlipY) switch
        {
            (true, true) => Strings.UiDescFlipBoth,
            (true, false) => Strings.UiDescFlipH,
            (false, true) => Strings.UiDescFlipV,
            _ => Strings.UiDescNone,
        },
        ImageEffect.Frames => Frames(look.Frames!),
        ImageEffect.Colors => Texts.Format(Strings.UiDescColors, Signed(look.Colors!.Saturation), look.Colors.Hue, Signed(look.Colors.Brightness), Signed(look.Colors.Contrast)),
        ImageEffect.Blur => Texts.Format(Strings.UiDescBlur, look.Blur!.Kind == BlurKind.Gaussian ? Strings.UiBlurGaussian : Strings.UiBlurPixelate, Percent(look.Blur.Intensity)),
        ImageEffect.Volume => look.Volume!.IsMuted ? Strings.UiDescMuted : Percent(look.Volume.Level),
        _ => "",
    };

    private static string Frames(FramesEffect frames)
    {
        string trim = frames.Last is { } last ? Texts.Format(Strings.UiDescFramesRange, frames.First, last)
            : frames.First > 0 ? Texts.Format(Strings.UiDescFramesFrom, frames.First) : Strings.UiDescFramesWhole;
        return frames.Frozen ? Texts.Format(Strings.UiDescFrozen, trim)
            : frames.Speed != FramesEffect.Default.Speed ? Texts.Format(Strings.UiDescStartingSpeed, trim, Percent(frames.Position), FramesEffect.SpeedText(frames.Speed))
            : Texts.Format(Strings.UiDescStarting, trim, Percent(frames.Position));
    }

    private static string RatioName(double ratio) =>
        CropEffect.Same(ratio, 1) ? "1:1"
        : CropEffect.Same(ratio, 4 / 3.0) ? "4:3"
        : CropEffect.Same(ratio, 16 / 9.0) ? "16:9"
        : CropEffect.Same(ratio, 9 / 16.0) ? "9:16"
        : Texts.Number(ratio, "0.##") + ":1";

    private static string Percent(double value) => $"{Math.Round(value * 100)} %";

    private static string Signed(double value) => $"{Math.Round(value * 100):+0;-0;0} %";

    // An image moved while its zoom is still the arrival one: the move is a choice, the zoom is not.
    private static bool MovedOnArrival(ImageLook look) =>
        Math.Abs(look.Focus.X - 0.5) > 0.001 || Math.Abs(look.Focus.Y - 0.5) > 0.001;
}
