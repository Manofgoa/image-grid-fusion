namespace ImageGridFusion.UI;

/// <summary>
/// A one-line text asked of the user — a face group's name in the People view: a prompt, the text box
/// holding the current value selected, OK and Cancel. See workfiles/20261027-face-detection.md § Decisions.
/// </summary>
internal sealed class NameDialog : Form
{
    private readonly TextBox _text = new() { Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };

    public NameDialog(string title, string prompt, string value)
    {
        this.Text = title;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Padding = new Padding(12);

        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        var label = new Label { Text = prompt, AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        this._text.Text = value;
        this._text.Width = this.LogicalToDeviceUnits(320);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.AddRange([cancel, ok]);
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(this._text, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        this.Controls.Add(layout);
        this.AcceptButton = ok;
        this.CancelButton = cancel;
        this.Shown += (_, _) =>
        {
            this._text.Focus();
            this._text.SelectAll();
        };
    }

    /// <summary>The text typed, trimmed.</summary>
    public string Value => this._text.Text.Trim();
}
