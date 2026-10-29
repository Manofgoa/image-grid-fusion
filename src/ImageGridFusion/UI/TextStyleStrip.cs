using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>An item of the text styles' strip: a style to apply, or the action saving a new one.</summary>
internal abstract record TextStyleChoice
{
    /// <summary>The action ending the strip: the selected text layer's style saved under a name.</summary>
    public static readonly TextStyleChoice Save = new SaveChoice();

    private sealed record SaveChoice : TextStyleChoice;
}

/// <summary>A style of the strip, applied to the selected text layer when clicked.</summary>
internal sealed record ApplyStyle(NamedTextStyle Style) : TextStyleChoice;

/// <summary>
/// The text styles in the Layers options (workfiles/20261029-text-layer-style-presets.md § UI): an "Aa"
/// drawn in each style over the cell's grey, its name below, the built-in styles then the user's past a
/// line, then Save style… set apart; the style matching the selected text layer's pressed. Each
/// thumbnail drawn once per style.
/// </summary>
internal sealed class TextStyleStrip : ThumbnailStrip<TextStyleChoice>
{
    // The sample drawn in each style, in capitals under ALL CAPS.
    private const string Sample = "Aa";

    // The space left around the sample in its box, in logical pixels.
    private const int SampleMargin = 3;

    // Each style's sample, drawn once; null when it cannot be drawn.
    private readonly Dictionary<LayerTextStyle, Bitmap?> _samples = [];

    private LayerTextStyle? _current;

    public TextStyleStrip()
        : base([TextStyleChoice.Save])
    {
        this.FitSize();
    }

    /// <summary>The selected text layer's style, its matching style pressed; <c>null</c> without one.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public LayerTextStyle? Current
    {
        get => this._current;
        set
        {
            if (value != this._current)
            {
                this._current = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>The styles shown, in their order, then Save style…; the samples of the styles gone dropped.</summary>
    public void SetStyles(IEnumerable<NamedTextStyle> styles)
    {
        var list = styles.ToList();
        foreach (var gone in this._samples.Keys.Where(style => !list.Any(s => s.Style == style)).ToList())
        {
            this._samples[gone]?.Dispose();
            this._samples.Remove(gone);
        }

        this.SetValues([.. list.Select(style => (TextStyleChoice)new ApplyStyle(style)), TextStyleChoice.Save]);
    }

    protected override Size Box => EffectBox;

    protected override bool IsPressed(TextStyleChoice choice) => choice is ApplyStyle apply && apply.Style.Style == this._current;

    // The first of the user's styles, past the built-in ones, and Save style…, an action.
    protected override bool IsApart(TextStyleChoice choice) =>
        choice is ApplyStyle { Style.BuiltIn: false } apply ? this.FirstUserStyle() == apply : choice == TextStyleChoice.Save;

    protected override string Label(TextStyleChoice choice) => choice is ApplyStyle apply ? apply.Style.Name : "Save style…";

    protected override string Tip(TextStyleChoice choice) => choice is ApplyStyle apply
        ? $"{apply.Style.Name}{(apply.Style.BuiltIn ? "" : " — right-click to rename, replace or delete it")}\n{Describe(apply.Style.Style)}"
        : "Save style… — keeps the selected text's style under a name, to apply it to other texts";

    protected override void PaintThumbnail(Graphics g, TextStyleChoice choice, Rectangle box, bool pressed)
    {
        this.PaintCell(g, box);
        if (choice is not ApplyStyle apply)
        {
            // Save style…: a plus over the cell.
            int arm = Math.Min(box.Width, box.Height) / 4;
            var center = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
            using var pen = new Pen(this.Shade(SystemColors.Window), this.LogicalToDeviceUnits(3));
            g.DrawLine(pen, center.X - arm, center.Y, center.X + arm, center.Y);
            g.DrawLine(pen, center.X, center.Y - arm, center.X, center.Y + arm);
            return;
        }

        if (this.SampleOf(apply.Style.Style) is not { } sample)
        {
            return;
        }

        var inner = Rectangle.Inflate(box, -this.LogicalToDeviceUnits(SampleMargin), -this.LogicalToDeviceUnits(SampleMargin));
        double scale = Math.Min((double)inner.Width / sample.Width, (double)inner.Height / sample.Height);
        var size = new Size(Math.Max(1, (int)(sample.Width * scale)), Math.Max(1, (int)(sample.Height * scale)));
        var drawn = new Rectangle(inner.X + (inner.Width - size.Width) / 2, inner.Y + (inner.Height - size.Height) / 2, size.Width, size.Height);
        var interpolation = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(sample, drawn);
        g.InterpolationMode = interpolation;
        if (!this.Enabled)
        {
            this.Fill(g, Color.FromArgb(150, SystemColors.Control), box);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var sample in this._samples.Values)
            {
                sample?.Dispose();
            }

            this._samples.Clear();
        }

        base.Dispose(disposing);
    }

    /// <summary>What a style draws, in a few words: the tooltip's second line.</summary>
    private static string Describe(LayerTextStyle style)
    {
        var parts = new List<string> { style.Font };
        parts.AddRange(new[] { (style.Bold, "bold"), (style.Italic, "italic"), (style.Caps, "ALL CAPS") }.Where(p => p.Item1).Select(p => p.Item2));
        if (style.Alignment != LayerTextAlignment.Center)
        {
            parts.Add(style.Alignment == LayerTextAlignment.Justified ? "justified" : $"aligned {style.Alignment.ToString().ToLowerInvariant()}");
        }

        parts.Add(Hex(style.Color));
        if (style.Outline)
        {
            parts.Add($"outline {Hex(style.OutlineColor)} {style.OutlineWidth * 100:0} %");
        }

        if (style.Shadow)
        {
            parts.Add($"{style.ShadowKind.ToString().ToLowerInvariant()} shadow {Hex(style.ShadowColor)} {style.ShadowSize * 100:0} %");
        }

        if (style.Background)
        {
            parts.Add($"box {Hex(style.BackgroundColor)} {style.BackgroundOpacity * 100:0} %");
        }

        return string.Join(", ", parts);
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private ApplyStyle? FirstUserStyle()
    {
        foreach (var choice in this.Values)
        {
            if (choice is ApplyStyle { Style.BuiltIn: false } apply)
            {
                return apply;
            }
        }

        return null;
    }

    private Bitmap? SampleOf(LayerTextStyle style)
    {
        if (!this._samples.TryGetValue(style, out var sample))
        {
            try
            {
                sample = TextRaster.Render(Sample, style).Bitmap;
            }
            catch (Exception ex) when (ex is ExternalException or OutOfMemoryException or ArgumentException)
            {
                sample = null;
            }

            this._samples[style] = sample;
        }

        return sample;
    }
}
