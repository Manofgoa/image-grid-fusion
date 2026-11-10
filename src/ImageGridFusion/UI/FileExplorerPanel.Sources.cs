using System.ComponentModel;
using ImageGridFusion.Explorer;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer's secondary sources (workfiles/20261105-secondary-file-sources.md): other folders on disk,
/// shown in blue as folders of the base folder's root in the folder view, each with an index of its own under
/// <see cref="SecondarySources.Folder"/> — its list of files read when it is opened, its content analysed by 🔄
/// inside it only, its run paused when it is left — searched only while it is open. Their faces join the People
/// view, their images the similar images; nothing else outside them ever reads them.
/// </summary>
internal sealed partial class FileExplorerPanel
{
    // The sources, in the settings' order; the one the folder view has open (null: the base folder's); the one
    // whose passes run, the base folder's waiting meanwhile; whether 🔄 may rescan the base folder.
    private readonly List<SourceRoot> _sources = [];
    private SourceRoot? _openSource;
    private SourceRoot? _sourceRun;
    private bool _baseRescanEnabled;

    /// <summary>
    /// The secondary sources — their folders, as full paths, and their colors: set from the settings at start-up and
    /// by the sources window. A new one has its faces and embeddings loaded from its index — never its folder read —
    /// a removed one its runs stopped and its index deleted, a kept one takes its color. Setting it raises nothing.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SecondarySource> SecondarySourceEntries
    {
        get => [.. this._sources.Select(source => new SecondarySource(source.Folder, source.Color))];
        set => this.ApplySources(value);
    }

    /// <summary>The base folder as the explorer holds it, normalized; null when none is chosen.</summary>
    public string? BaseFolder => this._baseFolder;

    /// <summary>A secondary source: its folder, its own index files, and what the panel holds of them.</summary>
    private sealed class SourceRoot(string folder)
    {
        public string Folder { get; } = folder;

        public IndexFiles Files { get; } = IndexFiles.Of(folder);

        /// <summary>Its name on its tile and in the breadcrumb: the folder's own, a drive root's path.</summary>
        public string Name => Path.GetFileName(this.Folder) is { Length: > 0 } name ? name : this.Folder;

        /// <summary>Its color, an index into <see cref="SourceColors.Palette"/>, kept in the settings.</summary>
        public int Color { get; set; }

        /// <summary>What its color draws with: its tile, its name in the breadcrumb, the status line while it is open.</summary>
        public SourceColor Colors => SourceColors.Of(this.Color);

        public FileIndex? Index { get; set; }

        public FolderTree? Tree { get; set; }

        public ContentIndex? Contents { get; set; }

        public FaceIndex? Faces { get; set; }

        public VisionIndex? Vision { get; set; }

        /// <summary>Its index's entries by full path, built on first use, dropped with the index.</summary>
        public Dictionary<string, IndexEntry>? ByPath { get; set; }

        public CancellationTokenSource? Scan { get; set; }

        public CancellationTokenSource? Extraction { get; set; }

        /// <summary>What still writes into its index files: its scan, its passes — awaited before they are deleted.</summary>
        public List<Task> Work { get; } = [];

        public bool Scanning { get; set; }

        public bool Removed { get; set; }

        /// <summary>Its index set, the folders derived from it and the entries by path dropped.</summary>
        public void SetIndex(FileIndex? index)
        {
            this.Index = index;
            this.Tree = null;
            this.ByPath = null;
        }

        public FolderTree? TreeOf() => this.Tree ??= this.Index is { } index ? FolderTree.Of(index.Entries) : null;

        public IndexEntry? EntryOf(string fullPath)
        {
            if (this.Index is not { } index)
            {
                return null;
            }

            this.ByPath ??= index.Entries.ToDictionary(index.FullPath, StringComparer.OrdinalIgnoreCase);
            return this.ByPath.GetValueOrDefault(fullPath);
        }
    }

    /// <summary>The source the folder view shows; null in the base folder, or in another view.</summary>
    private SourceRoot? ViewSource => this._folderView ? this._openSource : null;

    /// <summary>The folder the open folder is relative to: the open source's, else the base folder.</summary>
    private string? ViewRoot => this.ViewSource?.Folder ?? this._baseFolder;

    /// <summary>The index the folder view reads: the open source's, else the base folder's.</summary>
    private FileIndex? ViewIndex => this.ViewSource is { } source ? source.Index : this._index;

    /// <summary>The content texts the folder view's searches read: the open source's, else the base folder's.</summary>
    private ContentIndex? ViewContents => this.ViewSource is { } source ? source.Contents : this._contents;

    /// <summary>The embeddings the folder view's searches read: the open source's, else the base folder's.</summary>
    private VisionIndex? ViewVision => this.ViewSource is { } source ? source.Vision : this._vision;

    /// <summary>The folders of the index the folder view reads, derived once.</summary>
    private FolderTree? ViewTree()
    {
        if (this.ViewSource is { } source)
        {
            return source.TreeOf();
        }

        return this._index is { } index ? this._tree ??= FolderTree.Of(index.Entries) : null;
    }

    /// <summary>A file's entry in the index the folder view reads.</summary>
    private IndexEntry? ViewEntryOf(string fullPath) => this.ViewSource is { } source ? source.EntryOf(fullPath) : this.EntryOf(fullPath);

    /// <summary>
    /// Where a file or folder belongs: the base folder (<c>Source</c> null) or a source, and its path relative
    /// to it; null for one outside them all.
    /// </summary>
    private (SourceRoot? Source, string Relative)? Locate(string fullPath)
    {
        if (this._baseFolder is { } root && SecondarySources.Holds(root, fullPath))
        {
            return (null, Path.GetRelativePath(root, fullPath));
        }

        foreach (var source in this._sources)
        {
            if (SecondarySources.Holds(source.Folder, fullPath))
            {
                return (source, Path.GetRelativePath(source.Folder, fullPath));
            }
        }

        return null;
    }

    private FileIndex? IndexAt(SourceRoot? source) => source is null ? this._index : source.Index;

    private ContentIndex? ContentsAt(SourceRoot? source) => source is null ? this._contents : source.Contents;

    private FaceIndex? FacesAt(SourceRoot? source) => source is null ? this._faces : source.Faces;

    private VisionIndex? VisionAt(SourceRoot? source) => source is null ? this._vision : source.Vision;

    private FolderTree? TreeAt(SourceRoot? source) =>
        source is not null ? source.TreeOf() : this._index is { } index ? this._tree ??= FolderTree.Of(index.Entries) : null;

    /// <summary>A file's entry in whichever index holds it — the base folder's or a source's — with that source.</summary>
    private (IndexEntry Entry, SourceRoot? Source)? AnyEntryOf(string fullPath)
    {
        if (this.EntryOf(fullPath) is { } entry)
        {
            return (entry, null);
        }

        foreach (var source in this._sources)
        {
            if (source.EntryOf(fullPath) is { } found)
            {
                return (found, source);
            }
        }

        return null;
    }

    /// <summary>The source whose folder is exactly <paramref name="fullPath"/>; null for any other.</summary>
    private SourceRoot? SourceAt(string fullPath) =>
        this._sources.FirstOrDefault(source => string.Equals(source.Folder, Path.TrimEndingDirectorySeparator(fullPath), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The sources set: the new ones' faces and embeddings loaded, the removed ones stopped and their index
    /// deleted — the folder view leaving a removed one it showed — the groups and the list made again.
    /// </summary>
    private void ApplySources(IReadOnlyList<SecondarySource> sources)
    {
        var wanted = new List<string>();
        var colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in sources)
        {
            try
            {
                string normalized = FileIndex.NormalizeFolder(entry.Folder);
                if (!wanted.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    wanted.Add(normalized);
                    colors[normalized] = entry.Color;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        bool leftOpen = false;
        foreach (var source in this._sources.Where(s => !wanted.Contains(s.Folder, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            this._sources.Remove(source);
            source.Removed = true;
            source.Scan?.Cancel();
            source.Extraction?.Cancel();
            if (this._sourceRun == source)
            {
                this._sourceRun = null;
            }

            if (this._openSource == source)
            {
                this._openSource = null;
                this._openFolder = "";
                leftOpen = true;
            }

            _ = this.DeleteSourceIndexAsync(source);
        }

        var kept = this._sources.ToDictionary(source => source.Folder, StringComparer.OrdinalIgnoreCase);
        this._sources.Clear();
        foreach (string folder in wanted)
        {
            if (kept.TryGetValue(folder, out var source))
            {
                source.Color = colors[folder];
                this._sources.Add(source);
                continue;
            }

            var added = new SourceRoot(folder) { Color = colors[folder] };
            this._sources.Add(added);
            _ = this.LoadSourceStoresAsync(added);
        }

        this.Regroup(force: true);
        if (this._folderView)
        {
            this.RefreshRows(keepPlace: !leftOpen);
        }

        if (leftOpen)
        {
            this.FolderViewChanged?.Invoke(this, EventArgs.Empty);
            this.StartExtraction();
        }
    }

    /// <summary>
    /// A removed source's index deleted once what still writes into it is over — its folder on disk never
    /// touched; the status line says when it cannot be.
    /// </summary>
    private async Task DeleteSourceIndexAsync(SourceRoot source)
    {
        try
        {
            await Task.WhenAll(source.Work.ToArray());
        }
        catch (Exception)
        {
            // Their own failures were said, or are moot: the index goes.
        }

        try
        {
            await Task.Run(() => SecondarySources.DeleteIndex(source.Folder));
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            if (!this.IsDisposed)
            {
                this.Report(Texts.Format(Strings.SourcesIndexNotDeleted, source.Name, ex.Message), error: true);
            }
        }
    }

    /// <summary>
    /// A source's faces and embeddings, with its index to turn their keys into full paths, loaded from its index
    /// files off the UI thread — its folder never read — for the People view and the similar images.
    /// </summary>
    private async Task LoadSourceStoresAsync(SourceRoot source)
    {
        var (index, faces, vision) = await Task.Run(() => (
            FileIndex.Load(source.Files.Index, source.Folder),
            FaceIndex.Load(source.Files.Faces, source.Folder),
            VisionIndex.Load(source.Files.Vision, source.Folder)));
        if (source.Removed || this.IsDisposed)
        {
            return;
        }

        if (source.Index is null)
        {
            source.SetIndex(index);
        }

        source.Faces ??= faces;
        source.Vision ??= vision;
        this.Regroup(force: true);
    }

    /// <summary>The source tiles of the base folder's root, in the settings' order — each in its color, faded when its folder is not found.</summary>
    private List<ExplorerRow> SourceRows(IReadOnlyList<bool> found) =>
        [.. this._sources.Select((source, i) => new ExplorerRow(source.Folder, source.Name, IsFolder: true, Count: source.Index?.Count, Source: source.Colors, Unavailable: i < found.Count && !found[i]))];

    /// <summary>
    /// A source opened from its tile: the folder view shows its root — any source's run left paused — its cached
    /// list of files and its texts loaded, then its folder read again behind them.
    /// </summary>
    private void OpenSource(SourceRoot source)
    {
        this.SetOpenSource(source);
        this.Navigate("");
        _ = this.LoadAndScanSourceAsync(source, analyse: false);
    }

    /// <summary>Back from a source to the base folder's root, the source's tile selected.</summary>
    private void LeaveSource()
    {
        if (this._openSource is not { } source)
        {
            return;
        }

        this.SetOpenSource(null);
        this.Navigate("", source.Folder);
    }

    /// <summary>The source the folder view has open: one left behind has its run paused (§ 🔄 Inside a Source).</summary>
    private void SetOpenSource(SourceRoot? source)
    {
        if (source != this._openSource)
        {
            this.PauseSourceRun();
        }

        this._openSource = source;
        this.UpdateRescan();
        if (!this._transient.Enabled)
        {
            // The status line takes the source's color and its index's summary, or gives them back.
            this.ShowSummary();
        }
    }

    /// <summary>
    /// A source's run paused — left, another view shown: its passes cancelled, what they did kept, so the next 🔄
    /// there picks up where they stopped; the base folder's passes then resume.
    /// </summary>
    private void PauseSourceRun()
    {
        if (this._sourceRun is not { } running)
        {
            return;
        }

        this._sourceRun = null;
        running.Extraction?.Cancel();
    }

    /// <summary>
    /// 🔄 enabled as the folder view stands — inside a source, while its folder is not being read; elsewhere, as
    /// the base folder's scan left it — and its tooltip saying what it does there: inside a source's subfolder, that
    /// folder named (workfiles/20261110-secondary-source-subfolder-rescan.md § 🔄 Tooltip).
    /// </summary>
    private void UpdateRescan()
    {
        if (this.ViewSource is { } source)
        {
            this._rescan.Enabled = !source.Scanning;
            this._toolTip.SetToolTip(
                this._rescan,
                this._openFolder.Length == 0 ? Strings.SourcesTipRescan : Texts.Format(Strings.SourcesTipRescanFolder, Path.GetFileName(this._openFolder)));
        }
        else
        {
            this._rescan.Enabled = this._baseRescanEnabled;
            this._toolTip.SetToolTip(this._rescan, Strings.ExplorerTipRescan);
        }
    }

    /// <summary>The base folder's 🔄 enabled or not, the button following unless a source shows.</summary>
    private void SetBaseRescan(bool enabled)
    {
        this._baseRescanEnabled = enabled;
        this.UpdateRescan();
    }

    /// <summary>
    /// A source's cached texts, faces and embeddings loaded when missing, then its folder read again — the list
    /// shown following — and, for 🔄 (<paramref name="analyse"/>), its passes run once its list is there, if it is
    /// still the one shown. <paramref name="scope"/>, the folder 🔄 was clicked in, limits both to it and its
    /// subfolders — its nearest parent still on the disk when it is gone, the view going there; <c>""</c>, or a
    /// source without an index yet, the whole source (workfiles/20261110-secondary-source-subfolder-rescan.md).
    /// </summary>
    private async Task LoadAndScanSourceAsync(SourceRoot source, bool analyse, string scope = "")
    {
        if (source.Contents is null || source.Faces is null || source.Vision is null)
        {
            var (contents, faces, vision) = await Task.Run(() => (
                source.Contents is null ? ContentIndex.Load(source.Files.Content, source.Folder) : null,
                source.Faces is null ? FaceIndex.Load(source.Files.Faces, source.Folder) : null,
                source.Vision is null ? VisionIndex.Load(source.Files.Vision, source.Folder) : null));
            if (source.Removed || this.IsDisposed)
            {
                return;
            }

            source.Contents ??= contents;
            source.Faces ??= faces;
            source.Vision ??= vision;
            this.RefreshContentResults();
        }

        if (scope.Length > 0)
        {
            string asked = scope;
            scope = source.Index is null ? "" : await Task.Run(() => NearestFolder(source.Folder, asked));
            if (source.Removed || this.IsDisposed)
            {
                return;
            }

            if (!string.Equals(scope, asked, StringComparison.OrdinalIgnoreCase) && this.ViewSource == source
                && string.Equals(this._openFolder, asked, StringComparison.OrdinalIgnoreCase))
            {
                this.Navigate(scope);
            }
        }

        bool scanned = await this.ScanSourceAsync(source, scope);
        if (analyse && scanned && !this.IsDisposed && this.ViewSource == source)
        {
            this.StartSourceRun(source, scope);
        }
    }

    /// <summary>
    /// <paramref name="relative"/>, a folder of <paramref name="root"/>, or its nearest parent still on the disk —
    /// <c>""</c>, the root, at worst.
    /// </summary>
    private static string NearestFolder(string root, string relative)
    {
        while (relative.Length > 0 && !Directory.Exists(Path.Combine(root, relative)))
        {
            relative = FolderTree.Parent(relative);
        }

        return relative;
    }

    /// <summary>
    /// A source's folder scanned off the UI thread — the progress on the status line and the first bar while it
    /// shows — its index written into its own files, then taken over unless the source was scanned again or
    /// removed meanwhile; true once taken over. A <paramref name="scope"/> reads that folder and its subfolders only,
    /// merged into the source's index (<see cref="FileIndex.WithFolder"/>).
    /// </summary>
    private async Task<bool> ScanSourceAsync(SourceRoot source, string scope = "")
    {
        var current = source.Index;
        if (current is null)
        {
            scope = "";
        }

        source.Scan?.Cancel();
        var scan = new CancellationTokenSource();
        source.Scan = scan;
        var cancellation = scan.Token;
        source.Scanning = true;
        this.UpdateRescan();
        bool Shown() => !this.IsDisposed && this.ViewSource == source;
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!cancellation.IsCancellationRequested && Shown())
            {
                this.ShowStatus(p.Phase == ScanPhase.Counting ? Texts.Format(Strings.ExplorerCounting, p.Done) : Texts.Format(Strings.ExplorerIndexing, p.Done, p.Total));
                if (p.Phase == ScanPhase.Counting || p.Total == 0)
                {
                    this._progress.ShowUnknown();
                }
                else
                {
                    this._progress.ShowProgress((double)p.Done / p.Total);
                }
            }
        });
        var work = Task.Run(
            () =>
            {
                if (!Directory.Exists(source.Folder))
                {
                    throw new DirectoryNotFoundException(Texts.Format(Strings.ExplorerFolderNotFoundNamed, source.Folder));
                }

                var scanned = scope.Length == 0
                    ? FileIndex.Scan(source.Folder, progress, cancellation)
                    : current!.WithFolder(scope, FileIndex.Scan(source.Folder, scope, progress, cancellation));
                string? error = null;
                try
                {
                    scanned.Save(source.Files.Index);
                }
                catch (Exception ex) when (FileIndex.IsFileError(ex))
                {
                    error = ex.Message;
                }

                return (scanned, error);
            },
            cancellation);
        source.Work.Add(work);
        try
        {
            var (index, saveError) = await work;
            if (cancellation.IsCancellationRequested || source.Removed || this.IsDisposed)
            {
                return false;
            }

            source.SetIndex(index);
            this.Regroup();
            if (Shown())
            {
                if (saveError is not null)
                {
                    this.ShowTransient(Texts.Format(Strings.ExplorerSummaryNotSaved, Summary(index), saveError), error: true);
                }

                this.RefreshRows(keepPlace: true);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            if (Shown())
            {
                this.ShowTransient(Texts.Format(Strings.ExplorerScanFailed, ex.Message), error: true);
            }

            return false;
        }
        finally
        {
            source.Work.Remove(work);
            if (source.Scan == scan)
            {
                source.Scan = null;
                source.Scanning = false;
            }

            scan.Dispose();
            if (!this.IsDisposed)
            {
                if (Shown() && !this._scanning)
                {
                    this._progress.HideProgress();
                    this.ShowSummary();
                }

                this.UpdateRescan();
            }
        }
    }

    /// <summary>
    /// 🔄 in a source: its passes run over it — each under its ⚙ setting, as the base folder's — the base folder's
    /// waiting meanwhile and any other source's paused; they resume once it ends or is paused. Nothing turned on:
    /// the status line says so. The passes read the files under <paramref name="scope"/> only, <c>""</c> the whole source.
    /// </summary>
    private void StartSourceRun(SourceRoot source, string scope)
    {
        if (!(this._contentSearch || this._faceAnalysis || this._visualAnalysis || this._captions))
        {
            this.ShowTransient(Strings.SourcesNothingToAnalyse);
            return;
        }

        if (source.Index is not { } index || source.Contents is not { } contents || source.Faces is not { } faces || source.Vision is not { } vision)
        {
            return;
        }

        this.StopExtraction();
        var extraction = new CancellationTokenSource();
        source.Extraction = extraction;
        this._sourceRun = source;
        var passes = new ExtractionSettings(this._contentSearch, this._faceAnalysis, this._visualAnalysis, this._captions);
        var run = this.RunSourceAsync(source, index, contents, faces, vision, passes, scope, extraction);
        source.Work.Add(run);
        _ = run.ContinueWith(_ => source.Work.Remove(run), TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task RunSourceAsync(SourceRoot source, FileIndex index, ContentIndex contents, FaceIndex faces, VisionIndex vision, ExtractionSettings passes, string scope, CancellationTokenSource extraction)
    {
        try
        {
            await this.ExtractAsync(index, contents, faces, vision, source.Files, passes, scope, extraction.Token);
        }
        finally
        {
            if (this._sourceRun == source)
            {
                this._sourceRun = null;
            }

            if (source.Extraction == extraction)
            {
                source.Extraction = null;
            }

            extraction.Dispose();
            if (!this.IsDisposed)
            {
                // The base folder's passes take their turn again.
                this.StartExtraction();
            }
        }
    }

    /// <summary>Every source's scan and run stopped: the panel is going.</summary>
    private void StopSources()
    {
        foreach (var source in this._sources)
        {
            source.Scan?.Cancel();
            source.Extraction?.Cancel();
        }
    }

    /// <summary>
    /// The sources' faces forgotten with the base folder's (Forget faces…): each loaded store emptied, every
    /// source's face file deleted; the first failure given back.
    /// </summary>
    private string? ForgetSourceFaces()
    {
        string? error = null;
        foreach (var source in this._sources)
        {
            source.Faces?.Clear();
            try
            {
                FaceIndex.Delete(source.Files.Faces);
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                error ??= ex.Message;
            }
        }

        return error;
    }

    /// <summary>
    /// The sources' visual analysis forgotten with the base folder's (Forget visual analysis…): each embeddings file
    /// deleted, each source's tags and captions emptied — those whose texts are not loaded, off the UI thread — the
    /// first failure given back.
    /// </summary>
    private string? ForgetSourceVision()
    {
        string? error = null;
        foreach (var source in this._sources)
        {
            source.Vision?.Clear();
            try
            {
                VisionIndex.Delete(source.Files.Vision);
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                error ??= ex.Message;
            }

            if (source.Contents is { } contents)
            {
                if (contents.ForgetSeen())
                {
                    TrySave(contents, source.Files);
                }
            }
            else if (File.Exists(source.Files.Content))
            {
                var files = source.Files;
                string folder = source.Folder;
                _ = Task.Run(() =>
                {
                    var stored = ContentIndex.Load(files.Content, folder);
                    if (stored.ForgetSeen())
                    {
                        TrySave(stored, files);
                    }
                });
            }
        }

        return error;
    }
}
