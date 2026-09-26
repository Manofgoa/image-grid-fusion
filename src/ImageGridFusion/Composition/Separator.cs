namespace ImageGridFusion.Composition;

/// <summary>
/// A straight stretch of boundary shared by cells on both of its sides, which the user drags to
/// resize them. <see cref="Before"/> holds the cells left of it (vertical) or above it (horizontal),
/// <see cref="After"/> those on the other side; moving it moves all of them, and no other cell.
/// Positions are fractions of the canvas width (vertical) or height (horizontal).
/// </summary>
/// <param name="Vertical">Whether the separator is a vertical line, moved left and right.</param>
/// <param name="Position">Where it lies across its axis.</param>
/// <param name="Start">Where it starts along its length.</param>
/// <param name="End">Where it ends along its length.</param>
/// <param name="Before">Cells left of it, or above it.</param>
/// <param name="After">Cells right of it, or below it.</param>
public sealed record Separator(bool Vertical, double Position, double Start, double End, int[] Before, int[] After);
