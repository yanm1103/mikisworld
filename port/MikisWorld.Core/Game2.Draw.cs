namespace MikisWorld.Core;

/// <summary>Rendering of MikisWorld 2 (original c.a(Graphics) and helpers, d.a(Graphics), i.R4(...)). Text is emitted as
/// <see cref="TextCommand"/>s. Frames are validated pixel-for-pixel against the original (text excluded).</summary>
public sealed partial class Game2
{
    private Bitmap[] _tileImages;
    private TileCache2 _cache;
    private object _bubbleLast;
    private int _bubbleCounter;

    /// <summary>Loads the sprite sheets so <see cref="Draw"/> can be used. Logic-only users (tests) can skip this.</summary>
    public void EnableRendering()
    {
        if (_render) return;
        var sheet = _assets.LoadBitmap("tiles.png");
        _tileImages = new Bitmap[25];
        int n = 0;
        for (int i = 0; i < 25; i++)
        {
            // original f.a(Image,...): every tile starts as an opaque white 12x12 image and the sheet is drawn at a
            // (possibly negative) offset, so transparent sheet pixels come out white.
            _tileImages[i] = new Bitmap(_tileW, _tileH);
            new Canvas(_tileImages[i]).DrawImage(sheet, n % sheet.Width, n / sheet.Width * _tileH, TL);
            n -= _tileW;
        }
        _imgNumbers = _assets.LoadBitmap("numbers.png");
        _imgTime = _assets.LoadBitmap("time.png");
        _imgDamage = _assets.LoadBitmap("damage.png");
        _imgGameOver = _assets.LoadBitmap("gameover.png");
        _imgLord = _assets.LoadBitmap("lord.png");
        _imgPlayer = _assets.LoadBitmap("player.png");
        _imgItems = _assets.LoadBitmap("items.png");
        _imgEnd = _assets.LoadBitmap("end.png");
        _imgGrid = _assets.LoadBitmap("grid.png");
        _imgBonus = _assets.LoadBitmap("bonus.png");
        _imgCursor = _assets.LoadBitmap("cursor.png");
        _snapshot = new Bitmap(12, 12);          // sic: the original allocates the wipe snapshot as a 12x12 image
        _cache = new TileCache2();
        _cache.Setup(_tileImages, Layout2.ViewTilesW, Layout2.ViewTilesH);

        _barLeft = new Bitmap(Layout2.BarW, Layout2.ScreenH);
        _barRight = new Bitmap(Layout2.BarW, Layout2.ScreenH);
        int off = Layout2.OrbW * 2;
        var gl = new Canvas(_barLeft);
        for (int y = 0; y < _barLeft.Height; y += Layout2.BarStripH)
        {
            gl.SetClip(0, y, Layout2.BarW, Layout2.BarStripH);
            gl.DrawImage(_imgBonus, -off, y, TL);
        }
        off += Layout2.BarW;
        var gr = new Canvas(_barRight);
        for (int y = 0; y < _barRight.Height; y += Layout2.BarStripH)
        {
            gr.SetClip(0, y, Layout2.BarW, Layout2.BarStripH);
            gr.DrawImage(_imgBonus, -off, y, TL);
        }
        _render = true;
    }

    // every write the original does through the tile layer's setter goes through here
    private void PutTile(int x, int y, int tile)
    {
        _map.Set(x, y, tile);
        _cache?.OnTileChanged(x, y, tile);
    }

    /// <summary>Original c.a(Graphics).</summary>
    public bool Draw(Canvas g)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        if (_state == -1)
        {
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, H);
            return true;
        }
        if (!_repaint) return false;
        _repaint = false;
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
        return true;
    }

    /// <summary>Menu background: the minigame's flat sky with a row of clouds (this game has no parallax sky image).</summary>
    public void DrawBackdrop(Canvas g, int frame)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        g.SetClip(0, 0, W, H);
        g.SetColor(60, 179, 210);
        g.FillRect(0, 0, W, H);
        for (int x = -(frame / 4 % _tileW); x < W; x += _tileW)
            g.DrawImage(_tileImages[Tile2.Cloud], x, H - 1, BL);
    }

    protected override void DrawScene(Canvas g)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        g.SetClip(0, 0, W, H);
        g.SetClip(0, 0, W, H);
        int shift = (176 - W) / 2;
        if (_state == State.Cutscene) g.Translate(-shift, 0);
        _cache.Draw(g, 0, 0, W, H, _camX, _camY);
        for (int i = _visFirst; i < _visFirst + _visCount; i++)
        {
            int x = _platformX[i] - _camX;
            int y = _platforms[i][1] - _camY;
            if ((_platforms[i][3] & 1) == 0)
            {
                int bob = (_frame + i * 3) / 32 % 4;
                for (int k = 0; k < _platforms[i][2]; k++)
                {
                    g.DrawImage(_tileImages[Tile2.Cloud], x, y + bob / 2, TL);
                    bob++;
                    bob %= 4;
                    x += _tileW;
                }
            }
        }
        DrawActors(g);
        if (_imgDamage != null && !_damageFreed)
        {
            int w = _imgDamage.Width / 2, h = _imgDamage.Height;
            if (_spark > 0)
            {
                int x = (_px >> 4) + _hitFxDx - _camX;
                int y = (_py >> 4) + _hitFxDy - _camY;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - h, w, h);
                int f = _frame / 3 % 2;
                g.DrawImage(_imgDamage, x - f * w, y, BL);
            }
        }
        if (_state == State.Cutscene) g.Translate(shift, 0);
        if (_state == State.GameOver && !_damageFreed)
        {
            int x = (W - _imgGameOver.Width) / 2;
            int y = (H - _imgGameOver.Height / 2) / 2;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            if (_lives != 0) y -= _imgGameOver.Height / 2;
            g.DrawImage(_imgGameOver, x, y, TL);
        }
        if (_state != State.Unknown7 && _area >= 0)
        {
            g.SetClip(0, 0, W, H);
            int x = Layout2.LivesX, y = Layout2.LivesY;
            for (int i = 0; i < _lives; i++)
            {
                g.SetClip(x, y, _tileW, _tileH);
                g.DrawImage(_tileImages[Tile2.Heart], x, y, TL);
                x -= _tileW;
            }
            if (_area == Area.Final)
            {
                if (_finalTimer >= 60000)
                {
                    int t = _finalTimer - 60000;
                    if (t > 3500 && t < 5000) DrawBubble(g, Strings.Text(3));
                    else if (t > 2000 && t < 3400) DrawBubble(g, Strings.Text(4));
                    else if (t > 0 && t < 1900 && _finalTimer / 200 % 2 == 0) DrawBubble(g, Strings.Text(5));
                }
                if (_finalTimer < 60000 || _finalTimer / 500 % 2 == 0)
                {
                    g.SetClip(0, 0, W, H);
                    int bx = Layout2.CoinHudX;
                    int by = Layout2.CoinHudY - 2;
                    g.DrawImage(_tileImages[Tile2.RainbowL], bx, by, TL);
                    g.DrawImage(_tileImages[Tile2.RainbowR], bx + _tileW, by, TL);
                    DrawCounter(g, (bx += _tileW) + 3, by += (_tileH - _imgNumbers.Height) / 2, _imgNumbers, Layout2.DigitW, 2, 10 - _finalRainbows, false);
                }
                if (_finalTimer > 10000 || _frame / 15 % 2 == 0)
                {
                    int secs = _finalTimer / 1000;
                    if (secs > 60) secs = 60;
                    g.SetClip(0, 0, W, H);
                    DrawCounter(g, Layout2.TimeHudX, Layout2.TimeHudY, _imgTime, Layout2.TimeDigitW, 2, secs, false);
                }
            }
            else
            {
                g.SetClip(0, 0, W, H);
                int left = _totalCoins - _coins;
                if (left < 0) left = 0;
                DrawCounter(g, Layout2.CoinHudX, Layout2.CoinHudY, _imgNumbers, Layout2.DigitW, 4, left, true);
            }
        }
        if (_state == State.Cutscene || _state == State.Ending)
        {
            int bar = 15 * H / 100;
            g.SetClip(0, 0, W, H);
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, bar);
            g.FillRect(0, H - bar, W, bar);
            if (_state == State.Ending)
            {
                _damageFreed = true;               // the original nulls the damage and game-over images here
                g.SetClip(0, 0, W, H);
                g.DrawImage(_imgEnd, (W - _imgEnd.Width) / 2, (H - _imgEnd.Height) / 2, TL);
            }
        }
        if (_state == State.Cutscene) DrawCutsceneBubbles(g);
        if (_state != State.Cutscene && _state != State.Ending && _markCooldown < 6000)
        {
            if (_markCooldown < 3000) DrawBubble(g, Strings.Text(1));
            else if (_frame / 4 % 3 > 0) DrawBubble(g, Strings.Text(2));
        }
        if (_interact != -1 && _hintTimer > 0)
        {
            if (_hintTimer == 2000) _bubbleLast = null;
            DrawBubble(g, Strings.Text(0));
        }
        if (_bonusMsgTimer > 0) DrawBubble(g, Strings.Text(12));
    }

    private bool _damageFreed;   // set once the ending screen has been drawn (original: bX = aa = null)

    private void DrawActors(Canvas g)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        if (_state == State.Lord1 || _state == State.Lord2 || _state == State.FinalStage)
        {
            int x = _lordX - _camX;
            int y = _lordY - _camY;
            int h = _imgLord.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h + 1, Layout2.LordFrameW, h);
            g.DrawImage(_imgLord, x -= Layout2.LordFrameW * _lordFrame, y, BL);
        }
        if ((_morkY >> 4) - _tileH * Layout2.ViewTilesH - _imgLord.Height < _camY)
        {
            int x = (_morkX >> 4) - _camX;
            int y = (_morkY >> 4) - _camY;
            int h = _imgLord.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h + 1, Layout2.LordPoseW, h);
            g.DrawImage(_imgLord, x -= Layout2.LordFrameW * 2 + Layout2.LordPoseW * _morkFrame, y, BL);
        }
        for (int i = 0; i < 4; i++)
        {
            if (_fxX[i] != -1)
            {
                int x = _fxX[i] - _camX;
                int y = (_fxY[i] >> 4) - _camY;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - _itemH, _itemW, _itemH);
                g.DrawImage(_imgItems, x += -_itemW * (1 + _frame / 6 % 2), y, BL);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] != -1)
            {
                int x = _projPos[i * 2] - _camX;
                int y = _projPos[i * 2 + 1] - _camY;
                g.SetClip(0, 0, W, H);
                g.ClipRect(x, y - _itemH + 1, _itemW, _itemH);
                g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, BL);
            }
        }
        if (_state != State.GameOver)
        {
            int x = (_px >> 4) - _camX;
            int y = (_py >> 4) - _camY;
            int h = _imgPlayer.Height;
            g.SetClip(0, 0, W, H);
            g.ClipRect(x, y - h + 1, _playerW, h);
            g.DrawImage(_imgPlayer, x -= _playerW * _sprite, y, BL);
        }
        if (_state == State.Cutscene) DrawCutsceneActors(g);
    }

    // original d.a(Graphics)
    private void DrawCutsceneActors(Canvas g)
    {
        var c = Cutscene;
        for (int i = 0; i < c.SpriteType.Length; i++)
        {
            if (!c.ActorVisible[i]) continue;
            int x = (c.ActorX[i] * _scaleX >> 4) - c.CameraX;
            int y = (c.ActorY[i] * _scaleY >> 4) - c.CameraY;
            switch (c.SpriteType[i])
            {
                case 0:
                    g.SetClip(x, y - _playerH + 1, _playerW, _playerH);
                    g.DrawImage(_imgPlayer, x - c.Frame[i] * _playerW, y, BL);
                    break;
                case 1:
                    g.SetClip(x, y - _playerH + 1, _playerW, _playerH);
                    g.DrawImage(_imgPlayer, x - (4 + c.Frame[i]) * _playerW, y, BL);
                    break;
                case 2:
                    {
                        int h = _imgLord.Height;
                        g.SetClip(x, y - h + 1, Layout2.LordFrameW, h);
                        g.DrawImage(_imgLord, x -= Layout2.LordFrameW * c.Frame[i], y, BL);
                        break;
                    }
                case 3:
                    {
                        int h = _imgLord.Height;
                        g.SetClip(x, y - h + 1, Layout2.LordPoseW, h);
                        g.DrawImage(_imgLord, x -= Layout2.LordFrameW * 2 + Layout2.LordPoseW * c.Frame[i], y, BL);
                        break;
                    }
                case 4:
                    g.SetClip(x, y - _itemH, _itemW, _itemH);
                    g.DrawImage(_imgItems, x += -_itemW * (1 + c.Frame[i]), y, BL);
                    break;
                case 5:
                    if (c.Frame[i] != 0) break;
                    g.SetClip(x, y - _itemH + 1, _itemW, _itemH);
                    g.DrawImage(_imgItems, x, y, BL);
                    break;
                case 6:
                    {
                        int w = _imgDamage.Width / 2, h = _imgDamage.Height;
                        g.SetClip(x, y - h, w, h);
                        g.DrawImage(_imgDamage, x -= w * c.Frame[i], y, BL);
                        break;
                    }
                case 11:
                    g.SetClip(x, y, _tileW * 2, _tileH);
                    g.DrawImage(_tileImages[Tile2.Ledge], x, y, TL);
                    g.DrawImage(_tileImages[Tile2.Ledge], x + _tileW, y, TL);
                    break;
                case 12:
                    g.SetClip(x, y, _tileW * 3, _tileH);
                    g.DrawImage(_tileImages[Tile2.Cloud], x, y, TL);
                    g.DrawImage(_tileImages[Tile2.Cloud], x + _tileW, y, TL);
                    g.DrawImage(_tileImages[Tile2.Cloud], x + _tileW * 2, y, TL);
                    break;
            }
        }
    }

    // original d.R4(Graphics): every visible speech bubble, all with the colour set by the last opcode 6
    private void DrawCutsceneBubbles(Canvas g)
    {
        for (int i = 0; i < Cutscene.TextVisible.Length; i++)
            if (Cutscene.TextVisible[i])
                DrawBubble(g, Strings.Text(i), Cutscene.BubbleR, Cutscene.BubbleG, Cutscene.BubbleB);
    }

    private void DrawBubble(Canvas g, string text) => DrawBubble(g, text, 99, 48, 0);

    // original d.a(Graphics, String, r, g, b): bottom-of-screen bubble; text wider than the screen scrolls
    private void DrawBubble(Canvas g, string text, int r, int gr, int b)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        if (!ReferenceEquals(_bubbleLast, text))
        {
            _bubbleLast = text;
            _bubbleCounter = 0;
        }
        ++_bubbleCounter;
        ++_bubbleCounter;
        g.SetClip(0, 0, W, H + 1);
        g.SetFont(FontKind.Small);
        int cx = W / 2;
        int tw = g.Metrics.StringWidth(text, FontKind.Small) + 2;
        int th = g.Metrics.Height(FontKind.Small) + 4;
        if (tw > W)
        {
            cx = _bubbleCounter < 7 ? cx + (tw - W) / 2
               : _bubbleCounter < 7 + tw - W ? cx + (tw - W) / 2 - _bubbleCounter + 7
               : cx - (tw - W) / 2;
        }
        int by = H - 8 * H / 100;
        if (H - by < 2 || 100 * th / H >= 19) by = H - 2;
        g.SetColor(r, gr, b);
        g.FillRect(cx - tw / 2 - 1, by - th + 2, tw, th);
        g.SetColor(254, 254, 254);
        g.DrawRect(cx - tw / 2 - 1, by - th + 2, tw, th);
        g.DrawString(text, cx, by, Canvas.Bottom | Canvas.HCenter);
    }

    // ------------------------------------------------------------------ bonus minigame (original c.R4(Graphics))
    private void DrawBonus(Canvas g)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        if (_state == State.ModeMenu)
        {
            g.SetColor(255, 255, 255);
            g.FillRect(0, 0, W, H);
            return;
        }
        int gx = (W - _imgGrid.Width) / 2;
        int gy = (H - _imgGrid.Height) / 2 - 1;
        g.SetClip(0, 0, W, H);
        g.SetColor(60, 179, 210);
        g.FillRect(0, 0, W, H);
        for (int x = 0; x < W; x += _tileW)
            g.DrawImage(_tileImages[Tile2.Cloud], x, H - 1, BL);
        g.SetClip(0, 0, W, H);
        g.DrawImage(_imgGrid, gx, gy, TL);
        gx++; gy++;
        int cw = Layout2.CellW, ch = Layout2.CellH;
        int idx = 0;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 5; x++, idx++)
                DrawCellOrbs(g, gx + x * cw, gy + y * ch, Board[idx]);
        if (_state == State.BonusAnimating)
        {
            g.SetClip(0, 0, W, H);
            DrawExplosion(g, gx, gy, ExplodingCell % 5, ExplodingCell / 5, Turn, AnimProgress);
        }
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
        g.DrawImage(_barLeft, Layout2.LeftBarX, BarLeft, TL);
        g.DrawImage(_barRight, Layout2.RightBarX, BarRight, TL);
        int px = Layout2.LeftBarX - (_playerW - _barLeft.Width) / 2;
        int ph = _imgPlayer.Height;
        g.ClipRect(px, (leftY += _py) - ph, _playerW, ph);
        g.DrawImage(_imgPlayer, px -= _playerW * _sprite, leftY, BL);
        int lw = Layout2.LordFrameW;
        int lx = Layout2.RightBarX - (lw - _barRight.Width) / 2 - 1;
        int lh = _imgLord.Height;
        g.SetClip(0, 0, W, H);
        g.ClipRect(lx, (rightY += _lordY) - lh, lw, lh);
        g.DrawImage(_imgLord, lx -= lw * _lordFrame, rightY, BL);
        if (_state == State.BonusAiMoving)
        {
            int x = _cpuX * cw + (W - _imgGrid.Width) / 2;
            int y = _cpuY * ch + (H - _imgGrid.Height) / 2 - 1;
            g.SetClip(x += (cw - _itemW) / 2, y += (ch - _itemH) / 2, _itemW, _itemH);
            g.DrawImage(_imgItems, x -= _itemW * (3 + _frame / 2 % 2), y, TL);
        }
        if (_gameOverTimer > 1500)
        {
            int x = (W - _imgGameOver.Width) / 2;
            int y = (H - _imgGameOver.Height / 2) / 2;
            g.SetClip(x, y, _imgGameOver.Width, _imgGameOver.Height / 2);
            g.DrawImage(_imgGameOver, x, y, TL);
        }
    }

    private void DrawExplosion(Canvas g, int gx, int gy, int cellX, int cellY, int owner, int progress)
    {
        int x = gx + cellX * Layout2.CellW + 2;
        int y = gy + cellY * Layout2.CellH + 2;
        int w = Layout2.OrbW, h = Layout2.OrbH;
        int colour = owner == 1 ? w : 0;
        int dx = Layout2.CellW * progress / 10;
        int dy = Layout2.CellH * progress / 10;
        if (cellX > 0) { g.SetClip(x - dx, y, w, h); g.DrawImage(_imgBonus, x - colour - dx, y, TL); }
        if (cellX < 4) { g.SetClip(x + dx + w, y, w, h); g.DrawImage(_imgBonus, x - colour + dx + w, y, TL); }
        if (cellY > 0) { g.SetClip(x + w, y - dy, w, h); g.DrawImage(_imgBonus, x - colour + w, y - dy, TL); }
        if (cellY < 3) { g.SetClip(x, y + dy + h, w, h); g.DrawImage(_imgBonus, x - colour, y + dy + h, TL); }
    }
}
