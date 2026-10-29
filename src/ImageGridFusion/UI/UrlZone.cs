using System.Drawing.Drawing2D;
using ImageGridFusion.Imaging;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>
/// The URL zone, the file explorer's last row (see workfiles/20261009-url-content.md § URL Zone): a URL
/// typed or pasted, Load — or Enter — fetches the media behind it into the temp folder, a loader then
/// the media's thumbnail showing in the preview at the left, which is dragged into a cell like a tile.
/// One compact row: the preview, then the field and its button over a caption saying where the load
/// stands. Neither persisted nor in the undo history.
/// </summary>
internal sealed class UrlZone : Panel
{
    /// <summary>The zone's height in logical pixels: the separator's band and the preview.</summary>
    public const int LogicalHeight = SeparatorBand + PreviewSide;

    // In logical pixels: the preview's side, the band above the row holding the separator line, the
    // gap between the preview and the field.
    private const int PreviewSide = 56;
    private const int SeparatorBand = 7;
    private const int PreviewGap = 6;

    private const string Placeholder = "Paste a URL (X post, image, video…)";
    private const string EmptyCaption = "Load a URL, then drag its media into a cell";
    private const string ResolvingCaption = "Looking for the media…";
    private const string InstallLink = "Install yt-dlp…";
    private const string CompanionsLink = "Install yt-dlp companions…";
    private const string MergingCaption = "Joining the video and its sound…";
    private const string GoneCaption = "The downloaded file is gone: load it again";
    private const string ReadyTooltip = "Drag into a cell · right-click for more";

    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
    private readonly UrlPreview _preview = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TableLayoutPanel _fieldRow = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = Padding.Empty };
    private readonly TextBox _url = new() { PlaceholderText = Placeholder, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 3, 0) };
    private readonly Button _load = new() { Text = "Load", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Right, Enabled = false, Margin = Padding.Empty };
    private readonly LinkLabel _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly ToolTip _toolTip = new();

    // A file tile's menu on the ready media (see workfiles/20261014-url-zone-context-menu.md).
    private readonly ContextMenuStrip _menu = new();

    // The load running — a newer one cancels it; the downloaded media once ready; the URL an install
    // offer was made for, loaded again once installed, and what it offers; where the preview was
    // pressed, for its drag.
    private CancellationTokenSource? _loading;
    private string? _path;
    private Uri? _installFor;
    private InstallOffer _offer;
    private Point? _pressedAt;

    public UrlZone()
    {
        this._layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PreviewSide + PreviewGap));
        this._layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        this._layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this._layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        this._fieldRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        this._fieldRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._fieldRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this._fieldRow.Controls.Add(this._url, 0, 0);
        this._fieldRow.Controls.Add(this._load, 1, 0);
        this._layout.Controls.Add(this._preview, 0, 0);
        this._layout.SetRowSpan(this._preview, 2);
        this._layout.Controls.Add(this._fieldRow, 1, 0);
        this._layout.Controls.Add(this._caption, 1, 1);
        this.Controls.Add(this._layout);

        this._toolTip.SetToolTip(this._load, "Fetch the photo or video behind the URL");
        this._url.TextChanged += (_, _) => this._load.Enabled = this._url.Text.Trim().Length > 0;
        this._url.KeyDown += OnUrlKeyDown;
        this._load.Click += async (_, _) => await this.LoadAsync();
        this._caption.LinkClicked += async (_, _) => await (this._offer == InstallOffer.Companions ? this.OfferCompanionsAsync() : this.OfferInstallAsync());
        this._preview.MouseDown += OnPreviewMouseDown;
        this._preview.MouseMove += OnPreviewMouseMove;
        this._preview.MouseUp += (_, _) => this._pressedAt = null;
        this._menu.Items.AddRange([
            this.MenuItem(FileExplorerPanel.CopyText, FileAction.Copy, MenuIcons.Copy),
            this.MenuItem(FileExplorerPanel.CopyPngText, FileAction.CopyPng, MenuIcons.Png),
            this.MenuItem(FileExplorerPanel.CopyFullPathText, FileAction.CopyFullPath, MenuIcons.CopyPath),
            new ToolStripSeparator(),
            this.MenuItem(FileExplorerPanel.OpenFileLocationText, FileAction.OpenFileLocation, MenuIcons.OpenFolder),
        ]);
        MenuIcons.Attach(this._menu);
        this._menu.Opening += (_, e) => e.Cancel = this._preview.State != PreviewState.Ready;
        this._preview.ContextMenuStrip = this._menu;

        this.ApplyMetrics();
        this.ShowEmpty();
    }

    /// <summary>The ready media was dragged past the threshold: its file, to be dragged like a tile's.</summary>
    public event EventHandler<string>? DragRequested;

    /// <summary>An entry of the ready media's menu was chosen: its file, still there, and the entry.</summary>
    public event EventHandler<(string Path, FileAction Action)>? FileActionRequested;

    /// <summary>The entries of the ready media's menu, a file tile's.</summary>
    public enum FileAction
    {
        Copy,
        CopyPng,
        CopyFullPath,
        OpenFileLocation,
    }

    /// <summary>The URL field has the focus: the keys editing text are its own.</summary>
    public bool IsEditingText => this._url.Focused;

    /// <summary>yt-dlp's companions are being installed, from the zone's link or the ⚙ menu.</summary>
    public bool InstallingCompanions { get; private set; }

    /// <summary>
    /// The companions' offer — the zone's link, the ⚙ menu: a confirmation showing the winget command of
    /// each one missing, then their install and, when the offer was made for a URL, its load again;
    /// otherwise the media the zone held comes back. See workfiles/20261015-yt-dlp-best-format.md
    /// § When They Are Offered.
    /// </summary>
    public async Task OfferCompanionsAsync()
    {
        var missing = YtDlpCompanions.Missing;
        if (missing.Count == 0 || this.InstallingCompanions)
        {
            return;
        }

        string names = YtDlpCompanions.Names(missing);
        var answer = MessageBox.Show(
            this.FindForm(),
            "yt-dlp loads the largest formats with its companions: Deno, the JavaScript runtime YouTube's formats need, "
            + "and ffmpeg, which joins a video track and its sound.\n\n"
            + $"Install {names} with winget, each in its own folder next to the app? The app will run:\n\n{YtDlpCompanions.Commands(missing)}\n\n"
            + "This accepts winget's source and package agreements on your behalf.",
            "Install yt-dlp companions",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        Uri? url = this._installFor;
        string? held = this._path;
        this.InstallingCompanions = true;
        try
        {
            await this.RunAsync(url, $"Installing {names}…", async (target, progress, token) =>
            {
                progress.Report(new InstallingStep(names));
                await YtDlpCompanions.InstallAsync(missing, token);
                return target is not null ? await UrlResolver.LoadAsync(target, progress, token)
                    : held is not null && File.Exists(held) ? held
                    : throw new CompanionsInstalledException(names);
            });
        }
        finally
        {
            this.InstallingCompanions = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int y = this.LogicalToDeviceUnits(SeparatorBand) / 2;
        using var pen = new Pen(SystemColors.ControlDark);
        e.Graphics.DrawLine(pen, 0, y, this.Width, y);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        this.ApplyMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.ApplyMetrics();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._loading?.Cancel();
            this._toolTip.Dispose();
            this._menu.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>The preview's column and the separator's band, in device units.</summary>
    private void ApplyMetrics()
    {
        this._layout.ColumnStyles[0].Width = this.LogicalToDeviceUnits(PreviewSide + PreviewGap);
        this._preview.Margin = new Padding(0, 0, this.LogicalToDeviceUnits(PreviewGap), 0);
        this.Padding = new Padding(0, this.LogicalToDeviceUnits(SeparatorBand), 0, 0);
        this.Invalidate();
    }

    private async void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (this._load.Enabled)
            {
                await this.LoadAsync();
            }
        }
    }

    private Task LoadAsync()
    {
        if (UrlResolver.Parse(this._url.Text) is not { } url)
        {
            this.CancelLoad();
            this.ShowFailed("Not a web address");
            return Task.CompletedTask;
        }

        return this.RunAsync(url, ResolvingCaption, (target, progress, token) => UrlResolver.LoadAsync(target!, progress, token));
    }

    /// <summary>
    /// The install offer's link: a confirmation showing the winget command, then the install and, once
    /// yt-dlp is there, the load it was offered for again.
    /// </summary>
    private async Task OfferInstallAsync()
    {
        if (this._installFor is not { } url)
        {
            return;
        }

        // The companions missing are installed with it, in the same confirmation.
        var companions = YtDlpCompanions.Missing;
        string commands = string.Join("\n\n", [YtDlp.InstallCommand, .. companions.Select(t => t.InstallCommand)]);
        string with = companions.Count == 0 ? ""
            : $"\n\nWith its companions, {YtDlpCompanions.Names(companions)}, each in its own folder next to the app: "
              + "Deno, the JavaScript runtime YouTube's formats need, and ffmpeg, which joins a video track and its sound.";
        var answer = MessageBox.Show(
            this.FindForm(),
            "This site's media is loaded with yt-dlp, which isn't installed.\n\n"
            + $"Install it in {YtDlp.Folder} with winget?{with}\n\nThe app will run:\n\n{commands}\n\n"
            + "This accepts winget's source and package agreements on your behalf.",
            "Install yt-dlp",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await this.RunAsync(url, "Installing yt-dlp…", async (target, progress, token) =>
        {
            progress.Report(new InstallingStep("yt-dlp"));
            await YtDlp.InstallAsync(token);
            if (companions.Count > 0)
            {
                progress.Report(new InstallingStep(YtDlpCompanions.Names(companions)));
                await YtDlpCompanions.InstallAsync(companions, token);
            }

            return await UrlResolver.LoadAsync(target!, progress, token);
        });
    }

    /// <summary>
    /// Runs one load — cancelling the one before — showing its steps, then its thumbnail or why it failed.
    /// Only the latest load touches the zone.
    /// </summary>
    private async Task RunAsync(Uri? url, string caption, Func<Uri?, IProgress<UrlLoadStep>, CancellationToken, Task<string>> load)
    {
        this.CancelLoad();
        var cts = new CancellationTokenSource();
        this._loading = cts;
        this._path = null;
        this._installFor = null;
        this.ShowBusy(caption);
        int side = this.LogicalToDeviceUnits(PreviewSide);

        // yt-dlp loaded it without a companion: the file may be smaller than the site offers.
        bool withoutCompanions = false;
        var progress = new Progress<UrlLoadStep>(step =>
        {
            if (this._loading == cts)
            {
                withoutCompanions |= step is YtDlpStep { CompanionsMissing: true };
                this.OnStep(step, cts);
            }
        });
        try
        {
            string path = await Task.Run(() => load(url, progress, cts.Token), cts.Token);
            Bitmap? thumbnail = await Task.Run(() => Thumbnail(path, side));
            if (this._loading != cts)
            {
                thumbnail?.Dispose();
                return;
            }

            this.ShowReady(path, thumbnail);
            if (withoutCompanions && url is not null && YtDlpCompanions.Missing.Count > 0)
            {
                this.ShowCompanionsHint(url, path);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer load took over, or the zone closed.
        }
        catch (YtDlpMissingException) when (this._loading == cts && url is not null)
        {
            this.ShowInstallOffer(url);
        }
        catch (YtDlpCompanionsMissingException e) when (this._loading == cts && url is not null)
        {
            this.ShowCompanionsOffer(url, e.Message);
        }
        catch (CompanionsInstalledException e) when (this._loading == cts)
        {
            this.ShowEmpty();
            this.SetCaption(e.Message, error: false);
        }
        catch (UrlLoadException e) when (this._loading == cts)
        {
            this.ShowFailed(e.Message);
        }
        catch (Exception e) when (this._loading == cts && e is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException)
        {
            this.ShowFailed(e.Message);
        }
        finally
        {
            if (this._loading == cts)
            {
                this._loading = null;
            }

            cts.Dispose();
        }
    }

    private void CancelLoad()
    {
        this._loading?.Cancel();
        this._loading = null;
    }

    private void OnStep(UrlLoadStep step, CancellationTokenSource cts)
    {
        switch (step)
        {
            case ResolvingStep:
                this.SetCaption(ResolvingCaption, error: false);
                break;
            case InstallingStep installing:
                this.SetCaption($"Installing {installing.Name}…", error: false);
                break;
            case MergingStep:
                this.SetCaption(MergingCaption, error: false);
                break;
            case PosterStep poster:
                _ = this.ShowPosterAsync(poster.Url, cts);
                break;
            case DownloadingStep downloading:
                this.SetCaption(DownloadCaption(downloading), error: false);
                break;
        }
    }

    private static string DownloadCaption(DownloadingStep step) =>
        step.Percent is { } percent ? $"Downloading… {percent:0} %"
        : step.Total is > 0 and { } total ? $"Downloading… {100.0 * step.Received / total:0} %"
        : $"Downloading… {Megabytes(step.Received)}";

    private static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024):0.0} MB";

    /// <summary>The poster, under the loader while the media downloads; a poster that cannot be read is no matter.</summary>
    private async Task ShowPosterAsync(Uri url, CancellationTokenSource cts)
    {
        int side = this.LogicalToDeviceUnits(PreviewSide);
        Bitmap? poster = null;
        try
        {
            byte[] bytes = await Downloader.Client.GetByteArrayAsync(url, cts.Token);
            poster = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                using var image = Image.FromStream(stream);
                return Fit(image, side);
            });
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or ArgumentException or ObjectDisposedException)
        {
            return;
        }

        if (poster is null)
        {
            return;
        }

        if (this._loading == cts && this._preview.State == PreviewState.Busy)
        {
            this._preview.SetImage(poster);
        }
        else
        {
            poster.Dispose();
        }
    }

    /// <summary>Windows' thumbnail of the file, else its first frame as the app loads it, fitted to the preview.</summary>
    private static Bitmap? Thumbnail(string path, int side)
    {
        try
        {
            using var shell = ShellThumbnail.TryLoad(path, side);
            if (shell is not null)
            {
                return Fit(shell, side);
            }

            using var source = ImageLoader.TryLoadFile(path);
            return source is null ? null : Fit(source.Bitmap, side);
        }
        catch (Exception e) when (e is ArgumentException or OutOfMemoryException or IOException or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

    /// <summary><paramref name="image"/> scaled to fit a square of <paramref name="side"/>, its ratio kept.</summary>
    private static Bitmap Fit(Image image, int side)
    {
        double scale = Math.Min((double)side / image.Width, (double)side / image.Height);
        var size = new Size(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
        var fitted = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(fitted);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, new Rectangle(Point.Empty, size));
        return fitted;
    }

    private void ShowEmpty()
    {
        this._preview.Display(PreviewState.Empty, null, video: false);
        this._toolTip.SetToolTip(this._preview, null);
        this.SetCaption(EmptyCaption, error: false, dim: true);
    }

    private void ShowBusy(string caption)
    {
        this._preview.Display(PreviewState.Busy, null, video: false);
        this._toolTip.SetToolTip(this._preview, null);
        this.SetCaption(caption, error: false);
    }

    private void ShowReady(string path, Bitmap? thumbnail)
    {
        this._path = path;
        this._preview.Display(PreviewState.Ready, thumbnail, VideoFrames.IsVideoFile(path));
        this._toolTip.SetToolTip(this._preview, ReadyTooltip);
        long size = new FileInfo(path).Length;
        this.SetCaption($"{Path.GetFileName(path)} · {Megabytes(size)}", error: false);
    }

    private void ShowFailed(string message)
    {
        this._path = null;
        this._preview.Display(PreviewState.Failed, null, video: false);
        this._toolTip.SetToolTip(this._preview, message);
        this.SetCaption(message, error: true);
    }

    /// <summary>The site needs yt-dlp, missing: the caption's link offers to install it, then loads this URL again.</summary>
    private void ShowInstallOffer(Uri url)
    {
        this.ShowFailed($"{InstallLink} This site needs yt-dlp");
        this._installFor = url;
        this._offer = InstallOffer.YtDlp;
        this._caption.Links.Add(0, InstallLink.Length);
    }

    /// <summary>yt-dlp failed without a companion: the caption's link offers to install them, then loads this URL again.</summary>
    private void ShowCompanionsOffer(Uri url, string reason)
    {
        this.ShowFailed($"{CompanionsLink} {reason}");
        this._installFor = url;
        this._offer = InstallOffer.Companions;
        this._caption.Links.Add(0, CompanionsLink.Length);
    }

    /// <summary>
    /// The media is ready, loaded by yt-dlp without a companion: the caption's link offers to install
    /// them for a larger format, then loads this URL again; its file stays draggable meanwhile.
    /// </summary>
    private void ShowCompanionsHint(Uri url, string path)
    {
        long size = new FileInfo(path).Length;
        this.SetCaption($"{CompanionsLink} for a larger format · {Path.GetFileName(path)} · {Megabytes(size)}", error: false);
        this._installFor = url;
        this._offer = InstallOffer.Companions;
        this._caption.Links.Add(0, CompanionsLink.Length);
    }

    /// <summary>The caption's text, no link in it, its whole text in its tooltip.</summary>
    private void SetCaption(string text, bool error, bool dim = false)
    {
        this._caption.Text = text;
        this._caption.Links.Clear();
        this._caption.ForeColor = error ? Color.Firebrick : dim ? SystemColors.GrayText : SystemColors.ControlText;
        this._toolTip.SetToolTip(this._caption, text);
    }

    private ToolStripMenuItem MenuItem(string text, FileAction action, MenuIcon icon)
    {
        var item = new ToolStripMenuItem(text, null, (_, _) => this.RequestFileAction(action));
        MenuIcons.Set(item, icon);
        return item;
    }

    /// <summary>A menu entry on the downloaded file, gone from the temp folder: nothing done, the zone saying so.</summary>
    private void RequestFileAction(FileAction action)
    {
        if (this._path is { } path && File.Exists(path))
        {
            this.FileActionRequested?.Invoke(this, (path, action));
        }
        else
        {
            this.ShowFailed(GoneCaption);
        }
    }

    private void OnPreviewMouseDown(object? sender, MouseEventArgs e) =>
        this._pressedAt = e.Button == MouseButtons.Left && this._preview.State == PreviewState.Ready ? e.Location : null;

    private void OnPreviewMouseMove(object? sender, MouseEventArgs e)
    {
        if (this._pressedAt is not { } start || e.Button != MouseButtons.Left)
        {
            return;
        }

        var threshold = SystemInformation.DragSize;
        if (Math.Abs(e.X - start.X) < threshold.Width && Math.Abs(e.Y - start.Y) < threshold.Height)
        {
            return;
        }

        this._pressedAt = null;
        if (this._path is { } path && File.Exists(path))
        {
            this.DragRequested?.Invoke(this, path);
        }
        else
        {
            this.ShowFailed(GoneCaption);
        }
    }

    /// <summary>What the caption's link offers to install.</summary>
    private enum InstallOffer
    {
        YtDlp,
        Companions,
    }

    /// <summary>The companions installed from the ⚙ menu with no URL to load again: the zone says so.</summary>
    private sealed class CompanionsInstalledException(string names) : Exception($"{names} installed: yt-dlp now loads the largest formats");

    private enum PreviewState
    {
        Empty,
        Busy,
        Ready,
        Failed,
    }

    /// <summary>
    /// The zone's preview: a dim 🔗 while empty, a loader over the poster while busy, the media's
    /// thumbnail — a ▶ badge on a video — once ready, a dim ⚠ after a failure.
    /// </summary>
    private sealed class UrlPreview : Control
    {
        private const int SpinnerInterval = 50;
        private const int SpinnerStep = 30;

        private readonly System.Windows.Forms.Timer _spinner = new() { Interval = SpinnerInterval };
        private Bitmap? _image;
        private bool _video;
        private int _angle;

        public UrlPreview()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this._spinner.Tick += (_, _) =>
            {
                this._angle = (this._angle + SpinnerStep) % 360;
                this.Invalidate();
            };
        }

        public PreviewState State { get; private set; }

        public void Display(PreviewState state, Bitmap? image, bool video)
        {
            this.State = state;
            this._video = video;
            this.SetImage(image);
            this._spinner.Enabled = state == PreviewState.Busy;
            this.Cursor = state == PreviewState.Ready ? Cursors.SizeAll : Cursors.Default;
        }

        /// <summary>Takes <paramref name="image"/>, disposing the one shown before.</summary>
        public void SetImage(Bitmap? image)
        {
            if (!ReferenceEquals(image, this._image))
            {
                this._image?.Dispose();
                this._image = image;
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var box = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            Checkerboard.Fill(g, this.ClientRectangle);
            if (this._image is not null)
            {
                var at = new Point((this.Width - this._image.Width) / 2, (this.Height - this._image.Height) / 2);
                g.DrawImage(this._image, new Rectangle(at, this._image.Size));
            }

            switch (this.State)
            {
                case PreviewState.Empty:
                    this.PaintGlyph(g, "🔗");
                    break;
                case PreviewState.Failed:
                    this.PaintGlyph(g, "⚠");
                    break;
                case PreviewState.Busy:
                    this.PaintSpinner(g);
                    break;
                case PreviewState.Ready when this._video:
                    this.PaintPlayBadge(g);
                    break;
            }

            using var border = new Pen(SystemColors.ControlDark);
            g.DrawRectangle(border, box);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._spinner.Dispose();
                this._image?.Dispose();
                this._image = null;
            }

            base.Dispose(disposing);
        }

        private void PaintGlyph(Graphics g, string glyph)
        {
            using var font = new Font("Segoe UI Symbol", this.Font.SizeInPoints + 6f);
            TextRenderer.DrawText(g, glyph, font, this.ClientRectangle, SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>The loader: the poster faded, an arc turning over it.</summary>
        private void PaintSpinner(Graphics g)
        {
            if (this._image is not null)
            {
                using var veil = new SolidBrush(Color.FromArgb(150, SystemColors.Window));
                g.FillRectangle(veil, this.ClientRectangle);
            }

            int diameter = Math.Min(this.Width, this.Height) / 2;
            var ring = new Rectangle((this.Width - diameter) / 2, (this.Height - diameter) / 2, diameter, diameter);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(SystemColors.Highlight, Math.Max(2, diameter / 8)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, ring, this._angle, 270);
        }

        /// <summary>The ▶ badge in the bottom-left corner: a dark disc, a white triangle.</summary>
        private void PaintPlayBadge(Graphics g)
        {
            int diameter = Math.Max(12, Math.Min(this.Width, this.Height) / 3);
            int inset = Math.Max(2, diameter / 6);
            var disc = new Rectangle(inset, this.Height - inset - diameter, diameter, diameter);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var back = new SolidBrush(Color.FromArgb(180, Color.Black));
            g.FillEllipse(back, disc);
            float r = diameter / 4f;
            float cx = disc.X + diameter / 2f + r / 4f;
            float cy = disc.Y + diameter / 2f;
            using var triangle = new SolidBrush(Color.White);
            g.FillPolygon(triangle, [new PointF(cx - r, cy - r), new PointF(cx - r, cy + r), new PointF(cx + r, cy)]);
        }
    }
}
