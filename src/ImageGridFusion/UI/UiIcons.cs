using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// One menu entry's icon (workfiles/20261029-menu-icons.md): drawn at the size asked for, kept per size.
/// </summary>
internal sealed class UiIcon
{
    private readonly Func<int, Bitmap> _draw;
    private readonly Dictionary<int, Bitmap> _drawn = [];

    private UiIcon(Func<int, Bitmap> draw) => this._draw = draw;

    /// <summary>The icon at <paramref name="size"/> device pixels, a copy the caller owns.</summary>
    public Bitmap Copy(int size) => new(this.At(size));

    /// <summary>A Segoe UI Emoji glyph in its own colors, which only Direct2D draws (RULES.md § Layers).</summary>
    public static UiIcon Emoji(string glyph) => new(size => DrawEmoji(glyph, size));

    /// <summary>An icon the app already draws for that meaning.</summary>
    public static UiIcon Drawn(Func<int, Bitmap> draw) => new(draw);

    /// <summary>The icon at <paramref name="size"/> device pixels, shared: the caller copies it.</summary>
    public Bitmap At(int size)
    {
        if (!this._drawn.TryGetValue(size, out var bitmap))
        {
            bitmap = this._draw(size);
            this._drawn[size] = bitmap;
        }

        return bitmap;
    }

    private static readonly LayerTextStyle EmojiStyle = new("Segoe UI Emoji", false, Color.Black, false, Color.Black, 0, false, Color.Black, 0);

    /// <summary>The glyph drawn large by <see cref="TextRaster"/>, its ink fitted into a square of <paramref name="size"/>.</summary>
    private static Bitmap DrawEmoji(string glyph, int size)
    {
        var (large, _) = TextRaster.Render(glyph, EmojiStyle);
        using (large)
        {
            var ink = InkBounds(large);
            var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            float scale = size / (float)Math.Max(ink.Width, ink.Height);
            float w = ink.Width * scale, h = ink.Height * scale;
            g.DrawImage(large, new RectangleF((size - w) / 2, (size - h) / 2, w, h), ink, GraphicsUnit.Pixel);
            return bitmap;
        }
    }

    /// <summary>The smallest rectangle holding every pixel of <paramref name="bitmap"/> that is not transparent.</summary>
    private static Rectangle InkBounds(Bitmap bitmap)
    {
        var whole = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(whole, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int left = whole.Width, top = whole.Height, right = -1, bottom = -1;
            var row = new int[whole.Width];
            for (int y = 0; y < whole.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < row.Length; x++)
                {
                    if ((row[x] >>> 24) > 8)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            return right < 0 ? whole : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

/// <summary>
/// The icons of the menus' entries, the tabs, the buttons and the drop-downs (workfiles/20261029-menu-icons.md,
/// RULES.md § Icons): one per action, shared by every control showing it — an emoji in full color, the app's
/// drawing kept where no emoji says the thing. A menu entry gets its icon with <see cref="Set"/>, drawn at the
/// menu's size in device pixels each time a menu given to <see cref="Attach"/> opens; a button with
/// <see cref="SetImage"/>, a drop-down's entry with <see cref="DrawItem"/> — so they stay crisp at any DPI.
/// </summary>
internal static class UiIcons
{
    // An icon's side in logical pixels, Windows' own.
    public const int LogicalSize = 16;

    public static readonly UiIcon StartWithWindows = UiIcon.Emoji("\U0001F680");
    public static readonly UiIcon TwitterCorners = UiIcon.Drawn(EffectIcons.Borders);
    public static readonly UiIcon Video = UiIcon.Emoji("\U0001F3AC");
    public static readonly UiIcon Guides = UiIcon.Emoji("\U0001F4CF");
    public static readonly UiIcon GapBars = UiIcon.Emoji("\U0001F4D0");
    public static readonly UiIcon Folder = UiIcon.Emoji("\U0001F4C1");
    public static readonly UiIcon Pages = UiIcon.Emoji("\U0001F4C4");
    public static readonly UiIcon Download = UiIcon.Emoji("⬇️");
    public static readonly UiIcon TextRead = UiIcon.Emoji("\U0001F4DD");
    public static readonly UiIcon Rebuild = UiIcon.Emoji("\U0001F504");
    public static readonly UiIcon OcrResultStyle = UiIcon.Emoji("↪️");
    public static readonly UiIcon CurvedArrow = UiIcon.Emoji("⤴️");
    public static readonly UiIcon Thickness = UiIcon.Emoji("➖");
    public static readonly UiIcon People = UiIcon.Emoji("\U0001F468‍\U0001F9B0");
    public static readonly UiIcon Group = UiIcon.Emoji("\U0001F465");
    public static readonly UiIcon Forget = UiIcon.Emoji("\U0001F5D1️");
    public static readonly UiIcon Tags = UiIcon.Emoji("\U0001F3F7️");
    public static readonly UiIcon Strictness = UiIcon.Emoji("\U0001F39A️");
    public static readonly UiIcon Caption = UiIcon.Emoji("\U0001F4A1");
    public static readonly UiIcon OpenFolder = UiIcon.Emoji("\U0001F4C2");
    public static readonly UiIcon Gif = UiIcon.Emoji("\U0001F39E️");
    public static readonly UiIcon Share = UiIcon.Emoji("\U0001F4E4");
    public static readonly UiIcon Png = UiIcon.Emoji("\U0001F5BC️");
    public static readonly UiIcon LastVideo = UiIcon.Emoji("\U0001F558");
    public static readonly UiIcon FindSimilar = UiIcon.Emoji("\U0001F50D");
    public static readonly UiIcon Rename = UiIcon.Emoji("✏️");
    public static readonly UiIcon Hide = UiIcon.Emoji("\U0001F648");
    public static readonly UiIcon Unhide = UiIcon.Emoji("\U0001F441️");
    public static readonly UiIcon Unmerge = UiIcon.Emoji("✂️");
    public static readonly UiIcon NotThisPerson = UiIcon.Emoji("\U0001F6AB");
    public static readonly UiIcon Copy = UiIcon.Emoji("\U0001F4CB");
    public static readonly UiIcon CopyPath = UiIcon.Emoji("\U0001F517");
    public static readonly UiIcon ReplaceStyle = UiIcon.Emoji("\U0001F4E5");
    public static readonly UiIcon Quit = UiIcon.Emoji("\U0001F6AA");
    public static readonly UiIcon DuplicateLayer = UiIcon.Drawn(size => EffectIcons.Layer(size, EffectIcons.LayerAction.Duplicate));
    public static readonly UiIcon DeleteLayer = UiIcon.Emoji("❌");

    // The tabs (Iteration 8): an emoji where one says the effect, its drawing kept otherwise.
    public static readonly UiIcon Background = UiIcon.Emoji("\U0001F5BC️");
    public static readonly UiIcon Cutout = UiIcon.Emoji("✂️");
    public static readonly UiIcon Zoom = UiIcon.Emoji("\U0001F50D");
    public static readonly UiIcon Animations = UiIcon.Emoji("✨");
    public static readonly UiIcon Rotate = UiIcon.Emoji("\U0001F503");
    public static readonly UiIcon Flip = UiIcon.Emoji("\U0001FA9E");
    public static readonly UiIcon Frames = UiIcon.Emoji("\U0001F39E️");
    public static readonly UiIcon Colors = UiIcon.Emoji("\U0001F3A8");
    public static readonly UiIcon Blur = UiIcon.Emoji("\U0001F4A7");
    public static readonly UiIcon Volume = UiIcon.Emoji("\U0001F50A");
    public static readonly UiIcon Soundtrack = UiIcon.Emoji("\U0001F3B5");
    public static readonly UiIcon Cascade = UiIcon.Emoji("⏭️");
    public static readonly UiIcon Layers = UiIcon.Emoji("\U0001F4D1");
    public static readonly UiIcon Preset = UiIcon.Emoji("⭐");
    public static readonly UiIcon Reset = UiIcon.Emoji("↩️");

    // The layer list's buttons.
    public static readonly UiIcon AddImage = UiIcon.Emoji("\U0001F5BC️");
    public static readonly UiIcon AddText = UiIcon.Emoji("\U0001F524");
    public static readonly UiIcon CopyLayer = UiIcon.Emoji("\U0001F4C4");
    public static readonly UiIcon Edit = UiIcon.Emoji("✏️");
    public static readonly UiIcon Done = UiIcon.Emoji("✅");
    public static readonly UiIcon ToBack = UiIcon.Emoji("⏬");
    public static readonly UiIcon Lower = UiIcon.Emoji("\U0001F53D");
    public static readonly UiIcon Raise = UiIcon.Emoji("\U0001F53C");
    public static readonly UiIcon ToFront = UiIcon.Emoji("⏫");

    // The buttons of the bottom bar, the transport and the file explorer.
    public static readonly UiIcon ClearAll = UiIcon.Emoji("\U0001F9F9");
    public static readonly UiIcon Settings = UiIcon.Emoji("⚙️");
    public static readonly UiIcon Save = UiIcon.Emoji("\U0001F4BE");
    public static readonly UiIcon Play = UiIcon.Emoji("▶️");
    public static readonly UiIcon Pause = UiIcon.Emoji("⏸️");
    public static readonly UiIcon ByName = UiIcon.Emoji("\U0001F520");
    public static readonly UiIcon Rescan = UiIcon.Emoji("\U0001F504");
    public static readonly UiIcon Collapse = UiIcon.Emoji("⏩");
    public static readonly UiIcon Expand = UiIcon.Emoji("⏪");
    public static readonly UiIcon Clear = UiIcon.Emoji("✖️");
    public static readonly UiIcon Thumbnails = UiIcon.Emoji("\U0001F533");
    public static readonly UiIcon Details = UiIcon.Emoji("\U0001F4C3");

    // The drop-downs' entries.
    public static readonly UiIcon Favorites = UiIcon.Emoji("❤️");
    public static readonly UiIcon Date = UiIcon.Emoji("\U0001F4C5");
    public static readonly UiIcon FileSize = UiIcon.Emoji("\U0001F4E6");

    /// <summary>The app's own icon, at the size's frame.</summary>
    public static readonly UiIcon App = UiIcon.Drawn(size =>
    {
        using var icon = AppIcon.Load();
        using var sized = new Icon(icon, size, size);
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawIcon(sized, new Rectangle(0, 0, size, size));
        return bitmap;
    });

    /// <summary>
    /// Shows <paramref name="icon"/> on <paramref name="button"/> at <paramref name="size"/> device pixels, before its
    /// text when it has one — its own copy, the previous image disposed. A button sized to its content showing its
    /// icon alone stretches to its row's height (Iteration 12).
    /// </summary>
    public static void SetImage(ButtonBase button, UiIcon icon, int size)
    {
        var previous = button.Image;
        button.Image = icon.Copy(size);
        button.ImageAlign = string.IsNullOrEmpty(button.Text) ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
        button.TextImageRelation = string.IsNullOrEmpty(button.Text) ? TextImageRelation.Overlay : TextImageRelation.ImageBeforeText;
        previous?.Dispose();
        if (button.AutoSize && string.IsNullOrEmpty(button.Text))
        {
            // As tall as its row, as the split buttons' arrows were: its neighbours' text sets the height.
            button.Anchor |= AnchorStyles.Top | AnchorStyles.Bottom;
        }
    }

    /// <summary>
    /// An owner-drawn drop-down's entry: <paramref name="icon"/> then <paramref name="text"/>, the icon at the control's
    /// DPI, greyed while it is disabled.
    /// </summary>
    public static void DrawItem(ComboBox box, DrawItemEventArgs e, UiIcon icon, string text)
    {
        e.DrawBackground();
        bool disabled = (e.State & DrawItemState.Disabled) != 0 || !box.Enabled;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var textColor = disabled ? SystemColors.GrayText : selected ? SystemColors.HighlightText : e.ForeColor;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        int size = box.LogicalToDeviceUnits(LogicalSize), gap = box.LogicalToDeviceUnits(3);
        var bounds = e.Bounds;
        int x = bounds.X + gap;
        DrawIcon(e.Graphics, icon.At(size), new Point(x, bounds.Y + (bounds.Height - size) / 2), disabled);
        x += size + gap;
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? box.Font, new Rectangle(x, bounds.Y, Math.Max(0, bounds.Right - x), bounds.Height), textColor, flags);

        e.DrawFocusRectangle();
    }

    private static void DrawIcon(Graphics g, Image icon, Point at, bool disabled)
    {
        if (disabled)
        {
            ControlPaint.DrawImageDisabled(g, icon, at.X, at.Y, Color.Transparent);
        }
        else
        {
            g.DrawImage(icon, new Rectangle(at, icon.Size));
        }
    }

    // Each entry's icon, and the size its image was drawn at.
    private static readonly ConditionalWeakTable<ToolStripItem, Shown> Entries = new();

    private sealed class Shown(UiIcon icon)
    {
        public UiIcon Icon { get; } = icon;

        public int Size { get; set; }
    }

    /// <summary>Gives <paramref name="item"/> its icon, drawn when its menu opens.</summary>
    public static void Set(ToolStripItem item, UiIcon icon)
    {
        var shown = new Shown(icon);
        Entries.AddOrUpdate(item, shown);
        item.ImageScaling = ToolStripItemImageScaling.None;
        item.Disposed += (_, _) =>
        {
            if (shown.Size != 0)
            {
                item.Image?.Dispose();
            }
        };
    }

    /// <summary>Draws the icons of <paramref name="menu"/>'s entries, its submenus' included, as it opens.</summary>
    public static void Attach(ToolStripDropDown menu) => menu.Opening += (_, _) => Apply(menu.Items, SizeFor(menu));

    // The size in device pixels on the screen the menu opens on: its source control's, else its own.
    private static int SizeFor(ToolStripDropDown menu)
    {
        int dpi = (menu as ContextMenuStrip)?.SourceControl?.DeviceDpi ?? menu.DeviceDpi;
        return (int)Math.Round(LogicalSize * dpi / 96.0);
    }

    private static void Apply(ToolStripItemCollection items, int size)
    {
        foreach (ToolStripItem item in items)
        {
            if (Entries.TryGetValue(item, out var shown) && shown.Size != size)
            {
                // A copy per entry: an entry disposed may take its image with it.
                DropImage(item);
                item.Image = shown.Icon.Copy(size);
                shown.Size = size;
            }

            if (item is ToolStripDropDownItem { HasDropDownItems: true } parent)
            {
                Apply(parent.DropDownItems, size);
            }
        }
    }

    private static void DropImage(ToolStripItem item)
    {
        if (Entries.TryGetValue(item, out var shown) && shown.Size != 0)
        {
            var image = item.Image;
            item.Image = null;
            image?.Dispose();
            shown.Size = 0;
        }
    }
}
