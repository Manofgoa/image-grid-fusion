using ImageGridFusion.Composition;

namespace ImageGridFusion.UI;

/// <summary>One cell as a step of the undo history holds it: its image, and the look that image had then.</summary>
internal readonly record struct CellState(SourceImage Image, ImageLook Look);

/// <summary>
/// The global effects as a step holds them. The borders' color is an app setting of the ⚙ menu, not
/// part of the history: it is left out, and a restore keeps the current one.
/// </summary>
internal sealed record GlobalState(
    Soundtrack? Soundtrack,
    bool SoundtrackOn,
    double SoundtrackLevel,
    SoundFade Fade,
    bool FadeOn,
    GridBorders Borders,
    bool BordersOn,
    SeamFade Seams,
    bool SeamsOn,
    VideoCascade Cascade,
    bool CascadeOn);

/// <summary>
/// Everything the user composes, as it stood after an action: the cells' images and looks, the layout
/// with its separators, the output format and the global effects. Only references and immutable
/// values: a step never copies a bitmap.
/// </summary>
internal sealed class GridState(IReadOnlyList<CellState> cells, GridLayout? layout, OutputFormat format, GlobalState global)
    : IEquatable<GridState>
{
    public IReadOnlyList<CellState> Cells { get; } = cells;

    public GridLayout? Layout { get; } = layout;

    public OutputFormat Format { get; } = format;

    public GlobalState Global { get; } = global;

    public bool Equals(GridState? other) =>
        other is not null
        && this.Cells.SequenceEqual(other.Cells)
        && (this.Layout is null ? other.Layout is null : this.Layout.SameAs(other.Layout))
        && this.Format == other.Format
        && this.Global == other.Global;

    public override bool Equals(object? obj) => this.Equals(obj as GridState);

    public override int GetHashCode() => HashCode.Combine(this.Cells.Count, this.Format, this.Global);

    public bool Holds(SourceImage image) => this.Cells.Any(c => c.Image == image);
}

/// <summary>
/// The undo history (see RULES.md § Undo History): a step is committed once the composed state has
/// settled — unchanged for <see cref="SettleDelay"/>, no mouse button held, no gesture running — so a
/// drag, a wheel burst or a slider moved is one step, and every route changing the grid is covered
/// without committing its own. Keeps <see cref="Depth"/> steps; the images only its steps reference
/// stay alive until they drop off it.
/// </summary>
internal sealed class GridHistory : IDisposable
{
    public const int Depth = 50;
    private const int SettleDelay = 300;
    private const int PollInterval = 100;

    private readonly Func<GridState> _capture;
    private readonly Func<bool> _busy;
    private readonly List<GridState> _undo = [];
    private readonly List<GridState> _redo = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = PollInterval };
    private GridState? _committed;

    // The state seen changing, and since when it holds: committed once it held long enough.
    private GridState? _pending;
    private long _pendingSince;

    /// <param name="capture">The composed state as it stands.</param>
    /// <param name="busy">Whether a gesture runs, so nothing is committed before it ends.</param>
    public GridHistory(Func<GridState> capture, Func<bool> busy)
    {
        this._capture = capture;
        this._busy = busy;
        this._timer.Tick += (_, _) => this.OnTick();
    }

    /// <summary>Whether <see cref="Start"/> was called: before, the keys do nothing.</summary>
    public bool Started => this._committed is not null;

    public int UndoCount => this._undo.Count;

    public int RedoCount => this._redo.Count;

    /// <summary>Takes the state as it stands as the initial one, history empty, and starts watching it.</summary>
    public void Start()
    {
        this._committed = this._capture();
        this._timer.Start();
    }

    /// <summary>
    /// Commits the state as it stands if it differs from the last step, without waiting for it to
    /// settle: called before an undo or a redo, so a change just made is never lost.
    /// </summary>
    public void Commit()
    {
        if (this._committed is null)
        {
            return;
        }

        var state = this._capture();
        this._pending = null;
        if (!state.Equals(this._committed))
        {
            this.Push(state);
        }
    }

    /// <summary>Steps back: returns the state left and the one to restore, or <c>null</c> with nothing to undo.</summary>
    public (GridState From, GridState To)? Undo() => this.Step(this._undo, this._redo);

    /// <summary>Steps forward again: returns the state left and the one to restore, or <c>null</c> with nothing to redo.</summary>
    public (GridState From, GridState To)? Redo() => this.Step(this._redo, this._undo);

    /// <summary>
    /// An image left the grid: disposed at once unless a step still holds it, else kept until the
    /// last step holding it drops off the history.
    /// </summary>
    public void Release(SourceImage image)
    {
        if (!this.Referenced(image))
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// What changed from <paramref name="older"/> to <paramref name="newer"/>, named for the status line:
    /// the action that took the grid from one to the other.
    /// </summary>
    public static string Describe(GridState older, GridState newer)
    {
        var labels = new List<string>();
        var before = older.Cells.Select(c => c.Image).ToList();
        var after = newer.Cells.Select(c => c.Image).ToList();
        bool globals = older.Global != newer.Global || older.Format != newer.Format;
        if (after.Count == 0 && before.Count > 0 && (before.Count > 1 || globals))
        {
            return "Clear all";
        }

        if (!before.SequenceEqual(after))
        {
            int added = after.Count(i => !before.Contains(i));
            int removed = before.Count(i => !after.Contains(i));
            labels.Add(
                added == 0 && removed == 0 ? "cells swapped"
                : removed == 0 ? (added == 1 ? "image added" : "images added")
                : added == 0 ? (removed == 1 ? "image deleted" : "images deleted")
                : added == 1 && removed == 1 ? "image replaced"
                : "images");
        }
        else
        {
            var effects = older.Cells.Zip(newer.Cells, (o, n) => o.Look.ChangedEffects(n.Look)).SelectMany(e => e).Distinct().ToList();
            if (effects.Count > 0)
            {
                labels.Add(effects.Count == 1 ? MainForm.EffectTitle(effects[0]) : "effects");
            }

            if (older.Layout is { } layout && !layout.SameAs(newer.Layout))
            {
                labels.Add(layout.Id == newer.Layout?.Id && layout.IsMirrored == newer.Layout.IsMirrored ? "separators" : "layout");
            }
        }

        if (older.Format != newer.Format)
        {
            labels.Add("format");
        }

        var (o, n) = (older.Global, newer.Global);
        if (o.Soundtrack != n.Soundtrack || o.SoundtrackOn != n.SoundtrackOn || o.SoundtrackLevel != n.SoundtrackLevel)
        {
            labels.Add(nameof(GlobalEffect.Soundtrack));
        }

        if (o.Fade != n.Fade || o.FadeOn != n.FadeOn)
        {
            labels.Add(nameof(GlobalEffect.Fade));
        }

        if (o.Borders != n.Borders || o.BordersOn != n.BordersOn)
        {
            labels.Add(nameof(GlobalEffect.Borders));
        }

        if (o.Seams != n.Seams || o.SeamsOn != n.SeamsOn)
        {
            labels.Add(nameof(GlobalEffect.Seams));
        }

        if (o.Cascade != n.Cascade || o.CascadeOn != n.CascadeOn)
        {
            labels.Add(nameof(GlobalEffect.Cascade));
        }

        return labels.Count switch
        {
            0 => "no visible change",
            1 => labels[0],
            _ => "several changes",
        };
    }

    /// <summary>Disposes every image only the history still holds; the grid disposes its own.</summary>
    public void Dispose()
    {
        this._timer.Dispose();
        HashSet<SourceImage> shown = this._committed is null ? [] : this._capture().Cells.Select(c => c.Image).ToHashSet();
        foreach (var image in this.AllStates().SelectMany(s => s.Cells).Select(c => c.Image).Distinct())
        {
            if (!shown.Contains(image))
            {
                image.Dispose();
            }
        }

        this._undo.Clear();
        this._redo.Clear();
        this._committed = null;
    }

    private void OnTick()
    {
        if (this._committed is null)
        {
            return;
        }

        var state = this._capture();
        if (state.Equals(this._committed))
        {
            this._pending = null;
            return;
        }

        long now = Environment.TickCount64;
        if (!state.Equals(this._pending))
        {
            this._pending = state;
            this._pendingSince = now;
            return;
        }

        if (now - this._pendingSince >= SettleDelay && Control.MouseButtons == MouseButtons.None && !this._busy())
        {
            this._pending = null;
            this.Push(state);
        }
    }

    /// <summary>Makes <paramref name="state"/> the last step: the previous one goes onto undo, redo is emptied.</summary>
    private void Push(GridState state)
    {
        this._undo.Add(this._committed!);
        this._committed = state;
        var dropped = new List<GridState>(this._redo);
        this._redo.Clear();
        if (this._undo.Count > Depth)
        {
            dropped.Add(this._undo[0]);
            this._undo.RemoveAt(0);
        }

        this.DisposeDropped(dropped);
    }

    private (GridState From, GridState To)? Step(List<GridState> from, List<GridState> to)
    {
        if (this._committed is null)
        {
            return null;
        }

        this.Commit();
        if (from.Count == 0)
        {
            return null;
        }

        var left = this._committed;
        to.Add(left);
        this._committed = from[^1];
        from.RemoveAt(from.Count - 1);
        return (left, this._committed);
    }

    /// <summary>The images of steps gone from the history, disposed unless the grid or another step still holds them.</summary>
    private void DisposeDropped(List<GridState> dropped)
    {
        if (dropped.Count == 0)
        {
            return;
        }

        var shown = this._capture().Cells.Select(c => c.Image).ToHashSet();
        foreach (var image in dropped.SelectMany(s => s.Cells).Select(c => c.Image).Distinct())
        {
            if (!shown.Contains(image) && !this.Referenced(image))
            {
                image.Dispose();
            }
        }
    }

    private bool Referenced(SourceImage image) => this.AllStates().Any(s => s.Holds(image));

    private IEnumerable<GridState> AllStates() =>
        this._committed is null ? [.. this._undo, .. this._redo] : [this._committed, .. this._undo, .. this._redo];
}
