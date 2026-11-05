using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// How a text layer's changes are drawn (workfiles/20261105-text-layer-typing-refresh.md): every change of its text or
/// its style is recorded as wanted and drawn off the UI thread by <see cref="TextLayerRenderer"/> — a draft at the
/// preview's resolution at most every <see cref="TextDraftInterval"/> ms while the changes keep coming, then the full
/// render <see cref="TextFinalDelay"/> ms after the last one; a one-shot change drawn in full at once. The preview
/// holds it as a gesture meanwhile (<see cref="GridPreview.TextLayerTyping"/>).
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>The shortest time between two drafts while the changes keep coming.</summary>
    private const int TextDraftInterval = 50;

    /// <summary>How long after the last change the full render is asked for: the undo history's settle time.</summary>
    private const int TextFinalDelay = 300;

    /// <summary>The smallest font size a draft is drawn at, in pixels.</summary>
    private const float MinDraftEm = 32;

    private TextLayerRenderer? _textRenderer;
    private readonly System.Windows.Forms.Timer _textDraftTimer = new() { Interval = TextDraftInterval };
    private readonly System.Windows.Forms.Timer _textFinalTimer = new() { Interval = TextFinalDelay };

    // What the text layer being drawn is wanted to show; Target is its content as the preview holds it now.
    private TextWanted? _textWanted;

    // The last request's number, and the number of the one the preview shows: an older result is dropped.
    private long _textSequence;
    private long _textShown;

    // A change came since the last draft was asked for.
    private bool _textDraftDue;

    // LinePerEm: the target's line in pixels per pixel of font size, so that a draft's size is known whatever the target
    // was drawn at.
    private sealed record TextWanted(SourceImage Target, string Text, LayerTextStyle Style, double? Box, long Sequence, double LinePerEm);

    /// <summary>The worker, its timers, and the routes that put a text being drawn in place before they read the layers.</summary>
    private void SetUpTextRendering()
    {
        this._textRenderer = new TextLayerRenderer(this);
        this._textRenderer.Rendered += this.OnTextRendered;
        this._textDraftTimer.Tick += (_, _) =>
        {
            if (this._textDraftDue)
            {
                this._textDraftDue = false;
                this.SendTextRender(final: false);
            }
            else
            {
                this._textDraftTimer.Stop();
            }
        };
        this._textFinalTimer.Tick += (_, _) =>
        {
            this._textFinalTimer.Stop();
            this._textDraftTimer.Stop();
            this._textDraftDue = false;
            this.SendTextRender(final: true);
        };
        this._preview.EditOpening += (_, _) => this.FlushTextRender();
        this.FormClosed += (_, _) =>
        {
            this._textDraftTimer.Stop();
            this._textFinalTimer.Stop();
            this._textRenderer.Dispose();
        };
    }

    /// <summary>
    /// What <paramref name="layer"/> is wanted to draw: its text, style and box as last changed — still being drawn —,
    /// else as it holds them.
    /// </summary>
    private (string Text, LayerTextStyle Style, double? Box) WantedOf(Layer layer) =>
        this._textWanted is { } wanted && wanted.Target == layer.Image
            ? (wanted.Text, wanted.Style, wanted.Box)
            : (layer.Text ?? "", layer.Style ?? this._textStyles.NewTextStyle, layer.TextBox);

    /// <summary>
    /// <paramref name="layer"/> wanted to draw <paramref name="text"/> in <paramref name="style"/>, in
    /// <paramref name="box"/>: <paramref name="live"/> — a key, a slider, a color followed —, a draft now or at the next
    /// interval and the full render after the pause; else the full render at once. The Layers turned on (RULES.md).
    /// </summary>
    private void RequestTextRender(Layer layer, string text, LayerTextStyle style, double? box, bool live)
    {
        // Another layer's change still being drawn is put in place first: one wanted text at a time.
        if (this._textWanted is { } other && other.Target != layer.Image)
        {
            this.FlushTextRender();
        }

        this.TurnLayersOn();

        // A content not being drawn was drawn in full; a draft's proportion is the one its drawing gave.
        double perEm = this._textWanted is { } same && same.Target == layer.Image ? same.LinePerEm : layer.Place.LinePixels / TextRaster.EmSize;
        this._textWanted = new TextWanted(layer.Image, text, style, box, ++this._textSequence, perEm);
        this._preview.TextLayerTyping = true;
        if (live)
        {
            this._textFinalTimer.Stop();
            this._textFinalTimer.Start();
            if (this._textDraftTimer.Enabled)
            {
                this._textDraftDue = true;
            }
            else
            {
                this.SendTextRender(final: false);
                this._textDraftTimer.Start();
            }
        }
        else
        {
            this._textDraftTimer.Stop();
            this._textFinalTimer.Stop();
            this._textDraftDue = false;
            this.SendTextRender(final: true);
        }
    }

    /// <summary>The wanted text asked of the worker: a draft at the preview's resolution, or the full render.</summary>
    private void SendTextRender(bool final)
    {
        if (this._textWanted is not { } wanted || this._textRenderer is null)
        {
            return;
        }

        float em = TextRaster.EmSize;
        if (!final && wanted.LinePerEm > 0 && this.LayerShowing(wanted.Target) is { } layer)
        {
            // The font size that draws a line as tall as the preview shows it.
            double shown = this._preview.LayerLinePixels(layer);
            if (shown > 0)
            {
                em = (float)Math.Clamp(shown / wanted.LinePerEm, MinDraftEm, TextRaster.EmSize);
            }
        }

        this._textRenderer.Request(new TextRenderRequest(wanted.Sequence, wanted.Text, wanted.Style, wanted.Box, em, final));
    }

    /// <summary>A request drawn by the worker: put in place when newer than what shows, dropped otherwise.</summary>
    private void OnTextRendered(TextRenderResult result)
    {
        var request = result.Request;
        if (this._textWanted is not { } wanted || request.Sequence <= this._textShown)
        {
            result.Bitmap?.Dispose();
            return;
        }

        bool final = request.Final && request.Sequence == wanted.Sequence;
        if (result.Bitmap is not { } bitmap)
        {
            this.ShowStatus(Texts.Format(Strings.PanelLayerTextNotDrawn, ErrorLog.Describe(result.Error!)), error: true);
            if (final)
            {
                this.EndTextRender();
            }

            return;
        }

        this.PlaceTextRender(wanted, request, bitmap, result.LinePixels, final);
    }

    /// <summary>
    /// The wanted text drawn in full at once, on the UI thread, and put in place: before something reads the layers —
    /// an export, a copy, an edit view, another layer's change —, so it never misses the last changes.
    /// </summary>
    private void FlushTextRender()
    {
        if (this._textWanted is not { } wanted)
        {
            return;
        }

        this._textDraftTimer.Stop();
        this._textFinalTimer.Stop();
        this._textDraftDue = false;
        this._textRenderer?.Cancel();
        if (this.LayerShowing(wanted.Target) is null)
        {
            this.EndTextRender();
            return;
        }

        try
        {
            var (bitmap, line) = TextRaster.Render(wanted.Text, wanted.Style, wanted.Box);
            var request = new TextRenderRequest(wanted.Sequence, wanted.Text, wanted.Style, wanted.Box, TextRaster.EmSize, Final: true);
            this.PlaceTextRender(wanted, request, bitmap, line, final: true);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or OutOfMemoryException or ArgumentException)
        {
            this.ShowStatus(Texts.Format(Strings.PanelLayerTextNotDrawn, ErrorLog.Describe(ex)), error: true);
            this.EndTextRender();
        }
    }

    /// <summary>
    /// The text being drawn forgotten, nothing put in place: another route draws the layer itself with
    /// <see cref="WantedOf"/> — a text box's side dragged.
    /// </summary>
    private void CancelTextRender()
    {
        if (this._textWanted is null)
        {
            return;
        }

        this._textDraftTimer.Stop();
        this._textFinalTimer.Stop();
        this._textDraftDue = false;
        this._textRenderer?.Cancel();
        this.EndTextRender();
    }

    /// <summary>
    /// <paramref name="bitmap"/>, drawn for <paramref name="request"/>, made the content of the layer showing
    /// <paramref name="wanted"/>'s target: a draft keeps the list's thumbnail and the gesture; the final ends them.
    /// </summary>
    private void PlaceTextRender(TextWanted wanted, TextRenderRequest request, Bitmap bitmap, double line, bool final)
    {
        if (this.LayerShowing(wanted.Target) is not { } current)
        {
            bitmap.Dispose();
            if (final)
            {
                this.EndTextRender();
            }

            return;
        }

        var image = new SourceImage(bitmap, filePath: null) { Look = current.Look };
        var drawn = new Layer(image, current.Look, current.Place with { LinePixels = line }, request.Text, request.Style) { TextBox = request.Box };
        this._textShown = request.Sequence;
        if (final)
        {
            // Drawn in full: the gesture ends before the layers change, so the area is drawn in full and the options follow.
            this._textWanted = null;
            this._preview.TextLayerTyping = false;
        }
        else
        {
            // A draft: the list keeps the thumbnail it shows until the full render.
            this._textWanted = wanted with { Target = image, LinePerEm = line / request.Em };
            if (this._layerThumbnails.Remove(wanted.Target, out var thumbnail))
            {
                this._layerThumbnails[image] = thumbnail;
            }
        }

        if (!this._preview.ReplaceTextContent(wanted.Target, drawn))
        {
            image.Dispose();
        }
    }

    /// <summary>Nothing more to draw: the gesture ends, the options and buttons follow the layer as it stands.</summary>
    private void EndTextRender()
    {
        this._textWanted = null;
        this._textShown = this._textSequence;
        if (this._preview.TextLayerTyping)
        {
            this._preview.TextLayerTyping = false;
            this.UpdateButtons();
        }
    }

    /// <summary>The layer whose content is <paramref name="image"/>, or <c>null</c>.</summary>
    private Layer? LayerShowing(SourceImage image) => this._preview.Layers.Items.FirstOrDefault(l => l.Image == image);
}
