using System.ComponentModel;
using System.Windows.Forms.VisualStyles;
using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>
/// The tabs of the effects toolbar, hanging down from the options row above them (see RULES.md): one
/// per effect, each holding the checkbox that turns its effect on or off on the selected cell. The
/// selected tab, whose options the row shows, is drawn joined to it. A checkbox that cannot be used is
/// disabled, its tooltip saying why; its tab can still be selected.
/// </summary>
internal sealed class EffectTabs : Control
{
    // In logical pixels.
    private const int Pad = 8;
    private const int Gap = 4;
    private const int Spacing = 2;
    private const int IconSize = 16;

    private readonly ImageEffect[] _effects = Enum.GetValues<ImageEffect>();
    private readonly Dictionary<ImageEffect, Image> _icons = [];
    private readonly Dictionary<ImageEffect, bool> _checked = [];
    private readonly Dictionary<ImageEffect, string?> _unavailable = [];
    private readonly ToolTip _toolTip = new();
    private ImageEffect? _selected;
    private Color _selectedColor = SystemColors.Window;
    private ImageEffect? _hovered;
    private string? _tip;

    public EffectTabs()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>A tab was clicked outside its checkbox, or on a checkbox that cannot be used.</summary>
    public event EventHandler<ImageEffect>? TabClicked;

    /// <summary>A usable checkbox was clicked.</summary>
    public event EventHandler<ImageEffect>? CheckClicked;

    public static string Title(ImageEffect effect) => effect switch
    {
        ImageEffect.BlackAndWhite => "Black & white",
        _ => effect.ToString(),
    };

    /// <summary>The tab whose options show; none at startup.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ImageEffect? Selected
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                _selected = value;
                Invalidate();
            }
        }
    }

    /// <summary>The fill of the selected tab: the options row's color, so the two read as one.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (value != _selectedColor)
            {
                _selectedColor = value;
                Invalidate();
            }
        }
    }

    /// <summary>Takes <paramref name="icon"/> over, disposing the one it replaces.</summary>
    public void SetIcon(ImageEffect effect, Image icon)
    {
        _icons.GetValueOrDefault(effect)?.Dispose();
        _icons[effect] = icon;
        Invalidate();
    }

    /// <summary>
    /// Checks the tab's checkbox while its effect is on; <paramref name="unavailable"/>, when set, says
    /// why the effect does not apply, and disables the checkbox.
    /// </summary>
    public void SetState(ImageEffect effect, bool isChecked, string? unavailable)
    {
        if (_checked.GetValueOrDefault(effect) == isChecked && _unavailable.GetValueOrDefault(effect) == unavailable)
        {
            return;
        }

        _checked[effect] = isChecked;
        _unavailable[effect] = unavailable;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var tabs = Tabs();
        return new Size(tabs[^1].Bounds.Right, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using var border = new Pen(SystemColors.ControlDark);

        // The bottom edge of the options row, opened by the selected tab drawn over it.
        g.DrawLine(border, 0, 0, Width, 0);
        foreach (var tab in Tabs())
        {
            PaintTab(g, border, tab);
        }
    }

    private void PaintTab(Graphics g, Pen border, Tab tab)
    {
        bool selected = tab.Effect == _selected;
        var bounds = tab.Bounds;
        var fill = selected ? _selectedColor : tab.Effect == _hovered && Enabled ? SystemColors.ControlLight : SystemColors.Control;
        using (var brush = new SolidBrush(fill))
        {
            g.FillRectangle(brush, bounds.X, selected ? 0 : 1, bounds.Width, bounds.Height - (selected ? 0 : 1));
        }

        g.DrawLine(border, bounds.Left, 0, bounds.Left, bounds.Bottom - 1);
        g.DrawLine(border, bounds.Right - 1, 0, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(border, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);

        bool usable = Enabled && _unavailable.GetValueOrDefault(tab.Effect) is null;
        bool isChecked = _checked.GetValueOrDefault(tab.Effect);
        var state = (isChecked, usable) switch
        {
            (true, true) => CheckBoxState.CheckedNormal,
            (true, false) => CheckBoxState.CheckedDisabled,
            (false, true) => CheckBoxState.UncheckedNormal,
            _ => CheckBoxState.UncheckedDisabled,
        };
        CheckBoxRenderer.DrawCheckBox(g, tab.Check.Location, state);

        if (_icons.GetValueOrDefault(tab.Effect) is { } icon)
        {
            if (Enabled)
            {
                g.DrawImage(icon, tab.Icon);
            }
            else
            {
                ControlPaint.DrawImageDisabled(g, icon, tab.Icon.X, tab.Icon.Y, fill);
            }
        }

        TextRenderer.DrawText(
            g,
            Title(tab.Effect),
            Font,
            tab.Text,
            Enabled ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || HitTest(e.Location) is not { } hit)
        {
            return;
        }

        if (hit.OnCheck && _unavailable.GetValueOrDefault(hit.Tab.Effect) is null)
        {
            CheckClicked?.Invoke(this, hit.Tab.Effect);
        }
        else
        {
            TabClicked?.Invoke(this, hit.Tab.Effect);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        Hover(hit?.Tab.Effect, hit is { OnCheck: true } ? _unavailable.GetValueOrDefault(hit.Value.Tab.Effect) : null);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Hover(null, null);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Hover(null, null);
        Invalidate();
    }

    private void Hover(ImageEffect? effect, string? tip)
    {
        if (effect != _hovered)
        {
            _hovered = effect;
            Invalidate();
        }

        if (tip != _tip)
        {
            _tip = tip;
            _toolTip.SetToolTip(this, tip);
        }
    }

    /// <summary>The tab under <paramref name="location"/>; its checkbox answers on the padding around it too.</summary>
    private (Tab Tab, bool OnCheck)? HitTest(Point location)
    {
        foreach (var tab in Tabs())
        {
            if (tab.Bounds.Contains(location))
            {
                return (tab, location.X < tab.Check.Right + LogicalToDeviceUnits(Gap) / 2);
            }
        }

        return null;
    }

    private List<Tab> Tabs()
    {
        int pad = LogicalToDeviceUnits(Pad);
        int gap = LogicalToDeviceUnits(Gap);
        int icon = LogicalToDeviceUnits(IconSize);
        Size glyph;
        // The screen's device context: measuring must not create the handle.
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        {
            glyph = CheckBoxRenderer.GetGlyphSize(g, CheckBoxState.UncheckedNormal);
        }

        var tabs = new List<Tab>(_effects.Length);
        int x = 0;
        foreach (var effect in _effects)
        {
            var text = TextRenderer.MeasureText(Title(effect), Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            // Every tab as tall as the control, so as the Reset button beside them.
            int height = Height;
            var bounds = new Rectangle(x, 0, pad + glyph.Width + gap + icon + gap + text.Width + pad, height);
            var check = new Rectangle(new Point(x + pad, (height - glyph.Height) / 2), glyph);
            var iconBounds = new Rectangle(check.Right + gap, (height - icon) / 2, icon, icon);
            tabs.Add(new Tab(effect, bounds, check, iconBounds, new Rectangle(iconBounds.Right + gap, 0, text.Width, height)));
            x = bounds.Right + LogicalToDeviceUnits(Spacing);
        }

        return tabs;
    }

    private readonly record struct Tab(ImageEffect Effect, Rectangle Bounds, Rectangle Check, Rectangle Icon, Rectangle Text);
}
