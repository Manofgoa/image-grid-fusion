namespace ImageGridFusion.Composition;

/// <summary>
/// Pages of a file previewed as an image — PDF pages, video positions, text pages — rendered on
/// demand. <see cref="Render"/> runs off the UI thread, one call at a time per source; the other
/// members are used on the UI thread.
/// </summary>
public abstract class PageSource : IDisposable
{
    public abstract int Count { get; }

    /// <summary>Page shown when the file is added.</summary>
    public virtual int InitialPage => 0;

    /// <summary>What the slider shows for <paramref name="page"/>: a page number or a time.</summary>
    public abstract string Label(int page);

    /// <summary>Size the pages are laid out at, when the source takes the shape of its cell; else <c>null</c>.</summary>
    public virtual Size? PageSize => null;

    /// <summary>
    /// Lays the pages out again at <paramref name="pageSize"/> and returns the new page holding the
    /// start of <paramref name="page"/>. Only for a source with a <see cref="PageSize"/>.
    /// </summary>
    public virtual int Resize(Size pageSize, int page) => page;

    public abstract Bitmap Render(int page);

    public virtual void Dispose()
    {
    }
}
