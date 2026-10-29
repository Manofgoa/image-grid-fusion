using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// Computes the Cutout effect's cut-outs off the UI thread, one at a time: the mask from the engine's
/// models when the image does not keep it already, then the image cut out by it, handed back to the image
/// on the UI thread (<see cref="SourceImage.SetCut"/>). Asked again for every image still drawn uncut at
/// each paint (<see cref="Request"/>), so a new route changing a look is covered by itself; a request whose
/// look changed meanwhile is dropped when its turn comes. The models are released after 5 minutes unused.
/// Created and called on the UI thread. See workfiles/20261029-cutout.md § The Mask.
/// </summary>
internal sealed class CutoutService : IDisposable
{
    // How long the models stay loaded unused, and how often that is checked, in ms.
    private const int IdleMs = 5 * 60 * 1000;
    private const int IdleCheckMs = 60 * 1000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(SourceImage Image, int Page, CutoutEffect Effect, Size Size), Task> _running = [];
    private readonly HashSet<(SourceImage Image, int Page, string Key)> _failed = [];
    private readonly System.Windows.Forms.Timer _idle = new() { Interval = IdleCheckMs };
    private CutoutEngine? _engine;
    private long _usedAt;
    private bool _disposed;

    public CutoutService()
    {
        this._idle.Tick += (_, _) => _ = this.ReleaseIdleAsync();
        this._idle.Start();
    }

    /// <summary>A cut-out was handed to its image: its cell is drawn again.</summary>
    public event EventHandler<SourceImage>? Completed;

    /// <summary>A cut-out could not be computed: the message says why.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Whether <paramref name="image"/> waits for its cut-out: queued or being computed.</summary>
    public bool IsWorking(SourceImage image) => this._running.Keys.Any(k => k.Image == image);

    /// <summary>Whether any cut-out is queued or being computed.</summary>
    public bool Busy => this._running.Count > 0;

    /// <summary>
    /// Queues the cut-out of <paramref name="image"/> when its Cutout effect is on and its cut-out not
    /// ready — nothing when it is already queued, failed before, or the engine is not downloaded.
    /// </summary>
    public void Request(SourceImage image)
    {
        if (this._disposed || image.IsDisposed || !image.CutoutPending || image.Look.Cutout is not { } effect || !CutoutEngineFiles.IsAvailable)
        {
            return;
        }

        var key = (image, image.Page, effect, image.Bitmap.Size);
        if (this._running.ContainsKey(key) || this._failed.Contains((image, image.Page, effect.MaskKey)))
        {
            return;
        }

        var task = this.ComputeAsync(image, key.Page, effect, key.Size);
        if (!task.IsCompleted)
        {
            this._running[key] = task;
            _ = task.ContinueWith(_ => this._running.Remove(key), TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    /// <summary>
    /// Waits for the cut-outs of <paramref name="images"/> — what an export draws — requesting those not
    /// asked for yet: the export then draws them cut out. An image whose cut-out fails is drawn uncut.
    /// </summary>
    public async Task WhenReadyAsync(IEnumerable<SourceImage> images)
    {
        var list = images.ToList();
        while (true)
        {
            list.ForEach(this.Request);
            var waiting = this._running.Where(r => list.Contains(r.Key.Image)).Select(r => r.Value).ToList();
            if (waiting.Count == 0)
            {
                return;
            }

            await Task.WhenAll(waiting);
        }
    }

    /// <summary>
    /// The object a pick of <paramref name="points"/> would keep in <paramref name="image"/>, at half the
    /// size MobileSAM reads it at — what the hover outlines (workfiles/20261029-cutout.md § Hover
    /// Outline). The image's embedding is read first when it has none; without a point, that is all, and
    /// the result <c>null</c>, as when the engine is missing or fails.
    /// </summary>
    public async Task<CutoutMask?> PreviewAsync(SourceImage image, IReadOnlyList<CutoutPoint> points, CutoutReach reach = CutoutReach.Auto)
    {
        if (this._disposed || !CutoutEngineFiles.IsAvailable)
        {
            return null;
        }

        await this._gate.WaitAsync();
        try
        {
            if (this._disposed || image.IsDisposed || !image.CutsOut)
            {
                return null;
            }

            int page = image.Page;
            var embedding = image.PickEmbedding is { } kept && kept.Page == page ? kept : null;
            using var copy = embedding is null ? new Bitmap(image.Bitmap) : null;
            try
            {
                var (read, mask) = await Task.Run(() =>
                {
                    this._engine ??= new CutoutEngine();
                    var read = embedding ?? this._engine.Embed(copy!, page);
                    return (read, points.Count == 0 ? null : this._engine.Pick(read, points, reach, scale: 0.5));
                });
                if (!image.IsDisposed && image.Page == page)
                {
                    image.PickEmbedding = read;
                }

                return mask;
            }
            catch (Exception e) when (e is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or IOException or UnauthorizedAccessException or DllNotFoundException or ArgumentException)
            {
                return null;
            }
            finally
            {
                this._usedAt = Environment.TickCount64;
            }
        }
        finally
        {
            this._gate.Release();
        }
    }

    public void Dispose()
    {
        this._disposed = true;
        this._idle.Dispose();

        // A model still running is left to the process's end: disposed under it, it would crash.
        if (this._gate.Wait(0))
        {
            this._engine?.Dispose();
            this._engine = null;
        }
    }

    private async Task ComputeAsync(SourceImage image, int page, CutoutEffect effect, Size size)
    {
        await this._gate.WaitAsync();
        try
        {
            // Its turn come, the request may be stale: the look, the page or the size changed, or the image left.
            if (this._disposed || image.IsDisposed || image.Look.Cutout != effect || image.Page != page || image.Bitmap.Size != size || !image.CutoutPending)
            {
                return;
            }

            string key = effect.MaskKey;
            var mask = image.MaskFor(key);
            var embedding = image.PickEmbedding is { } kept && kept.Page == page ? kept : null;
            var copy = new Bitmap(image.Bitmap);
            (CutoutMask Mask, PickEmbedding? Embedding, Bitmap Cut) result;
            try
            {
                result = await Task.Run(() =>
                {
                    if (mask is null)
                    {
                        this._engine ??= new CutoutEngine();
                        if (effect.Mode == CutoutMode.Subject)
                        {
                            mask = this._engine.Subject(copy);
                        }
                        else
                        {
                            embedding ??= this._engine.Embed(copy, page);
                            mask = this._engine.Pick(embedding, effect.Points, effect.Reach);
                        }
                    }

                    return (mask, embedding, mask.Apply(copy, effect));
                });
            }
            catch (Exception e) when (e is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or IOException or UnauthorizedAccessException or DllNotFoundException or ArgumentException)
            {
                this._failed.Add((image, page, key));
                this.Failed?.Invoke(this, $"Couldn't cut the image out: {ErrorLog.Describe(e)}");
                return;
            }
            finally
            {
                copy.Dispose();
                this._usedAt = Environment.TickCount64;
            }

            if (image.IsDisposed)
            {
                result.Cut.Dispose();
                return;
            }

            image.KeepMask(page, key, result.Mask);
            if (result.Embedding is { } read)
            {
                image.PickEmbedding = read;
            }

            if (image.Page == page && image.Bitmap.Size == size)
            {
                image.SetCut(page, effect, result.Cut);
                this.Completed?.Invoke(this, image);
            }
            else
            {
                result.Cut.Dispose();
            }
        }
        finally
        {
            this._gate.Release();
        }
    }

    // The models released once unused for a while: they hold a few hundred MB.
    private async Task ReleaseIdleAsync()
    {
        if (this._engine is null || Environment.TickCount64 - this._usedAt < IdleMs || !await this._gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            this._engine?.Dispose();
            this._engine = null;
        }
        finally
        {
            this._gate.Release();
        }
    }
}
