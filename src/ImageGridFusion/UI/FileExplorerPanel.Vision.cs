using System.ComponentModel;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer's image content analysis (workfiles/20261027-image-content-analysis.md): the ⚙
/// settings — Visual analysis, Captions — and their engines' downloads, the forgetting, the similarity
/// tier of the searches under 💡, and the similar images search — from a tile, a cell, or an image dropped
/// onto the search box.
/// </summary>
internal sealed partial class FileExplorerPanel
{
    /// <summary>
    /// Whether the images' embeddings and tags are computed — the ⚙ menu's Visual analysis, off by default:
    /// on, the Vision pass runs with the extraction, once its engine is there, and the searches under 💡 add
    /// the images closest to the query; turned off, it stops, what was found kept. Setting it raises nothing.
    /// See workfiles/20261027-image-content-analysis.md § Engine Offer.
    /// </summary>
    [DefaultValue(false)]
    public bool VisualAnalysis
    {
        get => this._visualAnalysis;
        set
        {
            if (value == this._visualAnalysis)
            {
                return;
            }

            this._visualAnalysis = value;
            if (!value)
            {
                this._similarTo = null;
            }

            this.RestartForSettings();
        }
    }

    /// <summary>
    /// Whether a caption is written for every image — the ⚙ menu's Captions, off by default: on, the
    /// Caption pass runs last with the extraction, once its engine is there; turned off, it stops, the
    /// captions written kept. Setting it raises nothing. See workfiles/20261027-image-content-analysis.md § Engine Offer.
    /// </summary>
    [DefaultValue(false)]
    public bool Captions
    {
        get => this._captions;
        set
        {
            if (value == this._captions)
            {
                return;
            }

            this._captions = value;
            this.RestartForSettings();
        }
    }

    /// <summary>Whether the vision engine (SigLIP 2) is being downloaded.</summary>
    public bool DownloadingVisionEngine => this._visionEngineDownload is not null;

    /// <summary>Whether the caption engine (Florence-2) is being downloaded.</summary>
    public bool DownloadingCaptionEngine => this._captionEngineDownload is not null;

    /// <summary>Whether a similar images search can run: the visual analysis on, its engine there.</summary>
    public bool CanFindSimilar => this._visualAnalysis && VisionEngineFiles.IsAvailable;

    /// <summary>
    /// Downloads the vision engine (<see cref="VisionEngineFiles"/>), its progress after the status line's
    /// summary, then starts the extraction again so the Vision pass runs; a failure said in the status line.
    /// Nothing when it is already being downloaded.
    /// </summary>
    public Task DownloadVisionEngineAsync() =>
        this.DownloadEngineAsync("vision engine", VisionEngineFiles.DownloadAsync, () => this._visionEngineDownload, cts => this._visionEngineDownload = cts, "the images are analysed next");

    /// <summary>
    /// Downloads the caption engine (<see cref="CaptionEngineFiles"/>), as <see cref="DownloadVisionEngineAsync"/>
    /// does the vision engine's, so the Caption pass runs.
    /// </summary>
    public Task DownloadCaptionEngineAsync() =>
        this.DownloadEngineAsync("caption engine", CaptionEngineFiles.DownloadAsync, () => this._captionEngineDownload, cts => this._captionEngineDownload = cts, "the captions are written next");

    /// <summary>
    /// Forgets the visual analysis — <c>files.vision</c> and the concepts' cache deleted, the tags and the
    /// captions of the content texts emptied, the words read kept — the extraction going on without the
    /// Vision and Caption passes, which the caller turns off. The status line says what could not be
    /// deleted. See workfiles/20261027-image-content-analysis.md § Engine Offer.
    /// </summary>
    public void ForgetVisualAnalysis()
    {
        this.StopExtraction();
        this._vision?.Clear();
        this._similarTo = null;
        string? error = null;
        try
        {
            VisionIndex.Delete(VisionIndex.DefaultPath);
            VisionTags.DeleteCache();
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        if (this._contents is { } contents && contents.ForgetSeen())
        {
            TrySave(contents);
        }

        this.StartExtraction();
        this.RefreshRows(keepPlace: true);
        this.Report(error is null ? "Visual analysis forgotten: every embedding, tag and caption deleted" : $"Visual analysis forgotten, not every file deleted: {error}", error is not null);
    }

    /// <summary>
    /// Lists the images closest to a file — its embedding the index's when it has one, else computed from
    /// its pixels — in the search view, the file first when indexed, the caption line saying so with a ×
    /// that gives the search back. See workfiles/20261027-image-content-analysis.md § Similar Images.
    /// </summary>
    public async Task FindSimilarAsync(string fullPath)
    {
        if (!this.CanFindSimilar)
        {
            return;
        }

        float[]? embedding = this.EntryOf(fullPath) is { } entry ? this._vision?.EmbeddingOf(entry.RelativePath) : null;
        embedding ??= await this.EmbedImageAsync(() =>
        {
            if (ContentExtractor.DecodePixels(fullPath) is not var (pixels, width, height))
            {
                return null;
            }

            return encoder => encoder.Embed(pixels, width, height);
        });
        this.ShowSimilar(embedding, Path.GetFileName(fullPath), fullPath);
    }

    /// <summary>Lists the images closest to a cell's image — its file's embedding when indexed, else its frame's, computed on the spot.</summary>
    public async Task FindSimilarAsync(Image frame, string name, string? fullPath)
    {
        if (!this.CanFindSimilar)
        {
            return;
        }

        float[]? embedding = fullPath is not null && this.EntryOf(fullPath) is { } entry ? this._vision?.EmbeddingOf(entry.RelativePath) : null;
        if (embedding is null)
        {
            var copy = new Bitmap(frame);
            embedding = await this.EmbedImageAsync(() => encoder =>
            {
                using (copy)
                {
                    return encoder.Embed(copy);
                }
            });
        }

        this.ShowSimilar(embedding, name, fullPath);
    }

    /// <summary>Whether a screen point is over the search box: a cell released there by its ✥ handle looks for similar images.</summary>
    public bool SearchBoxContainsScreenPoint(Point screenPoint) =>
        this._open && this._search.Visible && this._search.RectangleToScreen(this._search.ClientRectangle).Contains(screenPoint);

    /// <summary>The settings changed: the extraction started again with them, the bars and the search shown following.</summary>
    private void RestartForSettings()
    {
        this.StopExtraction();
        this.StartExtraction();
        this.LayoutBars();
        this.ApplyBarTips();
        this.ApplyMetrics();
        this.RefreshRows(keepPlace: true);
    }

    private async Task DownloadEngineAsync(string name, Func<IProgress<double>, CancellationToken, Task> download, Func<CancellationTokenSource?> running, Action<CancellationTokenSource?> setRunning, string next)
    {
        if (running() is not null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        setRunning(cancellation);
        var progress = new Progress<double>(share =>
        {
            if (running() == cancellation && !this.IsDisposed)
            {
                this.SetEngineActivity($"Downloading the {name}… {share:P0}");
            }
        });
        try
        {
            await download(progress, cancellation.Token);
            setRunning(null);
            if (!this.IsDisposed)
            {
                this.SetEngineActivity(null);
                this.ShowTransient($"The {name} downloaded: {next}");
                this.ApplyBarTips();
                this.StopExtraction();
                this.StartExtraction();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Web.UrlLoadException ex)
        {
            setRunning(null);
            if (!this.IsDisposed)
            {
                this.SetEngineActivity(null);
                this.ShowTransient($"The {name} not downloaded: {ex.Message}", error: true);
            }
        }
        finally
        {
            if (running() == cancellation)
            {
                setRunning(null);
            }

            cancellation.Dispose();
        }
    }

    /// <summary>
    /// An image's embedding computed off the UI thread — the vision model loaded for it, then released —
    /// from what <paramref name="prepare"/> gives, read on that thread: null when the image cannot be read,
    /// which the status line says.
    /// </summary>
    private async Task<float[]?> EmbedImageAsync(Func<Func<VisionEncoder, float[]>?> prepare)
    {
        this.ShowStatus("Looking at the image…");
        try
        {
            var embedding = await Task.Run(() =>
            {
                if (prepare() is not { } embed)
                {
                    return null;
                }

                using var encoder = new VisionEncoder();
                return embed(encoder);
            });
            this.ShowSummary();
            if (embedding is null)
            {
                this.ShowTransient("This image cannot be read", error: true);
            }

            return embedding;
        }
        catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or ArgumentException or IOException or System.Runtime.InteropServices.ExternalException or DllNotFoundException)
        {
            this.ShowTransient($"The vision engine could not look at the image: {ex.Message}", error: true);
            return null;
        }
    }

    /// <summary>The similar images of <paramref name="embedding"/> shown in the search view, the search box emptied.</summary>
    private void ShowSimilar(float[]? embedding, string name, string? fullPath)
    {
        if (embedding is null || this.IsDisposed)
        {
            return;
        }

        this.SetOpen(true);
        this.Mode = ExplorerMode.Favorites;
        this._clearingSearch = true;
        try
        {
            this._search.Text = "";
        }
        finally
        {
            this._clearingSearch = false;
        }

        this._similarTo = new SimilarSource(name, embedding, fullPath);
        this.RefreshRows();
    }

    private void ClearSimilar()
    {
        this._similarTo = null;
        this.RefreshRows();
        this._search.Focus();
    }

    /// <summary>An image dropped onto the search box: its file — from the Explorer, a tile — or the bitmap it carries; nothing else.</summary>
    private void DropOnSearch(IDataObject? data)
    {
        switch (SimilarDropOf(data))
        {
            case string path:
                _ = this.FindSimilarAsync(path);
                break;
            case Image image:
                _ = this.FindSimilarAsync(image, "the dropped image", null);
                break;
        }
    }

    /// <summary>What a drop onto the search box would look for: the first image or PDF dropped, or a bitmap; null for anything else.</summary>
    private static object? SimilarDropOf(IDataObject? data)
    {
        if (data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            return paths.FirstOrDefault(p => ContentExtractor.KindOf(p) is ContentKind.Image or ContentKind.Pdf);
        }

        return data?.GetDataPresent(DataFormats.Bitmap) == true ? data.GetData(DataFormats.Bitmap) as Image : null;
    }

    /// <summary>The rows of a similar images search: the closest images of the index, the closest first.</summary>
    private IEnumerable<ExplorerRow> SimilarRows(SimilarSource source)
    {
        if (this._index is not { } index || this._vision is not { } vision)
        {
            return [];
        }

        return FileSearch.SearchSimilar(index.Entries, e => vision.EmbeddingOf(e.RelativePath), source.Embedding, SimilarCount, 0)
            .Select(match => new ExplorerRow(index.FullPath(match.Entry), match.Entry.Name, Size: match.Entry.Size));
    }

    /// <summary>
    /// The files a search found, followed by the similarity tier: the images closest to the query, the
    /// closest first, those found already left out — once the query's embedding is known, computed
    /// meanwhile; none while the visual analysis is off.
    /// </summary>
    private IReadOnlyList<SearchMatch> WithSimilar(IReadOnlyList<SearchMatch> found, IReadOnlyList<IndexEntry> entries, string[] words)
    {
        if (!this.CanFindSimilar || this._vision is not { } vision || vision.Count == 0)
        {
            return found;
        }

        string query = string.Join(' ', words);
        if (this._queryEmbedding is not { } known || known.Query != query)
        {
            this.RequestQueryEmbedding(query);
            return found;
        }

        var excluded = found.Select(match => match.Entry).ToHashSet();
        return [.. found, .. FileSearch.SearchSimilar(entries, e => vision.EmbeddingOf(e.RelativePath), known.Embedding, SimilarCount, words.Length, excluded)];
    }

    /// <summary>
    /// The query's embedding computed off the UI thread once the typing pauses, the search shown run again
    /// with it — unless the query changed meanwhile. A failure is said once.
    /// </summary>
    private async void RequestQueryEmbedding(string query)
    {
        if (this._queryPending == query)
        {
            return;
        }

        this._queryPending = query;
        await Task.Delay(QueryDelayMs);
        if (this._queryPending != query || this.IsDisposed)
        {
            return;
        }

        try
        {
            var embedding = await Task.Run(() => this.EmbedText(query));
            if (this._queryPending != query || this.IsDisposed)
            {
                return;
            }

            this._queryEmbedding = (query, embedding);
            this.RefreshContentResults();
        }
        catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or IOException or DllNotFoundException or ArgumentException)
        {
            this.ShowTransient($"The vision engine could not read the search: {ex.Message}", error: true);
        }
        finally
        {
            if (this._queryPending == query)
            {
                this._queryPending = null;
            }
        }
    }

    /// <summary>A text's embedding by the text model — loaded when first needed, one call at a time. From any thread.</summary>
    private float[] EmbedText(string text)
    {
        lock (this._textEncoderLock)
        {
            this._textEncoder ??= new TextEncoder();
            Interlocked.Exchange(ref this._textEncoderUsedAt, Environment.TickCount64);
            return this._textEncoder.Embed(text);
        }
    }

    /// <summary>The text model released once unused for a while — or at once, <paramref name="force"/>: about 300 MB given back.</summary>
    private void ReleaseTextEncoder(bool force = false)
    {
        lock (this._textEncoderLock)
        {
            if (this._textEncoder is { } encoder && (force || Environment.TickCount64 - Interlocked.Read(ref this._textEncoderUsedAt) >= TextEncoderIdleMs))
            {
                encoder.Dispose();
                this._textEncoder = null;
            }
        }
    }
}

/// <summary>What a similar images search started from: its name for the caption line, its embedding, its file when it has one.</summary>
internal sealed record SimilarSource(string Name, float[] Embedding, string? FullPath);

/// <summary>Which passes the extraction runs: the OCR ones, the faces, the Vision pass, the captions — each its ⚙ setting.</summary>
internal readonly record struct ExtractionSettings(bool Ocr, bool Faces, bool Vision, bool Captions);
