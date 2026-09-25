namespace ImageGridFusion.UI;

/// <summary>Kind of magnetic stop holding a moved image on one axis.</summary>
internal enum PanStop
{
    None,
    Edge,
    Center,
}

/// <summary>
/// Magnetic stops of a pan along one axis, in positions of the image's leading edge (left or top):
/// an edge stop holds the image when it moves outward, the center stop when it moves across it
/// either way, until the drag has gone the resistance distance past it; the image then jumps to
/// where the drag is. Moving back inward over an edge stop is free.
/// </summary>
internal sealed class PanMagnet
{
    // Positions within half a pixel are the same: they come back from fractions of the image.
    private const float Epsilon = 0.5f;

    private float _excess;

    public PanStop Held { get; private set; }

    /// <summary>Position of the stop held, when <see cref="Held"/> is not <see cref="PanStop.None"/>.</summary>
    public float Stop { get; private set; }

    /// <summary>Direction the edge stop held resists: +1 toward higher positions (the high stop), -1 toward lower ones; 0 for the center.</summary>
    public int Outward { get; private set; }

    public void Reset()
    {
        Held = PanStop.None;
        _excess = 0;
        Outward = 0;
    }

    /// <summary>
    /// Position of the image once the drag moved it by <paramref name="delta"/> from
    /// <paramref name="position"/>. <paramref name="low"/> and <paramref name="high"/> are the edge
    /// stops, <paramref name="center"/> the center stop; <paramref name="free"/> ignores them all.
    /// </summary>
    public float Move(float position, float delta, float low, float high, float center, float resistance, bool free)
    {
        if (free)
        {
            Reset();
            return position + delta;
        }

        float? released = null;
        if (Held != PanStop.None)
        {
            _excess += delta;
            bool inward = Outward != 0 && _excess * Outward <= 0;
            bool forced = Outward != 0 ? _excess * Outward > resistance : Math.Abs(_excess) > resistance;
            if (!inward && !forced)
            {
                return Stop;
            }

            // Released: the rest of the move starts from the stop, which cannot catch it again.
            released = Stop;
            position = Stop;
            delta = _excess;
            Reset();
        }

        float target = position + delta;
        int direction = Math.Sign(delta);
        if (direction == 0)
        {
            return target;
        }

        // The stops that hold in the move's direction, nearest first. An image resting on an edge stop
        // is held as soon as it moves outward; one resting on the center leaves it freely.
        (float At, PanStop Kind, int Outward)[] stops =
        [
            (center, PanStop.Center, 0),
            direction > 0 ? (high, PanStop.Edge, 1) : (low, PanStop.Edge, -1),
        ];
        foreach (var stop in stops.OrderBy(s => (s.At - position) * direction))
        {
            float ahead = (stop.At - position) * direction;
            bool reached = stop.Kind == PanStop.Edge ? ahead > -Epsilon : ahead > Epsilon;
            bool passed = (target - stop.At) * direction > 0;
            if (!reached || !passed || released is { } r && Math.Abs(stop.At - r) < Epsilon)
            {
                continue;
            }

            // Held even when this one move already goes the resistance past it: a quick drag still
            // shows the stop once, and the next move releases it where the drag is.
            Held = stop.Kind;
            Stop = stop.At;
            Outward = stop.Outward;
            _excess = target - stop.At;
            return Stop;
        }

        return target;
    }
}
