import re, os
os.chdir(r'C:\Users\TwistZero\mikisworld\port\MikisWorld.Core')


def load(p):
    s = open(p, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in s else '\n'
    return s.replace('\r\n', '\n'), nl


def save(p, s, nl):
    open(p, 'w', encoding='utf-8', newline='').write(s.replace('\n', nl))


def sub(s, old, new, cnt=1):
    assert old in s, old[:70]
    return s.replace(old, new, cnt)


# ---------------------------------------------------------------- interface
s, nl = load('IMikisGame.cs')
s = sub(s, '''    int ScreenWidth { get; }
    int ScreenHeight { get; }''', '''    /// <summary>The original screen (what the menus and the game logic work with).</summary>
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    /// <summary>The picture actually drawn: the original screen, or larger in the "Extended" view (see <see cref="SetView"/>).</summary>
    int ViewWidth { get; }
    int ViewHeight { get; }
    void SetView(int w, int h);''')
save('IMikisGame.cs', s, nl)

# ---------------------------------------------------------------- tweaks use in logic
s, nl = load('Game.Update.cs')
s = sub(s, 'UpdateVisibleWindow(_camY - _tileH, _camY + Layout.ScreenH + _tileH);', 'UpdateVisibleWindow(_camY - _tileH - ExtraView, _camY + Layout.ScreenH + _tileH + ExtraView);')
s = sub(s, 'int bottom = _camY + Layout.ScreenH;', 'int bottom = _camY + Layout.ScreenH + ExtraView;')
save('Game.Update.cs', s, nl)
s, nl = load('Game2.Update.cs')
s = sub(s, 'UpdateVisibleWindow(_camY - _tileH, _camY + Layout2.ScreenH + _tileH);', 'UpdateVisibleWindow(_camY - _tileH - ExtraView, _camY + Layout2.ScreenH + _tileH + ExtraView);')
s = sub(s, 'int bottom = _camY + Layout2.ScreenH;', 'int bottom = _camY + Layout2.ScreenH + ExtraView;')
save('Game2.Update.cs', s, nl)
for p in ('Game.Player.cs', 'Game2.Player.cs'):
    s, nl = load(p)
    s = sub(s, 'SpawnProjectile(-1, _camY);', 'SpawnProjectile(-1, _camY - ExtraView);')
    save(p, s, nl)

# ---------------------------------------------------------------- GameBase.Draw
s, nl = load('GameBase.Draw.cs')
s = sub(s, '''        if (!_render) return;
        if (Tweaks.FullWipeSnapshot && (_snapshot.Width != L.ScreenW || _snapshot.Height != L.ScreenH)) _snapshot = new Bitmap(L.ScreenW, L.ScreenH);
        DrawScene(new Canvas(_snapshot));''', '''        if (!_render) return;
        bool wide = WideView && DrawsWorld;                        // the extended picture is wiped as a whole
        int w = wide ? ViewWidth : L.ScreenW, h = wide ? ViewHeight : L.ScreenH;
        if ((wide || Tweaks.FullWipeSnapshot) && (_snapshot.Width != w || _snapshot.Height != h)) _snapshot = new Bitmap(w, h);
        DrawScene(new Canvas(_snapshot));''')
s = sub(s, 'g.SetClip(0, 0, L.ScreenW, L.ScreenH);\n            g.ClipRect(x, y, digitW, digits.Height);', 'g.SetClip(0, 0, _vw, _vh);\n            g.ClipRect(x, y, digitW, digits.Height);')
save('GameBase.Draw.cs', s, nl)

# ---------------------------------------------------------------- Hitboxes
s, nl = load('GameBase.Hitboxes.cs')
s = sub(s, 'Line(g, rgb, x - _camX, y - _camY, w - 1, h - 1);', 'Line(g, rgb, x - _dx, y - _dy, w - 1, h - 1);')
s = sub(s, 'x -= _camX; y -= _camY;', 'x -= _dx; y -= _dy;')
s = sub(s, '''        g.SetClip(0, 0, L.ScreenW, L.ScreenH);
        int tx = _px >> 4, ty = _py >> 4;''', '''        PrepareDraw(true);
        g.SetClip(0, 0, _vw, _vh);
        int tx = _px >> 4, ty = _py >> 4;''')
s = sub(s, 'int c0 = Math.Max(0, _camX / _tileW), c1 = Math.Min(w - 1, (_camX + L.ScreenW) / _tileW);', 'int c0 = Math.Max(0, _dx / _tileW), c1 = Math.Min(w - 1, (_dx + _vw) / _tileW);')
s = sub(s, 'int r0 = Math.Max(0, _camY / _tileH), r1 = Math.Min(rows - 1, (_camY + L.ScreenH) / _tileH);', 'int r0 = Math.Max(0, _dy / _tileH), r1 = Math.Min(rows - 1, (_dy + _vh) / _tileH);')
save('GameBase.Hitboxes.cs', s, nl)

# ---------------------------------------------------------------- Game 1 drawing
s, nl = load('Game.Draw.cs')
s = sub(s, '''    /// <summary>Original d.a(Graphics): the whole frame for the current state.</summary>
    public bool Draw(Canvas g)
    {
        int W = Layout.ScreenW, H = Layout.ScreenH;''', '''    protected override void DrawTile(Canvas g, int tile, int px, int py)
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
            int vw = ViewWidth, vh = ViewHeight, ox = (vw - Layout.ScreenW) / 2, oy = (vh - Layout.ScreenH) / 2;
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
        int W = _vw, H = _vh;''')
# DrawScene
a = s.index('    protected override void DrawScene(Canvas g)')
b = s.index('    // original d.cfr_renamed_1(Graphics): sky + two parallax cloud bands')
scene = s[a:b]
scene = sub(scene, '''        int W = Layout.ScreenW, H = Layout.ScreenH;
        g.SetClip(0, 0, W, H);
        DrawSky(g);
        g.SetClip(0, 0, W, H);
        DrawTileLayer(g, 0, 0, W, H, _camX, _camY);
        DrawActors(g);''', '''        PrepareDraw(DrawsWorld);
        int W = _vw, H = _vh;
        int ex = W - Layout.ScreenW, ey = H - Layout.ScreenH;       // how much bigger than the original screen (0 unless the view is wide)
        bool wide = ex != 0 || ey != 0;
        g.SetClip(0, 0, W, H);
        if (wide)
        {
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, H);                                 // outside the map and the area: black
            g.SetClip(_wx, _wy, _ww, _wh);
        }
        DrawSky(g, _frame, _camX, _sx, _sy, W, H, _wx, _wy, _ww, _wh);
        g.SetClip(0, 0, W, H);
        if (wide) DrawTileLayerWide(g);
        else DrawTileLayer(g, 0, 0, W, H, _camX, _camY);
        DrawActors(g);''')
scene = scene.replace('- _camX', '- _dx').replace('- _camY', '- _dy')
scene = sub(scene, 'int x = Layout.GameOverX, y = Layout.GameOverY;', 'int x = Layout.GameOverX + ex / 2, y = Layout.GameOverY + ey / 2;')
scene = sub(scene, 'int x = Layout.LivesX, y = Layout.LivesY;', 'int x = Layout.LivesX + ex, y = Layout.LivesY + ey;')
scene = sub(scene, 'int bx = Layout.CoinHudX;\n                    int by = Layout.CoinHudY - 2;', 'int bx = Layout.CoinHudX;\n                    int by = Layout.CoinHudY - 2 + ey;')
scene = sub(scene, 'DrawCounter(g, Layout.TimeHudX, Layout.TimeHudY,', 'DrawCounter(g, Layout.TimeHudX + ex, Layout.TimeHudY,')
scene = sub(scene, 'DrawCounter(g, Layout.CoinHudX, Layout.CoinHudY, _imgNumbers', 'DrawCounter(g, Layout.CoinHudX, Layout.CoinHudY + ey, _imgNumbers')
s = s[:a] + scene + s[b:]

# sky
a = s.index('    // original d.cfr_renamed_1(Graphics): sky + two parallax cloud bands')
b = s.index('    // original d.cfr_renamed_10(Graphics): lord, freed bird, lightning bolts, player')
s = s[:a] + '''    // original d.cfr_renamed_1(Graphics): sky + two parallax cloud bands
    /// <summary>The scrolling sky + clouds, for screens outside the game world (menus).</summary>
    public void DrawBackdrop(Canvas g, int frame) => DrawSky(g, frame, 0, 0, 0, Layout.ScreenW, Layout.ScreenH, 0, 0, Layout.ScreenW, Layout.ScreenH);

    /// <summary>The sky anchored at (ox, oy) of a W x H picture (the original screen's top-left), painted only inside the clip rectangle (cx, cy, cw, ch).
    /// Above and below the original sky strip a flat colour continues it.</summary>
    private void DrawSky(Canvas g, int frame, int camX, int ox, int oy, int W, int H, int cx, int cy, int cw, int ch)
    {
        int skyW = _imgSky.Width, skyH = _imgSky.Height;
        if (oy > 0 || H > oy + Layout.ScreenH)
        {
            int top = _imgSky.Pixels[0], bottom = _imgSky.Pixels[(skyH - 1) * skyW];
            g.SetClip(cx, cy, cw, ch);
            g.SetColor(top >> 16, top >> 8, top);
            g.FillRect(0, 0, W, oy);
            g.SetColor(bottom >> 16, bottom >> 8, bottom);
            g.FillRect(0, oy + skyH, W, H - oy - skyH);
        }
        g.SetClip(cx, cy, cw, ch);
        int x = ox - (frame / 32) % skyW;
        while (x > 0) x -= skyW;
        for (; x < W; x += skyW) g.DrawImage(_imgSky, x, oy, TL);
        int y = 99 + oy;
        x = ox - (frame / 25 + camX / 4) % 38;
        while (x > 0) x -= 38;
        for (; x < W; x += 38)
        {
            g.SetClip(cx, cy, cw, ch);
            g.ClipRect(x, y, 38, 27);
            g.DrawImage(_imgClouds, x, y, TL);
        }
        y = 114 + oy;
        x = ox - (frame / 5 + camX / 2) % 45;
        while (x > 0) x -= 45;
        for (; x < W; x += 45)
        {
            g.SetClip(cx, cy, cw, ch);
            g.ClipRect(x, y, 45, 30);
            g.DrawImage(_imgClouds, x, y - 27, TL);
        }
    }

''' + s[b:]

# actors
a = s.index('    private void DrawActors(Canvas g)')
b = s.index('    // original e.cfr_renamed_3(Graphics)')
act = s[a:b]
act = sub(act, 'int W = Layout.ScreenW, H = Layout.ScreenH;', 'int W = _vw, H = _vh;')
act = act.replace('- _camX', '- _dx').replace('- _camY', '- _dy')
s = s[:a] + act + s[b:]

# bubble
s = sub(s, '''        text = KeyHints(text);
        int W = Layout.ScreenW, H = Layout.ScreenH;''', '''        text = KeyHints(text);
        int W = _vw, H = _vh;''')
save('Game.Draw.cs', s, nl)
print('ok')
