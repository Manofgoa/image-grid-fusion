using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace ImageGridFusion.Composition;

/// <summary>How a fill spreads its colors over its rectangle (workfiles/20260926-forced-background.md § Shared Fill).</summary>
public enum FillKind
{
    /// <summary>The first color alone.</summary>
    Solid,

    /// <summary>From the first color to the second, along an angle.</summary>
    Linear,

    /// <summary>From the first color at the center to the second at the corners.</summary>
    Radial,
}

/// <summary>Where a gradient's second color comes from.</summary>
public enum SecondColor
{
    /// <summary>Given by the fill's user: the Grid background's, the last cell's automatic color; a cell's, its own.</summary>
    Auto,

    /// <summary>The first color darkened.</summary>
    Darker,

    Black,

    White,

    /// <summary>A color picked with the eyedropper or Color….</summary>
    Chosen,
}

/// <summary>
/// The shared fill (workfiles/20260926-forced-background.md § Shared Fill): its kind and, for a gradient, its
/// angle and second color with its opacity. The first color, and its opacity, are its user's — the cell's
/// Background, the Grid background — given when it is painted. One painter for both, <see cref="Paint"/>, its
/// geometry relative to the rectangle it fills, so the preview and an export at another size look the same.
/// </summary>
public sealed record FillStyle
{
    /// <summary>How much of its brightness the first color keeps in <see cref="SecondColor.Darker"/>.</summary>
    public const double DarkerShare = 0.45;

    /// <summary>A cell's Background: solid; a gradient goes to the first color darkened by default.</summary>
    public static readonly FillStyle Cell = new();

    /// <summary>The Grid background: solid; a gradient goes to the last cell's automatic color.</summary>
    public static readonly FillStyle Grid = new() { Second = SecondColor.Auto };

    public FillKind Kind { get; private init; } = FillKind.Solid;

    /// <summary>The linear gradient's direction, clockwise in degrees: 0 from left to right, 90 from top to bottom.</summary>
    public int Angle { get; private init; } = 90;

    public SecondColor Second { get; private init; } = SecondColor.Darker;

    /// <summary>The second color picked, opaque; read in <see cref="SecondColor.Chosen"/> only.</summary>
    public Color SecondChosen { get; private init; } = Color.Black;

    /// <summary>The second color's opacity, from 0 to 1.</summary>
    public double SecondOpacity { get; private init; } = 1;

    public bool IsGradient => this.Kind != FillKind.Solid;

    public FillStyle WithKind(FillKind kind) => this with { Kind = kind };

    public FillStyle WithAngle(int angle) => this with { Angle = ((angle % 360) + 360) % 360 };

    public FillStyle WithSecond(SecondColor second) => this with { Second = second };

    /// <summary>The second color picked: <see cref="SecondColor.Chosen"/> from now on.</summary>
    public FillStyle WithSecondChosen(Color color) => this with { Second = SecondColor.Chosen, SecondChosen = Color.FromArgb(255, color) };

    public FillStyle WithSecondOpacity(double opacity) => this with { SecondOpacity = Math.Clamp(opacity, 0, 1) };

    /// <summary>
    /// The second color, opaque, for the opaque <paramref name="first"/>: <paramref name="automatic"/> in
    /// <see cref="SecondColor.Auto"/> — the first darkened when the user gives none.
    /// </summary>
    public Color SecondFor(Color first, Color? automatic) => this.Second switch
    {
        SecondColor.Auto when automatic is { } auto => Color.FromArgb(255, auto),
        SecondColor.Black => Color.Black,
        SecondColor.White => Color.White,
        SecondColor.Chosen => this.SecondChosen,
        _ => Darker(first),
    };

    /// <summary><paramref name="color"/>, opaque, at <see cref="DarkerShare"/> of its brightness.</summary>
    public static Color Darker(Color color) => Color.FromArgb(
        255,
        (int)Math.Round(color.R * DarkerShare),
        (int)Math.Round(color.G * DarkerShare),
        (int)Math.Round(color.B * DarkerShare));

    /// <summary>
    /// Fills <paramref name="area"/> of <paramref name="g"/>: <paramref name="first"/>, at its own alpha — and in
    /// a gradient the second color at <see cref="SecondOpacity"/>, <paramref name="automatic"/> standing for
    /// Auto. <paramref name="adjust"/>, when given, turns both colors (the cell's Colors effect).
    /// </summary>
    public void Paint(Graphics g, Rectangle area, Color first, Color? automatic, Func<Color, Color>? adjust = null)
    {
        if (area.Width < 1 || area.Height < 1)
        {
            return;
        }

        var second = Color.FromArgb((int)Math.Round(255 * this.SecondOpacity), this.SecondFor(Color.FromArgb(255, first), automatic));
        if (adjust is not null)
        {
            first = adjust(first);
            second = adjust(second);
        }

        if (!this.IsGradient)
        {
            using var solid = new SolidBrush(first);
            g.FillRectangle(solid, area);
            return;
        }

        using var brush = this.Brush(area, first, second);
        g.FillRectangle(brush, area);
    }

    /// <summary>
    /// The fill of a <paramref name="width"/> by <paramref name="height"/> rectangle, its colors made opaque, as
    /// ARGB pixels, row after row: what the Background's extended edges lay over, pixel by pixel. The caller
    /// returns the array to <see cref="System.Buffers.ArrayPool{T}.Shared"/>.
    /// </summary>
    internal int[] OpaquePixels(int width, int height, Color first, Color? automatic, Func<Color, Color>? adjust)
    {
        var opaque = Color.FromArgb(255, first);
        var second = this.SecondFor(opaque, automatic);
        if (adjust is not null)
        {
            opaque = Color.FromArgb(255, adjust(opaque));
            second = Color.FromArgb(255, adjust(second));
        }

        var pixels = System.Buffers.ArrayPool<int>.Shared.Rent(width * height);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        using (var brush = this.Brush(new Rectangle(0, 0, width, height), opaque, second))
        {
            g.FillRectangle(brush, 0, 0, width, height);
        }

        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width, width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }

    /// <summary>The gradient's brush over <paramref name="area"/>: the caller's to dispose.</summary>
    private Brush Brush(Rectangle area, Color first, Color second)
    {
        if (this.Kind == FillKind.Radial)
        {
            // The ellipse through the rectangle's corners, at its shape: the second color reaches every corner.
            float rx = (float)(area.Width / Math.Sqrt(2)), ry = (float)(area.Height / Math.Sqrt(2));
            var center = new PointF(area.X + area.Width / 2f, area.Y + area.Height / 2f);
            using var path = new GraphicsPath();
            path.AddEllipse(center.X - rx, center.Y - ry, 2 * rx, 2 * ry);
            return new PathGradientBrush(path)
            {
                CenterPoint = center,
                CenterColor = first,
                SurroundColors = [second],
            };
        }

        // One pixel wider than the area, mirrored beyond it: GDI+ would wrap the first row's color onto the last.
        var bounds = Rectangle.Inflate(area, 1, 1);
        return new LinearGradientBrush(bounds, first, second, this.Angle, isAngleScaleable: false) { WrapMode = WrapMode.TileFlipXY };
    }

    /// <summary>Its settings as an effect preset saves them, inside its user's (the cell's Background).</summary>
    internal JsonObject ToJson() => new()
    {
        ["kind"] = this.Kind.ToString(),
        ["angle"] = this.Angle,
        ["second"] = this.Second.ToString(),
        ["secondColor"] = this.Second == SecondColor.Chosen ? $"#{this.SecondChosen.R:X2}{this.SecondChosen.G:X2}{this.SecondChosen.B:X2}" : null,
        ["secondOpacity"] = this.SecondOpacity,
    };

    /// <summary>The settings <see cref="ToJson"/> wrote, from <paramref name="fallback"/>'s for a missing or foreign value.</summary>
    internal static FillStyle FromJson(JsonObject json, FillStyle fallback)
    {
        var style = fallback
            .WithKind(PresetJson.Enum(json, "kind", fallback.Kind))
            .WithAngle(PresetJson.Int(json, "angle", fallback.Angle))
            .WithSecond(PresetJson.Enum(json, "second", fallback.Second))
            .WithSecondOpacity(PresetJson.Double(json, "secondOpacity", fallback.SecondOpacity));
        if (style.Second == SecondColor.Chosen)
        {
            style = json["secondColor"] is JsonValue value && value.TryGetValue(out string? hex)
                && hex.StartsWith('#') && int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb)
                    ? style.WithSecondChosen(Color.FromArgb(rgb))
                    : style.WithSecond(fallback.Second == SecondColor.Chosen ? SecondColor.Darker : fallback.Second);
        }

        return style;
    }
}
