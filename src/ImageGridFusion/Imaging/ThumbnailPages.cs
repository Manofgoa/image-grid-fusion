using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// A file previewed by the thumbnail Windows shows for it in Explorer (<see cref="ShellThumbnail"/>):
/// one still page, asked for again at a bigger size while zoomed in, so it stays sharp as far as the
/// file's thumbnail handler allows (workfiles/20260926-text-zoom-and-frames.md).
/// </summary>
public sealed class ThumbnailPages : PageSource
{
    private readonly string _path;

    /// <summary>Side asked of Windows now: <see cref="ShellThumbnail.RequestedSide"/>, more while zoomed in.</summary>
    private int _side = ShellThumbnail.RequestedSide;

    /// <summary>Long side of the thumbnail at <see cref="ShellThumbnail.RequestedSide"/>, and of the last one rendered.</summary>
    private readonly int _baseLongSide;
    private int _renderedLongSide;

    private ThumbnailPages(string path, Bitmap first)
    {
        this._path = path;
        this._baseLongSide = Math.Max(1, Math.Max(first.Width, first.Height));
        this._renderedLongSide = this._baseLongSide;
    }

    public override int Count => 1;

    public override string Label(int page) => "1 / 1";

    public override bool Sharpens => true;

    /// <summary>A handler capping its size gives the same thumbnail again: no sharper, never counted as such.</summary>
    public override double Sharpening => Volatile.Read(ref this._renderedLongSide) / (double)this._baseLongSide;

    /// <summary>
    /// The file's thumbnail, with the source that asks for it again: null when Windows has none.
    /// <paramref name="first"/> receives it.
    /// </summary>
    public static ThumbnailPages? TryOpen(string path, out Bitmap? first)
    {
        first = ShellThumbnail.TryLoad(path);
        return first is null ? null : new ThumbnailPages(path, first);
    }

    public override bool Sharpen(int longSide)
    {
        int side = Math.Clamp(longSide, ShellThumbnail.RequestedSide, MaxSharpSide);
        if (side == Volatile.Read(ref this._side))
        {
            return false;
        }

        Volatile.Write(ref this._side, side);
        return true;
    }

    public override Bitmap Render(int page)
    {
        var bitmap = ShellThumbnail.TryLoad(this._path, Volatile.Read(ref this._side))
            ?? throw new IOException($"Windows no longer gives a thumbnail for {this._path}.");
        Volatile.Write(ref this._renderedLongSide, Math.Max(bitmap.Width, bitmap.Height));
        return bitmap;
    }
}
