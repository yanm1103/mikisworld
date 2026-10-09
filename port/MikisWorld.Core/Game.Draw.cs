namespace MikisWorld.Core;

/// <summary>Rendering (original d.a(Graphics) and helpers, e.cfr_renamed_3(Graphics), a.a(Graphics,...)).
/// Draws a 176x144 frame onto a <see cref="Canvas"/>; text is emitted as <see cref="TextCommand"/>s.</summary>
public sealed partial class Game
{
    private bool _render;
    private Bitmap _imgTiles, _imgSky, _imgClouds, _imgLord, _imgPlayer, _imgItems, _imgNumbers, _imgTime,
                   _imgDamage, _imgGameOver, _imgEnd, _imgSymbols, _imgGrid, _imgBonus, _imgCursor,
                   _snapshot, _barLeft, _barRight;

    /// <summary>Loads the sprite sheets so <see cref="Draw"/> can be used. Logic-only users (tests) can skip this.</summary>
    public void EnableRendering()
    {
        if (_render) return;
        _imgTiles = _assets.LoadBitmap("tiles.png");
        _imgSky = _assets.LoadBitmap("sky.png");
        _imgClouds = _assets.LoadBitmap("clouds.png");
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
        _snapshot = new Bitmap(Layout.ScreenW, Layout.ScreenH);

        // lateral bars of the minigame: strips cut from bonus.png (original d.i())
        _barLeft = new Bitmap(Layout.BarW, Layout.ScreenH);
        _barRight = new Bitmap(Layout.BarW, Layout.ScreenH);
        int off = Layout.OrbW * 2;
        var gl = new Canvas(_barLeft);
        for (int y = 0; y < _barLeft.Height; y += Layout.BarStripH)
        {
            gl.SetClip(0, y, Layout.BarW, Layout.BarStripH);
            gl.DrawImage(_imgBonus, -off, y, Canvas.Left | Canvas.Top);
        }
        off += Layout.BarW;
        var gr = new Canvas(_barRight);
        for (int y = 0; y < _barRight.Height; y += Layout.BarStripH)
        {
            gr.SetClip(0, y, Layout.BarW, Layout.BarStripH);
            gr.DrawImage(_imgBonus, -off, y, Canvas.Left | Canvas.Top);
        }
        _render = true;
    }

    private const int TL = Canvas.Left | Canvas.Top;
    private const int BL = Canvas.Left | Canvas.Bottom;

    partial void SnapshotScene()
    {
        if (!_render) return;
        DrawScene(new Canvas(_snapshot));
    }

    /// <summary>Original d.a(Graphics): the whole frame for the current state.</summary>
    public void Draw(Canvas g)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;
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
    }

    // ------------------------------------------------------------------ world scene (original d.cfr_renamed_6(Graphics))
    private void DrawScene(Canvas g)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;
        g.SetClip(0, 0, W, H);
        DrawSky(g);
        g.SetClip(0, 0, W, H);
        DrawTileLayer(g, 0, 0, W, H, _camX, _camY);
        DrawActors(g);

        // floating cloud platforms
        for (int i = _visFirst; i < _visFirst + _visCount; i++)
        {
            int x = _platformX[i] - _camX;
            int y = _platforms[i][1] - _camY;
            if ((_platforms[i][3] & 1) == 0)
            {
                y -= 4;
                int bob = (_frame + i * 3) / 32 % 4;
                int sheetW = _imgTiles.Width;
                for (int k = 0; k < _platforms[i][2]; k++)
                {
                    int press = 0;
                    int rel;
                    if (_standingOn == i && (rel = (_px >> 4) - _camX + _playerW / 2 - x) <= _tileW && rel >= 0) press = 2;
                    g.SetClip(0, 0, W, H);
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
                int x = _hitFxPos[i] - _camX;
                int y = _hitFxPos[i + 1] - _camY;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - fxH, fxW, fxH);
                int f = (_frame - _hitFxStart[i / 2]) / 3 % 2;
                g.DrawImage(_imgDamage, x - f * fxW, y, BL);
            }
        }

        if (_state == State.GameOver)
        {
            int x = Layout.GameOverX, y = Layout.GameOverY;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            if (_lives != 0) y -= _imgGameOver.Height / 2;
            g.DrawImage(_imgGameOver, x, y, TL);
        }

        if (_state != State.Unknown7 && _area >= 0)
        {
            g.SetClip(0, 0, W, H);
            int x = Layout.LivesX, y = Layout.LivesY;
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
                    int bx = Layout.CoinHudX;
                    int by = Layout.CoinHudY - 2;
                    g.SetClip(bx, by, _tileW, _tileH);
                    g.DrawImage(_imgTiles, bx - Tile.CagedBird * _tileW % _imgTiles.Width, by - 12 * _tileW / _imgTiles.Width * _tileH, TL);
                    g.SetClip(bx, by - _tileH, _tileW, _tileH);
                    g.DrawImage(_imgTiles, bx - 12 * _tileW % _imgTiles.Width, by - Tile.BirdSign * _tileW / _imgTiles.Width * _tileH - _tileH, TL);
                    DrawCounter(g, (bx += _tileW) + 3, by += (_tileH - _imgNumbers.Height) / 2, _imgNumbers, Layout.DigitW, 1, _finalBirdCells.Length - _finalDoor, false);
                }
                if (_finalTimer > 10000 || _frame / 15 % 2 == 0)
                {
                    int secs = _finalTimer / 1000;
                    if (secs > 60) secs = 60;
                    g.SetClip(0, 0, W, H);
                    DrawCounter(g, Layout.TimeHudX, Layout.TimeHudY, _imgTime, Layout.TimeDigitW, 2, secs, false);
                }
            }
            else
            {
                g.SetClip(0, 0, W, H);
                int left = _totalCoins - _coins;
                if (left < 0) left = 0;
                DrawCounter(g, Layout.CoinHudX, Layout.CoinHudY, _imgNumbers, Layout.DigitW, 4, left, true);
            }
        }
        if (_state == State.Cutscene) DrawCutsceneBubbles(g);
        if (_interact != -1 && _hintTimer > 0) DrawBubble(g, Strings.Text(0));
        if (_bonusMsgTimer > 0) DrawBubble(g, Strings.Text(12));
    }

    // original d.cfr_renamed_1(Graphics): sky + two parallax cloud bands
    /// <summary>The scrolling sky + clouds, for screens outside the game world (menus).</summary>
    public void DrawBackdrop(Canvas g, int frame) => DrawSky(g, frame, 0);

    private void DrawSky(Canvas g) => DrawSky(g, _frame, _camX);

    private void DrawSky(Canvas g, int frame, int camX)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;
        int n = W / _imgSky.Width + 2;
        int x = -(frame / 32) % _imgSky.Width;
        for (int i = 0; i < n; i++)
        {
            g.DrawImage(_imgSky, x, 0, TL);
            x += _imgSky.Width;
        }
        n = W / 38 + 2;
        int y = 99;
        x = -(frame / 25 + camX / 4) % 38;
        for (int i = 0; i < n; i++)
        {
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y, 38, 27);
            g.DrawImage(_imgClouds, x, y, TL);
            x += 38;
        }
        n = W / 45 + 2;
        y = 114;
        x = -(frame / 5 + camX / 2) % 45;
        for (int i = 0; i < n; i++)
        {
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y, 45, 30);
            g.DrawImage(_imgClouds, x, y - 27, TL);
            x += 45;
        }
    }

    // original d.cfr_renamed_10(Graphics): lord, freed bird, lightning bolts, player
    private void DrawActors(Canvas g)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;
        if (_state == State.Lord1 || _state == State.Lord2 || _state == State.FinalStage)
        {
            int x = _lordX - _camX;
            int y = _lordY - _camY;
            int h = _imgLord.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h, Layout.LordPoseW, h);
            g.DrawImage(_imgLord, x -= Layout.LordFrameW * 2 + Layout.LordPoseW * _lordFrame, y, BL);
        }
        if (_birdFxX != -1)
        {
            int x = _birdFxX - _camX;
            int y = _birdFxY - _camY;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - _itemH, _itemW, _itemH);
            g.DrawImage(_imgItems, x += -_itemW * (1 + _frame / 6 % 2), y, BL);
        }
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] != -1)
            {
                int x = _projPos[i * 2] - _camX;
                int y = _projPos[i * 2 + 1] - _camY;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - _itemH, _itemW, _itemH);
                g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, BL);
            }
        }
        if (_state != State.GameOver)
        {
            int x = (_px >> 4) - _camX;
            int y = (_py >> 4) - _camY + 1;
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
                        g.SetClip(x, y - h, Layout.LordFrameW, h);
                        g.DrawImage(_imgLord, x -= Layout.LordFrameW * c.Frame[i], y, BL);
                        break;
                    }
                case 3:
                    {
                        int h = _imgLord.Height;
                        g.SetClip(x, y - h, Layout.LordPoseW, h);
                        g.DrawImage(_imgLord, x -= Layout.LordFrameW * 2 + Layout.LordPoseW * c.Frame[i], y, BL);
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
    private void DrawBubble(Canvas g, string text)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;
        g.SetClip(0, 0, W, H);
        g.SetFont(FontKind.Small);
        int w = g.Metrics.StringWidth(text, FontKind.Small) + 4;
        int h = g.Metrics.Height(FontKind.Small) + 4;
        int y = H / 2 + 3;
        g.SetColor(99, 48, 0);
        g.FillRect(W / 2 - w / 2 - 1, y - h + 2 - 1, w + 2, h + 2);
        g.SetColor(255, 255, 255);
        g.DrawRect(W / 2 - w / 2 - 1, y - h + 2 - 1, w + 2, h + 2);
        g.DrawString(text, W / 2, y, Canvas.Bottom | Canvas.HCenter);
    }

    // original d.a(Graphics,int,int,Image,int,int,int,boolean): right-aligned number made of sprite digits
    private void DrawCounter(Canvas g, int x, int y, Bitmap digits, int digitW, int count, int value, bool lastIsIcon)
    {
        x += (count - 1) * digitW;
        for (int i = 0; i < count; i++)
        {
            int d = value % 10;
            value /= 10;
            if (i == count - 1 && lastIsIcon) d = 10;
            g.SetClip(0, 0, Layout.ScreenW, Layout.ScreenH);
            g.ClipRect(x, y, digitW, digits.Height);
            g.DrawImage(digits, x - d * digitW, y, TL);
            x -= digitW;
            if (!lastIsIcon && value == 0) break;
        }
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
        int rows = Layout.ViewTilesH;
        if (ty + rows > h) rows = h - ty;
        int cols = Layout.ViewTilesW;
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
                if (tile != 0)
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
        int W = Layout.ScreenW, H = Layout.ScreenH;
        if (_state == State.ModeMenu)
        {
            g.SetColor(255, 255, 255);
            g.FillRect(0, 0, W, H);
            return;
        }
        int gx = (W - _imgGrid.Width) / 2;
        int gy = (H - _imgGrid.Height) / 2 - 1;
        DrawSky(g);
        g.SetClip(0, 0, W, H);
        g.DrawImage(_imgGrid, gx, gy, TL);
        gx++; gy++;
        int cw = Layout.CellW, ch = Layout.CellH;
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
        g.DrawImage(_barLeft, Layout.LeftBarX, BarLeft, TL);
        g.DrawImage(_barRight, Layout.RightBarX, BarRight, TL);
        int px = Layout.LeftBarX - (_playerW - _barLeft.Width) / 2;
        int ph = _imgPlayer.Height;
        g.ClipRect(px, (leftY += _py) - ph, _playerW, ph);
        g.DrawImage(_imgPlayer, px -= _playerW * _sprite, leftY, BL);
        int lw = Layout.LordPoseW;
        int lx = Layout.RightBarX - (lw - _barRight.Width) / 2 - 1;
        int lh = _imgLord.Height;
        g.SetClip(0, 0, W, H);
        g.ClipRect(lx, (rightY += _lordY) - lh, lw, lh);
        g.DrawImage(_imgLord, lx -= lw * _lordFrame + Layout.LordFrameW * 2, rightY, BL);
        if (_state == State.BonusAiMoving)
        {
            int x = _cpuX * cw + (W - _imgGrid.Width) / 2;
            int y = _cpuY * ch + (H - _imgGrid.Height) / 2 - 1;
            g.SetClip(x += (cw - _itemW) / 2, y += (ch - _itemH) / 2, _itemW, _itemH);
            g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, TL);
        }
        if (_gameOverTimer > 1500)
        {
            int x = Layout.GameOverX, y = Layout.GameOverY;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            g.DrawImage(_imgGameOver, x, y, TL);
        }
    }

    // original d.a(Graphics,int,int,int): orbs of a resting cell (bit 3 selects the CPU colour)
    private void DrawCellOrbs(Canvas g, int x, int y, int value)
    {
        x += 2; y += 2;
        int w = Layout.OrbW, h = Layout.OrbH;
        int colour = 0;
        if ((value & 8) != 0) { colour = w; value &= 7; }
        if (value > 0) { g.SetClip(x, y, w, h); g.DrawImage(_imgBonus, x - colour, y, TL); }
        if (value > 1) { g.SetClip(x + w, y, w, h); g.DrawImage(_imgBonus, x + w - colour, y, TL); }
        if (value > 2) { g.SetClip(x, y + h, w, h); g.DrawImage(_imgBonus, x - colour, y + h, TL); }
        if (value > 3) { g.SetClip(x + w, y + h, w, h); g.DrawImage(_imgBonus, x + w - colour, y + h, TL); }
    }

    // original d.a(Graphics,int,int,int,int,int,int): four orbs flying out of an exploding cell
    private void DrawExplosion(Canvas g, int gx, int gy, int cellX, int cellY, int owner, int progress)
    {
        g.SetClip(0, 0, Layout.ScreenW, Layout.ScreenH);
        int x = gx + cellX * Layout.CellW + 2;
        int y = gy + cellY * Layout.CellH + 2;
        int w = Layout.OrbW, h = Layout.OrbH;
        int colour = owner == 1 ? w : 0;
        int dx = Layout.CellW * progress / 10;
        int dy = Layout.CellH * progress / 10;
        if (cellX > 0) { g.SetClip(x - dx, y, w, h); g.DrawImage(_imgBonus, x - colour - dx, y, TL); }
        if (cellX < 4) { g.SetClip(x + dx + w, y, w, h); g.DrawImage(_imgBonus, x - colour + dx + w, y, TL); }
        if (cellY > 0) { g.SetClip(x + w, y - dy, w, h); g.DrawImage(_imgBonus, x - colour + w, y - dy, TL); }
        if (cellY < 3) { g.SetClip(x, y + dy + h, w, h); g.DrawImage(_imgBonus, x - colour, y + dy + h, TL); }
    }
}
