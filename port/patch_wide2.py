import os
os.chdir(r'C:\Users\TwistZero\mikisworld\port\MikisWorld.Core')


def load(p):
    s = open(p, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in s else '\n'
    return s.replace('\r\n', '\n'), nl


def save(p, s, nl):
    open(p, 'w', encoding='utf-8', newline='').write(s.replace('\n', nl))


def sub(s, old, new, cnt=1):
    assert old in s, old[:80]
    return s.replace(old, new, cnt)


s, nl = load('Game2.Draw.cs')
s = sub(s, '''    /// <summary>Original c.a(Graphics).</summary>
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
        _repaint = false;''', '''    protected override bool DrawsTileZero => true;          // tile 0 is this game's sky

    protected override void DrawTile(Canvas g, int tile, int px, int py) => g.DrawImage(_tileImages[tile], px, py, TL);

    protected override void OnViewModeChanged()
    {
        _cache?.Reset();                                    // the ring cache is not used in the wide view: it must redraw when it comes back
        _repaint = true;
    }

    /// <summary>Original c.a(Graphics). In the wide view, what is not the world (cutscenes, minigame) keeps the original screen, centred on black.</summary>
    public bool Draw(Canvas g)
    {
        if (_state == -1)
        {
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, ViewWidth, ViewHeight);
            return true;
        }
        if (!_repaint) return false;
        _repaint = false;
        if (WideView && !DrawsWorld)
        {
            int vw = ViewWidth, vh = ViewHeight, ox = (vw - Layout2.ScreenW) / 2, oy = (vh - Layout2.ScreenH) / 2;
            g.SetClip(0, 0, vw, vh);
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, vw, vh);
            g.Translate(ox, oy);
            DrawFrame(g);
            g.Translate(-ox, -oy);
            return true;
        }
        DrawFrame(g);
        return true;
    }

    private void DrawFrame(Canvas g)
    {
        PrepareDraw(DrawsWorld);
        int W = _vw, H = _vh;''')
s = sub(s, '''            g.DrawImage(_snapshot, n, 0, TL);
        }
        else DrawScene(g);
        return true;
    }''', '''            g.DrawImage(_snapshot, n, 0, TL);
        }
        else DrawScene(g);
    }''')
# scene
a = s.index('    protected override void DrawScene(Canvas g)')
b = s.index('    private bool _damageFreed;')
scene = s[a:b]
scene = sub(scene, '''        int W = Layout2.ScreenW, H = Layout2.ScreenH;
        g.SetClip(0, 0, W, H);
        g.SetClip(0, 0, W, H);
        int shift = (176 - W) / 2;
        if (_state == State.Cutscene) g.Translate(-shift, 0);
        _cache.Draw(g, 0, 0, W, H, _camX, _camY);''', '''        PrepareDraw(DrawsWorld);
        int W = _vw, H = _vh;
        int ex = W - Layout2.ScreenW, ey = H - Layout2.ScreenH;       // how much bigger than the original screen (0 unless the view is wide)
        g.SetClip(0, 0, W, H);
        g.SetClip(0, 0, W, H);
        int shift = (176 - W) / 2;
        if (_state == State.Cutscene) g.Translate(-shift, 0);
        if (ex != 0 || ey != 0)
        {
            g.SetColor(0, 0, 0);
            g.FillRect(0, 0, W, H);                                     // outside the map and the area: black
            DrawTileLayerWide(g);
        }
        else _cache.Draw(g, 0, 0, W, H, _camX, _camY);''')
scene = scene.replace('- _camX', '- _dx').replace('- _camY', '- _dy')
scene = sub(scene, 'int x = Layout2.LivesX, y = Layout2.LivesY;', 'int x = Layout2.LivesX + ex, y = Layout2.LivesY + ey;')
scene = sub(scene, 'int by = Layout2.CoinHudY - 2;', 'int by = Layout2.CoinHudY - 2 + ey;')
scene = sub(scene, 'DrawCounter(g, Layout2.TimeHudX, Layout2.TimeHudY,', 'DrawCounter(g, Layout2.TimeHudX + ex, Layout2.TimeHudY,')
scene = sub(scene, 'DrawCounter(g, Layout2.CoinHudX, Layout2.CoinHudY, _imgNumbers', 'DrawCounter(g, Layout2.CoinHudX, Layout2.CoinHudY + ey, _imgNumbers')
s = s[:a] + scene + s[b:]
# actors
a = s.index('    private void DrawActors(Canvas g)')
b = s.index('    // original d.a(Graphics)\n    private void DrawCutsceneActors')
act = s[a:b]
act = sub(act, 'int W = Layout2.ScreenW, H = Layout2.ScreenH;', 'int W = _vw, H = _vh;')
act = act.replace('- _camX', '- _dx').replace('- _camY', '- _dy')
act = sub(act, '< _camY)', '< _dy + (_vh - Layout2.ScreenH))')
s = s[:a] + act + s[b:]
# bubble
s = sub(s, '''    private void DrawBubble(Canvas g, string text, int r, int gr, int b, int lift = 0)
    {
        int W = Layout2.ScreenW, H = Layout2.ScreenH;''', '''    private void DrawBubble(Canvas g, string text, int r, int gr, int b, int lift = 0)
    {
        int W = _vw, H = _vh;''')
save('Game2.Draw.cs', s, nl)
print('ok')
