using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("MikisWorld.TraceTest")]

namespace MikisWorld.Core;

public sealed partial class Game
{
    // ---- test hooks (used by MikisWorld.TraceTest to replay traces recorded from the original bytecode) ----
    internal void DebugTeleport(int fixedX, int fixedY) { _px = fixedX; _py = fixedY; }
    internal void DebugSetState(int state) { _state = state; }
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

    /// <summary>One trace line, same fields and order as tools/harness/src/Trace.java line().</summary>
    internal string DebugLine(int tick)
    {
        var sb = new StringBuilder();
        sb.Append(tick).Append(' ').Append(_state).Append(' ').Append(_area).Append(' ').Append(_motion).Append(' ').Append(_sprite).Append(' ')
          .Append(_px).Append(' ').Append(_py).Append(' ').Append(_camX).Append(' ').Append(_camY).Append(' ')
          .Append(_coins).Append(' ').Append(_lives).Append(' ').Append(_birds).Append(' ').Append(_finalDoor).Append(' ').Append(_stage).Append(' ')
          .Append(_interact).Append(' ').Append(_gameOverTimer).Append(' ').Append(_finalTimer).Append(' ').Append(_frame).Append(' ')
          .Append(_save[0]).Append(' ').Append(_pendingMode).Append(' ');
        for (int i = 0; i < 3; i++) sb.Append(_projState[i]).Append(':').Append(_projPos[i * 2]).Append(':').Append(_projPos[i * 2 + 1]).Append(';');
        sb.Append(' ').Append(Hash(_platformX)).Append(' ').Append(Hash(_map.Tiles)).Append(' ');
        if (!_bonusInitialised) sb.Append('-');
        else for (int i = 0; i < Board.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Board[i]); }
        return sb.ToString();
    }

    internal static string DebugConsts(Game g) =>
        $"#consts bT={g._tileW} b6={g._tileH} cl={g._scaleX} ck={g._scaleY} aP={g._playerW} a0={(g._levelLoaded ? g._playerH : 0)} bZ={g._itemW} cU={g._itemH} C={g._hitFxDx} aw={(g._levelLoaded ? g._hitFxDy : 0)} ah={g._hitHalfW} cr={g._airSpeed} be={g._walkSpeed} bO={g._fallSpeed} c7={g._totalCoins} bq={g._gateX} bo={g._gateY} at={g._lord1Platform} cP={g._lord2Platform} cD={g._camMaxX} bV={g._outerX} cd={g._innerX} bY={g._innerY} b3={g._camMinY} cz={g._camMaxY} S={string.Join(",", g._jumpTable)}";
}
