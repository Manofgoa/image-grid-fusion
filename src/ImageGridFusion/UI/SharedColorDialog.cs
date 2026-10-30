using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ImageGridFusion.UI;

/// <summary>
/// The standard color dialog every Color… opens: fully expanded, its custom colors remembered between
/// sessions, the eyedropper's picks written into them (workfiles/20261021-color-eyedropper.md § Windows Dialog);
/// the color it shows followed live while it is open (workfiles/20261029-eyedropper-ergonomics.md § Live Color
/// Dialog). One for the whole session.
/// </summary>
internal sealed class SharedColorDialog : IDisposable
{
    /// <summary>How many custom colors the dialog holds.</summary>
    public const int SlotCount = 16;

    /// <summary>The shortest time between two colors shown live, as the eyedropper's.</summary>
    private const int LiveInterval = 33;

    // What the dialog shows in a custom slot never filled: white.
    private static readonly int EmptySlot = ColorTranslator.ToWin32(Color.White);

    private readonly LiveColorDialog _dialog = new() { AnyColor = true, FullOpen = true };
    private readonly Action<string> _saveFailed;
    private int[] _custom;
    private int _next;

    /// <param name="saveFailed">Told why the custom colors could not be remembered.</param>
    public SharedColorDialog(Action<string> saveFailed)
    {
        this._saveFailed = saveFailed;
        this._custom = AppSettings.CustomColors ?? [.. Enumerable.Repeat(EmptySlot, SlotCount)];
        this._next = AppSettings.CustomColorsNext;
    }

    /// <summary>
    /// Shows the dialog preselected on <paramref name="color"/>: the color chosen, null when cancelled. Its
    /// custom colors, which may be edited in it, are saved back either way. While it is open, every other color
    /// it shows is given to <paramref name="live"/>, at most every 33 ms, the last one always.
    /// </summary>
    public Color? Show(IWin32Window owner, Color color, Action<Color>? live = null)
    {
        this._dialog.Color = color;
        this._dialog.CustomColors = this._custom;
        DialogResult result;
        using (var timer = new System.Windows.Forms.Timer { Interval = LiveInterval })
        {
            int shown = color.ToArgb();
            Color? pending = null;
            long last = 0;
            void Apply()
            {
                timer.Stop();
                if (pending is { } next)
                {
                    pending = null;
                    last = Environment.TickCount64;
                    live!(next);
                }
            }

            timer.Tick += (_, _) => Apply();
            this._dialog.Changed = live is null ? null : next =>
            {
                if (next.ToArgb() == shown)
                {
                    return;
                }

                shown = next.ToArgb();
                pending = next;
                if (Environment.TickCount64 - last >= LiveInterval)
                {
                    Apply();
                }
                else
                {
                    timer.Start();
                }
            };
            try
            {
                result = this._dialog.ShowDialog(owner);
            }
            finally
            {
                this._dialog.Changed = null;
            }
        }

        var custom = this._dialog.CustomColors;
        if (!custom.SequenceEqual(this._custom))
        {
            this._custom = custom;
            this.Save();
        }

        return result == DialogResult.OK ? this._dialog.Color : null;
    }

    /// <summary>
    /// A color taken with the eyedropper joins the custom colors: in the first slot never filled, else in the
    /// next one round the 16, the others left in place.
    /// </summary>
    public void Add(Color color)
    {
        int slot = Array.IndexOf(this._custom, EmptySlot);
        if (slot < 0)
        {
            slot = this._next;
            this._next = (this._next + 1) % SlotCount;
        }

        this._custom[slot] = ColorTranslator.ToWin32(color);
        this.Save();
    }

    public void Dispose() => this._dialog.Dispose();

    /// <summary>
    /// The color dialog telling the color it shows: its Red, Green and Blue fields, which it updates for a basic or
    /// custom color clicked, the spectrum clicked or dragged, the luminance bar or a value typed.
    /// </summary>
    private sealed class LiveColorDialog : ColorDialog
    {
        private const int WM_COMMAND = 0x0111;
        private const int EN_CHANGE = 0x0300;
        private const int ColorRed = 706;
        private const int ColorGreen = 707;
        private const int ColorBlue = 708;

        /// <summary>Given the color shown at each change of its fields, while set.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<Color>? Changed { get; set; }

        protected override IntPtr HookProc(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            if (msg == WM_COMMAND && this.Changed is { } changed
                && (int)(((long)wparam >> 16) & 0xFFFF) == EN_CHANGE && (int)((long)wparam & 0xFFFF) is >= ColorRed and <= ColorBlue)
            {
                changed(Color.FromArgb(Channel(hWnd, ColorRed), Channel(hWnd, ColorGreen), Channel(hWnd, ColorBlue)));
            }

            return base.HookProc(hWnd, msg, wparam, lparam);
        }

        private static int Channel(IntPtr dialog, int id) => (int)Math.Min(GetDlgItemInt(dialog, id, out _, false), 255);

        [DllImport("user32.dll")]
        private static extern uint GetDlgItemInt(IntPtr dialog, int id, out bool translated, bool signed);
    }

    private void Save()
    {
        try
        {
            AppSettings.SaveCustomColors(this._custom, this._next);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            this._saveFailed(ex.Message);
        }
    }
}
