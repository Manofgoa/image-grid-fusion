namespace ImageGridFusion.UI;

/// <summary>
/// The standard color dialog every Color… opens: fully expanded, its custom colors remembered between
/// sessions, the eyedropper's picks written into them (workfiles/20261021-color-eyedropper.md § Windows Dialog).
/// One for the whole session.
/// </summary>
internal sealed class SharedColorDialog : IDisposable
{
    /// <summary>How many custom colors the dialog holds.</summary>
    public const int SlotCount = 16;

    // What the dialog shows in a custom slot never filled: white.
    private static readonly int EmptySlot = ColorTranslator.ToWin32(Color.White);

    private readonly ColorDialog _dialog = new() { AnyColor = true, FullOpen = true };
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
    /// custom colors, which may be edited in it, are saved back either way.
    /// </summary>
    public Color? Show(IWin32Window owner, Color color)
    {
        this._dialog.Color = color;
        this._dialog.CustomColors = this._custom;
        var result = this._dialog.ShowDialog(owner);
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
