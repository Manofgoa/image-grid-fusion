using System.Globalization;
using System.Windows.Forms.VisualStyles;
using ImageGridFusion.Components;
using ImageGridFusion.Explorer;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>What the Component updates window asks of the app around an install.</summary>
internal interface IComponentsHost
{
    /// <summary>Releases what holds <paramref name="component"/>'s files — its engine's sessions, the content passes — before they are swapped.</summary>
    Task ReleaseAsync(Component component);

    /// <summary><paramref name="component"/>'s files changed, or the swap is over: what was released resumes, the features using it refresh.</summary>
    void Changed(Component component);

    /// <summary>Starts the app again, the swaps left for the next start done before anything loads them.</summary>
    void Restart();
}

/// <summary>
/// The Component updates window: every downloadable component grouped by kind, with its installed, pinned, forced and
/// latest versions and their dates; each installed, updated or put back to another version on its own row, several at
/// once, the UI never waiting. The latest versions are checked when it opens and on Check now. Modeless, owned by the
/// main window, one at a time; closing it while an install runs asks first. Every install of the app goes through it.
/// See workfiles/20261102-component-update-verification.md § Components Window.
/// </summary>
internal sealed class ComponentsWindow : Form
{
    // The columns, in their order.
    private const int NameColumn = 0;
    private const int InstalledColumn = 1;
    private const int SizeColumn = 2;
    private const int InstalledOnColumn = 3;
    private const int UpdatedOnColumn = 4;
    private const int PinnedColumn = 5;
    private const int ForcedColumn = 6;
    private const int LatestColumn = 7;
    private const int ReleasedColumn = 8;
    private const int ActionsColumn = 9;

    // The narrowest a column shrinks to as the window narrows, in logical px.
    private const int MinColumnWidth = 36;

    // How often the spinners and the indeterminate bars move, in ms.
    private const int SpinInterval = 80;

    // How long the balloon of an offer stays, in ms.
    private const int BalloonMs = 15000;

    private static readonly string[] Spinner = ["◐", "◓", "◑", "◒"];

    private readonly IComponentsHost _host;
    private readonly ComponentManifest _manifest = ComponentManifest.Current;
    private readonly ListView _list = new BufferedList
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        OwnerDraw = true,
        ShowItemToolTips = true,
        MultiSelect = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
    };

    private readonly Label _checkedAt = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0) };
    private readonly Button _checkNow = new() { Text = "Check now", AutoSize = true };
    private readonly Label _total = new() { AutoSize = true };
    private readonly Font _linkFont;
    private Row? _hoveredName;
    private readonly Button _installAll = new() { Text = "Install all", AutoSize = true };
    private readonly Button _uninstallAll = new() { Text = "Uninstall all", AutoSize = true };
    private readonly Button _updateAll = new() { Text = "Update all", AutoSize = true };
    private readonly ToolTip _balloon = new() { IsBalloon = true, ToolTipTitle = "Needed" };
    private readonly ContextMenuStrip _backMenu = new();
    private readonly System.Windows.Forms.Timer _spin = new() { Interval = SpinInterval };
    private readonly Dictionary<ListViewItem, Row> _rows = [];
    private readonly List<int> _widths = [];
    private CancellationTokenSource? _check;
    private bool _fitting;
    private int _frame;
    private bool _restartAsked;
    private (IReadOnlyList<Component> Components, string Reason)? _need;

    public ComponentsWindow(IComponentsHost host)
    {
        this._host = host;
        this.Text = "Component updates";
        this.Icon = AppIcon.Load();
        this.ShowInTaskbar = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.Manual;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.BackColor = SystemColors.Window;
        this.MinimumSize = new Size(this.LogicalToDeviceUnits(720), this.LogicalToDeviceUnits(360));

        (string Text, int Width)[] columns =
        [
            ("Component", 290), ("Installed", 100), ("Size", 90), ("Installed on", 90), ("Updated on", 90), ("Pinned", 110), ("Forced", 90),
            ("Latest", 150), ("Released", 90), ("", 190),
        ];
        foreach (var (text, width) in columns)
        {
            this._widths.Add(this.LogicalToDeviceUnits(width));
            this._list.Columns.Add(text, this._widths[^1]);
        }

        foreach (var group in Enum.GetValues<ComponentGroup>())
        {
            this._list.Groups.Add(new ListViewGroup(group.ToString(), GroupName(group)));
        }

        // The rows as tall as a button's: an image list sets a ListView's row height.
        this._list.SmallImageList = new ImageList { ImageSize = new Size(1, this.LogicalToDeviceUnits(28)) };
        foreach (var component in ComponentRegistry.All)
        {
            var item = new ListViewItem([.. Enumerable.Repeat("", columns.Length)]) { Group = this._list.Groups[(int)component.Group] };
            this._list.Items.Add(item);
            this._rows[item] = new Row(component, item) { PendingRestart = this._manifest[component].Pending is not null };
            this.Fill(this._rows[item]);
        }

        this._list.ClientSizeChanged += (_, _) => this.FitColumns();
        this._list.DrawColumnHeader += (_, e) => e.DrawDefault = true;
        this._list.DrawItem += (_, _) => { };
        this._list.DrawSubItem += this.DrawCell;
        this._list.MouseClick += this.OnListClick;
        this._list.MouseMove += (_, e) => this.OnListMouseMove(e.Location);
        this._list.MouseLeave += (_, _) => this.SetHoveredName(null);
        this._linkFont = new Font(this._list.Font, FontStyle.Underline);

        UiIcons.SetImage(this._checkNow, UiIcons.CheckComponents, this.LogicalToDeviceUnits(16));
        UiIcons.SetImage(this._installAll, UiIcons.Download, this.LogicalToDeviceUnits(16));
        UiIcons.SetImage(this._uninstallAll, UiIcons.Uninstall, this.LogicalToDeviceUnits(16));
        UiIcons.SetImage(this._updateAll, UiIcons.UpdateComponent, this.LogicalToDeviceUnits(16));
        UiIcons.Attach(this._backMenu);
        this._checkNow.Click += (_, _) => this.CheckAll();
        this._installAll.Click += (_, _) => this.InstallAll();
        this._uninstallAll.Click += async (_, _) => await this.UninstallAllAsync();
        this._updateAll.Click += (_, _) => this.UpdateAll();
        this._spin.Tick += (_, _) => this.Spin();

        // Three rows, each in its place whatever the DPI: Check now, the list taking the rest, the buttons at the right
        // (Iteration 16).
        int pad = this.LogicalToDeviceUnits(8);
        var top = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(pad, pad, pad, pad / 2) };
        top.Controls.AddRange([this._checkNow, this._checkedAt]);
        this._checkedAt.Margin = new Padding(pad, this.LogicalToDeviceUnits(7), 0, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        buttons.Controls.AddRange([this._uninstallAll, this._installAll, this._updateAll]);

        // The total on the disk at the left, the buttons at the right (Iteration 27).
        var bottom = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(pad, pad / 2, pad, pad) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._total.Anchor = AnchorStyles.Left;
        bottom.Controls.Add(this._total, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);
        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = SystemColors.Window };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this._list.Margin = Padding.Empty;
        rows.Controls.Add(top, 0, 0);
        rows.Controls.Add(this._list, 0, 1);
        rows.Controls.Add(bottom, 0, 2);
        this.Controls.Add(rows);
        this.AcceptButton = null;
        this.CancelButton = null;
        this.UpdateButtons();
    }

    /// <summary>Whether an install runs on a row.</summary>
    public bool Running => this._rows.Values.Any(r => r.Run is not null);

    /// <summary>
    /// Opened by a feature needing components it lacks: their rows highlighted, the first brought into view, and a balloon
    /// on it saying what they are and why the feature wants them. Nothing installs by itself.
    /// </summary>
    public void ShowNeed(IReadOnlyList<Component> components, string reason)
    {
        foreach (var row in this._rows.Values)
        {
            row.Highlighted = components.Contains(row.Component);
        }

        this._need = (components, reason);
        this._list.Invalidate();
        if (this.IsHandleCreated && this.Visible)
        {
            this.BeginInvoke(this.ShowBalloon);
        }
    }

    /// <summary>
    /// Asks whether to cancel the installs running, and cancels them on Yes. Returns whether the window may close: none
    /// running, or Yes.
    /// </summary>
    public bool ConfirmCancel()
    {
        int running = this._rows.Values.Count(r => r.Run is not null);
        if (running == 0)
        {
            return true;
        }

        if (MessageBox.Show(
                this,
                running == 1 ? "A download is running. Cancel it and close?" : $"{running} downloads are running. Cancel them and close?",
                "Component updates",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return false;
        }

        foreach (var row in this._rows.Values)
        {
            row.Run?.Cancellation.Cancel();
        }

        return true;
    }

    /// <summary>Centered on its owner, within its screen's working area; the latest versions checked at once.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = this.Owner?.Bounds ?? Screen.PrimaryScreen!.WorkingArea;
        var screen = Screen.FromRectangle(area).WorkingArea;
        // The columns' opening widths, not the ones a first fit gave them before the window had its size (Iteration 28).
        int width = this._widths.Sum() + SystemInformation.GetVerticalScrollBarWidthForDpi(this.DeviceDpi) + (this.Width - this.ClientSize.Width) + this.LogicalToDeviceUnits(4);
        this.Size = new Size(Math.Min(width, screen.Width), Math.Min(this.LogicalToDeviceUnits(720), screen.Height * 9 / 10));
        this.Location = new Point(
            Math.Clamp(area.Left + (area.Width - this.Width) / 2, screen.Left, Math.Max(screen.Left, screen.Right - this.Width)),
            Math.Clamp(area.Top + (area.Height - this.Height) / 2, screen.Top, Math.Max(screen.Top, screen.Bottom - this.Height)));
        this.CheckAll();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        this.FitHeight();
        this.FitColumns();
        if (this._need is not null)
        {
            this.BeginInvoke(this.ShowBalloon);
        }
    }

    /// <summary>
    /// The window grown to show every row at once (Iteration 28) — the list's last row measured once laid out — within the
    /// screen's working area, kept on it.
    /// </summary>
    private void FitHeight()
    {
        if (this._list.Items.Count == 0)
        {
            return;
        }

        int needed = this._list.Items[this._list.Items.Count - 1].Bounds.Bottom + this.LogicalToDeviceUnits(24);
        int grow = needed - this._list.ClientSize.Height;
        if (grow <= 0)
        {
            return;
        }

        var screen = Screen.FromControl(this).WorkingArea;
        int height = Math.Min(this.Height + grow, screen.Height);
        int top = Math.Clamp(this.Top - (height - this.Height) / 2, screen.Top, screen.Bottom - height);
        this.SetBounds(this.Left, top, this.Width, height);
    }

    /// <summary>Escape closes.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Closed by the user while an install runs: asked first. Closed with the app: the app asked already.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !this.ConfirmCancel())
        {
            e.Cancel = true;
            return;
        }

        foreach (var row in this._rows.Values)
        {
            row.Run?.Cancellation.Cancel();
        }

        this._check?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._spin.Dispose();
            this._balloon.Dispose();
            this._backMenu.Dispose();
            this._list.SmallImageList?.Dispose();
            this._linkFont.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The columns sharing the list's width as the window is resized, each in proportion to its width at the opening; the
    /// Actions column keeps its own, so its buttons and progress bar never shrink.
    /// </summary>
    private void FitColumns()
    {
        if (this._fitting || this._list.Columns.Count != this._widths.Count || this._list.ClientSize.Width <= 0)
        {
            return;
        }

        this._fitting = true;

        // One redraw for every column, not one per column (Iteration 20).
        this._list.BeginUpdate();
        try
        {
            int fixedWidth = this._widths[ActionsColumn];
            int nominal = this._widths.Sum() - fixedWidth;
            // Down to a floor per column: below it, a horizontal scroll bar (Iteration 24).
            int floor = this.LogicalToDeviceUnits(MinColumnWidth) * (this._widths.Count - 1);
            int room = Math.Max(floor, this._list.ClientSize.Width - fixedWidth);
            int given = 0;
            for (int i = 0; i < this._widths.Count; i++)
            {
                if (i == ActionsColumn)
                {
                    continue;
                }

                // The last column shared takes what rounding left, so the row fills the width exactly.
                int width = Math.Max(this.LogicalToDeviceUnits(MinColumnWidth), i == ActionsColumn - 1 ? room - given : this._widths[i] * room / nominal);
                if (this._list.Columns[i].Width != width)
                {
                    this._list.Columns[i].Width = width;
                }

                given += width;
            }

            if (this._list.Columns[ActionsColumn].Width != fixedWidth)
            {
                this._list.Columns[ActionsColumn].Width = fixedWidth;
            }
        }
        finally
        {
            this._list.EndUpdate();
            this._fitting = false;
        }

        // The vertical scroll bar may come or go once the list lays itself out, without a resize: checked again then —
        // only when the width changed since this fit, so a fit that cannot match it is not posted again forever.
        int fitted = this._list.ClientSize.Width;
        if (this.IsHandleCreated)
        {
            this.BeginInvoke(() =>
            {
                if (!this.IsDisposed && this._list.ClientSize.Width != fitted)
                {
                    this.FitColumns();
                }
            });
        }
    }

    private static string GroupName(ComponentGroup group) => group switch
    {
        ComponentGroup.Engines => "Engines",
        ComponentGroup.Runtimes => "Runtimes",
        ComponentGroup.WebTools => "Web tools",
        _ => "Emoji fonts",
    };

    private static string SizeText(long bytes) => bytes switch
    {
        <= 0 => "—",
        < 1_000_000 => $"{Math.Max(1, bytes / 1000)} KB",
        < 100_000_000 => (bytes / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + " MB",
        _ => (bytes / 1e6).ToString("0", CultureInfo.InvariantCulture) + " MB",
    };

    private static string DateText(DateTimeOffset? date) => date is { } d ? d.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";

    private ComponentRecord RecordOf(Row row) => this._manifest[row.Component];

    /// <summary>The texts of a row's plain cells, and its tooltip: its purpose, its upstream and license, its failure.</summary>
    private void Fill(Row row)
    {
        // Each text set only when it changes: setting one redraws the row (Iteration 19).
        var record = this.RecordOf(row);
        var cells = row.Item.SubItems;
        static void Set(ListViewItem.ListViewSubItem cell, string text)
        {
            if (cell.Text != text)
            {
                cell.Text = text;
            }
        }

        Set(cells[NameColumn], row.Component.Name);
        Set(cells[InstalledColumn], record.Installed?.Label ?? "—");
        row.Size = record.Installed is null ? 0 : ComponentInstaller.SizeOf(row.Component, record.Installed);
        Set(cells[SizeColumn], record.Installed is null ? "—" : SizeText(row.Size));
        Set(cells[InstalledOnColumn], DateText(record.InstalledOn));
        Set(cells[UpdatedOnColumn], DateText(record.UpdatedOn));
        Set(cells[PinnedColumn], record.Pinned.Label);
        Set(cells[ForcedColumn], record.Forced?.Label ?? "—");
        Set(cells[ReleasedColumn], DateText(row.Latest?.Released));
        string tip = $"{row.Component.Purpose}\n{row.Component.Upstream} — {row.Component.License}"
            + (row.Error is { } error ? $"\n\n⚠ {error}" : "")
            + (row.CheckError is { } check ? $"\n\nCouldn't check: {check}" : "");
        if (row.Item.ToolTipText != tip)
        {
            row.Item.ToolTipText = tip;
        }
    }

    /// <summary>Every row's latest version read again, each row's spinner stopping on its own answer.</summary>
    private void CheckAll()
    {
        this._check?.Cancel();
        var check = new CancellationTokenSource();
        this._check = check;
        this._checkedAt.Text = $"Checked {DateTime.Now:HH:mm}";
        foreach (var row in this._rows.Values)
        {
            row.Checking = true;
            row.CheckError = null;
            this._list.Invalidate(row.Item.SubItems[LatestColumn].Bounds);
            _ = this.CheckAsync(row, check.Token);
        }

        this._spin.Start();
        this.UpdateButtons();
    }

    private async Task CheckAsync(Row row, CancellationToken token)
    {
        var component = row.Component;
        var installed = this.RecordOf(row).Installed;
        try
        {
            var (latest, label, upToDate) = await Task.Run(async () =>
            {
                var latest = await component.Source.LatestAsync(component, token);
                string? label = await component.Source.InstalledLabelAsync(component, token);
                var current = label is not null ? new ComponentVersion(label, []) : installed;
                return (latest, label, IsUpToDate(component, current, latest));
            }, token);
            if (token.IsCancellationRequested || this.IsDisposed)
            {
                return;
            }

            // A tool tells its version itself: recorded when it differs, an install older than the manifest included.
            var record = this.RecordOf(row);
            if (label is not null && record.Installed?.Label != label && record.Installed is not null)
            {
                record.Installed = new ComponentVersion(label, []);
                this.TrySave();
            }

            row.Latest = latest;
            row.UpToDate = upToDate;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception e) when (e is UrlLoadException or IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException)
        {
            if (token.IsCancellationRequested || this.IsDisposed)
            {
                return;
            }

            row.CheckError = e.Message;
        }

        row.Checking = false;
        this.Fill(row);
        this._list.Invalidate(row.Item.Bounds);
        this.UpdateButtons();
    }

    /// <summary>
    /// Whether <paramref name="installed"/> is <paramref name="latest"/>: each file's content when the source tells it — its
    /// SHA-256, its Git blob id read from the file there — else the version's label.
    /// </summary>
    private static bool IsUpToDate(Component component, ComponentVersion? installed, LatestVersion latest)
    {
        if (installed is null)
        {
            return false;
        }

        if (latest.Fingerprints.Count == 0 || latest.Version.Files.Any(f => !latest.Fingerprints.ContainsKey(f.Name)))
        {
            return string.Equals(installed.Label, latest.Version.Label, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var file in latest.Version.Files)
        {
            string fingerprint = latest.Fingerprints[file.Name];
            string path = component.PathOf(file.Name);
            if (!File.Exists(path))
            {
                return false;
            }

            string mine = fingerprint.StartsWith("git:", StringComparison.Ordinal)
                ? "git:" + ComponentInstaller.GitBlobOf(path)
                : installed.Files.FirstOrDefault(f => f.Name == file.Name)?.Sha256 ?? DirectEngineDownload.Sha256Of(path);
            if (!string.Equals(mine, fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The rows whose latest version differs from the one installed, started at once.</summary>
    private void UpdateAll()
    {
        foreach (var row in this.Updatable().ToList())
        {
            _ = this.RunAsync(row, row.Latest!.Version);
        }
    }

    /// <summary>The rows not installed, each at its forced version, else its pinned one, started at once (Iteration 16).</summary>
    private void InstallAll()
    {
        foreach (var row in this.Installable().ToList())
        {
            _ = this.RunAsync(row, this.RecordOf(row).Forced ?? row.Component.Pinned);
        }
    }

    private IEnumerable<Row> Installable() =>
        this._rows.Values.Where(r => r.Run is null && !r.PendingRestart && this.RecordOf(r).Installed is null);

    private IEnumerable<Row> Updatable() =>
        this._rows.Values.Where(r => r.Run is null && !r.PendingRestart && r.Latest is not null && !r.UpToDate && this.RecordOf(r).Installed is not null);

    private void UpdateButtons()
    {
        int count = this.Updatable().Count();
        this._updateAll.Text = $"Update all ({count})";
        this._updateAll.Enabled = count > 0;
        int missing = this.Installable().Count();
        this._installAll.Text = $"Install all ({missing})";
        this._installAll.Enabled = missing > 0;
        long total = this._rows.Values.Sum(r => r.Size);
        this._total.Text = $"Total on disk: {(total > 0 ? SizeText(total) : "0 MB")}";
        int installed = this.Uninstallable().Count();
        this._uninstallAll.Text = $"Uninstall all ({installed})";
        this._uninstallAll.Enabled = installed > 0;
    }

    /// <summary>
    /// Installs <paramref name="target"/> on <paramref name="row"/>: downloaded off the UI thread, then swapped once what
    /// holds the files is released — or left for the next start when a file stays locked. An engine installed without ONNX
    /// Runtime starts its row too.
    /// </summary>
    private async Task RunAsync(Row row, ComponentVersion target)
    {
        if (row.Run is not null)
        {
            return;
        }

        var component = row.Component;
        var run = new RowRun(new CancellationTokenSource(), new InstallProgress(0, "Starting…"));
        row.Run = run;
        row.Error = null;
        this.Fill(row);
        this._spin.Start();
        this.UpdateButtons();
        // Only stored: the spinner's timer redraws the rows running, never each report (Iteration 15).
        var progress = new Progress<InstallProgress>(p => run.Progress = p);

        if (component.NeedsRuntime && this._rows.Values.First(r => r.Component.Id == ComponentRegistry.OnnxRuntime) is { Run: null } runtime
            && !runtime.Component.IsInstalled && this.RecordOf(runtime).Pending is null)
        {
            _ = this.RunAsync(runtime, this.RecordOf(runtime).Forced ?? runtime.Component.Pinned);
        }

        var record = this.RecordOf(row);
        try
        {
            if (component.Tool is { } tool)
            {
                await Task.Run(() => ComponentInstaller.InstallToolAsync(tool, target.Label, progress, run.Cancellation.Token));
                this.Installed(record, target);
            }
            else
            {
                var staged = await Task.Run(() => ComponentInstaller.DownloadAsync(component, target, progress, run.Cancellation.Token));
                run.Progress = new InstallProgress(1, "Installing…");
                await this._host.ReleaseAsync(component);
                var installed = record.Installed;
                bool swapped = await Task.Run(() => ComponentInstaller.Swap(component, staged, installed));
                if (swapped)
                {
                    this.Installed(record, staged);
                }
                else
                {
                    record.Pending = staged;
                    row.PendingRestart = true;
                }
            }

            this.TrySave();
            row.UpToDate = row.Latest is { } latest && IsUpToDate(component, record.Installed, latest);
        }
        catch (OperationCanceledException) when (run.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception e) when (e is UrlLoadException or IOException or UnauthorizedAccessException)
        {
            row.Error = e.Message;
        }
        finally
        {
            row.Run = null;
            run.Cancellation.Dispose();
            if (!this.IsDisposed)
            {
                this._host.Changed(component);
                this.Fill(row);
                this._list.Invalidate(row.Item.Bounds);
                this.UpdateButtons();
                this.AskRestartWhenDone();
            }
            else
            {
                this._host.Changed(component);
            }
        }
    }

    private void Installed(ComponentRecord record, ComponentVersion version)
    {
        bool first = record.Installed is null;
        record.Installed = version;
        record.Pending = null;
        if (first)
        {
            record.InstalledOn = DateTimeOffset.Now;
            record.UpdatedOn = null;
        }
        else
        {
            record.UpdatedOn = DateTimeOffset.Now;
        }
    }

    private void TrySave()
    {
        try
        {
            this._manifest.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{ComponentManifest.FileName} couldn't be saved: {e.Message}", "Component updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Once nothing runs, a swap left for the next start offers a restart, once.</summary>
    private void AskRestartWhenDone()
    {
        if (this.Running || this._restartAsked || !this._rows.Values.Any(r => r.PendingRestart))
        {
            return;
        }

        this._restartAsked = true;
        string names = string.Join(", ", this._rows.Values.Where(r => r.PendingRestart).Select(r => r.Component.Name));
        if (MessageBox.Show(
                this,
                $"{names}: some files were in use and are replaced when the app starts again.\n\nRestart the app now?",
                "Component updates",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
        {
            this._host.Restart();
        }
    }

    private void ShowBalloon()
    {
        if (this._need is not { } need || this.IsDisposed || need.Components.Count == 0)
        {
            return;
        }

        var row = this._rows.Values.First(r => r.Component == need.Components[0]);
        row.Item.EnsureVisible();
        var cell = row.Item.SubItems[NameColumn].Bounds;
        string names = string.Join(", ", need.Components.Select(c => c.Name));
        this._balloon.Show($"{need.Reason}\n\n{names}: {need.Components[0].Purpose}.\n\nClick Install on the rows highlighted.", this._list,
            cell.Left + this.LogicalToDeviceUnits(40), cell.Top + cell.Height / 2, BalloonMs);
        this._need = null;
    }

    private void Spin()
    {
        this._frame = (this._frame + 1) % Spinner.Length;
        bool any = false;
        foreach (var row in this._rows.Values)
        {
            if (row.Checking)
            {
                this._list.Invalidate(row.Item.SubItems[LatestColumn].Bounds);
                any = true;
            }

            if (row.Run is not null)
            {
                this._list.Invalidate(row.Item.SubItems[ActionsColumn].Bounds);
                any = true;
            }
        }

        if (!any)
        {
            this._spin.Stop();
        }
    }

    // ── Drawing ─────────────────────────────────────────────────────────────

    private void DrawCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null || !this._rows.TryGetValue(e.Item, out var row))
        {
            return;
        }

        var g = e.Graphics;
        var bounds = e.Bounds;
        bool selected = e.Item.Selected;
        var back = selected ? SystemColors.Highlight : row.Highlighted ? SystemColors.Info : SystemColors.Window;
        var fore = selected ? SystemColors.HighlightText : row.Highlighted ? SystemColors.InfoText : SystemColors.WindowText;
        using (var brush = new SolidBrush(back))
        {
            g.FillRectangle(brush, bounds);
        }

        int pad = this.LogicalToDeviceUnits(6);
        var text = Rectangle.FromLTRB(bounds.Left + pad, bounds.Top, bounds.Right - pad / 2, bounds.Bottom);
        const TextFormatFlags Flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        switch (e.ColumnIndex)
        {
            case NameColumn:
            {
                // The ℹ️ first, aligned from row to row, the name after it (Iteration 14).
                int icon = this.LogicalToDeviceUnits(14);
                int gap = this.LogicalToDeviceUnits(6);
                g.DrawImage(UiIcons.ComponentInfo.At(icon), text.Left, bounds.Top + (bounds.Height - icon) / 2, icon, icon);
                var name = Rectangle.FromLTRB(Math.Min(text.Right, text.Left + icon + gap), text.Top, text.Right, text.Bottom);

                // A link to its upstream's page, underlined under the mouse (Iteration 26).
                var link = e.Item.Selected ? fore : SystemColors.HotTrack;
                TextRenderer.DrawText(g, row.Component.Name, row == this._hoveredName ? this._linkFont : this._list.Font, name, link, Flags);
                break;
            }

            case LatestColumn:
                this.DrawLatest(g, row, text, fore, Flags);
                break;

            case ActionsColumn:
                this.DrawActions(g, row, bounds, fore);
                break;

            case InstalledColumn when row.PendingRestart:
                TextRenderer.DrawText(g, $"{e.SubItem.Text} → {this.RecordOf(row).Pending?.Label}", this._list.Font, text, fore, Flags);
                break;

            default:
                TextRenderer.DrawText(g, e.SubItem.Text, this._list.Font, text, fore, Flags);
                break;
        }
    }

    private void DrawLatest(Graphics g, Row row, Rectangle text, Color fore, TextFormatFlags flags)
    {
        if (row.Checking)
        {
            TextRenderer.DrawText(g, $"{Spinner[this._frame]} Checking…", this._list.Font, text, SystemColors.GrayText, flags);
        }
        else if (row.CheckError is not null)
        {
            TextRenderer.DrawText(g, "Couldn't check", this._list.Font, text, SystemColors.GrayText, flags);
        }
        else if (row.Latest is { } latest)
        {
            bool installed = this.RecordOf(row).Installed is not null;
            var color = row.Item.Selected ? fore : !installed ? SystemColors.GrayText : row.UpToDate ? Color.FromArgb(0, 128, 0) : Color.FromArgb(176, 96, 0);
            TextRenderer.DrawText(g, (installed && row.UpToDate ? "✓ " : "") + latest.Version.Label, this._list.Font, text, color, flags);
        }
    }

    private void DrawActions(Graphics g, Row row, Rectangle bounds, Color fore)
    {
        int pad = this.LogicalToDeviceUnits(4);
        var area = Rectangle.Inflate(bounds, -pad, -pad);
        if (row.Run is { } run)
        {
            var cancel = this.CancelRect(area);
            var bar = Rectangle.FromLTRB(area.Left, area.Top, cancel.Left - pad, area.Bottom);
            if (ProgressBarRenderer.IsSupported)
            {
                ProgressBarRenderer.DrawHorizontalBar(g, bar);
                var inner = Rectangle.Inflate(bar, -2, -2);
                int width = run.Progress.Share is { } share ? (int)(inner.Width * Math.Clamp(share, 0, 1))
                    : inner.Width / 3;
                int left = run.Progress.Share is null ? inner.Left + (inner.Width - width) * this._frame / (Spinner.Length - 1) : inner.Left;
                if (width > 0)
                {
                    ProgressBarRenderer.DrawHorizontalChunks(g, new Rectangle(left, inner.Top, width, inner.Height));
                }
            }

            TextRenderer.DrawText(g, run.Progress.Text, this._list.Font, bar, SystemColors.ControlText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            ButtonRenderer.DrawButton(g, cancel, "×", this._list.Font, false, PushButtonState.Normal);
            return;
        }

        // The 📂 at the right end of every row installed (Iteration 22).
        if (this.FolderRect(row, area) is { } folder)
        {
            ButtonRenderer.DrawButton(g, folder, false, PushButtonState.Normal);
            int side = this.LogicalToDeviceUnits(14);
            g.DrawImage(UiIcons.OpenFolder.At(side), folder.Left + (folder.Width - side) / 2, folder.Top + (folder.Height - side) / 2, side, side);
            area = Rectangle.FromLTRB(area.Left, area.Top, folder.Left - pad, area.Bottom);
        }

        if (row.PendingRestart)
        {
            TextRenderer.DrawText(g, "Restart to finish", this._list.Font, area, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            return;
        }

        var (main, back) = this.ButtonRects(row, area);
        if (main is { } rect)
        {
            bool install = this.RecordOf(row).Installed is null;
            ButtonRenderer.DrawButton(g, rect, false, PushButtonState.Normal);
            int icon = this.LogicalToDeviceUnits(14);
            var image = (install ? UiIcons.Download : UiIcons.UpdateComponent).At(icon);
            string label = install ? "Install" : "Update";
            int textWidth = TextRenderer.MeasureText(g, label, this._list.Font).Width;
            int left = rect.Left + (rect.Width - icon - pad - textWidth) / 2;
            g.DrawImage(image, left, rect.Top + (rect.Height - icon) / 2, icon, icon);
            TextRenderer.DrawText(g, label, this._list.Font, new Point(left + icon + pad, rect.Top + (rect.Height - this._list.Font.Height) / 2), SystemColors.ControlText);
        }

        if (back is { } arrow)
        {
            ButtonRenderer.DrawButton(g, arrow, "▾", this._list.Font, false, PushButtonState.Normal);
        }

        if (row.Error is not null)
        {
            int left = (back ?? main)?.Right + pad ?? area.Left;
            TextRenderer.DrawText(g, "⚠ Failed", this._list.Font, Rectangle.FromLTRB(left, area.Top, area.Right, area.Bottom), Color.FromArgb(192, 0, 0),
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    // ── Clicks ──────────────────────────────────────────────────────────────

    /// <summary>The button of the Actions cell under <paramref name="point"/>, and its row.</summary>
    private (Row Row, RowAction Action)? ActionAt(Point point)
    {
        var hit = this._list.HitTest(point);
        if (hit.Item is null || !this._rows.TryGetValue(hit.Item, out var row) || hit.SubItem is null || hit.Item.SubItems.IndexOf(hit.SubItem) != ActionsColumn)
        {
            return null;
        }

        int pad = this.LogicalToDeviceUnits(4);
        var area = Rectangle.Inflate(hit.SubItem.Bounds, -pad, -pad);
        if (row.Run is not null)
        {
            return this.CancelRect(area).Contains(point) ? (row, RowAction.Cancel) : null;
        }

        if (this.FolderRect(row, area) is { } folder)
        {
            if (folder.Contains(point))
            {
                return (row, RowAction.Folder);
            }

            area = Rectangle.FromLTRB(area.Left, area.Top, folder.Left - pad, area.Bottom);
        }

        if (row.PendingRestart)
        {
            return null;
        }

        var (main, back) = this.ButtonRects(row, area);
        return main?.Contains(point) == true ? (row, RowAction.Main)
            : back?.Contains(point) == true ? (row, RowAction.Back)
            : null;
    }

    /// <summary>The component's name under <paramref name="point"/>: the ℹ️ left out, as wide as its text.</summary>
    private Row? NameAt(Point point)
    {
        var hit = this._list.HitTest(point);
        if (hit.Item is null || !this._rows.TryGetValue(hit.Item, out var row) || hit.SubItem is null || hit.Item.SubItems.IndexOf(hit.SubItem) != NameColumn)
        {
            return null;
        }

        var cell = hit.SubItem.Bounds;
        int left = cell.Left + this.LogicalToDeviceUnits(6) + this.LogicalToDeviceUnits(14) + this.LogicalToDeviceUnits(6);
        int width = TextRenderer.MeasureText(row.Component.Name, this._list.Font).Width;
        return point.X >= left && point.X < Math.Min(cell.Right, left + width) ? row : null;
    }

    private void OnListMouseMove(Point point)
    {
        var name = this.NameAt(point);
        this.SetHoveredName(name);
        this._list.Cursor = name is not null || this.ActionAt(point) is not null ? Cursors.Hand : Cursors.Default;
    }

    private void SetHoveredName(Row? row)
    {
        if (row == this._hoveredName)
        {
            return;
        }

        if (this._hoveredName is { } before)
        {
            this._list.Invalidate(before.Item.SubItems[NameColumn].Bounds);
        }

        this._hoveredName = row;
        if (row is not null)
        {
            this._list.Invalidate(row.Item.SubItems[NameColumn].Bounds);
        }
    }

    /// <summary>The upstream's page of <paramref name="row"/>'s component, in the browser.</summary>
    private void OpenPage(Row row)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(row.Component.PageUrl) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            MessageBox.Show(this, $"Couldn't open {row.Component.PageUrl}: {e.Message}", "Component updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnListClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && this.NameAt(e.Location) is { } named)
        {
            this.OpenPage(named);
            return;
        }

        if (e.Button != MouseButtons.Left || this.ActionAt(e.Location) is not var (row, action))
        {
            return;
        }

        var record = this.RecordOf(row);
        switch (action)
        {
            case RowAction.Cancel:
                row.Run?.Cancellation.Cancel();
                break;

            case RowAction.Main when record.Installed is null:
                _ = this.RunAsync(row, record.Forced ?? row.Component.Pinned);
                break;

            case RowAction.Main when row.Latest is { } latest:
                _ = this.RunAsync(row, latest.Version);
                break;

            case RowAction.Back:
                this.ShowBackMenu(row, e.Location);
                break;

            case RowAction.Folder:
                this.OpenFolder(row);
                break;
        }
    }

    private void ShowBackMenu(Row row, Point at)
    {
        var record = this.RecordOf(row);
        this._backMenu.Items.Clear();
        foreach (var (text, version) in this.BackTargets(row))
        {
            var item = new ToolStripMenuItem(text);
            UiIcons.Set(item, UiIcons.BackToVersion);
            var target = version;
            item.Click += (_, _) => _ = this.RunAsync(row, target);
            this._backMenu.Items.Add(item);
        }

        if (record.Installed is not null)
        {
            if (this._backMenu.Items.Count > 0)
            {
                this._backMenu.Items.Add(new ToolStripSeparator());
            }

            var uninstall = new ToolStripMenuItem("Uninstall…");
            UiIcons.Set(uninstall, UiIcons.Uninstall);
            uninstall.Click += (_, _) => _ = this.UninstallAsync(row);
            this._backMenu.Items.Add(uninstall);
        }

        this._backMenu.Show(this._list, at);
    }

    /// <summary>
    /// After a confirmation naming the size freed: what holds the files released, the component's files deleted, its
    /// installed version and dates cleared. A file still locked is left, the row saying a restart frees it (Iteration 21).
    /// </summary>
    private async Task UninstallAsync(Row row)
    {
        var record = this.RecordOf(row);
        if (row.Run is not null || record.Installed is not { } installed)
        {
            return;
        }

        var component = row.Component;
        if (MessageBox.Show(
                this,
                $"Uninstall {component.Name}? Its files are deleted ({SizeText(ComponentInstaller.SizeOf(component, installed))}); the features needing it are offered it again.",
                "Component updates",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        await this.UninstallRowsAsync([row]);
    }

    /// <summary>
    /// Every row installed and not running uninstalled, after one confirmation naming them and the size freed
    /// (Iteration 25).
    /// </summary>
    private async Task UninstallAllAsync()
    {
        var rows = this.Uninstallable().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        long size = rows.Sum(r => ComponentInstaller.SizeOf(r.Component, this.RecordOf(r).Installed!));
        if (MessageBox.Show(
                this,
                $"Uninstall {rows.Count} components? Their files are deleted ({SizeText(size)}):\n\n{string.Join("\n", rows.Select(r => r.Component.Name))}\n\nThe features needing them are offered them again.",
                "Component updates",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        await this.UninstallRowsAsync(rows);
    }

    private IEnumerable<Row> Uninstallable() =>
        this._rows.Values.Where(r => r.Run is null && this.RecordOf(r).Installed is not null);

    /// <summary>
    /// What holds the rows' files released — once per kind —, their files deleted, their installed version and dates
    /// cleared. A file still locked is left, its row saying a restart frees it.
    /// </summary>
    private async Task UninstallRowsAsync(IReadOnlyList<Row> rows)
    {
        foreach (var group in rows.Select(r => r.Component).GroupBy(c => c.Group))
        {
            await this._host.ReleaseAsync(group.First());
        }

        foreach (var row in rows)
        {
            var record = this.RecordOf(row);
            if (row.Run is not null || record.Installed is not { } installed)
            {
                continue;
            }

            var component = row.Component;
            row.Error = null;
            bool deleted = await Task.Run(() => ComponentInstaller.Uninstall(component, installed));
            if (deleted)
            {
                record.Installed = null;
                record.Pending = null;
                record.InstalledOn = null;
                record.UpdatedOn = null;
                row.UpToDate = false;
                row.PendingRestart = false;
            }
            else
            {
                row.Error = "Some of its files are in use: restart the app, then uninstall it again";
            }

            if (!this.IsDisposed)
            {
                this.Fill(row);
                this._list.Invalidate(row.Item.Bounds);
            }
        }

        this.TrySave();
        foreach (var group in rows.Select(r => r.Component).GroupBy(c => c.Group))
        {
            this._host.Changed(group.First());
        }

        if (!this.IsDisposed)
        {
            this.UpdateButtons();
        }
    }

    /// <summary>The versions a row can go back to: the forced one, the pinned one — each when it is not the one installed.</summary>
    private List<(string Text, ComponentVersion Version)> BackTargets(Row row)
    {
        var record = this.RecordOf(row);
        var targets = new List<(string, ComponentVersion)>();
        if (record.Installed is not { } installed)
        {
            return targets;
        }

        if (record.Forced is { } forced && !string.Equals(forced.Label, installed.Label, StringComparison.OrdinalIgnoreCase))
        {
            targets.Add(($"Back to the forced version ({forced.Label})", forced));
        }

        if (!string.Equals(record.Pinned.Label, installed.Label, StringComparison.OrdinalIgnoreCase))
        {
            targets.Add(($"Back to the pinned version ({record.Pinned.Label})", record.Pinned));
        }

        return targets;
    }

    /// <summary>The Actions cell's buttons: Install or Update when it applies, then ▾ when a version can be gone back to.</summary>
    private (Rectangle? Main, Rectangle? Back) ButtonRects(Row row, Rectangle area)
    {
        var record = this.RecordOf(row);
        bool main = record.Installed is null || (row.Latest is not null && !row.UpToDate);
        bool back = record.Installed is not null;
        int width = this.LogicalToDeviceUnits(96);
        int arrow = this.LogicalToDeviceUnits(24);
        int gap = this.LogicalToDeviceUnits(4);
        Rectangle? mainRect = main ? new Rectangle(area.Left, area.Top, width, area.Height) : null;
        Rectangle? backRect = back ? new Rectangle(main ? area.Left + width + gap : area.Left, area.Top, arrow, area.Height) : null;
        return (mainRect, backRect);
    }

    /// <summary>The 📂 button of a row installed, at the right end of its Actions cell; null otherwise.</summary>
    private Rectangle? FolderRect(Row row, Rectangle area) =>
        this.RecordOf(row).Installed is not null ? new Rectangle(area.Right - area.Height, area.Top, area.Height, area.Height) : null;

    /// <summary>Explorer opened on the component's folder, its first file selected — a winget tool's folder in its parent.</summary>
    private void OpenFolder(Row row)
    {
        var component = row.Component;
        var installed = this.RecordOf(row).Installed;
        string? path = component.Tool is { } tool ? tool.Folder
            : (installed?.Files.Count > 0 ? installed.Files : component.Pinned.Files).Select(f => component.PathOf(f.Name)).FirstOrDefault(File.Exists);
        try
        {
            if (path is not null && (File.Exists(path) || Directory.Exists(path)))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true })?.Dispose();
            }
            else if (Directory.Exists(component.Folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(component.Folder) { UseShellExecute = true })?.Dispose();
            }
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            MessageBox.Show(this, $"Couldn't open Explorer: {e.Message}", "Component updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private Rectangle CancelRect(Rectangle area)
    {
        int side = area.Height;
        return new Rectangle(area.Right - side, area.Top, side, area.Height);
    }

    private enum RowAction
    {
        Main,
        Back,
        Cancel,
        Folder,
    }

    /// <summary>The list drawn double-buffered: its rows redrawn while downloads run never flicker.</summary>
    private sealed class BufferedList : ListView
    {
        public BufferedList() => this.DoubleBuffered = true;
    }

    /// <summary>An install running on a row: its cancellation, and where it stands.</summary>
    private sealed class RowRun(CancellationTokenSource cancellation, InstallProgress progress)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public InstallProgress Progress { get; set; } = progress;
    }

    /// <summary>A component's row and what the window knows of it.</summary>
    private sealed class Row(Component component, ListViewItem item)
    {
        public Component Component { get; } = component;

        public ListViewItem Item { get; } = item;

        public bool Checking { get; set; }

        public LatestVersion? Latest { get; set; }

        public bool UpToDate { get; set; }

        public string? CheckError { get; set; }

        public RowRun? Run { get; set; }

        public string? Error { get; set; }

        public bool PendingRestart { get; set; }

        public bool Highlighted { get; set; }

        /// <summary>What its installed files take on the disk, 0 when not installed: summed under the list.</summary>
        public long Size { get; set; }
    }
}
