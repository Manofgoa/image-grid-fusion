using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The eyedropper over the cells: a color option armed it, the grid is held and read from a snapshot of
/// the cells as composed, a loupe follows the cursor, a click takes the color
/// (workfiles/20261021-color-eyedropper.md § Eyedropper Gesture).
/// </summary>
internal sealed partial class GridPreview
{
    /// <summary>The smallest and largest side of the square of preview pixels the eyedropper averages.</summary>
    public const int MinEyedropperSample = 1;
    public const int MaxEyedropperSample = 5;

    // The loupe: so many snapshot pixels a side, each drawn so many logical pixels wide.
    private const int LoupePixels = 15;
    private const int LoupeZoom = 8;
    private const int LoupeOffset = 24;
    private const int LoupeCaption = 22;

    // How often, at most, the color under the cursor is applied to the option: each application draws the cell again.
    private const int EyedropperPreviewInterval = 33;

    private Eyedropper? _eyedropper;
    private EyedropperFilter? _eyedropperFilter;
    private System.Windows.Forms.Timer? _eyedropperPreview;
    private static readonly Dictionary<int, Cursor> EyedropperCursors = [];

    /// <summary>Whether the eyedropper is armed: it owns the preview's mouse until a pick or a cancel.</summary>
    public bool EyedropperArmed => this._eyedropper is not null;

    /// <summary>
    /// Arms the eyedropper: the grid held, the cells as composed — the exports' drawing, at the preview's size,
    /// without what the preview alone draws — kept as the snapshot it reads. <paramref name="hovered"/> is told
    /// each new color under the cursor, null where nothing is read; <paramref name="picked"/> the color a left
    /// click takes, <paramref name="cancelled"/> a cancel; either ends it, the grid released first.
    /// </summary>
    public void ArmEyedropper(int sample, Action<Color?> hovered, Action<Color> picked, Action cancelled)
    {
        this.CancelEyedropper();
        var canvas = this.CanvasBounds();
        if (canvas.IsEmpty || this._locked)
        {
            cancelled();
            return;
        }

        this._player.Hold();
        var snapshot = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        if (this._images.Count > 0)
        {
            using (var g = Graphics.FromImage(snapshot))
            {
                Compositor.Draw(g, this._images, this._layout!, canvas.Size, this._borders, this._player.GridTime, this._seams, layers: this.ShownLayers);
            }

            this._borders?.CutCorners(snapshot);
        }

        this._eyedropper = new Eyedropper(snapshot, Pixels(snapshot), canvas.Location, Math.Clamp(sample, MinEyedropperSample, MaxEyedropperSample), hovered, picked, cancelled);
        this._eyedropperFilter ??= new EyedropperFilter(this);
        this._eyedropperFilter.Install();

        // The cell's buttons and outlines hidden: the eyedropper owns the preview's mouse.
        this._hovered = -1;
        this._hoveredBar = null;
        this._hoveredCorner = null;
        this._hoveredSeparator = null;
        this._hoveringClose = false;
        this._hoveringHandle = false;
        this._hoveringCanvas = false;
        this._hoveringDropZone = false;
        this._hoveringKept = false;
        this.SetSourceHover((false, false));
        var cursor = this.PointToClient(MousePosition);
        if (this.ClientRectangle.Contains(cursor))
        {
            this.HoverEyedropper(cursor);
        }

        this.Invalidate();
    }

    /// <summary>Cancels the eyedropper if armed: the option is given back its color by its owner.</summary>
    public void CancelEyedropper()
    {
        if (this.EndEyedropper() is { } ended)
        {
            ended.Cancelled();
        }
    }

    /// <summary>A left click at <paramref name="location"/>: takes the color there, if one is read.</summary>
    private void PickEyedropper(Point location)
    {
        if (this._eyedropper?.ColorAt(location) is { } color && this.EndEyedropper() is { } ended)
        {
            ended.Picked(color);
        }
    }

    /// <summary>Disarms the eyedropper, the grid released and the preview's own hover back; the one that was armed.</summary>
    private Eyedropper? EndEyedropper()
    {
        if (this._eyedropper is not { } ended)
        {
            return null;
        }

        this._eyedropper = null;
        this._eyedropperPreview?.Stop();
        ended.Snapshot.Dispose();
        this._player.Release();

        // Drawn fast meanwhile: the grid drawn again in full.
        this._cache?.Dispose();
        this._cache = null;
        var cursor = this.PointToClient(MousePosition);
        if (this.ClientRectangle.Contains(cursor))
        {
            this.UpdateHover(cursor);
        }
        else
        {
            this.Cursor = Cursors.Default;
        }

        this.Invalidate();
        return ended;
    }

    /// <summary>
    /// The cursor moved over the preview: the loupe follows it at once, only its area painted again; the
    /// option is told the color read there at most every <see cref="EyedropperPreviewInterval"/> ms, the
    /// last one winning — the mouse moves far more often than a cell can be drawn.
    /// </summary>
    private void HoverEyedropper(Point? location)
    {
        var eyedropper = this._eyedropper!;
        this.InvalidateLoupe(eyedropper.At);
        eyedropper.At = location;
        eyedropper.Hovered = location is { } at ? eyedropper.ColorAt(at) : null;
        this.Cursor = eyedropper.Hovered is null && location is not null ? Cursors.No : this.EyedropperCursor();
        this.InvalidateLoupe(location);
        if (eyedropper.Hovered != eyedropper.Applied && this._eyedropperPreview?.Enabled != true)
        {
            this.ApplyHovered();
            this._eyedropperPreview ??= this.NewPreviewTimer();
            this._eyedropperPreview.Start();
        }
    }

    /// <summary>The option given the color under the cursor, if it is not the one it shows.</summary>
    private void ApplyHovered()
    {
        if (this._eyedropper is { } eyedropper && eyedropper.Hovered != eyedropper.Applied)
        {
            eyedropper.Applied = eyedropper.Hovered;
            eyedropper.HoveredChanged(eyedropper.Hovered);
        }
    }

    /// <summary>Applies the color the cursor reached meanwhile, then waits for the next one.</summary>
    private System.Windows.Forms.Timer NewPreviewTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = EyedropperPreviewInterval };
        timer.Tick += (_, _) =>
        {
            if (this._eyedropper is { } eyedropper && eyedropper.Hovered != eyedropper.Applied)
            {
                this.ApplyHovered();
            }
            else
            {
                timer.Stop();
            }
        };
        return timer;
    }

    /// <summary>Paints again where the loupe stands for a cursor at <paramref name="location"/>.</summary>
    private void InvalidateLoupe(Point? location)
    {
        if (location is { } at)
        {
            var bounds = this.LoupeBounds(at);
            bounds.Inflate(2, 2);
            this.Invalidate(bounds);
        }
    }

    /// <summary>
    /// The loupe's view and its caption below, for a cursor at <paramref name="at"/>: below right of it,
    /// flipped to the other side near an edge, kept inside the preview.
    /// </summary>
    private (Rectangle View, Rectangle Caption) LoupeParts(Point at)
    {
        int side = LoupePixels * this.LogicalToDeviceUnits(LoupeZoom);
        int caption = this.LogicalToDeviceUnits(LoupeCaption);
        int offset = this.LogicalToDeviceUnits(LoupeOffset);
        int x = at.X + offset + side > this.ClientSize.Width ? at.X - offset - side : at.X + offset;
        int y = at.Y + offset + side + caption > this.ClientSize.Height ? at.Y - offset - side - caption : at.Y + offset;
        var view = new Rectangle(Math.Max(0, x), Math.Max(0, y), side, side);
        return (view, new Rectangle(view.X, view.Bottom, side, caption));
    }

    private Rectangle LoupeBounds(Point at) => Rectangle.Union(this.LoupeParts(at).View, this.LoupeParts(at).Caption);

    /// <summary>
    /// The loupe beside the cursor: the snapshot's pixels around it enlarged, the sample square outlined in
    /// the helpers' green, the color read and its hex value below — <c>—</c> where nothing is read.
    /// </summary>
    private void PaintEyedropper(Graphics g)
    {
        if (this._eyedropper is not { At: { } at } eyedropper)
        {
            return;
        }

        int zoom = this.LogicalToDeviceUnits(LoupeZoom);
        var (view, label) = this.LoupeParts(at);
        int side = view.Width;
        int caption = label.Height;

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.None;
        using (var checker = new HatchBrush(HatchStyle.LargeCheckerBoard, Checkerboard.Grey, Color.White))
        {
            g.FillRectangle(checker, view);
        }

        // The pixel under the cursor in the middle of the loupe.
        var center = eyedropper.SnapshotPoint(at);
        var source = new Rectangle(center.X - LoupePixels / 2, center.Y - LoupePixels / 2, LoupePixels, LoupePixels);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SetClip(view);
        g.DrawImage(eyedropper.Snapshot, view, source, GraphicsUnit.Pixel);
        g.ResetClip();
        g.PixelOffsetMode = PixelOffsetMode.Default;

        int first = LoupePixels / 2 - (eyedropper.Sample - 1) / 2;
        var square = new Rectangle(view.X + first * zoom, view.Y + first * zoom, eyedropper.Sample * zoom, eyedropper.Sample * zoom);
        using (var halo = new Pen(HelperHalo, this.LogicalToDeviceUnits(4)))
        {
            g.DrawRectangle(halo, square);
        }

        using (var helper = new Pen(HelperColor, this.LogicalToDeviceUnits(2)))
        {
            g.DrawRectangle(helper, square);
        }

        using (var back = new SolidBrush(Color.FromArgb(40, 40, 40)))
        {
            g.FillRectangle(back, label);
        }

        int swatch = caption - this.LogicalToDeviceUnits(8);
        var swatchBounds = new Rectangle(label.X + this.LogicalToDeviceUnits(4), label.Y + (caption - swatch) / 2, swatch, swatch);
        string text = "—";
        if (eyedropper.Hovered is { } color)
        {
            using var fill = new SolidBrush(color);
            g.FillRectangle(fill, swatchBounds);
            g.DrawRectangle(Pens.White, swatchBounds);
            text = string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        var textBounds = Rectangle.FromLTRB(swatchBounds.Right + this.LogicalToDeviceUnits(6), label.Y, label.Right, label.Bottom);
        TextRenderer.DrawText(g, text, this.Font, textBounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

        using (var frame = new Pen(Color.FromArgb(40, 40, 40)))
        {
            g.DrawRectangle(frame, Rectangle.Union(view, label));
        }

        g.Restore(state);
    }

    /// <summary>The eyedropper drawn with a white halo, its tip on the hot spot, at the monitor's DPI; one per size.</summary>
    private Cursor EyedropperCursor()
    {
        int size = this.LogicalToDeviceUnits(24);
        if (EyedropperCursors.TryGetValue(size, out var cursor))
        {
            return cursor;
        }

        using var icon = EffectIcons.Eyedropper(size - 2);
        using var image = new Bitmap(size, size);
        using (var g = Graphics.FromImage(image))
        using (var white = new ImageAttributes())
        {
            // The icon's silhouette in white around it, so it shows on dark images.
            white.SetColorMatrix(new ColorMatrix([[0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, 1, 0], [1, 1, 1, 0, 1]]));
            foreach (var (dx, dy) in ((int, int)[])[(0, 0), (2, 0), (0, 2), (2, 2), (1, 0), (0, 1), (2, 1), (1, 2)])
            {
                g.DrawImage(icon, new Rectangle(dx, dy, icon.Width, icon.Height), 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, white);
            }

            g.DrawImage(icon, 1, 1, icon.Width, icon.Height);
        }

        // The tip: where EffectIcons.Eyedropper starts its line.
        var hotSpot = new Point(1 + (int)Math.Round((size - 2) * 0.1f), 1 + (int)Math.Round((size - 2) * 0.9f));
        cursor = CreateCursor(image, hotSpot) ?? Cursors.Cross;
        EyedropperCursors[size] = cursor;
        return cursor;
    }

    private static Cursor? CreateCursor(Bitmap image, Point hotSpot)
    {
        IntPtr icon = image.GetHicon();
        try
        {
            if (!GetIconInfo(icon, out var info))
            {
                return null;
            }

            info.IsIcon = false;
            info.HotSpotX = hotSpot.X;
            info.HotSpotY = hotSpot.Y;
            IntPtr handle = CreateIconIndirect(ref info);
            DeleteObject(info.Mask);
            DeleteObject(info.Color);
            return handle == IntPtr.Zero ? null : new Cursor(handle);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    /// <summary>The snapshot's pixels, read once: its ARGB values, row after row.</summary>
    private static int[] Pixels(Bitmap snapshot)
    {
        var data = snapshot.LockBits(new Rectangle(Point.Empty, snapshot.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new int[snapshot.Width * snapshot.Height];
            for (int row = 0; row < snapshot.Height; row++)
            {
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * snapshot.Width, snapshot.Width);
            }

            return pixels;
        }
        finally
        {
            snapshot.UnlockBits(data);
        }
    }

    /// <summary>An armed eyedropper: its snapshot, where the canvas stood, its sample, its owner's callbacks and what it shows.</summary>
    private sealed class Eyedropper(Bitmap snapshot, int[] pixels, Point origin, int sample, Action<Color?> hoveredChanged, Action<Color> picked, Action cancelled)
    {
        public Bitmap Snapshot { get; } = snapshot;

        public int Sample { get; } = sample;

        public Action<Color?> HoveredChanged { get; } = hoveredChanged;

        public Action<Color> Picked { get; } = picked;

        public Action Cancelled { get; } = cancelled;

        /// <summary>Where the cursor is over the preview; null outside it.</summary>
        public Point? At { get; set; }

        /// <summary>The color under the cursor, the loupe's; null where nothing is read.</summary>
        public Color? Hovered { get; set; }

        /// <summary>The color last told to the owner; null where nothing is read.</summary>
        public Color? Applied { get; set; }

        public Point SnapshotPoint(Point location) => new(location.X - origin.X, location.Y - origin.Y);

        /// <summary>
        /// The average of the sample square around <paramref name="location"/>, each pixel weighted by its
        /// opacity, opaque; null when the square leaves the canvas or holds a fully transparent pixel —
        /// an empty cell, a gap, a cell without background.
        /// </summary>
        public Color? ColorAt(Point location)
        {
            var at = this.SnapshotPoint(location);
            int left = at.X - (this.Sample - 1) / 2;
            int top = at.Y - (this.Sample - 1) / 2;
            if (left < 0 || top < 0 || left + this.Sample > this.Snapshot.Width || top + this.Sample > this.Snapshot.Height)
            {
                return null;
            }

            long red = 0, green = 0, blue = 0, weight = 0;
            for (int y = top; y < top + this.Sample; y++)
            {
                for (int x = left; x < left + this.Sample; x++)
                {
                    int argb = pixels[y * this.Snapshot.Width + x];
                    int alpha = (argb >> 24) & 0xFF;
                    if (alpha == 0)
                    {
                        return null;
                    }

                    red += ((argb >> 16) & 0xFF) * alpha;
                    green += ((argb >> 8) & 0xFF) * alpha;
                    blue += (argb & 0xFF) * alpha;
                    weight += alpha;
                }
            }

            return Color.FromArgb((int)Math.Round(red / (double)weight), (int)Math.Round(green / (double)weight), (int)Math.Round(blue / (double)weight));
        }
    }

    /// <summary>
    /// While the eyedropper is armed, every press in the app is its own: a left one over the preview picks,
    /// any other cancels — each swallowed with its release, so nothing else acts on it — and so does a key,
    /// the modifiers aside. A press on a window's frame cancels and goes on, so the window still moves.
    /// </summary>
    private sealed class EyedropperFilter(GridPreview preview) : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmLButtonDblClk = 0x0203;
        private const int WmRButtonDown = 0x0204;
        private const int WmRButtonUp = 0x0205;
        private const int WmRButtonDblClk = 0x0206;
        private const int WmMButtonDown = 0x0207;
        private const int WmMButtonUp = 0x0208;
        private const int WmMButtonDblClk = 0x0209;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcRButtonDown = 0x00A4;

        // Whether the filter is wanted, and whether it is in the application's list: removed later than unwanted.
        private bool _wanted;
        private bool _registered;

        // The releases still to swallow, by message.
        private readonly HashSet<int> _releases = [];

        public void Install()
        {
            this._wanted = true;
            if (!this._registered)
            {
                Application.AddMessageFilter(this);
                this._registered = true;
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            bool armed = preview.EyedropperArmed;
            switch (m.Msg)
            {
                case WmLButtonDown or WmLButtonDblClk or WmRButtonDown or WmRButtonDblClk or WmMButtonDown or WmMButtonDblClk when armed:
                    this._releases.Add(m.Msg switch
                    {
                        WmLButtonDown or WmLButtonDblClk => WmLButtonUp,
                        WmRButtonDown or WmRButtonDblClk => WmRButtonUp,
                        _ => WmMButtonUp,
                    });
                    if (m.Msg is WmLButtonDown or WmLButtonDblClk && m.HWnd == preview.Handle)
                    {
                        long position = m.LParam.ToInt64();
                        preview.PickEyedropper(new Point((short)(position & 0xFFFF), (short)((position >> 16) & 0xFFFF)));
                    }
                    else
                    {
                        preview.CancelEyedropper();
                    }

                    this.UninstallWhenDone();
                    return true;
                case WmLButtonUp or WmRButtonUp or WmMButtonUp when this._releases.Remove(m.Msg):
                    this.UninstallWhenDone();
                    return true;
                case WmNcLButtonDown or WmNcRButtonDown when armed:
                    preview.CancelEyedropper();
                    this.UninstallWhenDone();
                    return false;
                case WmKeyDown or WmSysKeyDown when armed:
                    var key = (Keys)(int)m.WParam & Keys.KeyCode;
                    if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
                    {
                        return false;
                    }

                    preview.CancelEyedropper();
                    this.UninstallWhenDone();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Removed once the eyedropper is disarmed and every release swallowed; outside the filtering, which is running.</summary>
        private void UninstallWhenDone()
        {
            if (this._wanted && !preview.EyedropperArmed && this._releases.Count == 0)
            {
                this._wanted = false;
                preview.BeginInvoke(() =>
                {
                    if (!this._wanted && this._registered)
                    {
                        Application.RemoveMessageFilter(this);
                        this._registered = false;
                    }
                });
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool IsIcon;
        public int HotSpotX;
        public int HotSpotY;
        public IntPtr Mask;
        public IntPtr Color;
    }

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref IconInfo info);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
