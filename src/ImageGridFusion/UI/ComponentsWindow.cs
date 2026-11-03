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
    private const int InstalledOnColumn = 2;
    private const int UpdatedOnColumn = 3;
    private const int PinnedColumn = 4;
    private const int ForcedColumn = 5;
    private const int LatestColumn = 6;
    private const int ReleasedColumn = 7;
    private const int ActionsColumn = 8;

    // How often the spinners and the indeterminate bars move, in ms.
    private const int SpinInterval = 80;

    // How long the balloon of an offer stays, in ms.
    private const int BalloonMs = 15000;

    private static readonly string[] Spinner = ["◐", "◓", "◑", "◒"];

    private readonly IComponentsHost _host;
    private readonly ComponentManifest _manifest = ComponentManifest.Current;
    private readonly ListView _list = new()
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
    private readonly Button _updateAll = new() { Text = "Update all", AutoSize = true };
    private readonly Button _close = new() { Text = "Close", AutoSize = true };
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
            ("Component", 300), ("Installed", 110), ("Installed on", 90), ("Updated on", 90), ("Pinned", 110), ("Forced", 90),
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
        this._list.MouseMove += (_, e) => this._list.Cursor = this.ActionAt(e.Location) is not null ? Cursors.Hand : Cursors.Default;

        UiIcons.SetImage(this._checkNow, UiIcons.CheckComponents, this.LogicalToDeviceUnits(16));
        UiIcons.SetImage(this._updateAll, UiIcons.UpdateComponent, this.LogicalToDeviceUnits(16));
        UiIcons.SetImage(this._close, UiIcons.CloseWindow, this.LogicalToDeviceUnits(16));
        UiIcons.Attach(this._backMenu);
        this._checkNow.Click += (_, _) => this.CheckAll();
        this._updateAll.Click += (_, _) => this.UpdateAll();
        this._close.Click += (_, _) => this.Close();
        this._spin.Tick += (_, _) => this.Spin();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(this.LogicalToDeviceUnits(8)), WrapContents = false };
        top.Controls.AddRange([this._checkNow, this._checkedAt]);
        this._checkedAt.Margin = new Padding(this.LogicalToDeviceUnits(8), this.LogicalToDeviceUnits(7), 0, 0);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(this.LogicalToDeviceUnits(8)), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        bottom.Controls.AddRange([this._close, this._updateAll]);
        this.Controls.Add(this._list);
        this.Controls.Add(top);
        this.Controls.Add(bottom);
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
        int width = this._list.Columns.Cast<ColumnHeader>().Sum(c => c.Width) + this.LogicalToDeviceUnits(40);
        this.Size = new Size(Math.Min(width, screen.Width), Math.Min(this.LogicalToDeviceUnits(720), screen.Height * 9 / 10));
        this.Location = new Point(
            Math.Clamp(area.Left + (area.Width - this.Width) / 2, screen.Left, Math.Max(screen.Left, screen.Right - this.Width)),
            Math.Clamp(area.Top + (area.Height - this.Height) / 2, screen.Top, Math.Max(screen.Top, screen.Bottom - this.Height)));
        this.CheckAll();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (this._need is not null)
        {
            this.BeginInvoke(this.ShowBalloon);
        }
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
        try
        {
            int fixedWidth = this._widths[ActionsColumn];
            int nominal = this._widths.Sum() - fixedWidth;
            int room = Math.Max(nominal / 2, this._list.ClientSize.Width - fixedWidth);
            int given = 0;
            for (int i = 0; i < this._widths.Count; i++)
            {
                if (i == ActionsColumn)
                {
                    continue;
                }

                // The last column shared takes what rounding left, so the row fills the width exactly.
                int width = i == ActionsColumn - 1 ? room - given : this._widths[i] * room / nominal;
                this._list.Columns[i].Width = width;
                given += width;
            }

            this._list.Columns[ActionsColumn].Width = fixedWidth;
        }
        finally
        {
            this._fitting = false;
        }
    }

    private static string GroupName(ComponentGroup group) => group switch
    {
        ComponentGroup.Engines => "Engines",
        ComponentGroup.Runtimes => "Runtimes",
        ComponentGroup.WebTools => "Web tools",
        _ => "Emoji fonts",
    };

    private static string DateText(DateTimeOffset? date) => date is { } d ? d.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";

    private ComponentRecord RecordOf(Row row) => this._manifest[row.Component];

    /// <summary>The texts of a row's plain cells, and its tooltip: its purpose, its upstream and license, its failure.</summary>
    private void Fill(Row row)
    {
        var record = this.RecordOf(row);
        var cells = row.Item.SubItems;
        cells[NameColumn].Text = row.Component.Name;
        cells[InstalledColumn].Text = record.Installed?.Label ?? "—";
        cells[InstalledOnColumn].Text = DateText(record.InstalledOn);
        cells[UpdatedOnColumn].Text = DateText(record.UpdatedOn);
        cells[PinnedColumn].Text = record.Pinned.Label;
        cells[ForcedColumn].Text = record.Forced?.Label ?? "—";
        cells[ReleasedColumn].Text = DateText(row.Latest?.Released);
        row.Item.ToolTipText = $"{row.Component.Purpose}\n{row.Component.Upstream} — {row.Component.License}"
            + (row.Error is { } error ? $"\n\n⚠ {error}" : "")
            + (row.CheckError is { } check ? $"\n\nCouldn't check: {check}" : "");
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
            _ = this.CheckAsync(row, check.Token);
        }

        this._spin.Start();
        this._list.Invalidate();
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

    private IEnumerable<Row> Updatable() =>
        this._rows.Values.Where(r => r.Run is null && !r.PendingRestart && r.Latest is not null && !r.UpToDate && this.RecordOf(r).Installed is not null);

    private void UpdateButtons()
    {
        int count = this.Updatable().Count();
        this._updateAll.Text = $"Update all ({count})";
        this._updateAll.Enabled = count > 0;
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
        var progress = new Progress<InstallProgress>(p =>
        {
            run.Progress = p;
            if (!this.IsDisposed)
            {
                this._list.Invalidate(row.Item.SubItems[ActionsColumn].Bounds);
            }
        });

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
                this._list.Invalidate();
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

            if (row.Run is { } run)
            {
                if (run.Progress.Share is null)
                {
                    this._list.Invalidate(row.Item.SubItems[ActionsColumn].Bounds);
                }

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
                // The ℹ️ keeps its room: a name too long is cut before it.
                int icon = this.LogicalToDeviceUnits(14);
                int gap = this.LogicalToDeviceUnits(4);
                var name = Rectangle.FromLTRB(text.Left, text.Top, Math.Max(text.Left, text.Right - icon - gap), text.Bottom);
                int width = Math.Min(name.Width, TextRenderer.MeasureText(g, row.Component.Name, this._list.Font, name.Size, Flags).Width);
                TextRenderer.DrawText(g, row.Component.Name, this._list.Font, name, fore, Flags);
                int x = text.Left + width + gap;
                g.DrawImage(UiIcons.ComponentInfo.At(icon), x, bounds.Top + (bounds.Height - icon) / 2, icon, icon);
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

        if (row.PendingRestart)
        {
            TextRenderer.DrawText(g, "Restart to finish", this._list.Font, area, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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

        if (row.PendingRestart)
        {
            return null;
        }

        var (main, back) = this.ButtonRects(row, area);
        return main?.Contains(point) == true ? (row, RowAction.Main)
            : back?.Contains(point) == true ? (row, RowAction.Back)
            : null;
    }

    private void OnListClick(object? sender, MouseEventArgs e)
    {
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

        this._backMenu.Show(this._list, at);
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
        bool back = this.BackTargets(row).Count > 0;
        int width = this.LogicalToDeviceUnits(96);
        int arrow = this.LogicalToDeviceUnits(24);
        int gap = this.LogicalToDeviceUnits(4);
        Rectangle? mainRect = main ? new Rectangle(area.Left, area.Top, width, area.Height) : null;
        Rectangle? backRect = back ? new Rectangle(main ? area.Left + width + gap : area.Left, area.Top, arrow, area.Height) : null;
        return (mainRect, backRect);
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
    }
}
