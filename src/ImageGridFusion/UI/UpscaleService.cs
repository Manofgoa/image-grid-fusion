using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// Computes the Upscale effect's enlargements off the UI thread, one at a time: a still enlarged whole, handed back to
/// the image on the UI thread (<see cref="SourceImage.SetUpscaled"/>); an animated GIF frame by frame into the upscale
/// cache, its pages handed back once every frame is there (<see cref="SourceImage.SetUpscaledPages"/>). Asked again for
/// every image still drawn as loaded at each paint (<see cref="Request"/>), so a new route changing a look is covered
/// by itself; a request whose factor changed meanwhile stops. The models are released after 5 minutes unused.
/// Created and called on the UI thread. See workfiles/20261029-image-upscale.md § Drawn Bitmap.
/// </summary>
internal sealed class UpscaleService : IDisposable
{
    // How long the models stay loaded unused, and how often that is checked, in ms.
    private const int IdleMs = 5 * 60 * 1000;
    private const int IdleCheckMs = 60 * 1000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(SourceImage Image, int Page, int Factor), Task> _running = [];
    private readonly HashSet<(SourceImage Image, int Page, int Factor)> _failed = [];
    private readonly ConcurrentDictionary<SourceImage, double> _progress = [];
    private readonly ConditionalWeakTable<SourceImage, string> _tags = [];
    private readonly System.Windows.Forms.Timer _idle = new() { Interval = IdleCheckMs };
    private UpscaleEngine? _engine;
    private long _usedAt;
    private bool _disposed;

    public UpscaleService()
    {
        this._idle.Tick += (_, _) => _ = this.ReleaseIdleAsync();
        this._idle.Start();
    }

    /// <summary>An enlargement was handed to its image: its cell is drawn again, a GIF's player reads its new pages.</summary>
    public event EventHandler<SourceImage>? Completed;

    /// <summary>An enlargement could not be computed: the message says why.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Whether <paramref name="image"/> waits for its enlargement: queued or being computed.</summary>
    public bool IsWorking(SourceImage image) => this._running.Keys.Any(k => k.Image == image);

    /// <summary>The share of <paramref name="image"/>'s enlargement done, 0 to 1, while it is computed; <c>null</c> otherwise.</summary>
    public double? ProgressOf(SourceImage image) => this._progress.TryGetValue(image, out double done) ? done : null;

    /// <summary>
    /// Queues the enlargement of <paramref name="image"/> when its Upscale effect is on and its enlargement not ready
    /// — nothing when it is already queued, failed before, or the engine is not installed.
    /// </summary>
    public void Request(SourceImage image)
    {
        if (this._disposed || image.IsDisposed || !image.UpscalePending || image.Look.Upscale is not { } effect || !UpscaleEngineFiles.IsAvailable)
        {
            return;
        }

        // A GIF's frames are all enlarged at once: the page it shows does not matter.
        var key = (image, image.Pages is null ? image.Page : -1, effect.Factor);
        if (this._running.ContainsKey(key) || this._failed.Contains(key))
        {
            return;
        }

        var task = this.ComputeAsync(image, key.Item2, effect.Factor);
        if (!task.IsCompleted)
        {
            this._running[key] = task;
            _ = task.ContinueWith(_ => this._running.Remove(key), TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    /// <summary>
    /// Waits for the enlargements of <paramref name="images"/> — what an export draws or a save writes — requesting
    /// those not asked for yet. An image whose enlargement fails is drawn as loaded.
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

    /// <summary>
    /// The models released at once, once the enlargement computed now is done: their files can then be replaced
    /// (workfiles/20261102-component-update-verification.md § Updating). The next enlargement loads them again.
    /// </summary>
    public async Task ReleaseNowAsync()
    {
        if (this._disposed)
        {
            return;
        }

        await this._gate.WaitAsync();
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

    private async Task ComputeAsync(SourceImage image, int page, int factor)
    {
        await this._gate.WaitAsync();
        try
        {
            // Its turn come, the request may be stale: the factor or the page changed, or the image left.
            if (this._disposed || image.IsDisposed || image.Look.Upscale?.Factor != factor || (page >= 0 && image.Page != page) || !image.UpscalePending)
            {
                return;
            }

            bool Stale() => this._disposed || image.IsDisposed || image.Look.Upscale?.Factor != factor;
            this._progress[image] = 0;
            try
            {
                if (image.Pages is { } pages)
                {
                    string tag = this._tags.GetValue(image, i => UpscaleCache.Tag(i.FilePath));
                    await Task.Run(() => this.UpscaleFrames(image, pages, tag, factor, Stale));
                    if (!Stale())
                    {
                        image.SetUpscaledPages(factor, new UpscaledPages(pages, tag, factor));
                        this.Completed?.Invoke(this, image);
                    }

                    return;
                }

                var copy = new Bitmap(image.Bitmap);
                Bitmap upscaled;
                try
                {
                    upscaled = await Task.Run(() =>
                    {
                        this._engine ??= new UpscaleEngine();
                        return this._engine.Upscale(copy, factor, done => this._progress[image] = done, Stale);
                    });
                }
                finally
                {
                    copy.Dispose();
                }

                if (image.IsDisposed || image.Page != page)
                {
                    upscaled.Dispose();
                    return;
                }

                image.SetUpscaled(page, factor, upscaled);
                this.Completed?.Invoke(this, image);
            }
            catch (OperationCanceledException)
            {
                // The factor changed or the image left: the next paint asks again if need be.
            }
            catch (Exception e) when (e is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or IOException or UnauthorizedAccessException
                or DllNotFoundException or ArgumentException or ExternalException or InvalidOperationException)
            {
                if (!image.IsDisposed)
                {
                    this._failed.Add((image, page, factor));
                    this.Failed?.Invoke(this, $"Couldn't upscale the image: {ErrorLog.Describe(e)}");
                }
            }
            finally
            {
                this._progress.TryRemove(image, out _);
                this._usedAt = Environment.TickCount64;
            }
        }
        finally
        {
            this._gate.Release();
        }
    }

    // Every frame of a GIF not in the cache yet enlarged and written there, the share done reported frame by frame.
    private void UpscaleFrames(SourceImage image, PageSource pages, string tag, int factor, Func<bool> stale)
    {
        int count = pages.Count;
        for (int page = 0; page < count; page++)
        {
            if (stale())
            {
                throw new OperationCanceledException();
            }

            if (File.Exists(UpscaleCache.PathOf(tag, factor, page)))
            {
                continue;
            }

            using var frame = pages.Render(page);
            this._engine ??= new UpscaleEngine();
            int done = page;
            using var upscaled = this._engine.Upscale(frame, factor, share => this._progress[image] = (done + share) / count, stale);
            UpscaleCache.Save(tag, factor, page, upscaled);
            this._progress[image] = (page + 1) / (double)count;
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
