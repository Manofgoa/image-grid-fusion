using System.Drawing.Drawing2D;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Grid background image's thumbnail in its options (workfiles/20261104-grid-background-image.md § Options): the
/// image as drawn, its look applied, or an empty drop zone; a double-click opens the background edit view.
/// </summary>
internal sealed class BackgroundImageBox : Control
{
    private BackgroundImage? _image;

    public BackgroundImageBox()
    {
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        this.SetStyle(ControlStyles.StandardDoubleClick, true);
        this.Cursor = Cursors.Hand;
    }

    /// <summary>The image shown; <c>null</c> for the empty drop zone.</summary>
    public void Show(BackgroundImage? image)
    {
        if (image != this._image)
        {
            this._image = image;
            this.Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(this.Parent?.BackColor ?? SystemColors.Control);
        var bounds = new Rectangle(0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
        if (this._image is { Image.IsDisposed: false } background && background.Shown is { Width: > 0, Height: > 0 } shown)
        {
            // The image whole, its look applied: a box of its own shape, so Fill and Contain draw it alike.
            double scale = Math.Min(bounds.Width / (double)shown.Width, bounds.Height / (double)shown.Height);
            var size = new Size(Math.Max(1, (int)(shown.Width * scale)), Math.Max(1, (int)(shown.Height * scale)));
            var cell = new Rectangle(bounds.X + (bounds.Width - size.Width) / 2, bounds.Y + (bounds.Height - size.Height) / 2, size.Width, size.Height);
            var look = background.Look.WithZoom(1).WithFocus(new PointF(0.5f, 0.5f));
            var image = background.Image;
            Compositor.DrawCell(g, new Frame(image.ShownBitmap, image.BandColor, look, TimeSpan.Zero, image.Sharpening, image.Sharpens), cell);
            g.DrawRectangle(this.Enabled ? SystemPens.ControlDarkDark : SystemPens.ControlDark, cell.X, cell.Y, cell.Width - 1, cell.Height - 1);
            return;
        }

        using var pen = new Pen(this.Enabled ? SystemColors.ControlDarkDark : SystemColors.ControlDark) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, bounds);
        TextRenderer.DrawText(
            g,
            "Drop an image here",
            this.Font,
            bounds,
            this.Enabled ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        this.Invalidate();
    }
}
