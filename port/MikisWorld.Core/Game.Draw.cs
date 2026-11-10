namespace MikisWorld.Core;

/// <summary>Rendering (original d.a(Graphics) and helpers, e.cfr_renamed_3(Graphics), a.a(Graphics,...)).
/// Draws a 176x144 frame onto a <see cref="Canvas"/>; text is emitted as <see cref="TextCommand"/>s.</summary>
public sealed partial class Game
{
    private Bitmap _imgTiles, _imgSymbols;
    private int _skyR, _skyG, _skyB;                   // flat sky colour of the 128x128 build (menus and the minigame)

    protected override bool DrawsTileZero => L.TileSky;

    /// <summary>Loads the sprite sheets so <see cref="Draw"/> can be used. Logic-only users (tests) can skip this.</summary>
    public override void EnableRendering()
    {
        if (_render) return;
        _imgTiles = _assets.LoadBitmap("tiles.png");
        if (!L.TileSky)
        {
            _imgSky = _assets.LoadBitmap("sky.png");
            _imgClouds = _assets.LoadBitmap("clouds.png");
        }
        else { int c = _imgTiles.Pixels[0]; _skyR = c >> 16 & 255; _skyG = c >> 8 & 255; _skyB = c & 255; }   // the 128x128 build paints its sky into tile 0
        _imgLord = _assets.LoadBitmap("lord.png");
        _imgPlayer = _assets.LoadBitmap("player.png");
        _imgItems = _assets.LoadBitmap("items.png");
        _imgNumbers = _assets.LoadBitmap("numbers.png");
        _imgTime = _assets.LoadBitmap("time.png");
        _imgDamage = _assets.LoadBitmap("damage.png");
        _imgGameOver = _assets.LoadBitmap("gameover.png");
        _imgEnd = _assets.LoadBitmap("end.png");
        _imgSymbols = _assets.LoadBitmap("symbols.png");
        _imgGrid = _assets.LoadBitmap("grid.png");
        _imgBonus = _assets.LoadBitmap("bonus.png");
        _imgCursor = _assets.LoadBitmap("cursor.png");
        _snapshot = new Bitmap(L.ScreenW, L.ScreenH);

        // lateral bars of the minigame: strips cut from bonus.png (original d.i())
        _barLeft = new Bitmap(L.BarW, L.ScreenH);
        _barRight = new Bitmap(L.BarW, L.ScreenH);
        int off = L.OrbW * 2;
        var gl = new Canvas(_barLeft);
        for (int y = 0; y < _barLeft.Height; y += L.BarStripH)
        {
            gl.SetClip(0, y, L.BarW, L.BarStripH);
            gl.DrawImage(_imgBonus, -off, y, Canvas.Left | Canvas.Top);
        }
        off += L.BarW;
        var gr = new Canvas(_barRight);
        for (int y = 0; y < _barRight.Height; y += L.BarStripH)
        {
            gr.SetClip(0, y, L.BarW, L.BarStripH);
            gr.DrawImage(_imgBonus, -off, y, Canvas.Left | Canvas.Top);
        }
        _render = true;
    }

    protected override void DrawTile(Canvas g, int tile, int px, int py)
    {
        int src = -tile * _tileW;
        int sheetW = _imgTiles.Width;
        g.SetClip(0, 0, _vw, _vh);
        g.ClipRect(px, py, _tileW, _tileH);
        g.DrawImage(_imgTiles, px + src % sheetW, py + src / sheetW * _tileH, TL);
    }

    /// <summary>Original d.a(Graphics): the whole frame for the current state. In the wide view, what is not the world (cutscenes, minigame)
    /// keeps the original screen, centred on black.</summary>
    public bool Draw(Canvas g)
    {
        if (WideView && !DrawsWorld)
        {
            int vw = ViewWidth, vh = ViewHeight, ox = (vw - L.ScreenW) / 2, oy = (vh - L.ScreenH) / 2;
            g.SetClip(0, 0, vw, vh);
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, vw, vh);
            g.Translate(ox, oy);
            DrawFrame(g);
            g.Translate(-ox, -oy);
            return true;
        }
        return DrawFrame(g);
    }

    private bool DrawFrame(Canvas g)
    {
        PrepareDraw(DrawsWorld);
        int W = _vw, H = _vh;
        if (_state >= State.ModeMenu && _state <= State.BonusAiMoving) DrawBonus(g);
        else if (_state == State.WipeOut)
        {
            g.SetClip(0, 0, W, H);
            int n = -W * (35 - _frame) / 35;
            g.SetColor(0, 0, 0);
            g.FillRect(n + W, 0, -n, H);
            g.DrawImage(_snapshot, n, 0, TL);
        }
        else if (_state == State.WipeIn)
        {
            g.SetClip(0, 0, W, H);
            int n = W * _frame / 35;
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, n, H);
            g.DrawImage(_snapshot, n, 0, TL);
        }
        else DrawScene(g);

        if (_state == State.Cutscene || _state == State.Ending)
        {
            int bar = 15 * H / 100;
            g.SetClip(0, 0, W, H);
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, bar);
            g.FillRect(0, H - bar, W, bar);
            if (_state == State.Ending)
            {
                if (_gameOverTimer > 7000)
                {
                    g.SetFont(FontKind.Bold);
                    g.SetClip(0, 0, W, H);
                    g.SetColor(0, 0, 0);
                    g.FillRect(0, 0, W, H);
                    g.SetColor(255, 255, 255);
                    int x = W / 2;
                    int fh = g.Metrics.Height(FontKind.Bold);
                    int y = H / 2 - fh;
                    g.DrawString(Strings.Text(4), x, y, Canvas.Bottom | Canvas.HCenter);
                    g.DrawString(Strings.Text(5), x, y += fh, Canvas.Bottom | Canvas.HCenter);
                    g.DrawString(Strings.Text(6), x, y += fh, Canvas.Bottom | Canvas.HCenter);
                    g.DrawString(Strings.Text(7), x, y += fh, Canvas.Bottom | Canvas.HCenter);
                }
                else
                {
                    g.SetClip(0, 0, W, H);
                    g.DrawImage(_imgEnd, (W - _imgEnd.Width) / 2, (H - _imgEnd.Height) / 2, TL);
                }
            }
        }
        return true;
    }

    // ------------------------------------------------------------------ world scene (original d.cfr_renamed_6(Graphics))
    protected override void DrawScene(Canvas g)
    {
        PrepareDraw(DrawsWorld);
        int W = _vw, H = _vh;
        int ex = W - L.ScreenW, ey = H - L.ScreenH;       // how much bigger than the original screen (0 unless the view is wide)
        bool wide = ex != 0 || ey != 0;
        g.SetClip(0, 0, W, H);
        if (wide)
        {
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, H);                                 // outside the map and the area: black
            g.SetClip(_wx, _wy, _ww, _wh);
        }
        if (!L.TileSky) DrawSky(g, _frame, _camX, _sx, _sy, W, H, _wx, _wy, _ww, _wh);
        g.SetClip(0, 0, W, H);
        if (wide) DrawTileLayerWide(g);
        else DrawTileLayer(g, 0, 0, W, H, _camX, _camY);
        DrawActors(g);

        // floating cloud platforms
        for (int i = _visFirst; i < _visFirst + _visCount; i++)
        {
            int x = _platformX[i] - _dx;
            int y = _platforms[i][1] - _dy;
            if ((_platforms[i][3] & 1) == 0)
            {
                if (!L.TileSky) y -= 4;
                int bob = (_frame + i * 3) / 32 % 4;
                int sheetW = _imgTiles.Width;
                for (int k = 0; k < _platforms[i][2]; k++)
                {
                    int press = 0;
                    int rel;
                    if (_standingOn == i && (rel = (_px >> 4) - _dx + _playerW / 2 - x) <= _tileW && rel >= 0) press = 2;
                    g.SetClip(_wx, _wy, _ww, _wh);                  // a platform that sticks out of the map is cut at its edge
                    g.ClipRect(x, y + bob / 2 + press, _tileW, _tileH);
                    int src = -Tile.Cloud * _tileW;
                    int sx = src % sheetW;
                    int sy = src / sheetW * _tileH;
                    g.DrawImage(_imgTiles, x + sx, y + bob / 2 + sy + press, TL);
                    bob++;
                    bob %= 4;
                    x += _tileW;
                }
            }
            g.SetClip(0, 0, W, H);
        }

        // damage sparks
        int fxW = _imgDamage.Width / 2, fxH = _imgDamage.Height;
        for (int i = 0; i < _hitFxPos.Length; i += 2)
        {
            if (_hitFxPos[i] != -1)
            {
                int x = _hitFxPos[i] - _dx;
                int y = _hitFxPos[i + 1] - _dy;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - fxH, fxW, fxH);
                int f = (_frame - _hitFxStart[i / 2]) / 3 % 2;
                g.DrawImage(_imgDamage, x - f * fxW, y, BL);
            }
        }

        if (_state == State.GameOver)
        {
            int x = L.GameOverX + _hcx, y = L.GameOverY + _hcy;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            if (_lives != 0) y -= _imgGameOver.Height / 2;
            g.DrawImage(_imgGameOver, x, y, TL);
        }

        if (_state != State.Unknown7 && _area >= 0)
        {
            g.SetClip(0, 0, W, H);
            int x = L.LivesX + _hr, y = L.LivesY + _hb;
            for (int i = 0; i < _lives; i++)
            {
                g.SetClip(x, y, _tileW, _tileH);
                g.DrawImage(_imgTiles, x - Tile.Heart * _tileW % _imgTiles.Width, y - Tile.Heart * _tileW / _imgTiles.Width * _tileH, TL);
                x -= _tileW;
            }
            if (_area == Area.Final)
            {
                if (_finalTimer >= 60000)
                {
                    int t = _finalTimer - 60000;
                    if (t > 3500 && t < 5000) DrawBubble(g, Strings.Text(1));
                    else if (t > 2000 && t < 3400) DrawBubble(g, Strings.Text(2));
                    else if (t > 0 && t < 1900 && _finalTimer / 200 % 2 == 0) DrawBubble(g, Strings.Text(3));
                }
                if (_finalTimer < 60000 || _finalTimer / 500 % 2 == 0)
                {
                    g.SetClip(0, 0, W, H);
                    int bx = L.CoinHudX + _hl;
                    int by = L.CoinHudY - 2 + _hb;
                    g.SetClip(bx, by, _tileW, _tileH);
                    g.DrawImage(_imgTiles, bx - Tile.CagedBird * _tileW % _imgTiles.Width, by - 12 * _tileW / _imgTiles.Width * _tileH, TL);
                    g.SetClip(bx, by - _tileH, _tileW, _tileH);
                    g.DrawImage(_imgTiles, bx - 12 * _tileW % _imgTiles.Width, by - Tile.BirdSign * _tileW / _imgTiles.Width * _tileH - _tileH, TL);
                    DrawCounter(g, (bx += _tileW) + 3, by += (_tileH - _imgNumbers.Height) / 2, _imgNumbers, L.DigitW, 1, _finalBirdCells.Length - _finalDoor, false);
                }
                if (_finalTimer > 10000 || _frame / 15 % 2 == 0)
                {
                    int secs = _finalTimer / 1000;
                    if (secs > 60) secs = 60;
                    g.SetClip(0, 0, W, H);
                    DrawCounter(g, L.TimeHudX + _hr, L.TimeHudY + _ht, _imgTime, L.TimeDigitW, 2, secs, false);
                }
            }
            else
            {
                g.SetClip(0, 0, W, H);
                int left = _totalCoins - _coins;
                if (left < 0) left = 0;
                DrawCounter(g, L.CoinHudX + _hl, L.CoinHudY + _hb, _imgNumbers, L.DigitW, 4, left, true);
            }
        }
        if (_state == State.Cutscene) DrawCutsceneBubbles(g);
        bool hintShown = _interact != -1 && _hintTimer > 0;
        if (hintShown) DrawBubble(g, Strings.Text(0));
        if (_bonusMsgTimer > 0) DrawBubble(g, Strings.Text(12), BubbleLift(g, hintShown ? 1 : 0));
    }

    // original d.cfr_renamed_1(Graphics): sky + two parallax cloud bands
    /// <summary>The scrolling sky + clouds, for screens outside the game world (menus).</summary>
    public void DrawBackdrop(Canvas g, int frame)
    {
        if (L.TileSky) DrawFlatSky(g, frame / 4);
        else DrawSky(g, frame, 0, 0, 0, L.ScreenW, L.ScreenH, 0, 0, L.ScreenW, L.ScreenH);
    }

    /// <summary>The 128x128 build has no sky pictures: a flat colour (tile 0's) with a row of cloud tiles along the bottom edge.</summary>
    private void DrawFlatSky(Canvas g, int scroll)
    {
        int W = L.ScreenW, H = L.ScreenH;
        g.SetClip(0, 0, W, H);
        g.SetColor(_skyR, _skyG, _skyB);
        g.FillRect(0, 0, W, H);
        int src = -Tile.Cloud * _tileW, sheetW = _imgTiles.Width;
        for (int x = -(scroll % _tileW); x < W; x += _tileW)
        {
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, H - _tileH, _tileW, _tileH);
            g.DrawImage(_imgTiles, x + src % sheetW, H - _tileH + src / sheetW * _tileH, TL);
        }
    }

    // original d.cfr_renamed_10(Graphics): lord, freed bird, lightning bolts, player
    private void DrawActors(Canvas g)
    {
        int W = _vw, H = _vh;
        if (_state == State.Lord1 || _state == State.Lord2 || _state == State.FinalStage)
        {
            int x = _lordX - _dx;
            int y = _lordY - _dy;
            int h = _imgLord.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h, L.LordPoseW, h);
            g.DrawImage(_imgLord, x -= L.LordFrameW * 2 + L.LordPoseW * _lordFrame, y, BL);
        }
        if (_birdFxX != -1)
        {
            int x = _birdFxX - _dx;
            int y = _birdFxY - _dy;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - _itemH, _itemW, _itemH);
            g.DrawImage(_imgItems, x += -_itemW * (1 + _frame / 6 % 2), y, BL);
        }
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] != -1)
            {
                int x = _projPos[i * 2] - _dx;
                int y = _projPos[i * 2 + 1] - _dy;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - _itemH, _itemW, _itemH);
                g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, BL);
            }
        }
        if (_state != State.GameOver)
        {
            int x = (_px >> 4) - _dx;
            int y = (_py >> 4) - _dy + 1;
            int h = _imgPlayer.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h, _playerW, h);
            g.DrawImage(_imgPlayer, x -= _playerW * _sprite, y, BL);
        }
        if (_state == State.Cutscene) DrawCutsceneActors(g);
    }

    // original e.cfr_renamed_3(Graphics)
    private void DrawCutsceneActors(Canvas g)
    {
        var c = Cutscene;
        int symW = _imgSymbols.Width / 4, symH = _imgSymbols.Height;
        for (int i = 0; i < c.SpriteType.Length; i++)
        {
            if (!c.ActorVisible[i]) continue;
            int x = (c.ActorX[i] * _scaleX >> 4) - c.CameraX;
            int y = (c.ActorY[i] * _scaleY >> 4) - c.CameraY;
            switch (c.SpriteType[i])
            {
                case 0:
                    g.SetClip(x, y - _playerH, _playerW, _playerH);
                    g.DrawImage(_imgPlayer, x - c.Frame[i] * _playerW, y, BL);
                    break;
                case 1:
                    g.SetClip(x, y - _playerH, _playerW, _playerH);
                    g.DrawImage(_imgPlayer, x - (4 + c.Frame[i]) * _playerW, y, BL);
                    break;
                case 2:
                    {
                        int h = _imgLord.Height;
                        g.SetClip(x, y - h, L.LordFrameW, h);
                        g.DrawImage(_imgLord, x -= L.LordFrameW * c.Frame[i], y, BL);
                        break;
                    }
                case 3:
                    {
                        int h = _imgLord.Height;
                        g.SetClip(x, y - h, L.LordPoseW, h);
                        g.DrawImage(_imgLord, x -= L.LordFrameW * 2 + L.LordPoseW * c.Frame[i], y, BL);
                        break;
                    }
                case 4:
                    g.SetClip(x, y - _itemH, _itemW, _itemH);
                    g.DrawImage(_imgItems, x += -_itemW * (1 + c.Frame[i]), y, BL);
                    break;
                case 5:
                    if (c.Frame[i] != 0) break;
                    g.SetClip(x, y - _itemH, _itemW, _itemH);
                    g.DrawImage(_imgItems, x, y, BL);
                    break;
                case 6:
                    {
                        int w = _imgDamage.Width / 2, h = _imgDamage.Height;
                        g.SetClip(x, y - h, w, h);
                        g.DrawImage(_imgDamage, x -= w * c.Frame[i], y, BL);
                        break;
                    }
                case 7:
                case 8:
                case 9:
                case 10:
                    g.SetClip(x, y - symH, symW, symH);
                    g.DrawImage(_imgSymbols, x - symW * (c.SpriteType[i] - 7), y, BL);
                    break;
            }
        }
    }

    // original e.a(Graphics): speech bubbles that are currently on
    private void DrawCutsceneBubbles(Canvas g)
    {
        for (int i = 0; i < Cutscene.TextVisible.Length; i++)
            if (Cutscene.TextVisible[i]) DrawBubble(g, Strings.Text(i));
    }

    // original e.a(Graphics, String)
    private void DrawBubble(Canvas g, string text, int lift = 0)
    {
        text = KeyHints(text);
        int W = _vw, H = _vh;
        g.SetClip(0, 0, W, H);
        g.SetFont(FontKind.Small);
        int w = g.Metrics.StringWidth(text, FontKind.Small) + 4;
        int h = g.Metrics.Height(FontKind.Small) + 4;
        int y = H / 2 + 3 - lift;
        g.SetColor(99, 48, 0);
        g.FillRect(W / 2 - w / 2 - 1, y - h + 2 - 1, w + 2, h + 2);
        g.SetColor(255, 255, 255);
        g.DrawRect(W / 2 - w / 2 - 1, y - h + 2 - 1, w + 2, h + 2);
        g.DrawString(text, W / 2, y, Canvas.Bottom | Canvas.HCenter);
    }

    // original a.a(Graphics, ...): visible part of the tile map at camera (camX, camY)
    private void DrawTileLayer(Canvas g, int x, int y, int viewW, int viewH, int camX, int camY)
    {
        int w = _map.Width, h = _map.Height;
        if (camX < 0) camX = 0;
        else if (camX >= w * _tileW - viewW) camX = w * _tileW - viewW;
        if (camY < 0) camY = 0;
        else if (camY >= h * _tileH - viewH) camY = h * _tileH - viewH;
        int tx = camX / _tileW, ty = camY / _tileH;
        int offX = camX % _tileW, offY = camY % _tileH;
        int ox = x - offX, oy = y - offY;
        int rows = L.ViewTilesH;
        if (ty + rows > h) rows = h - ty;
        int cols = L.ViewTilesW;
        if (tx + cols > w) cols = w - tx;
        int clipX = g.ClipX, clipY = g.ClipY, clipW = g.ClipWidth, clipH = g.ClipHeight;
        int sheetW = _imgTiles.Width;
        int py = oy;
        int row = tx + ty * w;
        for (int j = 0; j < rows; j++)
        {
            int px = ox;
            int idx = row;
            for (int i = 0; i < cols; i++)
            {
                int tile = _map.Tiles[idx];
                if (tile != 0 || L.TileSky)
                {
                    g.SetClip(clipX, clipY, clipW, clipH);
                    g.ClipRect(px, py, _tileW, _tileH);
                    int src = -tile * _tileW;
                    g.DrawImage(_imgTiles, px + src % sheetW, py + src / sheetW * _tileH, TL);
                }
                px += _tileW;
                idx++;
            }
            py += _tileH;
            row += w;
        }
        g.SetClip(clipX, clipY, clipW, clipH);
    }

    // ------------------------------------------------------------------ bonus minigame (original d.cfr_renamed_3(Graphics))
    private void DrawBonus(Canvas g)
    {
        int W = L.ScreenW, H = L.ScreenH;
        if (_state == State.ModeMenu)
        {
            g.SetColor(255, 255, 255);
            g.FillRect(0, 0, W, H);
            return;
        }
        int gx = (W - _imgGrid.Width) / 2;
        int gy = (H - _imgGrid.Height) / 2 - 1;
        if (L.TileSky) DrawFlatSky(g, 0); else DrawSky(g, _frame, _camX, 0, 0, W, H, 0, 0, W, H);
        g.SetClip(0, 0, W, H);
        g.DrawImage(_imgGrid, gx, gy, TL);
        gx++; gy++;
        int cw = L.CellW, ch = L.CellH;
        int idx = 0;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 5; x++, idx++)
                DrawCellOrbs(g, gx + x * cw, gy + y * ch, Board[idx]);
        if (_state == State.BonusAnimating)
            DrawExplosion(g, gx, gy, ExplodingCell % 5, ExplodingCell / 5, Turn, AnimProgress);
        if (_state == State.BonusPlayerTurn && Winner == 0)
        {
            int cursorW = _imgCursor.Width / 2, cursorH = _imgCursor.Height;
            int cx = gx + CursorX * cw - (cursorW - cw) / 2;
            int cy = gy + CursorY * ch - (cursorH - ch) / 2;
            g.SetClip(cx, cy, cursorW, cursorH);
            if (CursorValid && _frame / 16 % 2 == 0) cx -= _imgCursor.Width / 2;
            g.DrawImage(_imgCursor, cx, cy, TL);
        }
        g.SetClip(0, 0, W, H);
        int leftY = BarLeft, rightY = BarRight;
        g.DrawImage(_barLeft, L.LeftBarX, BarLeft, TL);
        g.DrawImage(_barRight, L.RightBarX, BarRight, TL);
        int px = L.LeftBarX - (_playerW - _barLeft.Width) / 2;
        int ph = _imgPlayer.Height;
        g.ClipRect(px, (leftY += _py) - ph, _playerW, ph);
        g.DrawImage(_imgPlayer, px -= _playerW * _sprite, leftY, BL);
        int lw = L.LordPoseW;
        int lx = L.RightBarX - (lw - _barRight.Width) / 2 - 1;
        int lh = _imgLord.Height;
        g.SetClip(0, 0, W, H);
        g.ClipRect(lx, (rightY += _lordY) - lh, lw, lh);
        g.DrawImage(_imgLord, lx -= lw * _lordFrame + L.LordFrameW * 2, rightY, BL);
        if (_state == State.BonusAiMoving)
        {
            int x = _cpuX * cw + (W - _imgGrid.Width) / 2;
            int y = _cpuY * ch + (H - _imgGrid.Height) / 2 - 1;
            g.SetClip(x += (cw - _itemW) / 2, y += (ch - _itemH) / 2, _itemW, _itemH);
            g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, TL);
        }
        if (_gameOverTimer > 1500)
        {
            int x = L.GameOverX, y = L.GameOverY;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            g.DrawImage(_imgGameOver, x, y, TL);
        }
    }

}
