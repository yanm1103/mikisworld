namespace MikisWorld.Core;

/// <summary>Tile collision of MikisWorld 2 (original class <c>e</c>). Differs from the first game's <see cref="TileCollision"/>:
/// the box is 1 px smaller in each axis, positions wrap around the map, scanning rows/columns wraps around the tile array,
/// and the vertical pass uses the horizontal result (<c>x + OutDx</c>) to choose its columns.</summary>
public sealed class TileCollision2
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

    public bool Move(int x, int y, int w, int h, int dx, int dy)
    {
        int a = Shift;
        w -= 1 << a;
        h -= 1 << a;
        OutDx = dx; OutDy = dy; _hitX = false; _hitY = false;
        int mapPixelsW = _mapW * _tileW << a;
        int mapPixelsH = _mapH * _tileH << a;
        if (x < 0) x = x % mapPixelsW + mapPixelsW;
        else if (x >= mapPixelsW) x %= mapPixelsW;
        if (y < 0) y = y % mapPixelsH + mapPixelsH;
        else if (y >= mapPixelsH) y %= mapPixelsH;

        int t0, t1, r0, r1, idx;
        if (dx != 0)
        {
            t0 = x; t1 = x + dx;
            if (dx > 0) { t0 += w; t1 += w; }
            t0 /= _tileW; t1 /= _tileW;
            if ((t0 >>= a) != (t1 >>= a))
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
                    idx = idx + _mapW < _map.Length ? idx + _mapW : (idx + _mapW) % _map.Length;
                    r0++;
                }
            }
        }
        if (dy != 0)
        {
            t0 = y; t1 = y + dy;
            if (dy > 0) { t0 += h; t1 += h; }
            t0 /= _tileH; t1 /= _tileH;
            if ((t0 >>= a) != (t1 >>= a))
            {
                r0 = (x + OutDx) / _tileW >> a;
                r1 = (x + OutDx + w) / _tileW >> a;
                idx = r0 + t1 * _mapW;
                int col = r0;
                while (r0 <= r1)
                {
                    if (idx >= _map.Length) idx %= _map.Length;
                    int f = _flags[_map[idx]];
                    if (f != 0)
                    {
                        if (dy > 0 && (f & 8) == 0) { OutDy = t1 * _tileH - (h >> a) - (y >> a) - 1 << a; _hitY = true; break; }
                        if (dy < 0 && (f & 0x10) == 0) { OutDy = -y % (_tileH << a); _hitY = true; break; }
                    }
                    ++idx;
                    if (++col == _mapW) { col = 0; idx -= _mapW; }
                    r0++;
                }
            }
        }
        return _hitX || _hitY;
    }
}
