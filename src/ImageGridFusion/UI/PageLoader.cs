using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// Renders the pages the grid asks for, off the UI thread and one at a time per image. Only the
/// latest request of an image is rendered: the ones made while a render runs are skipped but the
/// last. Used from the UI thread only.
/// </summary>
internal sealed class PageLoader
{
    private readonly Dictionary<SourceImage, int> _pending = [];
    private readonly Dictionary<SourceImage, int> _targets = [];

    /// <summary>Raised on the UI thread once an image shows a newly rendered page.</summary>
    public event EventHandler<SourceImage>? PageShown;

    /// <summary>The page an image is heading to: the latest one requested, else the one it shows.</summary>
    public int Target(SourceImage image) => _targets.TryGetValue(image, out int page) ? page : image.Page;

    public void Request(SourceImage image, int page)
    {
        bool idle = !_targets.ContainsKey(image);
        _targets[image] = page;
        _pending[image] = page;
        if (idle)
        {
            _ = RenderAsync(image);
        }
    }

    private async Task RenderAsync(SourceImage image)
    {
        var pages = image.Pages!;
        while (!image.IsDisposed && _pending.Remove(image, out int page))
        {
            Bitmap bitmap;
            try
            {
                bitmap = await Task.Run(() => pages.Render(page));
            }
            catch (Exception)
            {
                // A page that fails to render (or a source disposed meanwhile) keeps the previous page shown.
                continue;
            }

            if (image.IsDisposed)
            {
                bitmap.Dispose();
                break;
            }

            image.ShowPage(page, bitmap);
            PageShown?.Invoke(this, image);
        }

        _pending.Remove(image);
        _targets.Remove(image);
    }
}
