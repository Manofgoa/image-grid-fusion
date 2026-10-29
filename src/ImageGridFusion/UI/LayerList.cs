using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;

namespace ImageGridFusion.UI;

/// <summary>
/// The Layers options' list, as Paint.NET's layers panel (workfiles/20261029-layers.md § Layer List): one row
/// per layer, the top one first — its thumbnail over the squares of transparency, its name, and its
/// visibility checkbox at the right. Its selection is the selected layer: a click picks a layer, a click
/// on a checkbox shows or hides that layer, each told to the window, which owns them.
/// </summary>
internal sealed class LayerList : ListBox
{
    private const int RowHeight = 34;
    private const int ThumbnailWidth = 40;
    private const int ThumbnailHeight = 28;
    private const int Gap = 6;
    private const int CheckerSquare = 4;

    private bool _syncing;

    public LayerList()
    {
        this.DrawMode = DrawMode.OwnerDrawFixed;
        this.IntegralHeight = false;
        this.BorderStyle = BorderStyle.FixedSingle;
        this.ItemHeight = RowHeight;
    }

    /// <summary>A row: the layer's name, its thumbnail — the window's, kept by it — and whether it is visible.</summary>
    public sealed record Row(string Name, Bitmap? Thumbnail, bool Visible);

    /// <summary>Raised with the index of the layer — in the layers' own order, the bottom one 0 — whose row was clicked.</summary>
    public event EventHandler<int>? LayerPicked;

    /// <summary>Raised with the index of the layer whose visibility checkbox was clicked.</summary>
    public event EventHandler<int>? VisibilityClicked;

    /// <summary>
    /// Raised when a row is right-clicked, once released, with its layer's index and where, for its menu: the
    /// press picked it already (workfiles/20261029-duplicate-layer.md § The Context Menu).
    /// </summary>
    public event EventHandler<(int Layer, Point Location)>? MenuRequested;

    /// <summary>
    /// Shows <paramref name="rows"/>, given in the layers' order — the bottom one first — and drawn the top one
    /// first, the row of the layer <paramref name="selected"/> selected, none for -1.
    /// </summary>
    public void SetRows(IReadOnlyList<Row> rows, int selected)
    {
        this._syncing = true;
        this.BeginUpdate();
        this.Items.Clear();
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            this.Items.Add(rows[i]);
        }

        this.SelectedIndex = selected >= 0 && selected < rows.Count ? rows.Count - 1 - selected : -1;
        this.EndUpdate();
        this._syncing = false;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        this.ItemHeight = this.LogicalToDeviceUnits(RowHeight);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.ItemHeight = this.LogicalToDeviceUnits(RowHeight);
    }

    /// <summary>The selection follows the window's: the list's own change of selection is not a pick.</summary>
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (!this._syncing)
        {
            base.OnSelectedIndexChanged(e);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int row = this.IndexFromPoint(e.Location);
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right) || row < 0 || row >= this.Items.Count)
        {
            base.OnMouseDown(e);
            return;
        }

        int layer = this.Items.Count - 1 - row;
        if (e.Button == MouseButtons.Left && this.CheckBounds(this.GetItemRectangle(row)).Contains(e.Location))
        {
            this.VisibilityClicked?.Invoke(this, layer);
            return;
        }

        this.LayerPicked?.Invoke(this, layer);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int row = this.IndexFromPoint(e.Location);
        if (e.Button == MouseButtons.Right && row >= 0 && row < this.Items.Count)
        {
            this.MenuRequested?.Invoke(this, (this.Items.Count - 1 - row, e.Location));
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= this.Items.Count || this.Items[e.Index] is not Row row)
        {
            return;
        }

        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(selected ? Color.FromArgb(214, 226, 246) : this.BackColor))
        {
            g.FillRectangle(back, e.Bounds);
        }

        if (selected)
        {
            using var edge = new Pen(Color.FromArgb(70, 130, 220));
            g.DrawRectangle(edge, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }

        int gap = this.LogicalToDeviceUnits(Gap);
        var thumbnail = new Rectangle(
            e.Bounds.X + gap,
            e.Bounds.Y + (e.Bounds.Height - this.LogicalToDeviceUnits(ThumbnailHeight)) / 2,
            this.LogicalToDeviceUnits(ThumbnailWidth),
            this.LogicalToDeviceUnits(ThumbnailHeight));
        this.PaintThumbnail(g, thumbnail, row.Thumbnail);

        var check = this.CheckBounds(e.Bounds);
        CheckBoxRenderer.DrawCheckBox(g, check.Location, row.Visible ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);

        var text = Rectangle.FromLTRB(thumbnail.Right + gap, e.Bounds.Top, check.Left - gap, e.Bounds.Bottom);
        TextRenderer.DrawText(
            g,
            row.Name,
            this.Font,
            text,
            row.Visible ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>The visibility checkbox of a row, at its right, centered.</summary>
    private Rectangle CheckBounds(Rectangle row)
    {
        Size glyph;
        using (var g = this.CreateGraphics())
        {
            glyph = CheckBoxRenderer.GetGlyphSize(g, CheckBoxState.CheckedNormal);
        }

        int gap = this.LogicalToDeviceUnits(Gap);
        return new Rectangle(row.Right - gap - glyph.Width, row.Y + (row.Height - glyph.Height) / 2, glyph.Width, glyph.Height);
    }

    /// <summary>The thumbnail fitted whole into its box, over the grey and white squares, outlined.</summary>
    private void PaintThumbnail(Graphics g, Rectangle box, Bitmap? thumbnail)
    {
        int square = this.LogicalToDeviceUnits(CheckerSquare);
        using (var white = new SolidBrush(Color.White))
        using (var grey = new SolidBrush(Checkerboard.Grey))
        {
            g.FillRectangle(white, box);
            for (int y = 0; y < box.Height; y += square)
            {
                for (int x = (y / square) % 2 == 0 ? square : 0; x < box.Width; x += 2 * square)
                {
                    g.FillRectangle(grey, box.X + x, box.Y + y, Math.Min(square, box.Width - x), Math.Min(square, box.Height - y));
                }
            }
        }

        if (thumbnail is not null && thumbnail.Width > 0 && thumbnail.Height > 0)
        {
            double scale = Math.Min(box.Width / (double)thumbnail.Width, box.Height / (double)thumbnail.Height);
            int width = Math.Max(1, (int)(thumbnail.Width * scale)), height = Math.Max(1, (int)(thumbnail.Height * scale));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(thumbnail, new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height));
        }

        using var rim = new Pen(SystemColors.ControlDark);
        g.DrawRectangle(rim, box.X, box.Y, box.Width - 1, box.Height - 1);
    }
}
