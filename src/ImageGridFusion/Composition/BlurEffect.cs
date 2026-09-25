namespace ImageGridFusion.Composition;

/// <summary>How the bands around the sharp rectangle are blurred.</summary>
public enum BlurKind
{
    Gaussian,
    Pixelate,
}

/// <summary>A side of the sharp rectangle, each moved by its own bar.</summary>
public enum BlurSide
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// The blur effect of an image: a rectangle of its cell stays sharp, the bands around it are blurred
/// with <see cref="Kind"/> at <see cref="Intensity"/>. The sides are fractions of the cell's width and
/// height, so the rectangle stays where it is when the image is zoomed, panned or turned, and survives
/// a change of layout.
/// </summary>
public sealed record BlurEffect
{
    /// <summary>Centered, half the cell's width and height, gaussian at mid-course.</summary>
    public static readonly BlurEffect Default = new();

    /// <summary>From 0, the left edge of the cell, to 1, its right edge.</summary>
    public double Left { get; private init; } = 0.25;

    /// <summary>From 0, the top edge of the cell, to 1, its bottom edge.</summary>
    public double Top { get; private init; } = 0.25;

    public double Right { get; private init; } = 0.75;

    public double Bottom { get; private init; } = 0.75;

    public BlurKind Kind { get; private init; } = BlurKind.Gaussian;

    /// <summary>From 0, the lightest blur, to 1, the strongest.</summary>
    public double Intensity { get; private init; } = 0.5;

    public double Side(BlurSide side) => side switch
    {
        BlurSide.Left => Left,
        BlurSide.Top => Top,
        BlurSide.Right => Right,
        _ => Bottom,
    };

    /// <summary>
    /// Moves one side to <paramref name="value"/>, kept within the cell and at least
    /// <paramref name="minGap"/> away from the opposite side, so the bars never cross.
    /// </summary>
    public BlurEffect WithSide(BlurSide side, double value, double minGap = 0) => side switch
    {
        BlurSide.Left => this with { Left = Math.Max(0, Math.Min(value, Right - minGap)) },
        BlurSide.Top => this with { Top = Math.Max(0, Math.Min(value, Bottom - minGap)) },
        BlurSide.Right => this with { Right = Math.Min(1, Math.Max(value, Left + minGap)) },
        _ => this with { Bottom = Math.Min(1, Math.Max(value, Top + minGap)) },
    };

    public BlurEffect WithKind(BlurKind kind) => this with { Kind = kind };

    public BlurEffect WithIntensity(double intensity) => this with { Intensity = Math.Clamp(intensity, 0, 1) };

    /// <summary>The part of <paramref name="cell"/> that stays sharp: a side on 0 or 1 lands exactly on the cell's edge, leaving no blur there.</summary>
    public Rectangle Area(Rectangle cell) => Rectangle.FromLTRB(
        cell.X + (int)Math.Round(Left * cell.Width),
        cell.Y + (int)Math.Round(Top * cell.Height),
        cell.X + (int)Math.Round(Right * cell.Width),
        cell.Y + (int)Math.Round(Bottom * cell.Height));
}
