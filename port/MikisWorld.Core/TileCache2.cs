namespace MikisWorld.Core;

/// <summary>Drawing side of MikisWorld 2's tile layer (original class <c>i</c>): the map is painted through a ring-buffer
/// cache (a bitmap holding <c>cols x rows</c> tiles that wraps around as the camera scrolls). The behaviour, including
/// the stale content direct writes into the tile array can leave behind, is reproduced literally so frames match.</summary>
public sealed class TileCache2
{
    private int[] _data;
    private int _mapW, _mapH, _tileW, _tileH;
    private Bitmap[] _tiles;
    private Bitmap _cache;
    private Canvas _cg;
    private int _cols, _rows;
    private int _lastCol = -1, _lastRow = -1;
    public bool ClampX = true, ClampY = true;

    public void Setup(Bitmap[] tiles, int cols, int rows)
    {
        int w = cols * tiles[0].Width, h = rows * tiles[0].Height;
        if (_cache == null || _cache.Width != w || _cache.Height != h)
        {
            _cache = new Bitmap(w, h);
            _cg = new Canvas(_cache);
        }
        _tiles = tiles;
        _tileW = tiles[0].Width;
        _tileH = tiles[0].Height;
        _cols = cols;
        _rows = rows;
    }

    public void Attach(int[] data, int mapW, int mapH) { _data = data; _mapW = mapW; _mapH = mapH; Reset(); }
    public void Reset() { _lastCol = -1; _lastRow = -1; }

    /// <summary>Original i.a(x, y, tile) after the data write: repaint the tile if it lies in the cached window.</summary>
    public void OnTileChanged(int x, int y, int tile)
    {
        if (_cg == null) return;
        if (x >= _lastCol && x < _lastCol + _cols && y >= _lastRow && y < _lastRow + _rows)
            _cg.DrawImage(_tiles[tile], x % _cols * _tileW, y % _rows * _tileH, Canvas.Left | Canvas.Top);
    }

    private void DrawTiles(Canvas g, int x, int y, int mapX, int mapY, int cntW, int cntH)
    {
        if (mapX + cntW > _mapW) --cntW;
        if (mapY + cntH > _mapH) --cntH;
        int py = y;
        int row = mapX + mapY * _mapW;
        for (int j = 0; j < cntH; j++)
        {
            int px = x;
            int idx = row;
            for (int i = 0; i < cntW; i++, idx++)
            {
                g.DrawImage(_tiles[_data[idx]], px, py, Canvas.Left | Canvas.Top);
                px += _tileW;
            }
            py += _tileH;
            row += _mapW;
        }
    }

    /// <summary>Original i.R4(Graphics, x, y, w, h, camX, camY).</summary>
    public void Draw(Canvas g, int x, int y, int viewW, int viewH, int camX, int camY)
    {
        int cw = _cache.Width, ch = _cache.Height;
        if (ClampX)
        {
            if (camX < 0) camX = 0;
            else if (camX >= _mapW * _tileW - viewW) camX = _mapW * _tileW - viewW;
        }
        else if (camX < 0) camX = camX % (_mapW * _tileW) + _mapW * _tileW;
        if (ClampY)
        {
            if (camY < 0) camY = 0;
            else if (camY >= _mapH * _tileH - viewH) camY = _mapH * _tileH - viewH;
        }
        else if (camY < 0) camY = camY % (_mapH * _tileH) + _mapH * _tileH;
        int tileCol = camX / _tileW, tileRow = camY / _tileH;
        int offX = camX % _tileW, offY = camY % _tileH;
        int dCol = tileCol - _lastCol, dRow = tileRow - _lastRow;
        if (_lastCol == -1 && ClampX || dCol > 1 || dCol < -1 || dRow > 1 || dRow < -1)
        {
            int px = camX - offX;
            int py = camY - offY;
            int c = tileCol % _cols, r = tileRow % _rows;
            int rc = _cols - c, rr = _rows - r;
            px %= cw;
            py %= ch;
            DrawTiles(_cg, px, py, tileCol, tileRow, rc, rr);
            DrawTiles(_cg, px, py - r * _tileH, tileCol, tileRow + rr, rc, r);
            DrawTiles(_cg, px - c * _tileW, py, tileCol + rc, tileRow, c, rr);
            DrawTiles(_cg, px - c * _tileW, py - r * _tileH, tileCol + rc, tileRow + rr, c, r);
        }
        else if (dCol != 0 || dRow != 0)
        {
            int newCol = -1, newRow = -1;
            if (tileCol < _lastCol) newCol = 0;
            else if (tileCol > _lastCol) newCol = _cols - 1;
            if (tileRow < _lastRow) newRow = 0;
            else if (tileRow > _lastRow) newRow = _rows - 1;
            int a, b, idx, count, k;
            if (newCol != -1)
            {
                int mapCol = (tileCol + newCol) % _mapW;
                a = camX - offX + newCol * _tileW;
                b = camY - offY;
                a %= cw;
                b %= ch;
                idx = mapCol + tileRow * _mapW;
                count = _rows;
                if (tileRow + count > _mapH) count = _mapH - tileRow;
                for (k = 0; k < _rows; k++)
                {
                    if (!ClampY) idx %= _data.Length;
                    _cg.DrawImage(_tiles[_data[idx]], a, b, Canvas.Left | Canvas.Top);
                    idx += _mapW;
                    if ((b += _tileH) >= ch) b = 0;
                    if (k == count - 1) idx %= _mapW;
                }
            }
            if (newRow != -1)
            {
                int mapRow = (tileRow + newRow) % _mapH;
                a = camX - offX;
                b = camY - offY + newRow * _tileH;
                a %= cw;
                b %= ch;
                idx = tileCol + mapRow * _mapW;
                count = _cols;
                if (tileCol + count > _mapW) count = _mapW - tileCol;
                for (k = 0; k < _cols; k++)
                {
                    if (!ClampX) idx %= _data.Length;
                    _cg.DrawImage(_tiles[_data[idx]], a, b, Canvas.Left | Canvas.Top);
                    ++idx;
                    if ((a += _tileW) >= cw) a = 0;
                    if (k == count - 1) idx -= _mapW;
                }
            }
        }
        _lastCol = tileCol;
        _lastRow = tileRow;
        int sx = camX - offX;
        int sy = camY - offY;
        sx %= cw;
        sy %= ch;
        const int TL = Canvas.Left | Canvas.Top;
        g.DrawImage(_cache, x - offX - sx, y - offY - sy, TL);
        g.DrawImage(_cache, x - offX - sx, y - offY - sy + ch, TL);
        g.DrawImage(_cache, x - offX - sx + cw, y - offY - sy, TL);
        g.DrawImage(_cache, x - offX - sx + cw, y - offY - sy + ch, TL);
    }
}
