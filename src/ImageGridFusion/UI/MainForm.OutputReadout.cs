using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The output readout (workfiles/20261107-status-bar-file-size.md, RULES.md § Output Readout): just left of Copy, what
/// the main part of Copy / Save produces as the grid stands — its format, its size and its dimensions,
/// <c>PNG · ≈ 24,7 Mo · 2400 × 1256</c>. The size is the worst case (<see cref="OutputEstimate"/>), marked <c>≈</c>, or
/// the exact size of the file the last Copy / Save produced while the grid is unchanged; during an animated export,
/// the bytes it has written so far.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>How often the readout looks for a change of the grid, or for the bytes an export has written.</summary>
    private const int OutputReadoutInterval = 250;

    private readonly Label _outputReadout = new()
    {
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        TextAlign = ContentAlignment.MiddleRight,
        Margin = new Padding(6, 3, 6, 3),
    };

    private readonly System.Windows.Forms.Timer _outputTimer = new() { Interval = OutputReadoutInterval };

    // The file the last Copy / Save of the main part produced, while it may still be what the grid gives.
    private KnownOutput? _knownOutput;

    // The animated export running, while its bytes are shown.
    private ExportWriting? _exportWriting;

    // What the readout was last computed for: a change of either computes it again.
    private int _outputVersion = -1;
    private int _outputVideoSide;

    /// <summary>A file the main part produced: exact while the grid's <paramref name="GridVersion"/>, the format and the size still match.</summary>
    private sealed record KnownOutput(bool Video, long Bytes, Size Size, int GridVersion);

    /// <summary>An animated export running: the file it writes, its format's name and its dimensions.</summary>
    private sealed record ExportWriting(string Path, string Format, Size? Size);

    /// <summary>What the main part of Copy / Save would produce: an MP4 or a PNG, its dimensions, its worst-case size and how it was reckoned.</summary>
    private sealed record MainOutputFigure(bool Video, Size Size, long Estimate, string Basis);

    /// <summary>Puts the readout just left of Copy and starts its timer.</summary>
    private void SetUpOutputReadout()
    {
        this._outputButtons.Controls.Add(this._outputReadout);
        this._outputButtons.Controls.SetChildIndex(this._outputReadout, this._outputButtons.Controls.IndexOf(this._copyButton));
        this._outputTimer.Tick += (_, _) => this.OnOutputTick();
        this._outputTimer.Start();
    }

    private void OnOutputTick()
    {
        if (this._exportWriting is { } writing)
        {
            this.ShowWriting(writing);
        }
        else if (this._preview.ContentVersion != this._outputVersion || AppSettings.VideoMaxSide != this._outputVideoSide)
        {
            // A zoom, a crop or a separator changes the native size without touching the buttons.
            this.RefreshOutputReadout();
        }
    }

    /// <summary>The main part's figure as the grid stands, the exact size when it is known; left alone while an export shows its bytes.</summary>
    private void RefreshOutputReadout()
    {
        if (this._exportWriting is not null)
        {
            return;
        }

        this._outputVersion = this._preview.ContentVersion;
        this._outputVideoSide = AppSettings.VideoMaxSide;
        if (this.MainOutput() is not { } output)
        {
            this.ShowOutput(FileSizes.Unknown, Strings.MainOutputTipNone);
            return;
        }

        string format = output.Video ? "MP4" : "PNG";
        var known = this._knownOutput is { } k && k.Video == output.Video && k.GridVersion == this._preview.ContentVersion && k.Size == output.Size ? k : null;
        string size = known is not null ? FileSizes.Text(known.Bytes) : Texts.Format(Strings.MainOutputEstimate, FileSizes.Text(output.Estimate));
        this.ShowOutput(
            Texts.Format(Strings.MainOutputReadout, format, size, output.Size.Width, output.Size.Height),
            known is not null ? Strings.MainOutputTipKnown : output.Basis);
    }

    /// <summary>
    /// What the main part of Copy / Save produces as the grid stands — an MP4 while it plays or a soundtrack is on, else
    /// a PNG at its native size —; <c>null</c> with nothing to export.
    /// </summary>
    private MainOutputFigure? MainOutput()
    {
        if (!this.HasContent)
        {
            return null;
        }

        if (!this.ProducesVideo)
        {
            return this.PngSize(CanvasSizer.MaxLongSide) is { } png
                ? new MainOutputFigure(false, png, OutputEstimate.PngUpperBound(png), Strings.MainOutputTipPng)
                : null;
        }

        if (this.PngSize(AppSettings.VideoMaxSide) is not { } canvas)
        {
            return null;
        }

        var size = Animation.EvenSize(canvas);
        var length = Animation.VideoLength(this._preview.Images, this.ActiveSoundtrack, this.ActiveCascade, this.ActiveBesideCells, this._range);
        if (length <= TimeSpan.Zero)
        {
            return null;
        }

        // The export carries a sound track with the soundtrack or a heard content, the cells' and those beside them.
        bool sound = this.ActiveSoundtrack is not null || Animation.Heard(this._preview.Images.Concat(this.ActiveBesideCells)).Count > 0;
        string megabits = Texts.Number(OutputEstimate.VideoBitrate(size) / 1e6, "0.#");
        string basis = Texts.Format(sound ? Strings.MainOutputTipMp4Sound : Strings.MainOutputTipMp4, megabits, Seconds(length));
        return new MainOutputFigure(true, size, OutputEstimate.Mp4UpperBound(size, length, sound), basis);
    }

    /// <summary>
    /// Shows the bytes an animated export has written so far, read from its file: <c>MP4 · 1,6 Mo… · 1920 × 1004</c>.
    /// </summary>
    private void ShowWriting(ExportWriting writing)
    {
        long bytes;
        try
        {
            var file = new FileInfo(writing.Path);
            bytes = file.Exists ? file.Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            bytes = 0;
        }

        string size = Texts.Format(Strings.MainOutputWriting, FileSizes.Text(bytes));
        this.ShowOutput(
            writing.Size is { } dimensions
                ? Texts.Format(Strings.MainOutputReadout, writing.Format, size, dimensions.Width, dimensions.Height)
                : Texts.Format(Strings.MainOutputReadoutNoSize, writing.Format, size),
            Strings.MainOutputTipWriting);
    }

    private void ShowOutput(string text, string tip)
    {
        // As wide as a figure in megabytes, so the buttons stay put while the digits change and when the grid empties.
        string widest = Texts.Format(Strings.MainOutputReadout, "MP4", Texts.Format(Strings.MainOutputEstimate, FileSizes.Text(999L << 20)), 8888, 8888);
        var minimum = new Size(TextRenderer.MeasureText(widest, this._outputReadout.Font).Width, 0);
        if (this._outputReadout.MinimumSize != minimum)
        {
            this._outputReadout.MinimumSize = minimum;
        }

        if (this._outputReadout.Text != text)
        {
            this._outputReadout.Text = text;
        }

        this._toolTip.SetToolTip(this._outputReadout, tip);
    }

    /// <summary>The export about to write <paramref name="path"/> in <paramref name="format"/>: the readout shows its bytes until <see cref="EndWriting"/>.</summary>
    private void BeginWriting(string path, GridExport.Format format)
    {
        var size = this.PngSize(AppSettings.VideoMaxSide) is { } canvas ? Animation.EvenSize(canvas) : (Size?)null;
        this._exportWriting = new ExportWriting(path, FormatName(format), size);
        this.ShowWriting(this._exportWriting);
    }

    private void EndWriting() => this._exportWriting = null;

    /// <summary>
    /// After a Copy or a Save: <paramref name="bytes"/> becomes the exact size shown while the grid stays as it was at
    /// <paramref name="gridVersion"/> — when the file is what the main part produces, <paramref name="expected"/> as the
    /// export started: an MP4, or a PNG at its native size. A GIF, a PNG max size or a JPEG for sharing leaves the known
    /// size alone.
    /// </summary>
    private void KeepOutputSize(MainOutputFigure? expected, GridExport.Format? format, int stillMaxSide, long bytes, int gridVersion)
    {
        bool? video = format switch
        {
            GridExport.Format.Mp4 => true,
            null when stillMaxSide == CanvasSizer.MaxLongSide => false,
            _ => null,
        };
        if (expected is { } main && main.Video == video)
        {
            this._knownOutput = new KnownOutput(main.Video, bytes, main.Size, gridVersion);
        }

        this.RefreshOutputReadout();
    }

    /// <summary><see cref="KeepOutputSize"/> for a saved file, its size read from the disk.</summary>
    private void KeepSavedSize(MainOutputFigure? expected, GridExport.Format? format, int stillMaxSide, string path, int gridVersion)
    {
        try
        {
            this.KeepOutputSize(expected, format, stillMaxSide, new FileInfo(path).Length, gridVersion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file already gone: the estimate stays.
            this.RefreshOutputReadout();
        }
    }
}
