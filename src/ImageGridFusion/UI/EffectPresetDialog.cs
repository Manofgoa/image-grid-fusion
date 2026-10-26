namespace ImageGridFusion.UI;

/// <summary>
/// The effect preset dialog: the file's name, the effects the preset saves, read-only, and its
/// description in a multi-line box — <c>Enter</c> a new line, <c>Ctrl+Enter</c> saving. Saving a new
/// preset (<see cref="Purpose.Save"/>), from the ✥ handle with <b>File only</b> beside it
/// (<see cref="Purpose.SaveOrFileOnly"/>, <see cref="DialogResult.Ignore"/>), or editing a saved one's
/// description (<see cref="Purpose.Edit"/>). See workfiles/20261026-effect-preset-favorites.md § The Dialog.
/// </summary>
internal sealed class EffectPresetDialog : Form
{
    private readonly TextBox _description = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly ToolTip _toolTip = new();

    public EffectPresetDialog(string fileName, IReadOnlyList<string> effects, string description, Purpose purpose)
    {
        this.Text = purpose == Purpose.Edit ? "Edit effect preset" : "Save effect preset";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(460, 360);
        this.Padding = new Padding(12);

        var name = new Label { Text = fileName, AutoSize = true, AutoEllipsis = true, MaximumSize = new Size(436, 0), Margin = new Padding(0, 0, 0, 8) };
        name.Font = new Font(this.Font, FontStyle.Bold);
        var saved = new Label
        {
            Text = effects.Count == 0 ? "No effect: the file arrives as it is." : string.Join(Environment.NewLine, effects),
            AutoSize = true,
            MaximumSize = new Size(436, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 12),
        };
        var label = new Label { Text = "&Description — what it shows, to find it again by searching:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        this._description.Text = description;

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
        buttons.Controls.Add(cancel);
        if (purpose == Purpose.SaveOrFileOnly)
        {
            var fileOnly = new Button { Text = "File only", DialogResult = DialogResult.Ignore, AutoSize = true };
            buttons.Controls.Add(fileOnly);
            this._toolTip.SetToolTip(fileOnly, "Make the file a favorite, as it is, without its effects");
        }

        buttons.Controls.Add(save);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(name, 0, 0);
        layout.Controls.Add(saved, 0, 1);
        layout.Controls.Add(label, 0, 2);
        layout.Controls.Add(this._description, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        this.Controls.Add(layout);

        // No accept button: Enter is the box's new line; Ctrl+Enter saves.
        this.CancelButton = cancel;
        this._description.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                this.DialogResult = DialogResult.OK;
            }
        };
        this.Shown += (_, _) =>
        {
            this._description.Focus();
            this._description.SelectionStart = this._description.TextLength;
        };
    }

    /// <summary>What the dialog is opened for.</summary>
    public enum Purpose
    {
        /// <summary>A new preset, from the effects toolbar's button.</summary>
        Save,

        /// <summary>A new preset, from the ✥ handle released over the file explorer: <b>File only</b> offered too.</summary>
        SaveOrFileOnly,

        /// <summary>A saved preset's description.</summary>
        Edit,
    }

    /// <summary>The description as typed, trimmed of its blank lines around.</summary>
    public string Description => this._description.Text.Trim();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
