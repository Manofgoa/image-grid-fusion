using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Cutout tab's reach of a pick: Auto, the model's choice, then Part, Object and Whole, from the
/// smallest of the model's masks of a click to the largest — a tyre, the car's body, the car. The one in
/// use pressed; a click picks one, the one shown included. See workfiles/20261029-cutout.md § Reach.
/// </summary>
internal sealed class CutoutReachStrip : SelectionStrip<CutoutReach>
{
    public CutoutReachStrip()
        : base(CutoutReach.Auto)
    {
        this.FitSize();
    }

    protected override Size Box => EffectBox;

    protected override bool PicksSelected => true;

    protected override string Label(CutoutReach reach) => reach.ToString();

    protected override string Tip(CutoutReach reach) => reach switch
    {
        CutoutReach.Auto => "A pick keeps what the model finds likeliest under the click",
        CutoutReach.Part => "A pick keeps the smallest part under the click — a tyre",
        CutoutReach.Object => "A pick keeps a middle reach under the click — the car's body",
        _ => "A pick keeps the largest object under the click — the whole car",
    };

    protected override void PaintThumbnail(Graphics g, CutoutReach reach, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        int width = box.Width * 5 / 7;
        int height = box.Height * 7 / 10;
        var image = new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        this.Fill(g, SystemColors.Window, image);

        // Three nested shapes around the click, the one kept filled; Auto outlines them all.
        var center = new Point(image.X + image.Width / 2, image.Y + image.Height / 2);
        int[] radii = [image.Height / 7, image.Height * 2 / 7, image.Height * 3 / 7];
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int kept = reach switch
        {
            CutoutReach.Part => 0,
            CutoutReach.Object => 1,
            CutoutReach.Whole => 2,
            _ => -1,
        };
        if (kept >= 0)
        {
            using var brush = new SolidBrush(this.Shade(Marker));
            int r = radii[kept];
            g.FillEllipse(brush, center.X - r, center.Y - r, 2 * r, 2 * r);
        }

        using (var pen = new Pen(this.Shade(Color.FromArgb(90, 90, 90)), 1) { DashStyle = kept < 0 ? DashStyle.Dash : DashStyle.Solid })
        {
            foreach (int r in radii)
            {
                g.DrawEllipse(pen, center.X - r, center.Y - r, 2 * r, 2 * r);
            }
        }

        g.SmoothingMode = smoothing;
        this.Outline(g, image);
    }
}
