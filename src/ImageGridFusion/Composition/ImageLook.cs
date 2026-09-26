using System.Drawing.Drawing2D;

namespace ImageGridFusion.Composition;

/// <summary>Effects of the effects toolbar, in the order of its buttons: the background, the lowest layer, then geometry, then rendering, then sound.</summary>
public enum ImageEffect
{
    Background,
    Zoom,
    Rotate,
    Flip,
    Frames,
    BlackAndWhite,
    Blur,
    Volume,
}

/// <summary>
/// Effects applied to one image of the grid, toggled from the effects toolbar (see RULES.md): the
/// background behind it, a zoom around <see cref="Focus"/>, a rotation by quarter turns, flips in the
/// screen frame, black &amp; white, and the blur; the frames effect for an animated image, the volume
/// for a video with sound. An effect turned off keeps its settings, drawn as its defaults until it is
/// turned on again — but the background, on by default, draws no fill while off (RULES.md).
/// Immutable, so an export can capture it.
/// </summary>
public sealed record ImageLook
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 16;

    /// <summary>A fine angle goes this far either way from the quarter turn; beyond, the next quarter turn is nearer.</summary>
    public const int MaxFineAngle = 45;

    // Before None, which reads it: static fields are initialized in order.
    private static readonly PointF Center = new(0.5f, 0.5f);

    public static readonly ImageLook None = new();

    // Zoom, Rotate and Flip are active once activated or changed, even at their default values; the
    // other effects are active while their settings are set.
    private int Activations { get; init; }

    // The settings of the effects turned off, brought back when they are turned on again; never drawn.
    // A zoom or a move of the image replaces the kept zoom, and turning or flipping the image turns or
    // flips the kept focus and flips with it, so they come back on the same part of the image.
    private (double Zoom, PointF Focus)? KeptZoom { get; init; }
    private (int Rotation, int FineAngle)? KeptRotation { get; init; }
    private (bool X, bool Y)? KeptFlip { get; init; }
    private FramesEffect? KeptFrames { get; init; }
    private double? KeptGrayscale { get; init; }
    private BlurEffect? KeptBlur { get; init; }
    private VolumeEffect? KeptVolume { get; init; }
    private BackgroundEffect? KeptBackground { get; init; }

    /// <summary>Clockwise rotation, in degrees: 0, 90, 180 or 270.</summary>
    public int Rotation { get; private init; }

    /// <summary>
    /// Clockwise angle added to <see cref="Rotation"/>, in degrees, within ±<see cref="MaxFineAngle"/>:
    /// the image turns around the center of its cell, zoomed just enough to keep covering it.
    /// </summary>
    public int FineAngle { get; private init; }

    /// <summary>Left↔right flip, as the image is seen once rotated.</summary>
    public bool FlipX { get; private init; }

    /// <summary>Top↔bottom flip, as the image is seen once rotated.</summary>
    public bool FlipY { get; private init; }

    /// <summary>Share of the way to black &amp; white, from 0 (the colors) to 1; <c>null</c> while the effect is inactive.</summary>
    public double? Grayscale { get; private init; }

    /// <summary>Scale of the fit given by the fitting rule: 1 draws the image as that rule does.</summary>
    public double Zoom { get; private init; } = 1;

    /// <summary>
    /// Point of the oriented image kept at the center of the cell, in fractions of its width and
    /// height; beyond 0…1 when the image is moved past the cell's edges (see <see cref="FitCalculator.Place"/>).
    /// </summary>
    public PointF Focus { get; private init; } = Center;

    /// <summary>The blur effect, <c>null</c> while inactive. Turning or zooming the image leaves it in place.</summary>
    public BlurEffect? Blur { get; private init; }

    /// <summary>The frames effect, <c>null</c> while inactive: the content then plays from its beginning.</summary>
    public FramesEffect? Frames { get; private init; }

    /// <summary>The volume effect, <c>null</c> while inactive: the sound is then mixed as it is, at 100 %.</summary>
    public VolumeEffect? Volume { get; private init; }

    /// <summary>The background effect, on by default; <c>null</c> while off: nothing is then painted behind the image.</summary>
    public BackgroundEffect? Background { get; private init; } = BackgroundEffect.Default;

    /// <summary>Scale the image's sound is mixed at: 0 while muted, 1 while the volume effect is off.</summary>
    public double SoundGain => Volume?.Gain ?? 1;

    /// <summary>Every effect at its default: none on but the background, no settings kept.</summary>
    public bool IsNone => this == None;

    public bool IsActive(ImageEffect effect) => effect switch
    {
        ImageEffect.Background => Background is not null,
        ImageEffect.Frames => Frames is not null,
        ImageEffect.BlackAndWhite => Grayscale is not null,
        ImageEffect.Blur => Blur is not null,
        ImageEffect.Volume => Volume is not null,
        _ => (Activations & Bit(effect)) != 0,
    };

    /// <summary>
    /// Turns an effect on, with the settings it kept when turned off, else with its defaults; one
    /// already on is left as it is. Also what the options of an effect that is off show.
    /// </summary>
    public ImageLook TurnOn(ImageEffect effect)
    {
        if (IsActive(effect))
        {
            return this;
        }

        switch (effect)
        {
            case ImageEffect.Zoom when KeptZoom is { } zoom:
                return Activated(effect) with { Zoom = zoom.Zoom, Focus = zoom.Focus, KeptZoom = null };
            case ImageEffect.Rotate when KeptRotation is { } rotation:
                return WithRotation(rotation.Rotation) with { FineAngle = rotation.FineAngle, KeptRotation = null };
            case ImageEffect.Flip when KeptFlip is { } flip:
                var flipped = Activated(effect) with { KeptFlip = null };
                flipped = flip.X ? flipped.ToggleFlipX() : flipped;
                return flip.Y ? flipped.ToggleFlipY() : flipped;
            case ImageEffect.Frames when KeptFrames is { } frames:
                return this with { Frames = frames, KeptFrames = null };
            case ImageEffect.BlackAndWhite when KeptGrayscale is { } grayscale:
                return this with { Grayscale = grayscale, KeptGrayscale = null };
            case ImageEffect.Blur when KeptBlur is { } blur:
                return this with { Blur = blur, KeptBlur = null };
            case ImageEffect.Volume when KeptVolume is { } volume:
                return this with { Volume = volume, KeptVolume = null };
            case ImageEffect.Background when KeptBackground is { } background:
                return this with { Background = background, KeptBackground = null };
            default:
                return Activate(effect);
        }
    }

    /// <summary>Turns an effect off, drawn as its default from now on, its settings kept for <see cref="TurnOn"/>.</summary>
    public ImageLook TurnOff(ImageEffect effect)
    {
        if (!IsActive(effect))
        {
            return this;
        }

        var off = Deactivate(effect);
        return effect switch
        {
            ImageEffect.Zoom => off with { KeptZoom = (Zoom, Focus) },
            ImageEffect.Rotate => off with { KeptRotation = (Rotation, FineAngle) },
            ImageEffect.Flip => off with { KeptFlip = (FlipX, FlipY) },
            ImageEffect.Frames => off with { KeptFrames = Frames },
            ImageEffect.BlackAndWhite => off with { KeptGrayscale = Grayscale },
            ImageEffect.Volume => off with { KeptVolume = Volume },
            ImageEffect.Background => off with { KeptBackground = Background },
            _ => off with { KeptBlur = Blur },
        };
    }

    /// <summary>
    /// Brings an effect back to its default state: off, its settings at their defaults, none kept — the
    /// background on, its default state (RULES.md).
    /// </summary>
    public ImageLook Reset(ImageEffect effect)
    {
        var look = Deactivate(effect);
        return effect switch
        {
            ImageEffect.Background => look with { Background = BackgroundEffect.Default, KeptBackground = null },
            ImageEffect.Zoom => look with { KeptZoom = null },
            ImageEffect.Rotate => look with { KeptRotation = null },
            ImageEffect.Flip => look with { KeptFlip = null },
            ImageEffect.Frames => look with { KeptFrames = null },
            ImageEffect.BlackAndWhite => look with { KeptGrayscale = null },
            ImageEffect.Volume => look with { KeptVolume = null },
            _ => look with { KeptBlur = null },
        };
    }

    /// <summary>Activates an effect with its defaults; one already active is left as it is.</summary>
    private ImageLook Activate(ImageEffect effect) => IsActive(effect) ? this : effect switch
    {
        ImageEffect.Background => this with { Background = BackgroundEffect.Default },
        ImageEffect.Frames => this with { Frames = FramesEffect.Default },
        ImageEffect.BlackAndWhite => this with { Grayscale = 1 },
        ImageEffect.Blur => this with { Blur = BlurEffect.Default },
        ImageEffect.Volume => this with { Volume = VolumeEffect.Default },
        _ => Activated(effect),
    };

    /// <summary>Deactivates an effect, bringing back its defaults: no background, centered at 100 %, upright, unflipped, playing from the beginning, in color, sharp, heard at 100 %.</summary>
    private ImageLook Deactivate(ImageEffect effect)
    {
        var look = effect switch
        {
            ImageEffect.Background => this with { Background = null },
            ImageEffect.Zoom => this with { Zoom = 1, Focus = Center },
            ImageEffect.Rotate => WithRotation(0),
            ImageEffect.Flip => (FlipX ? ToggleFlipX() : this) is var flipped && flipped.FlipY ? flipped.ToggleFlipY() : flipped,
            ImageEffect.Frames => this with { Frames = null },
            ImageEffect.BlackAndWhite => this with { Grayscale = null },
            ImageEffect.Volume => this with { Volume = null },
            _ => this with { Blur = null },
        };
        return look with { Activations = look.Activations & ~Bit(effect) };
    }

    /// <summary>A quarter turn either way: the width and height of the image swap.</summary>
    public bool SwapsAxes => Rotation % 180 != 0;

    /// <summary>Size of an image of <paramref name="size"/> once rotated.</summary>
    public Size Oriented(Size size) => SwapsAxes ? new Size(size.Height, size.Width) : size;

    /// <summary>Turns the image by <paramref name="quarterTurns"/> clockwise (negative: counter-clockwise); the focus turns with it.</summary>
    public ImageLook Rotate(int quarterTurns)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;

        // The flips are in the screen frame: turning a flipped image a quarter turn swaps which axis is flipped.
        bool swap = turns % 2 == 1;
        return Activated(ImageEffect.Rotate) with
        {
            Rotation = (Rotation + 90 * turns) % 360,
            FlipX = swap ? FlipY : FlipX,
            FlipY = swap ? FlipX : FlipY,
            Focus = Turned(Focus, turns),
            KeptFlip = swap && KeptFlip is { } flip ? (flip.Y, flip.X) : KeptFlip,
            KeptZoom = KeptZoom is { } zoom ? (zoom.Zoom, Turned(zoom.Focus, turns)) : null,
        };
    }

    private static PointF Turned(PointF focus, int quarterTurns)
    {
        for (int i = 0; i < quarterTurns; i++)
        {
            focus = new PointF(1 - focus.Y, focus.X);
        }

        return focus;
    }

    /// <summary>Turns the image to <paramref name="degrees"/> exactly: 0, 90, 180 or 270, with no fine angle.</summary>
    public ImageLook WithRotation(int degrees) => Rotate((degrees - Rotation) / 90) with { FineAngle = 0 };

    public ImageLook WithFineAngle(int degrees) => Activated(ImageEffect.Rotate) with { FineAngle = Math.Clamp(degrees, -MaxFineAngle, MaxFineAngle) };

    public ImageLook ToggleFlipX() => Activated(ImageEffect.Flip) with
    {
        FlipX = !FlipX,
        Focus = new PointF(1 - Focus.X, Focus.Y),
        KeptZoom = KeptZoom is { } zoom ? (zoom.Zoom, new PointF(1 - zoom.Focus.X, zoom.Focus.Y)) : null,
    };

    public ImageLook ToggleFlipY() => Activated(ImageEffect.Flip) with
    {
        FlipY = !FlipY,
        Focus = new PointF(Focus.X, 1 - Focus.Y),
        KeptZoom = KeptZoom is { } zoom ? (zoom.Zoom, new PointF(zoom.Focus.X, 1 - zoom.Focus.Y)) : null,
    };

    /// <summary>The focus is kept at every zoom; the gesture brings the image back within its stops (see <see cref="FitCalculator.WithinStops"/>).</summary>
    public ImageLook WithZoom(double zoom) => Activated(ImageEffect.Zoom) with { Zoom = Math.Clamp(zoom, MinZoom, MaxZoom), KeptZoom = null };

    /// <summary>Unclamped: how far the image may go depends on its cell, and is applied where the image is placed.</summary>
    public ImageLook WithFocus(PointF focus) => Activated(ImageEffect.Zoom) with { Focus = focus, KeptZoom = null };

    public ImageLook WithFrames(FramesEffect? frames) => this with { Frames = frames };

    public ImageLook WithGrayscale(double intensity) => this with { Grayscale = Math.Clamp(intensity, 0, 1) };

    public ImageLook WithBlur(BlurEffect? blur) => this with { Blur = blur };

    public ImageLook WithVolume(VolumeEffect? volume) => this with { Volume = volume };

    public ImageLook WithBackground(BackgroundEffect? background) => this with { Background = background };

    /// <summary>
    /// Every effect back to its default state, no settings kept — the background on: the look of an
    /// image that moves into another cell (RULES.md). The volume stays, so a shift never changes what is
    /// heard.
    /// </summary>
    public ImageLook WithoutEffects() => None with { Volume = Volume, KeptVolume = KeptVolume };

    private static int Bit(ImageEffect effect) => 1 << (int)effect;

    private ImageLook Activated(ImageEffect effect) => this with { Activations = Activations | Bit(effect) };

    /// <summary>
    /// Maps the pixels of a bitmap of <paramref name="size"/> to the oriented image: rotated, then
    /// flipped, with its top-left corner at the origin.
    /// </summary>
    public Matrix Orientation(Size size)
    {
        var oriented = Oriented(size);
        var matrix = new Matrix();

        // Built from the last operation to the first: each call prepends.
        matrix.Translate(FlipX ? oriented.Width : 0, FlipY ? oriented.Height : 0);
        matrix.Scale(FlipX ? -1 : 1, FlipY ? -1 : 1);
        switch (Rotation)
        {
            case 90:
                matrix.Translate(size.Height, 0);
                break;
            case 180:
                matrix.Translate(size.Width, size.Height);
                break;
            case 270:
                matrix.Translate(0, size.Width);
                break;
        }

        matrix.Rotate(Rotation);
        return matrix;
    }
}
