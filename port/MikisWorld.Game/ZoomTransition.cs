namespace MikisWorld.Game;

/// <summary>A short glide between two zoom levels of the extended view. Only the scale the picture is shown at is animated: the picture is
/// the larger of the two (the one that shows more of the level) for as long as it lasts, so nothing is missing at the edges, and the game
/// goes on running behind it.</summary>
internal sealed class ZoomTransition
{
    public const double Duration = 0.15;

    private double _start;
    private float _from, _to;
    private int _keptK;
    private bool _on;

    /// <summary>Begins gliding from the scale being shown to the scale of the new zoom. <paramref name="keptK"/> is the zoom of the picture that exists now.</summary>
    public void Start(double now, float fromScale, float toScale, int keptK)
    {
        _start = now; _from = fromScale; _to = toScale; _keptK = keptK; _on = true;
    }

    public bool Running(double now)
    {
        if (_on && now - _start >= Duration) _on = false;
        return _on;
    }

    /// <summary>The scale to draw with: eased from the old to the new one (slow at both ends).</summary>
    public float Scale(double now)
    {
        if (!Running(now)) return _to;
        double t = (now - _start) / Duration;
        double e = t * t * (3 - 2 * t);
        return (float)(_from + (_to - _from) * e);
    }

    /// <summary>The zoom the picture is made for: while gliding, the one that shows more (the smaller zoom) of the old and the new.</summary>
    public int PictureZoom(double now, int targetK) => Running(now) ? Math.Min(_keptK, targetK) : targetK;
}
