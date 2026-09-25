using System.Drawing.Drawing2D;

namespace ImageGridFusion.Composition;

/// <summary>Effects of the effects toolbar, in the order of its buttons: geometry first, then rendering.</summary>
public enum ImageEffect
{
    Zoom,
    Rotate,
    Flip,
    BlackAndWhite,
    Blur,
}

/// <summary>
/// Effects applied to one image of the grid, toggled from the effects toolbar (see RULES.md): a zoom
/// around <see cref="Focus"/>, a rotation by quarter turns, flips in the screen frame, black &amp;
/// white, and the blur. Immutable, so an export can capture it.
/// </summary>
public sealed record ImageLook
{
    public const double MinZoom = 0.5;
    public const double MaxZoom = 4;

    /// <summary>A fine angle goes this far either way from the quarter turn; beyond, the next quarter turn is nearer.</summary>
    public const int MaxFineAngle = 45;

    // Before None, which reads it: static fields are initialized in order.
    private static readonly PointF Center = new(0.5f, 0.5f);

    public static readonly ImageLook None = new();

    // Zoom, Rotate and Flip are active once activated or changed, even at their default values; the
    // other effects are active while their settings are set.
    private int Activations { get; init; }

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

    public bool Grayscale { get; private init; }

    /// <summary>Scale of the fit given by the fitting rule: 1 draws the image as that rule does.</summary>
    public double Zoom { get; private init; } = 1;

    /// <summary>
    /// Point of the oriented image kept at the center of the cell, in fractions of its width and
    /// height; beyond 0…1 when the image is moved past the cell's edges (see <see cref="FitCalculator.Place"/>).
    /// </summary>
    public PointF Focus { get; private init; } = Center;

    /// <summary>The blur effect, <c>null</c> while inactive. Turning or zooming the image leaves it in place.</summary>
    public BlurEffect? Blur { get; private init; }

    public bool IsNone => this == None;

    public bool IsActive(ImageEffect effect) => effect switch
    {
        ImageEffect.BlackAndWhite => Grayscale,
        ImageEffect.Blur => Blur is not null,
        _ => (Activations & Bit(effect)) != 0,
    };

    /// <summary>Activates an effect with its defaults; one already active is left as it is.</summary>
    public ImageLook Activate(ImageEffect effect) => IsActive(effect) ? this : effect switch
    {
        ImageEffect.BlackAndWhite => this with { Grayscale = true },
        ImageEffect.Blur => this with { Blur = BlurEffect.Default },
        _ => Activated(effect),
    };

    /// <summary>Deactivates an effect, bringing back its defaults: centered at 100 %, upright, unflipped, in color, sharp.</summary>
    public ImageLook Deactivate(ImageEffect effect)
    {
        var look = effect switch
        {
            ImageEffect.Zoom => this with { Zoom = 1, Focus = Center },
            ImageEffect.Rotate => WithRotation(0),
            ImageEffect.Flip => (FlipX ? ToggleFlipX() : this) is var flipped && flipped.FlipY ? flipped.ToggleFlipY() : flipped,
            ImageEffect.BlackAndWhite => this with { Grayscale = false },
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
        var focus = Focus;
        for (int i = 0; i < turns; i++)
        {
            focus = new PointF(1 - focus.Y, focus.X);
        }

        // The flips are in the screen frame: turning a flipped image a quarter turn swaps which axis is flipped.
        bool swap = turns % 2 == 1;
        return Activated(ImageEffect.Rotate) with
        {
            Rotation = (Rotation + 90 * turns) % 360,
            FlipX = swap ? FlipY : FlipX,
            FlipY = swap ? FlipX : FlipY,
            Focus = focus,
        };
    }

    /// <summary>Turns the image to <paramref name="degrees"/> exactly: 0, 90, 180 or 270, with no fine angle.</summary>
    public ImageLook WithRotation(int degrees) => Rotate((degrees - Rotation) / 90) with { FineAngle = 0 };

    public ImageLook WithFineAngle(int degrees) => Activated(ImageEffect.Rotate) with { FineAngle = Math.Clamp(degrees, -MaxFineAngle, MaxFineAngle) };

    public ImageLook ToggleFlipX() => Activated(ImageEffect.Flip) with { FlipX = !FlipX, Focus = new PointF(1 - Focus.X, Focus.Y) };

    public ImageLook ToggleFlipY() => Activated(ImageEffect.Flip) with { FlipY = !FlipY, Focus = new PointF(Focus.X, 1 - Focus.Y) };

    /// <summary>The focus is kept at every zoom; the gesture brings the image back within its stops (see <see cref="FitCalculator.WithinStops"/>).</summary>
    public ImageLook WithZoom(double zoom) => Activated(ImageEffect.Zoom) with { Zoom = Math.Clamp(zoom, MinZoom, MaxZoom) };

    /// <summary>Unclamped: how far the image may go depends on its cell, and is applied where the image is placed.</summary>
    public ImageLook WithFocus(PointF focus) => Activated(ImageEffect.Zoom) with { Focus = focus };

    public ImageLook WithBlur(BlurEffect? blur) => this with { Blur = blur };

    /// <summary>Every effect removed: the look of an image that moves into another cell (RULES.md).</summary>
    public ImageLook WithoutEffects() => None;

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
