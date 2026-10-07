using System.ComponentModel;

namespace ImageGridFusion.UI;

/// <summary>
/// The Rotate tab's quarter turns, 0°, 90°, 180° and 270°: the one the angle falls exactly on pressed,
/// none with a fine angle. A click on any one sets that rotation, the fine angle back to 0°.
/// </summary>
internal sealed class QuarterTurnStrip : ThumbnailStrip<int>
{
    private int? _rotation;

    public QuarterTurnStrip()
        : base([0, 90, 180, 270])
    {
        this.FitSize();
    }

    /// <summary>The quarter turn pressed, in degrees; null for none.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? Rotation
    {
        get => this._rotation;
        set
        {
            if (value != this._rotation)
            {
                this._rotation = value;
                this.Invalidate();
            }
        }
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(int degrees) => degrees == this._rotation;

    protected override string Label(int degrees) => $"{degrees}°";

    protected override string Tip(int degrees) => degrees == 0
        ? "The image as loaded, the fine angle back to 0°"
        : $"Turns the image {degrees}° clockwise, the fine angle back to 0°";

    protected override void PaintThumbnail(Graphics g, int degrees, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);

        // A landscape image, its marker top-left, turned clockwise: portrait on a quarter, the marker following.
        int length = box.Width * 4 / 7;
        int breadth = box.Height / 2;
        bool upright = degrees % 180 == 0;
        var size = upright ? new Size(length, breadth) : new Size(breadth, length);
        var image = new Rectangle(box.X + (box.Width - size.Width) / 2, box.Y + (box.Height - size.Height) / 2, size.Width, size.Height);
        var marker = degrees switch
        {
            0 => ContentAlignment.TopLeft,
            90 => ContentAlignment.TopRight,
            180 => ContentAlignment.BottomRight,
            _ => ContentAlignment.BottomLeft,
        };
        this.PaintImage(g, image, marker);
    }
}
