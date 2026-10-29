using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// One menu entry's icon (workfiles/20261029-menu-icons.md): drawn at the size asked for, kept per size.
/// </summary>
internal sealed class MenuIcon
{
    private readonly Func<int, Bitmap> _draw;
    private readonly Dictionary<int, Bitmap> _drawn = [];

    private MenuIcon(Func<int, Bitmap> draw) => this._draw = draw;

    /// <summary>A Segoe UI Emoji glyph in its own colors, which only Direct2D draws (RULES.md § Layers).</summary>
    public static MenuIcon Emoji(string glyph) => new(size => DrawEmoji(glyph, size));

    /// <summary>An icon the app already draws for that meaning.</summary>
    public static MenuIcon Drawn(Func<int, Bitmap> draw) => new(draw);

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
/// The icons of the menus' entries (workfiles/20261029-menu-icons.md, RULES.md § Menus): one per action,
/// shared by every menu showing it — the icon the app already shows for that meaning, else an emoji in
/// full color. Set on an entry with <see cref="Set"/>, drawn at the menu's size in device pixels each time a
/// menu given to <see cref="Attach"/> opens, so they stay crisp at any DPI.
/// </summary>
internal static class MenuIcons
{
    // A menu icon's side in logical pixels, Windows' own.
    private const int LogicalSize = 16;

    public static readonly MenuIcon StartWithWindows = MenuIcon.Emoji("\U0001F680");
    public static readonly MenuIcon TwitterCorners = MenuIcon.Drawn(EffectIcons.Borders);
    public static readonly MenuIcon Video = MenuIcon.Emoji("\U0001F3AC");
    public static readonly MenuIcon Guides = MenuIcon.Emoji("\U0001F4CF");
    public static readonly MenuIcon GapBars = MenuIcon.Emoji("\U0001F4D0");
    public static readonly MenuIcon Folder = MenuIcon.Emoji("\U0001F4C1");
    public static readonly MenuIcon Pages = MenuIcon.Emoji("\U0001F4C4");
    public static readonly MenuIcon Download = MenuIcon.Emoji("⬇️");
    public static readonly MenuIcon TextRead = MenuIcon.Emoji("\U0001F4DD");
    public static readonly MenuIcon Rebuild = MenuIcon.Emoji("\U0001F504");
    public static readonly MenuIcon OcrResultStyle = MenuIcon.Emoji("↪️");
    public static readonly MenuIcon CurvedArrow = MenuIcon.Emoji("⤴️");
    public static readonly MenuIcon Thickness = MenuIcon.Emoji("➖");
    public static readonly MenuIcon People = MenuIcon.Emoji("\U0001F468‍\U0001F9B0");
    public static readonly MenuIcon Group = MenuIcon.Emoji("\U0001F465");
    public static readonly MenuIcon Forget = MenuIcon.Emoji("\U0001F5D1️");
    public static readonly MenuIcon Tags = MenuIcon.Emoji("\U0001F3F7️");
    public static readonly MenuIcon Strictness = MenuIcon.Emoji("\U0001F39A️");
    public static readonly MenuIcon Caption = MenuIcon.Emoji("\U0001F4A1");
    public static readonly MenuIcon OpenFolder = MenuIcon.Emoji("\U0001F4C2");
    public static readonly MenuIcon Gif = MenuIcon.Emoji("\U0001F39E️");
    public static readonly MenuIcon Share = MenuIcon.Emoji("\U0001F4E4");
    public static readonly MenuIcon Png = MenuIcon.Emoji("\U0001F5BC️");
    public static readonly MenuIcon LastVideo = MenuIcon.Emoji("\U0001F558");
    public static readonly MenuIcon FindSimilar = MenuIcon.Emoji("\U0001F50D");
    public static readonly MenuIcon Rename = MenuIcon.Emoji("✏️");
    public static readonly MenuIcon Hide = MenuIcon.Emoji("\U0001F648");
    public static readonly MenuIcon Unhide = MenuIcon.Emoji("\U0001F441️");
    public static readonly MenuIcon Unmerge = MenuIcon.Emoji("✂️");
    public static readonly MenuIcon NotThisPerson = MenuIcon.Emoji("\U0001F6AB");
    public static readonly MenuIcon Copy = MenuIcon.Emoji("\U0001F4CB");
    public static readonly MenuIcon CopyPath = MenuIcon.Emoji("\U0001F517");
    public static readonly MenuIcon ReplaceStyle = MenuIcon.Emoji("\U0001F4E5");
    public static readonly MenuIcon Quit = MenuIcon.Emoji("\U0001F6AA");
    public static readonly MenuIcon DuplicateLayer = MenuIcon.Drawn(size => EffectIcons.Layer(size, EffectIcons.LayerAction.Duplicate));
    public static readonly MenuIcon DeleteLayer = MenuIcon.Drawn(size => EffectIcons.Layer(size, EffectIcons.LayerAction.Delete));

    /// <summary>The app's own icon, at the size's frame.</summary>
    public static readonly MenuIcon App = MenuIcon.Drawn(size =>
    {
        using var icon = AppIcon.Load();
        using var sized = new Icon(icon, size, size);
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawIcon(sized, new Rectangle(0, 0, size, size));
        return bitmap;
    });

    // Each entry's icon, and the size its image was drawn at.
    private static readonly ConditionalWeakTable<ToolStripItem, Shown> Entries = new();

    private sealed class Shown(MenuIcon icon)
    {
        public MenuIcon Icon { get; } = icon;

        public int Size { get; set; }
    }

    /// <summary>Gives <paramref name="item"/> its icon, drawn when its menu opens.</summary>
    public static void Set(ToolStripItem item, MenuIcon icon)
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
                item.Image = new Bitmap(shown.Icon.At(size));
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
