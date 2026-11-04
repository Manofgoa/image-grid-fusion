using System.Globalization;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The Upscale window: what an enlargement will make — its size, its PNG's weight and its duration, estimated — then,
/// on <b>Upscale</b>, the enlargement computed with its progress, the time left and <b>Cancel</b>, the rest of the app
/// blocked behind it. Closes with <see cref="DialogResult.OK"/> once the image holds its enlargement. See
/// workfiles/20261029-image-upscale.md § Upscale Window.
/// </summary>
internal sealed class UpscaleWindow : Form
{
    // A pixel of a PNG, in bytes, when the image's file says nothing of it: a photo enlarged compresses to about this.
    private const double DefaultBytesPerPixel = 2;

    private readonly SourceImage _image;
    private readonly UpscaleEffect _setting;
    private readonly UpscaleService _service;
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Maximum = 1000, Visible = false, Margin = new Padding(0, 8, 0, 0) };
    private readonly Button _upscale = new() { Text = "Upscale", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Cancel", AutoSize = true };
    private readonly System.Diagnostics.Stopwatch _watch = new();
    private CancellationTokenSource? _running;
    private bool _closeWhenStopped;

    public UpscaleWindow(SourceImage image, UpscaleEffect setting, UpscaleService service, string name)
    {
        this._image = image;
        this._setting = setting;
        this._service = service;
        this.Text = $"Upscale — {name}";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Padding = new Padding(12);

        var from = image.UpscaleSize;
        var to = new Size(from.Width * setting.Factor, from.Height * setting.Factor);
        int frames = image.Pages is { } pages ? pages.Count : 1;
        var lines = new[]
        {
            $"{setting.Model} model, ×{setting.Factor}{(frames > 1 ? $", {frames} frames" : "")}",
            $"Size: {from.Width} × {from.Height}  →  {to.Width} × {to.Height}",
            $"File: ≈ {ComponentsWindow.SizeText(this.EstimatedBytes(to, frames))} as a {(frames > 1 ? "GIF" : "PNG")} (an estimate)",
            $"Time: ≈ {DurationText(service.Estimate(image, setting))} on this computer (an estimate)",
        };

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        foreach (string line in lines)
        {
            layout.Controls.Add(new Label { Text = line, AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
        }

        this._progress.Width = this.LogicalToDeviceUnits(360);
        layout.Controls.Add(this._progress);
        layout.Controls.Add(this._status);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange([this._cancel, this._upscale]);
        layout.Controls.Add(buttons);
        this.Controls.Add(layout);
        UiIcons.SetImage(this._upscale, UiIcons.Upscale, this.LogicalToDeviceUnits(16));
        this.AcceptButton = this._upscale;
        this.CancelButton = this._cancel;
        this._upscale.Click += async (_, _) => await this.RunAsync();
        this._cancel.Click += (_, _) => this.Stop(close: true);
    }

    /// <summary>A running enlargement stops before the window closes; closed meanwhile, it closes once stopped.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (this._running is not null)
        {
            e.Cancel = true;
            this.Stop(close: true);
        }

        base.OnFormClosing(e);
    }

    private static string DurationText(TimeSpan duration) => duration.TotalSeconds switch
    {
        < 1 => "1 s",
        < 60 => $"{Math.Ceiling(duration.TotalSeconds):0} s",
        < 3600 => $"{(int)duration.TotalMinutes} min {duration.Seconds:00} s",
        _ => $"{(int)duration.TotalHours} h {duration.Minutes:00} min",
    };

    // The PNG's — or the GIF's — weight: the image's file's bytes per pixel when it is of that format, else a default, times the pixels made.
    private long EstimatedBytes(Size size, int frames)
    {
        double perPixel = DefaultBytesPerPixel;
        if (this._image.FilePath is { } path && string.Equals(Path.GetExtension(path), frames > 1 ? ".gif" : ".png", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var from = this._image.UpscaleSize;
                perPixel = new FileInfo(path).Length / Math.Max(1.0, (double)from.Width * from.Height * frames);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unreadable: the default.
            }
        }

        return (long)(perPixel * size.Width * size.Height * frames);
    }

    private async Task RunAsync()
    {
        this._upscale.Enabled = false;
        this._progress.Visible = true;
        this._progress.Value = 0;
        this._status.ForeColor = SystemColors.ControlText;
        this._status.Text = "Upscaling…";
        this._running = new CancellationTokenSource();
        this._watch.Restart();
        var progress = new Progress<double>(this.ShowProgress);
        try
        {
            await this._service.ComputeAsync(this._image, this._setting, progress, this._running.Token);
            this._running = null;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (OperationCanceledException)
        {
            this._running = null;
            if (this._closeWhenStopped)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            this._status.Text = "Cancelled";
            this._upscale.Enabled = true;
        }
        catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or IOException or UnauthorizedAccessException
            or DllNotFoundException or ArgumentException or System.Runtime.InteropServices.ExternalException or InvalidOperationException or ObjectDisposedException)
        {
            this._running = null;
            this._status.ForeColor = Color.Firebrick;
            this._status.Text = $"Couldn't upscale the image: {ErrorLog.Describe(ex)}";
            this._upscale.Enabled = true;
        }
    }

    private void ShowProgress(double done)
    {
        if (this.IsDisposed || this._running is null)
        {
            return;
        }

        this._progress.Value = Math.Clamp((int)(done * 1000), 0, 1000);
        string left = done > 0.01 ? $", ≈ {DurationText(this._watch.Elapsed * ((1 - done) / done))} left" : "";
        this._status.Text = string.Create(CultureInfo.InvariantCulture, $"Upscaling… {done * 100:0} %{left}");
    }

    private void Stop(bool close)
    {
        if (this._running is null)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
            return;
        }

        this._closeWhenStopped = close;
        this._cancel.Enabled = false;
        this._status.Text = "Stopping after the current tile…";
        this._running.Cancel();
    }
}
