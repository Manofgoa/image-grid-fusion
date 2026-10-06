namespace ImageGridFusion.UI;

/// <summary>
/// Owns the app's lifetime instead of the window: the tray icon stays for the whole run, closing
/// the window only hides it, and the app quits from the tray menu (or when Windows ends the session).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    /// <summary>Starts the app hidden, with the tray icon only; given by the startup registration.</summary>
    public const string HiddenArgument = "--tray";

    // The longest tooltip a NotifyIcon accepts: longer, setting it throws.
    private const int TooltipMaxLength = 127;

    // Qualified: inside an ApplicationContext, MainForm names its property, not the window's class.
    private const string AppTitle = global::ImageGridFusion.UI.MainForm.AppTitle;

    private readonly MainForm _form;
    private readonly ContextMenuStrip _menu = new();
    private readonly NotifyIcon _tray;
    private bool _exited;

    public TrayApplicationContext(MainForm form, bool hidden)
    {
        _form = form;

        // Not set as MainForm: Application.Run would show it, and closing it would end the app.
        _form.FormClosed += OnFormClosed;

        _menu.Items.Add("Open", null, (_, _) => ShowForm());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Quit", null, (_, _) => Quit());
        _tray = new NotifyIcon
        {
            Icon = new Icon(_form.Icon!, SystemInformation.SmallIconSize),
            Text = Tooltip(this._form.SecondTitle),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowForm();
            }
        };

        // An export that ended while the window was hidden: the tray icon says so; clicked, it opens the window.
        _form.ExportEndedHidden += (_, notice) => _tray.ShowBalloonTip(5000, notice.Title, notice.Text, notice.Error ? ToolTipIcon.Error : ToolTipIcon.Info);
        _tray.BalloonTipClicked += (_, _) => ShowForm();

        if (!hidden)
        {
            _form.Show();
        }
    }

    protected override void ExitThreadCore()
    {
        // Reached twice when Quit closes the window at once: FormClosed, then Quit itself.
        if (_exited)
        {
            return;
        }

        _exited = true;

        // Disposing removes the icon from the tray at once, instead of leaving a ghost until hovered.
        _tray.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }

    private void ShowForm()
    {
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized)
        {
            _form.WindowState = FormWindowState.Normal;
        }

        _form.Activate();
    }

    private void Quit()
    {
        _form.CloseForGood();

        // Closed at once, or never shown (disposed without FormClosed). A running export delays the
        // close until it has stopped: FormClosed then ends the app.
        if (_form.IsDisposed)
        {
            ExitThread();
        }
    }

    /// <summary>The window closed for real: Quit, Windows ending the session, or the Task Manager.</summary>
    private void OnFormClosed(object? sender, FormClosedEventArgs e) => ExitThread();

    /// <summary>
    /// The app's name, then the second title on a line of its own when there is one, cut with an ellipsis past the
    /// tooltip's cap.
    /// </summary>
    private static string Tooltip(string? secondTitle)
    {
        if (secondTitle is null)
        {
            return AppTitle;
        }

        string tooltip = $"{AppTitle}\n{secondTitle}";
        return tooltip.Length <= TooltipMaxLength ? tooltip : tooltip[..(TooltipMaxLength - 1)].TrimEnd() + "…";
    }
}
