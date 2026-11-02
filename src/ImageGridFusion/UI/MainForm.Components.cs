using System.Diagnostics;
using ImageGridFusion.Components;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>What a feature needs installed, and why it wants it: the Component updates window's balloon.</summary>
internal sealed record ComponentNeed(IReadOnlyList<Component> Components, string Reason);

/// <summary>
/// The Component updates window's side of the main window: opening it, from the ⚙ menu or a feature needing a component,
/// and what an install asks of the app — releasing the engines' files, refreshing what uses them, restarting. See
/// workfiles/20261102-component-update-verification.md.
/// </summary>
internal sealed partial class MainForm : IComponentsHost
{
    /// <summary>
    /// The environment variable a restart gives the new process: the old one's id, waited for before the single-instance
    /// lock is taken (<c>Program.Main</c>).
    /// </summary>
    public const string RestartWaitVariable = "IMAGE_GRID_FUSION_WAIT_FOR_PID";

    // The launch's emoji fonts offer opened the window: closed with none installed, the No is remembered.
    private bool _emojiFontsOffered;

    // What the status line says of the components once the window shows: the manifest unreadable, the swaps done.
    private string? _componentsStatus;
    private bool _componentsStatusError;

    /// <summary>
    /// What <c>Program.Main</c> did with the components before this window: the manifest's error, the components whose swap
    /// it finished. Said in the status line once the window shows.
    /// </summary>
    public void ComponentsAtStart(string? manifestError, IReadOnlyList<string> swapped)
    {
        this._componentsStatusError = manifestError is not null;
        this._componentsStatus = manifestError ?? (swapped.Count > 0 ? $"Updated at start-up: {string.Join(", ", swapped)}" : null);
    }

    private void ShowComponentsStatus()
    {
        if (this._componentsStatus is { } status)
        {
            this.ShowStatus(status, error: this._componentsStatusError);
            this._componentsStatus = null;
        }
    }

    /// <summary>The ⚙ menu's Component updates…: the window, modeless and owned by this one; already open, it comes back to the front.</summary>
    private ComponentsWindow ShowComponents()
    {
        if (this._componentsWindow is { IsDisposed: false } open)
        {
            if (open.WindowState == FormWindowState.Minimized)
            {
                open.WindowState = FormWindowState.Normal;
            }

            open.Activate();
            return open;
        }

        var window = new ComponentsWindow(this);
        window.FormClosed += (_, _) =>
        {
            this._componentsWindow = null;
            this.EmojiFontsOfferClosed();
        };
        this._componentsWindow = window;
        window.Show(this);
        return window;
    }

    /// <summary>A feature lacking components: the window opened on their rows, a balloon saying why. Nothing when none is missing.</summary>
    private void ShowComponents(ComponentNeed need)
    {
        if (need.Components.Count > 0)
        {
            this.ShowComponents().ShowNeed(need.Components, need.Reason);
        }
    }

    /// <summary>
    /// The emoji fonts offered at the first launch: the window opened on their rows (workfiles/20261102-emoji-fonts.md
    /// § Shipped Fonts). Closed without installing one, the No is remembered and the launch offers them no more.
    /// </summary>
    private void OfferEmojiFontsAtLaunch()
    {
        this._emojiFontsOffered = true;
        this.ShowComponents(new ComponentNeed(
            ComponentRegistry.EmojiFontComponents,
            "A text's emojis can be drawn in another emoji font than Windows' own, picked in the Emoji drop-down beside the text's font. If you close this window without installing one, the launch offers them no more."));
    }

    private void EmojiFontsOfferClosed()
    {
        if (!this._emojiFontsOffered)
        {
            return;
        }

        this._emojiFontsOffered = false;
        if (EmojiFonts.AnyPresent)
        {
            return;
        }

        try
        {
            AppSettings.SaveEmojiFontsDeclined();
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            this.ShowStatus($"The emoji fonts' answer not remembered: {ex.Message}", error: true);
        }
    }

    /// <summary>The engines' files released before a swap: the cutout engine's sessions, the text model, the content passes.</summary>
    async Task IComponentsHost.ReleaseAsync(Component component)
    {
        if (component.Group is ComponentGroup.Engines or ComponentGroup.Runtimes)
        {
            this._explorer.SuspendEngines();
            await this._cutouts.ReleaseNowAsync();
        }
    }

    /// <summary>
    /// A component installed, updated, put back, or its install over: the content passes start again with what is there
    /// now, the cells waiting for the cutout engine cut out, the emoji fonts read again.
    /// </summary>
    void IComponentsHost.Changed(Component component)
    {
        if (this.IsDisposed)
        {
            return;
        }

        switch (component.Group)
        {
            case ComponentGroup.Engines or ComponentGroup.Runtimes:
                this._explorer.EnginesChanged();
                this._preview.Invalidate();
                break;

            case ComponentGroup.EmojiFonts:
                EmojiFonts.NotifyChanged();
                this.EmojiFontsChanged();
                break;
        }
    }

    /// <summary>
    /// Starts the exe again with this instance's second title and test flag — not its files —, the new process waiting for
    /// this one to end; then quits as the tray's Exit does.
    /// </summary>
    void IComponentsHost.Restart()
    {
        var info = new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath) { UseShellExecute = false };
        if (!this._savesWindowSize)
        {
            info.ArgumentList.Add(SingleInstance.NewInstanceArgument);
        }

        if (this.SecondTitle is { } title)
        {
            info.ArgumentList.Add(TitleArgument);
            info.ArgumentList.Add(title);
        }

        info.Environment[RestartWaitVariable] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            Process.Start(info)?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            this.ShowStatus($"Couldn't start the app again: {ex.Message}", error: true);
            return;
        }

        this.CloseForGood();
    }
}
