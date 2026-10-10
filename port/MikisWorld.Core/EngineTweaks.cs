namespace MikisWorld.Core;

/// <summary>Deliberate departures from the original engine. Everything is off by default, so the logic stays bit-for-bit identical
/// to the original bytecode (that is what the golden traces check); the application turns them on from the user's settings.</summary>
public sealed class EngineTweaks
{
    /// <summary>The jump takes twice as long: every step of the arc lasts two ticks (same height, same horizontal distance).</summary>
    public bool SlowJump { get; set; }

    /// <summary>The protagonist's walking animation shows each sprite twice as long.</summary>
    public bool SlowWalkAnimation { get; set; }

    /// <summary>Mork's speed while he is moving, in percent of the original (100 = original). Distances and timings of each of his
    /// moves stay the same, he just takes proportionally longer: some of his movement ticks are skipped.</summary>
    public int MorkSpeedPercent { get; set; } = 100;

    /// <summary>"Revamped" movement: (1) a held jump key does not make the character jump again and again - it needs a new press;
    /// (2) the direction keys that are actually held keep working after any other key is released (the original zeroes all input
    /// on ANY release), so you can change direction while falling or after landing.</summary>
    public bool RevampedMovement { get; set; }

    /// <summary>One tick of a jump arc. Normally every tick advances the arc. With <paramref name="slow"/> each arc step lasts two ticks:
    /// the step's movement is split between them (first tick takes the larger half, so the totals are exactly the original ones).
    /// Returns true when the arc should advance to its next step.</summary>
    internal static bool JumpAdvance(bool slow, ref int sub, ref int dx, ref int dy)
    {
        if (!slow) return true;
        if (sub == 0)
        {
            sub = 1;
            dx -= dx / 2;
            dy -= dy / 2;
            return false;
        }
        sub = 0;
        dx /= 2;
        dy /= 2;
        return true;
    }
}

/// <summary>Which direction keys are physically held, by key code, so releasing one key only releases that key.</summary>
internal sealed class HeldDirections
{
    private readonly HashSet<int> _left = new(), _right = new();
    private int _last;                                   // -1 / +1: direction of the most recent press

    private static bool IsLeft(int key, int action) => key == 52 || action == 2 || key == 49;
    private static bool IsRight(int key, int action) => key == 54 || action == 5 || key == 51;

    public void Press(int key, int action)
    {
        if (IsLeft(key, action)) { _left.Add(key); _last = -1; }
        else if (IsRight(key, action)) { _right.Add(key); _last = 1; }
    }

    public void Release(int key)
    {
        _left.Remove(key);
        _right.Remove(key);
    }

    public void Clear() { _left.Clear(); _right.Clear(); }

    /// <summary>Same scale as the original input: -2 left, +2 right, 0 none. The latest press wins when both are held.</summary>
    public int X
    {
        get
        {
            bool l = _left.Count > 0, r = _right.Count > 0;
            if (l && r) return _last < 0 ? -2 : 2;
            return l ? -2 : r ? 2 : 0;
        }
    }
}
