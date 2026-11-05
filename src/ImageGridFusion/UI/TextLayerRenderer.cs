using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// What a text layer is wanted to draw: its text, its style, its box, at a font size — a draft below
/// <see cref="TextRaster.EmSize"/> —; <paramref name="Sequence"/> orders the requests, <paramref name="Final"/> marks the
/// full render that ends a burst of changes.
/// </summary>
internal sealed record TextRenderRequest(long Sequence, string Text, LayerTextStyle Style, double? Box, float Em, bool Final);

/// <summary>What a request gave: the bitmap and its line, or the error that stopped it.</summary>
internal sealed record TextRenderResult(TextRenderRequest Request, Bitmap? Bitmap, double LinePixels, Exception? Error);

/// <summary>
/// Draws the text layers' changes off the UI thread, one at a time, on a worker of its own — so its Direct2D factories
/// are made once (<see cref="TextRaster"/>) —: the last request wins, the ones it replaced before their turn are never
/// drawn (workfiles/20261105-text-layer-typing-refresh.md § Pending Text and Style). Each result is handed to
/// <see cref="Rendered"/> on the UI thread, through <paramref name="owner"/>.
/// </summary>
internal sealed class TextLayerRenderer : IDisposable
{
    private readonly Control _owner;
    private readonly Thread _worker;
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _gate = new();
    private TextRenderRequest? _wanted;
    private bool _stopped;

    public TextLayerRenderer(Control owner)
    {
        this._owner = owner;
        this._worker = new Thread(this.Run) { IsBackground = true, Name = "Text layer renderer" };
        this._worker.SetApartmentState(ApartmentState.MTA);
        this._worker.Start();
    }

    /// <summary>A request drawn, on the UI thread; its bitmap is the handler's to keep or dispose.</summary>
    public event Action<TextRenderResult>? Rendered;

    /// <summary>Asks for <paramref name="request"/>, replacing the one waiting for its turn, if any.</summary>
    public void Request(TextRenderRequest request)
    {
        lock (this._gate)
        {
            this._wanted = request;
        }

        this._wake.Set();
    }

    /// <summary>The request waiting for its turn dropped; the one being drawn still comes back.</summary>
    public void Cancel()
    {
        lock (this._gate)
        {
            this._wanted = null;
        }
    }

    public void Dispose()
    {
        lock (this._gate)
        {
            this._stopped = true;
            this._wanted = null;
        }

        this._wake.Set();
    }

    private void Run()
    {
        while (true)
        {
            this._wake.WaitOne();
            while (this.Take() is { } request)
            {
                TextRenderResult result;
                try
                {
                    var (bitmap, line) = TextRaster.Render(request.Text, request.Style, request.Box, request.Em);
                    result = new TextRenderResult(request, bitmap, line, null);
                }
                catch (Exception ex)
                {
                    // Any failure goes back to the form, which says it and ends the gesture: the worker never dies of it.
                    result = new TextRenderResult(request, null, 0, ex);
                }

                if (!this.Post(result))
                {
                    result.Bitmap?.Dispose();
                    return;
                }
            }

            lock (this._gate)
            {
                if (this._stopped)
                {
                    return;
                }
            }
        }
    }

    private TextRenderRequest? Take()
    {
        lock (this._gate)
        {
            if (this._stopped)
            {
                return null;
            }

            var request = this._wanted;
            this._wanted = null;
            return request;
        }
    }

    /// <summary>Hands <paramref name="result"/> to the UI thread; false once the owner is gone.</summary>
    private bool Post(TextRenderResult result)
    {
        try
        {
            if (this._owner.IsDisposed || !this._owner.IsHandleCreated)
            {
                return false;
            }

            this._owner.BeginInvoke(() =>
            {
                if (this._stopped)
                {
                    result.Bitmap?.Dispose();
                    return;
                }

                this.Rendered?.Invoke(result);
            });
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }
}
