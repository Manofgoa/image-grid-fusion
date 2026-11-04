using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// Computes an enlargement for the Upscale window, off the UI thread, one at a time: a still enlarged whole, handed
/// back to the image on the UI thread (<see cref="SourceImage.SetUpscaled"/>); an animated GIF frame by frame into the
/// upscale cache, its pages handed back once every frame is there (<see cref="SourceImage.SetUpscaledPages"/>). It
/// times what it computes, so the window estimates the next one. The models are released after 5 minutes unused.
/// Created and called on the UI thread. See workfiles/20261029-image-upscale.md § Upscale Window.
/// </summary>
internal sealed class UpscaleService : IDisposable
{
    // How long the models stay loaded unused, and how often that is checked, in ms.
    private const int IdleMs = 5 * 60 * 1000;
    private const int IdleCheckMs = 60 * 1000;

    // Seconds a megapixel of the image as loaded takes before anything was timed on this machine: measured on a
    // 16-thread CPU, the Quality ×4 at ~230 s, its ×2 about a quarter, the Fast model about a fifteenth of it.
    private static readonly IReadOnlyDictionary<(UpscaleModel, int), double> DefaultSecondsPerMegapixel = new Dictionary<(UpscaleModel, int), double>
    {
        [(UpscaleModel.Quality, 4)] = 230,
        [(UpscaleModel.Quality, 2)] = 60,
        [(UpscaleModel.Fast, 4)] = 15,
        [(UpscaleModel.Fast, 2)] = 15,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<(UpscaleModel, int), double> _measured = [];
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

    /// <summary>
    /// How long enlarging <paramref name="image"/> as <paramref name="setting"/> says should take: its pixels — every
    /// frame of a GIF's — times the time a megapixel took at the last run of that model and factor this session, a
    /// default before.
    /// </summary>
    public TimeSpan Estimate(SourceImage image, UpscaleEffect setting)
    {
        var size = image.UpscaleSize;
        double megapixels = (double)size.Width * size.Height * Math.Max(1, image.Pages?.Count ?? 1) / 1_000_000;
        var key = (setting.Model, setting.Factor);
        double perMegapixel = this._measured.TryGetValue(key, out double measured) ? measured : DefaultSecondsPerMegapixel[key];
        return TimeSpan.FromSeconds(megapixels * perMegapixel);
    }

    /// <summary>
    /// Enlarges <paramref name="image"/> as <paramref name="setting"/> says and hands the enlargement to it, reporting the
    /// share done; <paramref name="token"/> stops it at the next tile with an <see cref="OperationCanceledException"/>.
    /// The engine's own errors are thrown as they come.
    /// </summary>
    public async Task ComputeAsync(SourceImage image, UpscaleEffect setting, IProgress<double> progress, CancellationToken token)
    {
        await this._gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(this._disposed || image.IsDisposed, this);
            bool Stopped() => token.IsCancellationRequested || this._disposed || image.IsDisposed;
            var watch = Stopwatch.StartNew();
            try
            {
                if (image.Pages is { } pages)
                {
                    string tag = this._tags.GetValue(image, i => UpscaleCache.Tag(i.FilePath));
                    await Task.Run(() => this.UpscaleFrames(pages, tag, setting, progress, Stopped), token);
                    token.ThrowIfCancellationRequested();
                    image.SetUpscaledPages(setting, new UpscaledPages(pages, tag, setting.Factor, setting.Model));
                }
                else
                {
                    int page = image.Page;
                    Bitmap upscaled;
                    using (var copy = new Bitmap(image.Bitmap))
                    {
                        upscaled = await Task.Run(
                            () => (this._engine ??= new UpscaleEngine()).Upscale(copy, setting.Model, setting.Factor, progress.Report, Stopped),
                            token);
                    }

                    if (token.IsCancellationRequested || image.IsDisposed || image.Page != page)
                    {
                        upscaled.Dispose();
                        token.ThrowIfCancellationRequested();
                        return;
                    }

                    image.SetUpscaled(page, setting, upscaled);
                }

                var size = image.UpscaleSize;
                double megapixels = (double)size.Width * size.Height * Math.Max(1, image.Pages?.Count ?? 1) / 1_000_000;
                this._measured[(setting.Model, setting.Factor)] = watch.Elapsed.TotalSeconds / Math.Max(megapixels, 0.01);
                this.Completed?.Invoke(this, image);
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

    // Every frame of a GIF not in the cache yet enlarged and written there, the share done reported frame by frame.
    private void UpscaleFrames(PageSource pages, string tag, UpscaleEffect setting, IProgress<double> progress, Func<bool> stopped)
    {
        int count = pages.Count;
        for (int page = 0; page < count; page++)
        {
            if (stopped())
            {
                throw new OperationCanceledException();
            }

            if (!File.Exists(UpscaleCache.PathOf(tag, setting.Factor, setting.Model, page)))
            {
                using var frame = pages.Render(page);
                this._engine ??= new UpscaleEngine();
                int done = page;
                using var upscaled = this._engine.Upscale(frame, setting.Model, setting.Factor, share => progress.Report((done + share) / count), stopped);
                UpscaleCache.Save(tag, setting.Factor, setting.Model, page, upscaled);
            }

            progress.Report((page + 1) / (double)count);
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
