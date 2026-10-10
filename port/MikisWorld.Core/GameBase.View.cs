namespace MikisWorld.Core;

/// <summary>The "Extended" view: the picture the frontend asks for can be larger than the original screen (176x144 / 128x128).
/// The game logic keeps working with the original screen: the camera, its dead zones and everything validated against the original
/// stay as they were. Only the drawing shows more around the camera's window, up to the limits of the map and of the current area.</summary>
public abstract partial class GameBase
{
    private int _viewW, _viewH;

    public abstract void EnableRendering();

    /// <summary>Reads the sprite sheets again, keeping the game as it is.</summary>
    public void ReloadAssets()
    {
        _render = false;
        EnableRendering();
        OnViewModeChanged();
    }

    /// <summary>Size of the picture the game draws (never smaller than the original screen).</summary>
    public int ViewWidth => Math.Max(_viewW, L.ScreenW);
    public int ViewHeight => Math.Max(_viewH, L.ScreenH);

    /// <summary>True when the picture is larger than the original screen.</summary>
    public bool WideView => ViewWidth > L.ScreenW || ViewHeight > L.ScreenH;

    public void SetView(int w, int h)
    {
        int oldW = ViewWidth, oldH = ViewHeight;
        _viewW = Math.Max(w, L.ScreenW);
        _viewH = Math.Max(h, L.ScreenH);
        if (ViewWidth != oldW || ViewHeight != oldH) OnViewModeChanged();
    }

    internal void DebugSetStage(int stage) { _stage = stage; }

    /// <summary>Hook: the debug "Skip level" is about to move the player somewhere the story did not take him.</summary>
    protected virtual void OnSkipLevel() { }

    /// <summary>Hook: the picture changed size; a game with a drawing cache has to forget it.</summary>
    protected virtual void OnViewModeChanged() { }

    /// <summary>How much further than the original screen the picture can reach vertically (0 unless the wide view is on).</summary>
    protected int ExtraView => Tweaks.WideView ? Math.Max(0, ViewHeight - L.ScreenH) : 0;

    /// <summary>Where the last stage's lightning is launched: the top of the original screen, or (wide view) the top of the picture.
    /// Never so high that its collision box would leave the map (the original never gets there: its camera is below the top).</summary>
    protected int StormSpawnY => ExtraView > 0 ? Math.Max(_camY - ExtraView, _itemH + 2) : _camY;

    // ---- what the next world drawing uses (set by PrepareDraw)
    protected int _vw, _vh;           // size of the picture being drawn
    protected int _dx, _dy;           // world position of its top-left corner (the camera when not wide)
    protected int _wx, _wy, _ww, _wh; // the part of the picture that is inside the map / current area
    protected int _sx, _sy;           // where the original screen's top-left sits for things anchored to the screen (the sky)

    /// <summary>Is the state something that draws the world (and so can use the whole wide picture)?</summary>
    protected bool DrawsWorld => _area >= 0 && (_state == State.Playing || _state == State.Lord1 || _state == State.Lord2 || _state == State.FinalStage
                                               || _state == State.GameOver || _state == State.Unknown7 || _state == State.WipeIn || _state == State.WipeOut);

    /// <summary>Chooses the drawing area for this frame: the original screen, or (wide view, world states) a larger window that follows the camera
    /// but stays inside the map and the current area, with the original screen's window always inside it.</summary>
    protected void PrepareDraw(bool world)
    {
        int lw = L.ScreenW, lh = L.ScreenH;
        if (!world || !WideView)
        {
            _vw = lw; _vh = lh; _dx = _camX; _dy = _camY;
            _wx = 0; _wy = 0; _ww = lw; _wh = lh; _sx = 0; _sy = 0;
            return;
        }
        _vw = ViewWidth; _vh = ViewHeight;
        int mapW = _map.Width * _tileW;
        int top = _camMinY, bottom = _camMaxY + lh + 1;
        _dx = Place(_camX, lw, _vw, 0, mapW);
        _dy = Place(_camY, lh, _vh, top, bottom);
        int x0 = Math.Max(0, -_dx), x1 = Math.Min(_vw, mapW - _dx);
        int y0 = Math.Max(0, top - _dy), y1 = Math.Min(_vh, bottom - _dy);
        _wx = x0; _ww = Math.Max(0, x1 - x0);
        _wy = y0; _wh = Math.Max(0, y1 - y0);
        _sx = (_vw - lw) / 2; _sy = _vh - lh;              // the sky hangs from the bottom of the picture, where the original screen had its horizon
    }

    /// <summary>Origin of a picture axis: centred on the camera's window, kept inside [lo, hi] when the area is larger than the picture
    /// (otherwise the area is centred), and always containing the camera's window.</summary>
    private static int Place(int cam, int logical, int view, int lo, int hi)
    {
        int span = hi - lo;
        if (view >= span) return lo - (view - span) / 2;
        int want = cam - (view - logical) / 2;
        int min = Math.Max(cam + logical - view, lo), max = Math.Min(cam, hi - view);
        return min > max ? cam : Math.Clamp(want, min, max);
    }

    protected void FullClip(Canvas g) => g.SetClip(0, 0, _vw, _vh);

    /// <summary>Draws one tile of the map at a picture position (each game has its own tile sheet).</summary>
    protected abstract void DrawTile(Canvas g, int tile, int px, int py);

    /// <summary>The map inside the area, for a picture larger than the original screen (no caching, no tile limit).</summary>
    protected void DrawTileLayerWide(Canvas g)
    {
        int rows0 = Math.Max(0, (_camMinY) / _tileH), rows1 = Math.Min(_map.Height - 1, (_camMaxY + L.ScreenH) / _tileH);
        int c0 = Math.Max(0, _dx / _tileW), c1 = Math.Min(_map.Width - 1, (_dx + _vw - 1) / _tileW);
        int r0 = Math.Max(rows0, Math.Max(0, _dy / _tileH)), r1 = Math.Min(rows1, (_dy + _vh - 1) / _tileH);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                int tile = _map.Tiles[r * _map.Width + c];
                if (tile != 0 || DrawsTileZero) DrawTile(g, tile, c * _tileW - _dx, r * _tileH - _dy);
            }
    }

    /// <summary>Game 2 paints its sky as tile 0; game 1 has a separate sky and tile 0 is empty.</summary>
    protected virtual bool DrawsTileZero => false;
}
