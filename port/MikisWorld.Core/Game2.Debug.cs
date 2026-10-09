using System.Text;

namespace MikisWorld.Core;

public sealed partial class Game2
{
    // ---- test hooks (MikisWorld.TraceTest replays traces recorded from the original MikisWorld2.jar) ----
    internal void DebugTeleport(int fixedX, int fixedY) { _px = fixedX; _py = fixedY; }
    internal void DebugSetState(int state) { _state = state; }
    internal int DebugLives { get => _lives; set => _lives = value; }
    internal void DebugSetCoins(int coins) { _coins = coins; }
    internal void DebugEndGame() { _gameOverTimer = 0; _state = State.GameOver; }

    private static int Hash(short[] a)
    {
        if (a == null) return 0;
        int h = 17;
        foreach (short x in a) h = unchecked(h * 31 + x);
        return h;
    }

    private static int Hash(int[] a)
    {
        if (a == null) return 0;
        int h = 17;
        foreach (int x in a) h = unchecked(h * 31 + x);
        return h;
    }

    private static string Arr(int[] a) => a == null ? "null" : string.Join(",", a);

    /// <summary>One trace line, same fields and order as tools/harness/src/Trace.java (with names_mw2.properties).</summary>
    internal string DebugLine(int tick)
    {
        var sb = new StringBuilder();
        sb.Append(tick).Append(' ').Append(_state).Append(' ').Append(_area).Append(' ').Append(_motion).Append(' ').Append(_sprite).Append(' ')
          .Append(_px).Append(' ').Append(_py).Append(' ').Append(_camX).Append(' ').Append(_camY).Append(' ')
          .Append(_coins).Append(' ').Append(_lives).Append(' ').Append(_rainbows).Append(' ').Append(_finalRainbows).Append(' ').Append(_stage).Append(' ')
          .Append(_interact).Append(' ').Append(_gameOverTimer).Append(' ').Append(_finalTimer).Append(' ').Append(_frame).Append(' ')
          .Append(_save[0]).Append(' ').Append(_pendingMode).Append(' ');
        if (_projState == null || _projPos == null) sb.Append('-');
        else for (int i = 0; i < 3; i++) sb.Append(_projState[i]).Append(':').Append(_projPos[i * 2]).Append(':').Append(_projPos[i * 2 + 1]).Append(';');
        sb.Append(' ').Append(Hash(_platformX)).Append(' ').Append(Hash(_map.Tiles)).Append(' ');
        if (!_bonusInitialised) sb.Append('-');
        else for (int i = 0; i < Board.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Board[i]); }
        sb.Append(' ').Append(_morkState).Append(' ').Append(_morkX).Append(' ').Append(_morkY).Append(' ').Append(_morkTimer).Append(' ')
          .Append(_spark).Append(' ').Append(_markCooldown).Append(' ').Append(_morkFrame).Append(' ')
          .Append(_rainbows).Append(' ').Append(_finalRainbows).Append(' ').Append(Arr(_fxX)).Append(' ').Append(Arr(_fxY));
        return sb.ToString();
    }

    internal static string DebugConsts(Game2 g) =>
        $"#consts b2={g._tileW} cg={g._tileH} cy={g._scaleX} cx={g._scaleY} aT={g._playerW} a6={g._playerH} b9={g._itemW} da={g._itemH} " +
        $"G={g._hitFxDx} aA={g._hitFxDy} am={g._camX} cE={g._airSpeed} bi={g._walkSpeed} bW={g._fallSpeed} aK={g._riseSpeed} " +
        $"dl={g._totalCoins} bu={g._gateX} bs={g._gateY} ax={g._lord1Platform} c4={g._lord2Platform} cR={g._camMaxX} " +
        $"b4={g._outerX} cp={g._innerX} b8={g._innerY} cd={g._camMinY} cN={g._camMaxY} S={string.Join(",", g._jumpTable)}";
}
