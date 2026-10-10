namespace MikisWorld.Core;

/// <summary>Debug wireframe of the boxes the game logic really uses. None of them exists as a rectangle in the original code:
/// each one is reconstructed from the arithmetic of the update step, so what is drawn is what is tested.</summary>
public abstract partial class GameBase
{
    public bool ShowHitboxes { get; set; }

    /// <summary>One entry per tile id: collision flags (0 free, 1 solid, 22 one-way platform...).</summary>
    protected abstract int[] TileFlags { get; }

    /// <summary>The tile collision of game 2 shrinks the box by one pixel per axis.</summary>
    protected virtual int ColliderShrink => 0;

    /// <summary>Boxes of things only one game has (Mork).</summary>
    protected virtual void DrawGameSpecificHitboxes(Canvas g) { }

    // colours of the wireframe
    protected const int ColSprite = 0x808080, ColCollider = 0x00FF00, ColLanding = 0xFFFF00, ColPlatform = 0x00FFFF,
                        ColSolid = 0xFF4040, ColOneWay = 0xFFA000, ColPickup = 0x8080FF, ColHurt = 0xFF0080, ColProjectile = 0xFFFFFF,
                        ColMork = 0xFF00FF;

    protected static void Line(Canvas g, int rgb, int x, int y, int w, int h)
    {
        g.SetColor(rgb >> 16, rgb >> 8, rgb);
        g.DrawRect(x, y, w, h);
    }

    /// <summary>A box given by its top-left corner and size in WORLD pixels (drawn inclusively, so a w x h box covers w x h pixels).</summary>
    protected void Box(Canvas g, int rgb, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        Line(g, rgb, x - _camX, y - _camY, w - 1, h - 1);
    }

    protected void Cross(Canvas g, int rgb, int x, int y)
    {
        g.SetColor(rgb >> 16, rgb >> 8, rgb);
        x -= _camX; y -= _camY;
        g.FillRect(x - 2, y, 5, 1);
        g.FillRect(x, y - 2, 1, 5);
    }

    private bool InPlay => _state == State.Playing || _state == State.Lord1 || _state == State.Lord2 || _state == State.FinalStage;

    public void DrawHitboxes(Canvas g)
    {
        if (!ShowHitboxes || !_render || _area < 0 || !InPlay) return;
        g.SetClip(0, 0, L.ScreenW, L.ScreenH);
        int tx = _px >> 4, ty = _py >> 4;

        // solid / one-way tiles in view
        var flags = TileFlags;
        int w = _map.Width, rows = _map.Height;
        int c0 = Math.Max(0, _camX / _tileW), c1 = Math.Min(w - 1, (_camX + L.ScreenW) / _tileW);
        int r0 = Math.Max(0, _camY / _tileH), r1 = Math.Min(rows - 1, (_camY + L.ScreenH) / _tileH);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                int tile = _map.Tiles[r * w + c];
                int f = tile >= 0 && tile < flags.Length ? flags[tile] : 0;
                if (f != 0) Box(g, f == 1 ? ColSolid : ColOneWay, c * _tileW, r * _tileH, _tileW, _tileH);
            }

        // platforms (the sweep only looks at their top edge)
        for (int p = _visFirst; p < _visFirst + _visCount && p < _platforms.Length; p++)
        {
            if ((_platforms[p][3] & 1) != 0) continue;
            Box(g, ColPlatform, _platformX[p], _platforms[p][1], _tileW * _platforms[p][2], 1);
        }

        // the player: sprite frame, collision box against tiles, landing line against platforms
        Box(g, ColSprite, tx, ty + 1 - _playerH, _playerW, _playerH);
        int inset = 70 * _playerW / 200;
        Box(g, ColCollider, (_px + inset) >> 4, ty - _playerH, ((_playerW - inset << 4) >> 4) - ColliderShrink, _playerH - ColliderShrink);
        Box(g, ColLanding, tx + _playerW / 2 - _hitHalfW, ty + 1, 2 * _hitHalfW + 1, 1);

        // the tiles the player picks up / enters (coins, keys, portals, gate)
        int rowTop = (ty - _playerH / 2) / _tileH, rowBottom = ty / _tileH;
        int colLeft = (tx + _playerW / 3) / _tileW, colRight = (tx + _playerW - _playerW / 3) / _tileW;
        Box(g, ColPickup, colLeft * _tileW, rowTop * _tileH, (colRight - colLeft + 1) * _tileW, (rowBottom - rowTop + 1) * _tileH);

        // projectiles: the point the damage test uses, and the zone around the player's feet it has to reach
        int cx = tx + _playerW / 2;
        Box(g, ColHurt, cx - 3, ty - 4, 7, 6);
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] == -1) continue;
            int px = _projPos[i * 2], py = _projPos[i * 2 + 1];
            Box(g, ColProjectile, px, py - _itemH, _itemW, _itemH);
            Cross(g, ColProjectile, px, py);
        }

        DrawGameSpecificHitboxes(g);
    }
}
