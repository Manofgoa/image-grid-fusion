namespace ImageGridFusion.UI;

/// <summary>
/// Owns the app's lifetime instead of the window: the tray icon stays for the whole run, closing
/// the window only hides it, and the app quits from the tray menu (or when Windows ends the session).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    /// <summary>Starts the app hidden, with the tray icon only; given by the startup registration.</summary>
    public const string HiddenArgument = "--tray";

    private readonly MainForm _form;
    private readonly ContextMenuStrip _menu = new();
    private readonly NotifyIcon _tray;

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
            Text = "Image Grid Fusion",
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

        if (!hidden)
        {
            _form.Show();
        }
    }

    protected override void ExitThreadCore()
    {
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
        _form.FormClosed -= OnFormClosed;
        _form.CloseForGood();
        ExitThread();
    }

    /// <summary>The window closed for real: Windows is ending the session, or the Task Manager closed it.</summary>
    private void OnFormClosed(object? sender, FormClosedEventArgs e) => ExitThread();
}
