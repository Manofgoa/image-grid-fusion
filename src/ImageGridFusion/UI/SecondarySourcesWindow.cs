using ImageGridFusion.Explorer;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The ⚙ menu's Secondary sources… window (workfiles/20261105-secondary-file-sources.md § Managing the Sources):
/// the sources' folders listed, each its full path, with Add… — a folder picker, the base folder, a folder
/// overlapping it or a source refused with the reason — and Remove, after a confirmation. Every change is
/// given at once through <see cref="SourcesChanged"/>; the window keeps nothing of its own.
/// </summary>
internal sealed class SecondarySourcesWindow : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly Button _add = new() { Text = Strings.SourcesAdd, AutoSize = true };
    private readonly Button _remove = new() { Text = Strings.SourcesRemove, AutoSize = true, Enabled = false };
    private readonly Label _empty = new() { Text = Strings.SourcesEmpty, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, 0, 0) };
    private readonly Func<string?> _baseFolder;
    private readonly List<string> _sources;

    /// <param name="sources">The sources as they stand, full paths.</param>
    /// <param name="baseFolder">The base folder as it stands when a folder is added: the one a source must not overlap.</param>
    public SecondarySourcesWindow(IReadOnlyList<string> sources, Func<string?> baseFolder)
    {
        this._sources = [.. sources];
        this._baseFolder = baseFolder;
        this.Text = Strings.SourcesWindowTitle;
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.Padding = new Padding(12);
        this.ClientSize = new Size(560, 320);
        this.MinimumSize = new Size(420, 260);

        var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var intro = new Label { Text = Strings.SourcesWindowIntro, AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 0, 0, 8) };
        var side = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), WrapContents = false };
        this._add.Margin = new Padding(0, 0, 0, 6);
        side.Controls.AddRange([this._add, this._remove]);
        var listHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        listHost.Controls.Add(this._list);
        listHost.Controls.Add(this._empty);
        var close = new Button { Text = Strings.SourcesClose, DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 10, 0, 0) };
        layout.Controls.Add(intro, 0, 0);
        layout.SetColumnSpan(intro, 2);
        layout.Controls.Add(listHost, 0, 1);
        layout.Controls.Add(side, 1, 1);
        layout.Controls.Add(close, 1, 2);
        this.Controls.Add(layout);
        this.AcceptButton = close;
        this.CancelButton = close;

        this._add.Click += (_, _) => this.AddSource();
        this._remove.Click += (_, _) => this.RemoveSelected();
        this._list.SelectedIndexChanged += (_, _) => this._remove.Enabled = this._list.SelectedIndex >= 0;
        this.Fill();
    }

    /// <summary>The sources changed — one added, one removed: their folders as they stand, to save and apply.</summary>
    public event EventHandler<IReadOnlyList<string>>? SourcesChanged;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        int size = this.LogicalToDeviceUnits(UiIcons.LogicalSize);
        UiIcons.SetImage(this._add, UiIcons.AddSource, size);
        UiIcons.SetImage(this._remove, UiIcons.RemoveSource, size);
    }

    /// <summary>The list as the sources stand, the selection kept on <paramref name="select"/> when given.</summary>
    private void Fill(string? select = null)
    {
        this._list.BeginUpdate();
        this._list.Items.Clear();
        foreach (string source in this._sources)
        {
            this._list.Items.Add(source);
        }

        this._list.EndUpdate();
        this._list.Visible = this._sources.Count > 0;
        this._empty.Visible = this._sources.Count == 0;
        int at = select is null ? -1 : this._sources.FindIndex(source => string.Equals(source, select, StringComparison.OrdinalIgnoreCase));
        this._list.SelectedIndex = at;
        this._remove.Enabled = at >= 0;
    }

    /// <summary>Add…: a folder picked, refused with its reason when it cannot be a source, else added at the end.</summary>
    private void AddSource()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Strings.SourcesPickerDescription,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string folder;
        try
        {
            folder = FileIndex.NormalizeFolder(dialog.SelectedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (SecondarySources.Refusal(folder, this._baseFolder(), this._sources) is { } reason)
        {
            MessageBox.Show(this, Texts.Format(Strings.SourcesRefusedText, folder, reason), Strings.SourcesRefusedTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        this._sources.Add(folder);
        this.Fill(folder);
        this.SourcesChanged?.Invoke(this, [.. this._sources]);
    }

    /// <summary>Remove: the selected source taken out once the user confirms, its index deleted by the explorer.</summary>
    private void RemoveSelected()
    {
        if (this._list.SelectedIndex is not (>= 0 and var at) || at >= this._sources.Count)
        {
            return;
        }

        string folder = this._sources[at];
        if (MessageBox.Show(
                this,
                Texts.Format(Strings.SourcesRemovePrompt, folder),
                Strings.SourcesRemoveTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }

        this._sources.RemoveAt(at);
        this.Fill(at < this._sources.Count ? this._sources[at] : this._sources.LastOrDefault());
        this.SourcesChanged?.Invoke(this, [.. this._sources]);
    }
}
