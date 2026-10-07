using System.ComponentModel;

namespace ImageGridFusion.UI;

/// <summary>
/// A thumbnail strip over the values of an enum, exactly one of them selected: the selected one pressed,
/// a click on another one picking it.
/// </summary>
internal abstract class SelectionStrip<T> : ThumbnailStrip<T>
    where T : struct, Enum
{
    private T _selected;

    protected SelectionStrip(T selected)
        : base(Enum.GetValues<T>())
    {
        this._selected = selected;
    }

    /// <summary>The value shown as selected.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T Selected
    {
        get => this._selected;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(value, this._selected))
            {
                this._selected = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>A click on the selected thumbnail picks it too.</summary>
    protected virtual bool PicksSelected => false;

    protected override bool IsPressed(T value) => EqualityComparer<T>.Default.Equals(value, this._selected);

    protected override bool Picks(T value) => this.PicksSelected || !this.IsPressed(value);
}
