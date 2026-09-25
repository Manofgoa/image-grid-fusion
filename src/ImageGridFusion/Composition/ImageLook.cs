using System.Drawing.Drawing2D;

namespace ImageGridFusion.Composition;

/// <summary>
/// Actions applied to one image of the grid: a rotation by quarter turns, flips in the screen frame,
/// black &amp; white, and a zoom around <see cref="Focus"/>; and its effects, toggled from the effects
/// toolbar (see RULES.md). Immutable, so an export can capture it.
/// </summary>
public sealed record ImageLook
{
    public const double MinZoom = 0.5;
    public const double MaxZoom = 4;

    // Before None, which reads it: static fields are initialized in order.
    private static readonly PointF Center = new(0.5f, 0.5f);

    public static readonly ImageLook None = new();

    /// <summary>Clockwise rotation, in degrees: 0, 90, 180 or 270.</summary>
    public int Rotation { get; private init; }

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
        return this with
        {
            Rotation = (Rotation + 90 * turns) % 360,
            FlipX = swap ? FlipY : FlipX,
            FlipY = swap ? FlipX : FlipY,
            Focus = focus,
        };
    }

    public ImageLook ToggleFlipX() => this with { FlipX = !FlipX, Focus = new PointF(1 - Focus.X, Focus.Y) };

    public ImageLook ToggleFlipY() => this with { FlipY = !FlipY, Focus = new PointF(Focus.X, 1 - Focus.Y) };

    public ImageLook ToggleGrayscale() => this with { Grayscale = !Grayscale };

    /// <summary>The focus is kept at every zoom; the gesture brings the image back within its stops (see <see cref="FitCalculator.WithinStops"/>).</summary>
    public ImageLook WithZoom(double zoom) => this with { Zoom = Math.Clamp(zoom, MinZoom, MaxZoom) };

    /// <summary>Unclamped: how far the image may go depends on its cell, and is applied where the image is placed.</summary>
    public ImageLook WithFocus(PointF focus) => this with { Focus = focus };

    public ImageLook WithBlur(BlurEffect? blur) => this with { Blur = blur };

    /// <summary>The same actions, every effect removed.</summary>
    public ImageLook WithoutEffects() => this with { Blur = null };

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
