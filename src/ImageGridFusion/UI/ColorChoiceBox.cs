using System.Drawing.Drawing2D;

namespace ImageGridFusion.UI;

/// <summary>
/// A drop-down of color sources — a fill's From and To (workfiles/20261105-cell-background-auto-colors.md): each
/// choice drawn after the swatch of the color it gives now, then its name.
/// </summary>
internal sealed class ColorChoiceBox<T> : ComboBox
    where T : struct, Enum
{
    private readonly T[] _choices;
    private readonly Func<T, string> _textOf;
    private readonly Func<T, Color> _swatchOf;

    public ColorChoiceBox(IEnumerable<T> choices, Func<T, string> textOf, Func<T, Color> swatchOf)
    {
        this._choices = [.. choices];
        this._textOf = textOf;
        this._swatchOf = swatchOf;
        this.DropDownStyle = ComboBoxStyle.DropDownList;
        this.DrawMode = DrawMode.OwnerDrawFixed;
        this.Anchor = AnchorStyles.Left;
        this.Items.AddRange([.. this._choices.Select(choice => (object)textOf(choice))]);
        this.SelectionChangeCommitted += (_, _) =>
        {
            if (this.Choice is { } choice)
            {
                this.Picked?.Invoke(this, choice);
            }
        };
    }

    /// <summary>A choice picked by the user; never raised when <see cref="Choice"/> is set by the code.</summary>
    public event EventHandler<T>? Picked;

    /// <summary>The choice shown, <c>null</c> for none.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public T? Choice
    {
        get => this.SelectedIndex >= 0 ? this._choices[this.SelectedIndex] : null;
        set => this.SelectedIndex = value is { } choice ? Array.IndexOf(this._choices, choice) : -1;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0)
        {
            return;
        }

        var choice = this._choices[e.Index];
        bool disabled = (e.State & DrawItemState.Disabled) != 0 || !this.Enabled;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        int size = this.LogicalToDeviceUnits(14), gap = this.LogicalToDeviceUnits(3);
        var corner = new Size(this.LogicalToDeviceUnits(5), this.LogicalToDeviceUnits(5));
        var swatch = new Rectangle(e.Bounds.X + gap, e.Bounds.Y + (e.Bounds.Height - size) / 2, size - 1, size - 1);
        var g = e.Graphics;
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(Color.FromArgb(disabled ? 96 : 255, this._swatchOf(choice))))
        {
            g.FillRoundedRectangle(fill, swatch, corner);
        }

        g.DrawRoundedRectangle(SystemPens.ControlDark, swatch, corner);
        g.SmoothingMode = smoothing;
        int x = swatch.Right + 1 + gap;
        var color = disabled ? SystemColors.GrayText : selected ? SystemColors.HighlightText : e.ForeColor;
        TextRenderer.DrawText(g, this._textOf(choice), e.Font ?? this.Font, new Rectangle(x, e.Bounds.Y, Math.Max(0, e.Bounds.Right - x), e.Bounds.Height), color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}
