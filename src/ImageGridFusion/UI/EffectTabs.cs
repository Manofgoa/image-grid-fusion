using System.ComponentModel;
using System.Windows.Forms.VisualStyles;

namespace ImageGridFusion.UI;

/// <summary>
/// The tabs of an effects toolbar (see RULES.md): one per effect, each holding the checkbox that turns
/// its effect on or off. They hang down from the options row above them — the cell effects — or,
/// <see cref="Standing"/>, stand up from the options row below them — the global effects. The
/// selected tab, whose options the row shows, is drawn joined to it. A checkbox that cannot be used is
/// disabled, its tooltip saying why; its tab can still be selected. A setting's tab — always in force,
/// like the output format — has no checkbox at all.
/// </summary>
internal sealed class EffectTabs<TEffect> : Control
    where TEffect : struct, Enum
{
    // In logical pixels.
    private const int Pad = 8;
    private const int Gap = 4;
    private const int Spacing = 2;
    private const int IconSize = 16;

    private readonly TEffect[] _effects = Enum.GetValues<TEffect>();
    private readonly Func<TEffect, string> _title;
    private readonly Func<TEffect, bool> _hasCheck;
    private readonly Dictionary<TEffect, Image> _icons = [];
    private readonly Dictionary<TEffect, bool> _checked = [];
    private readonly Dictionary<TEffect, string?> _unavailable = [];
    private readonly ToolTip _toolTip = new();
    private TEffect? _selected;
    private TEffect? _hovered;
    private string? _tip;

    /// <param name="title">The text of each effect's tab.</param>
    /// <param name="standing">The tabs stand on the options row below them, instead of hanging from the one above.</param>
    /// <param name="hasCheck">Whether a tab holds an activation checkbox; every one when not given.</param>
    public EffectTabs(Func<TEffect, string> title, bool standing = false, Func<TEffect, bool>? hasCheck = null)
    {
        _title = title;
        _hasCheck = hasCheck ?? (_ => true);
        Standing = standing;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>A tab was clicked outside its checkbox, or on a checkbox that cannot be used.</summary>
    public event EventHandler<TEffect>? TabClicked;

    /// <summary>A usable checkbox was clicked.</summary>
    public event EventHandler<TEffect>? CheckClicked;

    /// <summary>Whether the tabs stand on the options row below them, its edge along their bottom.</summary>
    public bool Standing { get; }

    /// <summary>The tab whose options show; none at startup.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TEffect? Selected
    {
        get => _selected;
        set
        {
            if (!Nullable.Equals(value, _selected))
            {
                _selected = value;
                Invalidate();
            }
        }
    }

    /// <summary>Takes <paramref name="icon"/> over, disposing the one it replaces.</summary>
    public void SetIcon(TEffect effect, Image icon)
    {
        _icons.GetValueOrDefault(effect)?.Dispose();
        _icons[effect] = icon;
        Invalidate();
    }

    /// <summary>
    /// Checks the tab's checkbox while its effect is on; <paramref name="unavailable"/>, when set, says
    /// why the effect does not apply, and disables the checkbox.
    /// </summary>
    public void SetState(TEffect effect, bool isChecked, string? unavailable)
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

        // The edge of the options row the tabs hang from or stand on, opened by the selected tab drawn over it.
        int edge = Standing ? Height - 1 : 0;
        g.DrawLine(border, 0, edge, Width, edge);
        foreach (var tab in Tabs())
        {
            PaintTab(g, border, tab);
        }
    }

    private void PaintTab(Graphics g, Pen border, Tab tab)
    {
        bool selected = Nullable.Equals(tab.Effect, _selected);
        var bounds = tab.Bounds;
        var fill = selected ? SystemColors.Window : Nullable.Equals(tab.Effect, _hovered) && Enabled ? SystemColors.ControlLight : SystemColors.Control;

        // The selected tab covers the options row's edge, the others stop short of it; the side away from
        // the row is closed.
        int edgeGap = selected ? 0 : 1;
        using (var brush = new SolidBrush(fill))
        {
            g.FillRectangle(brush, bounds.X, Standing ? 0 : edgeGap, bounds.Width, bounds.Height - edgeGap);
        }

        g.DrawLine(border, bounds.Left, 0, bounds.Left, bounds.Bottom - 1);
        g.DrawLine(border, bounds.Right - 1, 0, bounds.Right - 1, bounds.Bottom - 1);
        int closed = Standing ? 0 : bounds.Bottom - 1;
        g.DrawLine(border, bounds.Left, closed, bounds.Right - 1, closed);

        if (_hasCheck(tab.Effect))
        {
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
        }

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
            _title(tab.Effect),
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

    private void Hover(TEffect? effect, string? tip)
    {
        if (!Nullable.Equals(effect, _hovered))
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

    /// <summary>The tab under <paramref name="location"/>; its checkbox, if it has one, answers on the padding around it too.</summary>
    private (Tab Tab, bool OnCheck)? HitTest(Point location)
    {
        foreach (var tab in Tabs())
        {
            if (tab.Bounds.Contains(location))
            {
                return (tab, _hasCheck(tab.Effect) && location.X < tab.Check.Right + LogicalToDeviceUnits(Gap) / 2);
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
            var text = TextRenderer.MeasureText(_title(effect), Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            // Every tab as tall as the control, so as the Reset button beside them.
            int height = Height;
            // A tab without checkbox starts on its icon.
            bool hasCheck = _hasCheck(effect);
            int checkWidth = hasCheck ? glyph.Width + gap : 0;
            var bounds = new Rectangle(x, 0, pad + checkWidth + icon + gap + text.Width + pad, height);
            var check = new Rectangle(new Point(x + pad, (height - glyph.Height) / 2), hasCheck ? glyph : new Size(0, glyph.Height));
            var iconBounds = new Rectangle(x + pad + checkWidth, (height - icon) / 2, icon, icon);
            tabs.Add(new Tab(effect, bounds, check, iconBounds, new Rectangle(iconBounds.Right + gap, 0, text.Width, height)));
            x = bounds.Right + LogicalToDeviceUnits(Spacing);
        }

        return tabs;
    }

    private readonly record struct Tab(TEffect Effect, Rectangle Bounds, Rectangle Check, Rectangle Icon, Rectangle Text);
}
