using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ImageGridFusion.UI;

/// <summary>
/// Windows' own split button (<c>BS_SPLITBUTTON</c>): one button, its arrow built in, opening
/// <see cref="Menu"/> above it (workfiles/20261029-menu-icons.md, Iteration 10). Drawn by the system, its icon
/// given through the button's image list so its transparency is kept.
/// </summary>
internal sealed class SplitButton : Button
{
    private const int BS_SPLITBUTTON = 0x0C;
    private const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;
    private const int BCN_DROPDOWN = -1250 + 2;
    private const int BCM_SETIMAGELIST = 0x1602;
    private const int BCM_GETIDEALSIZE = 0x1601;
    private const int BUTTON_IMAGELIST_ALIGN_LEFT = 0;

    private ImageList? _images;
    private Bitmap? _icon;
    private bool _mainEnabled = true;

    public SplitButton()
    {
        this.FlatStyle = FlatStyle.System;
    }

    /// <summary>The menu its arrow opens.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ContextMenuStrip? Menu { get; set; }

    /// <summary>
    /// Whether the main part acts: when it does not, while the arrow still does, a click on it opens the menu too —
    /// the system button cannot grey one part alone.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MainEnabled
    {
        get => this._mainEnabled;
        set => this._mainEnabled = value;
    }

    /// <summary>The icon before the text, at its size in device pixels; the button keeps its own copy.</summary>
    public void SetIcon(Bitmap icon)
    {
        // The list reads its image again whenever it makes its handle: it keeps a copy of its own.
        var copy = new Bitmap(icon);
        var images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = icon.Size };
        images.Images.Add(copy);
        var (previous, previousIcon) = (this._images, this._icon);
        (this._images, this._icon) = (images, copy);
        this.ApplyImages();
        previous?.Dispose();
        previousIcon?.Dispose();
        this.AdjustSize();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= BS_SPLITBUTTON;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        this.ApplyImages();
        this.AdjustSize();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        this.AdjustSize();
    }

    protected override void OnClick(EventArgs e)
    {
        if (this._mainEnabled)
        {
            base.OnClick(e);
        }
        else
        {
            this.ShowMenu();
        }
    }

    /// <summary>
    /// The system's ideal width — the text, the icon and the arrow part — once the handle exists, at the height of
    /// the app's other buttons.
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (!this.IsHandleCreated)
        {
            return base.GetPreferredSize(proposedSize);
        }

        var size = default(NativeSize);
        SendMessage(this.Handle, BCM_GETIDEALSIZE, IntPtr.Zero, ref size);
        var preferred = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, preferred.Width), preferred.Height);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_REFLECT_NOTIFY && Marshal.PtrToStructure<NotifyHeader>(m.LParam).Code == BCN_DROPDOWN)
        {
            this.ShowMenu();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._images?.Dispose();
            this._icon?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ShowMenu() => this.Menu?.Show(this, Point.Empty, ToolStripDropDownDirection.AboveRight);

    private void ApplyImages()
    {
        if (!this.IsHandleCreated || this._images is null)
        {
            return;
        }

        int gap = this.LogicalToDeviceUnits(3);
        var list = new ButtonImageList { ImageList = this._images.Handle, Margin = new NativeRect { Left = gap, Right = 0 }, Align = BUTTON_IMAGELIST_ALIGN_LEFT };
        SendMessage(this.Handle, BCM_SETIMAGELIST, IntPtr.Zero, ref list);
    }

    private void AdjustSize()
    {
        if (this.AutoSize && this.Parent is { } parent)
        {
            parent.PerformLayout(this, nameof(this.Size));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyHeader
    {
        public IntPtr From;
        public UIntPtr Id;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ButtonImageList
    {
        public IntPtr ImageList;
        public NativeRect Margin;
        public int Align;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref NativeSize lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref ButtonImageList lParam);
}
