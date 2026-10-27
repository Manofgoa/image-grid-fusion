namespace ImageGridFusion.UI;

/// <summary>
/// What the index holds of a file, for its tile's tooltip: its caption, its tags concept by concept, the
/// beginning of the text read in it, the people its faces belong to — each null when the index holds none.
/// See workfiles/20261027-image-content-analysis.md § Tile Tooltip.
/// </summary>
internal sealed record TileIndex(string? Caption, string? Tags, string? Text, string? People)
{
    public bool IsEmpty => this.Caption is null && this.Tags is null && this.Text is null && this.People is null;
}

/// <summary>
/// The file explorer's tooltip, drawn by the app: its text as a plain tooltip shows it, then — for a file
/// whose index holds something — one line per kind of content, after an icon: 💡 the caption, 🏷 the tags,
/// 📝 the text read, 👨‍🦰 the people. The icons are Segoe UI Emoji glyphs drawn in a colour of their own,
/// as the tiles' light bulb is — GDI draws no full-colour emoji. The system tooltip's colours and border;
/// the lines wrapped at a maximum width. See workfiles/20261027-image-content-analysis.md § Tile Tooltip.
/// </summary>
internal sealed class IndexToolTip : IDisposable
{
    // The widest the tooltip grows, in logical pixels; its padding; the gap above the index's lines and between them.
    private const int MaxWidth = 420;
    private const int Padding = 5;
    private const int SectionGap = 6;
    private const int LineGap = 2;

    private const string FaceIcon = "\U0001F468‍\U0001F9B0";
    private const string FaceIconFallback = "\U0001F464";

    private static readonly (string Icon, Color Color)[] Icons =
    [
        ("\U0001F4A1", Color.FromArgb(214, 150, 0)),
        ("\U0001F3F7", Color.FromArgb(0, 120, 215)),
        ("\U0001F4DD", Color.FromArgb(16, 124, 16)),
        (FaceIcon, Color.FromArgb(202, 80, 16)),
    ];

    private const TextFormatFlags Wrapped = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    private readonly Control _owner;
    private readonly ToolTip _tip = new() { OwnerDraw = true };
    private readonly Font _font = SystemFonts.StatusFont ?? Control.DefaultFont;
    private readonly Font _iconFont;
    private string? _faceIcon;
    private TileIndex? _index;

    public IndexToolTip(Control owner)
    {
        this._owner = owner;
        this._iconFont = new Font("Segoe UI Emoji", this._font.SizeInPoints);
        this._tip.Popup += this.OnPopup;
        this._tip.Draw += this.OnDraw;
    }

    /// <summary>The tooltip of the owner: <paramref name="text"/>, then <paramref name="index"/>'s lines when it holds any; null text for none.</summary>
    public void Set(string? text, TileIndex? index = null)
    {
        this._index = index is { IsEmpty: false } ? index : null;
        this._tip.SetToolTip(this._owner, text);
    }

    public void Dispose()
    {
        this._tip.Dispose();
        this._iconFont.Dispose();
    }

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        e.ToolTipSize = this.Layout(this._tip.GetToolTip(this._owner) ?? "", null, Point.Empty);
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        e.DrawBackground();
        e.DrawBorder();
        this.Layout(e.ToolTipText ?? "", e.Graphics, e.Bounds.Location);
    }

    /// <summary>The tooltip's size — and, with <paramref name="graphics"/>, its drawing at <paramref name="origin"/>.</summary>
    private Size Layout(string text, Graphics? graphics, Point origin)
    {
        int padding = this._owner.LogicalToDeviceUnits(Padding);
        int maxWidth = this._owner.LogicalToDeviceUnits(MaxWidth);
        int x = origin.X + padding;
        int y = origin.Y + padding;
        int width = 0;

        var textSize = TextRenderer.MeasureText(text, this._font, new Size(maxWidth, int.MaxValue), Wrapped);
        if (graphics is not null)
        {
            TextRenderer.DrawText(graphics, text, this._font, new Rectangle(x, y, textSize.Width, textSize.Height), SystemColors.InfoText, Wrapped);
        }

        width = Math.Max(width, textSize.Width);
        y += textSize.Height;
        if (this._index is { } index)
        {
            y += this._owner.LogicalToDeviceUnits(SectionGap);
            string?[] lines = [index.Caption, index.Tags, index.Text, index.People];
            int iconWidth = TextRenderer.MeasureText("\U0001F4A1", this._iconFont, Size.Empty, TextFormatFlags.NoPadding).Width + padding;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] is not { } line)
                {
                    continue;
                }

                var lineSize = TextRenderer.MeasureText(line, this._font, new Size(maxWidth - iconWidth, int.MaxValue), Wrapped);
                if (graphics is not null)
                {
                    var (icon, color) = Icons[i];
                    string glyph = icon == FaceIcon ? this.FaceGlyph() : icon;
                    TextRenderer.DrawText(graphics, glyph, this._iconFont, new Rectangle(x, y, iconWidth, lineSize.Height), color, TextFormatFlags.NoPadding | TextFormatFlags.Top);
                    TextRenderer.DrawText(graphics, line, this._font, new Rectangle(x + iconWidth, y, lineSize.Width, lineSize.Height), SystemColors.InfoText, Wrapped);
                }

                width = Math.Max(width, iconWidth + lineSize.Width);
                y += lineSize.Height + this._owner.LogicalToDeviceUnits(LineGap);
            }
        }

        return new Size(width + 2 * padding, y - origin.Y + padding);
    }

    /// <summary>👨‍🦰 when the font draws it as one glyph, 👤 when it would draw the man and his red hair apart.</summary>
    private string FaceGlyph()
    {
        if (this._faceIcon is null)
        {
            int joined = TextRenderer.MeasureText(FaceIcon, this._iconFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            int single = TextRenderer.MeasureText("\U0001F468", this._iconFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            this._faceIcon = joined <= single * 1.4 ? FaceIcon : FaceIconFallback;
        }

        return this._faceIcon;
    }
}
