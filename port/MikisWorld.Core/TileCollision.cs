namespace MikisWorld.Core;

/// <summary>Swept tile collision, one axis at a time (original class <c>f</c>).
/// All coordinates are fixed point with <see cref="Shift"/> fractional bits except tile sizes.</summary>
public sealed class TileCollision : ITileCollision
{
    private int[] _map; private int _mapW, _mapH, _tileW, _tileH; private int[] _flags;
    public int Shift { get; private set; }
    public int OutDx { get; private set; }
    public int OutDy { get; private set; }
    private bool _hitX, _hitY;

    public void Setup(int[] map, int mapW, int mapH, int tileW, int tileH, int[] flags)
    {
        _map = map; _mapW = mapW; _mapH = mapH; _tileW = tileW; _tileH = tileH; _flags = flags;
    }

    public void SetShift(int s) { Shift = s; }

    /// <summary>Moves a box (x,y,w,h) by (dx,dy). Returns true if blocked; OutDx/OutDy hold the allowed motion.
    /// Tile flag bits: 2 = passable moving right, 4 = passable moving left, 8 = passable moving down, 16 = passable moving up.</summary>
    public bool Move(int x, int y, int w, int h, int dx, int dy)
    {
        int a = Shift;
        OutDx = dx; OutDy = dy; _hitX = false; _hitY = false;
        int t0, t1, r0, r1, idx;
        if (dx != 0)
        {
            t0 = x; t1 = x + dx;
            if (dx > 0) { t0 += w; t1 += w; }
            t0 /= _tileW; t1 /= _tileW;
            t0 >>= a; t1 >>= a;
            if (t0 != t1)
            {
                r0 = y / _tileH >> a;
                r1 = (y + h) / _tileH >> a;
                idx = t1 + r0 * _mapW;
                while (r0 <= r1)
                {
                    int f = _flags[_map[idx]];
                    if (f != 0)
                    {
                        if (dx > 0 && (f & 2) == 0) { OutDx = t1 * _tileW - (w >> a) - (x >> a) - 1 << a; _hitX = true; break; }
                        if (dx < 0 && (f & 4) == 0) { OutDx = -x % (_tileW << a); _hitX = true; break; }
                    }
                    r0++; idx += _mapW;
                }
            }
        }
        if (dy != 0)
        {
            t0 = y; t1 = y + dy;
            if (dy > 0) { t0 += h; t1 += h; }
            t0 /= _tileH; t1 /= _tileH;
            t0 >>= a; t1 >>= a;
            if (t0 != t1)
            {
                r0 = x / _tileW >> a;
                r1 = (x + w) / _tileW >> a;
                idx = r0 + t1 * _mapW;
                while (r0 <= r1)
                {
                    int f = _flags[_map[idx]];
                    if (f != 0)
                    {
                        if (dy > 0 && (f & 8) == 0) { OutDy = t1 * _tileH - (h >> a) - (y >> a) - 1 << a; _hitY = true; break; }
                        if (dy < 0 && (f & 0x10) == 0) { OutDy = -y % (_tileH << a); _hitY = true; break; }
                    }
                    r0++; idx++;
                }
            }
        }
        return _hitX || _hitY;
    }
}
