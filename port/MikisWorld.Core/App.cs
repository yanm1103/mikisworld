namespace MikisWorld.Core;

/// <summary>
/// The application shell: optional splash, title screen, options menu, instructions, high scores, name entry, game-mode
/// list and the game itself. Replaces the phone's softkey menu of the original (classes <c>GameMIDlet</c>, title class,
/// <c>k</c>) with an on-screen menu, keeping its commands and texts. Works for both games through <see cref="IMikisGame"/>.
/// </summary>
public sealed class App
{
    private enum Screen { Splash, Title, Help, About, HighScores, NameEntry, Options, Game }

    private readonly IStorage _storage;
    private readonly SaveData _save;
    private readonly HallOfFame _hof = new("aaa", 0, 1, 10, true);
    private readonly SettingsHost _host;
    private readonly IMikisGame _game;
    private readonly AppInfo _info;
    private readonly DisplaySettings _set;
    private int _optSel;
    private readonly Bitmap _titleArt, _splash;
    private readonly GameStrings _s;
    private readonly string _helpText;
    private readonly Bitmap _gameBuf;                 // last painted game frame (the game only repaints when it changed)
    private List<TextCommand> _gameTexts = new();

    private Screen _screen;
    private Screen _formReturn;               // where Back leads from a form
    private bool _formReopensMenu;            // ...and whether the option menu is shown again
    private bool _menuOpen;                   // option menu (covers title or paused game)
    private int _menuSel;
    private bool _gameInProgress;             // "Continue game" is available
    private int _modeSel;                     // "Select game" list (main / bonus)
    private int _ms;                          // animation clock
    private int _gameClock;                   // game time since the run started
    private int _scroll;
    private string _name = "";
    private int _pendingScore;
    private bool _titleSoundPlayed;

    public bool WantsExit { get; private set; }
    private int W => _game.ScreenWidth;
    private int H => _game.ScreenHeight;

    /// <summary>MikisWorld 1 convenience constructor.</summary>
    public App(IAssets assets, IGameHost host, IStorage storage, long seed)
        : this(h => new Game(assets, h, seed), assets, host, storage, AppInfo.MikisWorld1) { }

    public App(Func<IGameHost, IMikisGame> createGame, IAssets assets, IGameHost host, IStorage storage, AppInfo info,
               DisplaySettings settings = null)
    {
        _storage = storage;
        _info = info;
        _set = settings ?? new DisplaySettings();
        _save = storage.Load() ?? new SaveData();
        _host = new SettingsHost(host) { Enabled = _set.Volume > 0 };
        _game = createGame(_host);
        _game.BonusUnlocked = _save.BonusUnlocked;
        _game.EnableRendering();
        _s = _game.Strings;
        _helpText = GameStrings.LoadHelp(assets);
        _titleArt = assets.LoadBitmap("title.png");
        _splash = info.HasSplash ? assets.LoadBitmap("splash.png") : null;
        _hof.Import(0, _save.HighScores.Select(e => (e.Name, e.Score)).ToList());
        _gameBuf = new Bitmap(_game.ScreenWidth, _game.ScreenHeight);
        _screen = _splash != null ? Screen.Splash : Screen.Title;
    }

    public IMikisGame Game => _game;
    public DisplaySettings Settings => _set;

    /// <summary>The frontend should deliver key repeats (held key) while this is true: the options sliders use them.</summary>
    public bool AcceptsRepeat => _screen == Screen.Options;
    public int ScreenWidth => _game.ScreenWidth;
    public int ScreenHeight => _game.ScreenHeight;

    // ---- test hooks ----
    internal string DebugScreen => _screen.ToString();
    internal bool DebugMenuOpen => _menuOpen;
    internal List<string> DebugMenuLabels() => MenuItems().Select(i => i.Label).ToList();
    internal string DebugName => _name;
    internal List<string> DebugOptionRows() => OptionRows().Select(r => r.Label).ToList();

    // ------------------------------------------------------------------ time
    public void Update(int dt)
    {
        _ms += dt;
        if (_screen == Screen.Splash && _ms >= 4000) EndSplash();
        if (_screen == Screen.Title && !_titleSoundPlayed)
        {
            _titleSoundPlayed = true;
            _host.Play(Sfx.Title);
        }
        if (_screen != Screen.Game || _menuOpen) return;
        _gameClock += dt;
        _game.Update(dt, _gameClock);
        if (_game.BonusUnlocked != _save.BonusUnlocked)
        {
            _save.BonusUnlocked = _game.BonusUnlocked;
            _storage.Save(_save);
        }
        if (_game.WantsQuit) GameFinished();
    }

    private void EndSplash()
    {
        _screen = Screen.Title;
        _ms = 0;
    }

    // original GameMIDlet.for() when the game ended on its own
    private void GameFinished()
    {
        _gameInProgress = false;
        int score = _game.Score;
        if (_hof.IsHighScore(0, score))
        {
            _pendingScore = score;
            _name = "";
            _screen = Screen.NameEntry;
        }
        else
        {
            _screen = Screen.Title;
            _menuOpen = false;
        }
    }

    // ------------------------------------------------------------------ input
    private static bool IsUp(int key, int action) => action == 1 || key == 50;
    private static bool IsDown(int key, int action) => action == 6 || key == 56;
    private static bool IsSelect(int key, int action) => action == 8 || key == 53;
    private static bool IsLeft(int key, int action) => action == 2 || key == 52;
    private static bool IsRight(int key, int action) => action == 5 || key == 54;

    /// <summary>Same key/action pair the MIDP canvas delivered (see Game.KeyPressed).</summary>
    public void KeyPressed(int key, int action)
    {
        switch (_screen)
        {
            case Screen.Splash:
                EndSplash();
                break;
            case Screen.Title:
                if (!_menuOpen) { if (IsSelect(key, action)) OpenMenu(); }
                else MenuKey(key, action);
                break;
            case Screen.Game:
                if (_menuOpen) MenuKey(key, action);
                else if (_game.CurrentState == State.ModeMenu) ModeListKey(key, action);
                else _game.KeyPressed(key, action);
                break;
            case Screen.Help:
                if (IsUp(key, action) && _scroll > 0) _scroll--;
                else if (IsDown(key, action)) _scroll++;
                else if (IsSelect(key, action)) CloseForm();
                break;
            case Screen.About:
            case Screen.HighScores:
                if (IsSelect(key, action)) CloseForm();
                break;
            case Screen.NameEntry:
                break;
            case Screen.Options:
                OptionsKey(key, action);
                break;
        }
    }

    public void KeyReleased(int key, int action)
    {
        if (_screen == Screen.Game && !_menuOpen && _game.CurrentState != State.ModeMenu) _game.KeyReleased(key, action);
    }

    /// <summary>Esc / the platform's "back" button.</summary>
    public void Back()
    {
        switch (_screen)
        {
            case Screen.Splash:
                EndSplash();
                break;
            case Screen.Title:
                _menuOpen = !_menuOpen;
                _menuSel = 0;
                break;
            case Screen.Game:
                if (_menuOpen) _menuOpen = false;
                else OpenMenu();
                break;
            case Screen.Help:
            case Screen.About:
            case Screen.HighScores:
            case Screen.Options:
                CloseForm();
                break;
            case Screen.NameEntry:
                break;
        }
    }

    /// <summary>Typed character for the high-score name field (max <see cref="Layout.NameMaxLen"/>).</summary>
    public void TextInput(char c)
    {
        if (_screen != Screen.NameEntry || c < 32 || c > 126) return;
        if (_name.Length < Layout.NameMaxLen) _name += c;
    }

    public void Backspace()
    {
        if (_screen == Screen.NameEntry && _name.Length > 0) _name = _name[..^1];
    }

    /// <summary>Original "Save" command of the name form.</summary>
    public void ConfirmName()
    {
        if (_screen != Screen.NameEntry) return;
        _hof.Insert(0, _name, _pendingScore);
        _save.HighScores = _hof.Export(0).Select(e => new ScoreEntry { Name = e.Name, Score = e.Score }).ToList();
        _storage.Save(_save);
        _formReturn = Screen.Title;
        _formReopensMenu = false;
        _scroll = 0;
        _screen = Screen.HighScores;
    }

    // ------------------------------------------------------------------ menu
    private List<(string Label, Action Run)> MenuItems()
    {
        var items = new List<(string, Action)>();
        if (_gameInProgress) items.Add((_s.Ui(0), () => { _menuOpen = false; _screen = Screen.Game; }));
        items.Add((_s.Ui(10), StartNewGame));
        items.Add((_s.Ui(1), () => OpenForm(Screen.Help)));
        items.Add((_s.Ui(2), () => OpenForm(Screen.HighScores)));
        items.Add((OptionsLabel, () => { OpenForm(Screen.Options); _optSel = 0; }));
        items.Add((_s.Ui(11), () => OpenForm(Screen.About)));
        items.Add((_s.Ui(3), () => WantsExit = true));
        return items;
    }

    private void OpenMenu() { _menuOpen = true; _menuSel = 0; }

    private void MenuKey(int key, int action)
    {
        var items = MenuItems();
        if (IsUp(key, action)) _menuSel = (_menuSel + items.Count - 1) % items.Count;
        else if (IsDown(key, action)) _menuSel = (_menuSel + 1) % items.Count;
        else if (IsSelect(key, action)) items[Math.Min(_menuSel, items.Count - 1)].Run();
    }

    private void StartNewGame()
    {
        _menuOpen = false;
        _screen = Screen.Game;
        _gameInProgress = true;
        _modeSel = 0;
        _gameClock = 0;
        _game.Start();
    }

    private const string OptionsLabel = "Options";

    private sealed record OptionRow(string Label, Func<string> Value, Action<int> Adjust, bool Slider);

    private List<OptionRow> OptionRows()
    {
        var rows = new List<OptionRow>
        {
            new(_s.Ui(14).TrimEnd(' ', ':'), () => _set.Volume + "%", dir =>
            {
                _set.Volume = Math.Clamp(_set.Volume + dir * 5, 0, 100);
                _host.Enabled = _set.Volume > 0;
                _set.NotifyChanged();
            }, true),
            new("Fullscreen", () => _s.Ui(_set.Fullscreen ? 12 : 13), _ => { _set.Fullscreen = !_set.Fullscreen; _set.NotifyChanged(); }, false),
            new("Integer scale", () => _s.Ui(_set.IntegerScale ? 12 : 13), _ => { _set.IntegerScale = !_set.IntegerScale; _set.NotifyChanged(); }, false),
        };
        // smoothing only makes sense when the picture is not magnified by a whole number
        if (!_set.IntegerScale)
            rows.Add(new("Filter", () => _set.Bilinear ? "bilinear" : "nearest", _ => { _set.Bilinear = !_set.Bilinear; _set.NotifyChanged(); }, false));
        return rows;
    }

    private void OptionsKey(int key, int action)
    {
        var rows = OptionRows();
        _optSel = Math.Clamp(_optSel, 0, rows.Count - 1);
        if (IsUp(key, action)) _optSel = (_optSel + rows.Count - 1) % rows.Count;
        else if (IsDown(key, action)) _optSel = (_optSel + 1) % rows.Count;
        else if (IsLeft(key, action)) rows[_optSel].Adjust(-1);
        else if (IsRight(key, action)) rows[_optSel].Adjust(+1);
        else if (IsSelect(key, action))
        {
            if (rows[_optSel].Slider) CloseForm();
            else rows[_optSel].Adjust(1);
        }
        _optSel = Math.Clamp(_optSel, 0, OptionRows().Count - 1);     // the Filter row appears/disappears with Integer scale
    }

    private void OpenForm(Screen f)
    {
        _formReturn = _screen;
        _formReopensMenu = true;
        _screen = f;
        _scroll = 0;
        _menuOpen = false;
    }

    private void CloseForm()
    {
        _screen = _formReturn;
        _menuOpen = _formReopensMenu;
        _menuSel = 0;
    }

    private void ModeListKey(int key, int action)
    {
        if (IsUp(key, action) || IsDown(key, action)) _modeSel = 1 - _modeSel;
        else if (IsSelect(key, action)) _game.ChooseMode(_modeSel);
    }

    // ------------------------------------------------------------------ drawing
    private const int TL = Canvas.Left | Canvas.Top;

    public void Draw(Canvas g)
    {
        switch (_screen)
        {
            case Screen.Splash:
                DrawSplash(g);
                break;
            case Screen.Title:
                g.SetClip(0, 0, W, H);
                g.SetColor(0, 0, 0);
                g.FillRect(0, 0, W, H);
                g.DrawImage(_titleArt, (W - _titleArt.Width) / 2, (H - _titleArt.Height) / 2, TL);
                if (_menuOpen) { Dim(g); DrawMenu(g); }
                else if (_ms / 600 % 2 == 0) DrawShadowed(g, "Press Enter", W / 2, H - 14, FontKind.Small, 0xFFFFFF, Canvas.HCenter);
                break;
            case Screen.Game:
                DrawGame(g);
                if (_menuOpen) { Dim(g); DrawMenu(g); }
                else if (_game.CurrentState == State.ModeMenu) DrawModeList(g);
                break;
            case Screen.Help: DrawBackdrop(g); DrawHelp(g); break;
            case Screen.About: DrawBackdrop(g); DrawAbout(g); break;
            case Screen.HighScores: DrawBackdrop(g); DrawScores(g); break;
            case Screen.NameEntry: DrawBackdrop(g); DrawNameEntry(g); break;
            case Screen.Options: DrawBackdrop(g); DrawOptions(g); break;
        }
    }

    // original title class of the second game: black for 1 s, the splash for 2 s, black for 1 s
    private void DrawSplash(Canvas g)
    {
        g.SetClip(0, 0, W, H);
        g.SetColor(0, 0, 0);
        g.FillRect(0, 0, W, H);
        if (_ms >= 1000 && _ms < 3000)
            g.DrawImage(_splash, (W - _splash.Width) / 2, (H - _splash.Height) / 2, TL);
    }

    // The game paints into its own buffer (it skips frames where nothing changed); the shell copies it and re-emits the
    // text of the last real paint so speech bubbles don't flicker.
    private void DrawGame(Canvas g)
    {
        var gc = new Canvas(_gameBuf) { Metrics = g.Metrics };
        if (_game.Draw(gc)) _gameTexts = new List<TextCommand>(gc.Texts);
        g.SetClip(0, 0, W, H);
        g.DrawImage(_gameBuf, 0, 0, TL);
        g.Texts.AddRange(_gameTexts);
    }

    private void DrawBackdrop(Canvas g)
    {
        g.SetClip(0, 0, W, H);
        _game.DrawBackdrop(g, _ms / 20);
    }

    private void Dim(Canvas g)
    {
        g.SetClip(0, 0, W, H);
        g.SetColorArgb(0x99, 0, 0, 0);
        g.FillRect(0, 0, W, H);
    }

    private void Panel(Canvas g, int x, int y, int w, int h)
    {
        g.SetClip(0, 0, W, H);
        g.SetColor(99, 48, 0);
        g.FillRect(x, y, w, h);
        g.SetColor(255, 255, 255);
        g.DrawRect(x, y, w, h);
    }

    private static void DrawShadowed(Canvas g, string text, int x, int y, FontKind font, int rgb, int anchorExtra)
    {
        g.SetFont(font);
        g.SetColor(0, 0, 0);
        g.DrawString(text, x + 1, y + 1, TL | anchorExtra);
        g.SetColor(rgb >> 16, rgb >> 8, rgb);
        g.DrawString(text, x, y, TL | anchorExtra);
    }

    private void DrawMenu(Canvas g)
    {
        var items = MenuItems();
        int lineH = g.Metrics.Height(FontKind.Bold) + 3;
        int w = Math.Min(124, W - 4), h = items.Count * lineH + 8;
        int x = (W - w) / 2, y = (H - h) / 2;
        Panel(g, x, y, w, h);
        for (int i = 0; i < items.Count; i++)
        {
            int ty = y + 4 + i * lineH;
            if (i == _menuSel)
            {
                g.SetClip(0, 0, W, H);
                g.SetColor(160, 90, 20);
                g.FillRect(x + 3, ty - 1, w - 5, lineH);
            }
            DrawShadowed(g, items[i].Label, x + 10, ty, FontKind.Bold, i == _menuSel ? 0xFFFFFF : 0xFFE0A0, 0);
        }
    }

    private void DrawModeList(Canvas g)
    {
        DrawBackdrop(g);
        int lineH = g.Metrics.Height(FontKind.Bold) + 4;
        int w = Math.Min(124, W - 4), h = 3 * lineH + 10;
        int x = (W - w) / 2, y = (H - h) / 2;
        Panel(g, x, y, w, h);
        DrawShadowed(g, _s.Text(15), x + 8, y + 5, FontKind.Bold, 0xFFFFFF, 0);
        string[] labels = { _s.Text(13), _s.Text(14) };
        for (int i = 0; i < 2; i++)
        {
            int ty = y + 6 + (i + 1) * lineH;
            if (i == _modeSel)
            {
                g.SetClip(0, 0, W, H);
                g.SetColor(160, 90, 20);
                g.FillRect(x + 3, ty - 1, w - 5, lineH);
            }
            DrawShadowed(g, labels[i], x + 14, ty, FontKind.Bold, i == _modeSel ? 0xFFFFFF : 0xFFE0A0, 0);
        }
    }

    private static List<string> Wrap(Canvas g, string text, int maxW, FontKind font)
    {
        var lines = new List<string>();
        foreach (string para in text.Split('\n'))
        {
            string line = "";
            foreach (string word in para.Split(' '))
            {
                string t = line.Length == 0 ? word : line + " " + word;
                if (g.Metrics.StringWidth(t, font) <= maxW || line.Length == 0) line = t;
                else { lines.Add(line); line = word; }
            }
            lines.Add(line);
        }
        return lines;
    }

    private void DrawTitled(Canvas g, string title, out int bodyX, out int bodyY, out int bodyW, out int bodyH)
    {
        Panel(g, 6, 6, W - 12, H - 12);
        DrawShadowed(g, title, 12, 10, FontKind.Bold, 0xFFFFFF, 0);
        int th = g.Metrics.Height(FontKind.Bold);
        g.SetClip(0, 0, W, H);
        g.SetColor(255, 255, 255);
        g.FillRect(10, 10 + th + 2, W - 20, 1);
        bodyX = 12; bodyY = 10 + th + 6; bodyW = W - 24; bodyH = H - 12 - bodyY - 4;
    }

    private void DrawHelp(Canvas g)
    {
        DrawTitled(g, _s.Ui(1), out int x, out int y, out int w, out int h);
        var lines = Wrap(g, _helpText, w, FontKind.Small);
        int lh = g.Metrics.Height(FontKind.Small) + 1;
        int visible = Math.Max(1, h / lh);
        _scroll = Math.Max(0, Math.Min(_scroll, Math.Max(0, lines.Count - visible)));
        for (int i = 0; i < visible && _scroll + i < lines.Count; i++)
            DrawShadowed(g, lines[_scroll + i], x, y + i * lh, FontKind.Small, 0xFFFFFF, 0);
        if (_scroll > 0) DrawShadowed(g, "^", W - 18, y, FontKind.Small, 0xFFE0A0, 0);
        if (_scroll + visible < lines.Count) DrawShadowed(g, "v", W - 18, y + h - lh, FontKind.Small, 0xFFE0A0, 0);
    }

    private void DrawAbout(Canvas g)
    {
        DrawTitled(g, _s.Ui(11), out int x, out int y, out int w, out int h);
        var lines = Wrap(g, _info.About, w, FontKind.Small);
        int lh = g.Metrics.Height(FontKind.Small) + 1;
        for (int i = 0; i < lines.Count; i++) DrawShadowed(g, lines[i], x, y + i * lh, FontKind.Small, 0xFFFFFF, 0);
    }

    private void DrawScores(Canvas g)
    {
        DrawTitled(g, _s.Ui(2), out int x, out int y, out int w, out int h);
        int lh = Math.Min(g.Metrics.Height(FontKind.Small) + 1, h / _hof.Entries);
        for (int i = 0; i < _hof.Entries; i++)
        {
            string line = (i + 1) + ". " + _hof.Name(0, i) + " - " + _hof.Score(0, i);
            DrawShadowed(g, line, x, y + i * lh, FontKind.Small, i == 0 ? 0xFFE060 : 0xFFFFFF, 0);
        }
    }

    private void DrawOptions(Canvas g)
    {
        DrawTitled(g, OptionsLabel, out int x, out int y, out int w, out int h);
        var rows = OptionRows();
        int lh = g.Metrics.Height(FontKind.Bold) + 3;
        for (int i = 0; i < rows.Count; i++)
        {
            int ty = y + 2 + i * lh;
            bool sel = i == _optSel;
            if (sel)
            {
                g.SetClip(0, 0, W, H);
                g.SetColor(160, 90, 20);
                g.FillRect(x - 3, ty - 1, w + 5, lh);
            }
            DrawShadowed(g, rows[i].Label, x, ty, FontKind.Bold, sel ? 0xFFFFFF : 0xFFE0A0, 0);
            string value = rows[i].Value();
            int vw = g.Metrics.StringWidth(value, FontKind.Bold);
            DrawShadowed(g, value, x + w - vw, ty, FontKind.Bold, sel ? 0xFFFFFF : 0xFFE0A0, 0);
            if (rows[i].Slider)
            {
                int barW = 36, bx = x + w - vw - 6 - barW, by = ty + 3, bh = lh - 8;
                g.SetClip(0, 0, W, H);
                g.SetColor(40, 20, 0);
                g.FillRect(bx, by, barW, bh);
                g.SetColor(255, 224, 160);
                g.FillRect(bx, by, barW * _set.Volume / 100, bh);
                g.SetColor(255, 255, 255);
                g.DrawRect(bx, by, barW, bh);
            }
        }
        DrawShadowed(g, "< >", x, y + h - g.Metrics.Height(FontKind.Small) - 1, FontKind.Small, 0xFFE0A0, 0);
    }

    private void DrawNameEntry(Canvas g)
    {
        DrawTitled(g, _s.Ui(7), out int x, out int y, out int w, out int h);
        var lines = Wrap(g, _s.Ui(8), w, FontKind.Small);
        int lh = g.Metrics.Height(FontKind.Small) + 1;
        for (int i = 0; i < lines.Count; i++) DrawShadowed(g, lines[i], x, y + i * lh, FontKind.Small, 0xFFFFFF, 0);
        int by = y + lines.Count * lh + 6;
        g.SetClip(0, 0, W, H);
        g.SetColor(40, 20, 0);
        g.FillRect(x, by, w, lh + 6);
        g.SetColor(255, 255, 255);
        g.DrawRect(x, by, w, lh + 6);
        string shown = _name + (_ms / 400 % 2 == 0 ? "_" : " ");
        DrawShadowed(g, shown, x + 4, by + 3, FontKind.Small, 0xFFFFFF, 0);
        DrawShadowed(g, _pendingScore + " ", x, by + lh + 12, FontKind.Small, 0xFFE060, 0);
        DrawShadowed(g, "[Enter] " + _s.Ui(4), x, y + h - lh, FontKind.Small, 0xFFE0A0, 0);
    }
}

/// <summary>Forwards sound to the real host only when the Sound option is on (original c.a(boolean)).</summary>
public sealed class SettingsHost : IGameHost
{
    private readonly IGameHost _inner;
    public bool Enabled { get => _enabled; set { _enabled = value; if (!value) _inner.StopSound(); } }
    private bool _enabled = true;
    public SettingsHost(IGameHost inner) { _inner = inner; }
    public void Play(Sfx sound) { if (_enabled) _inner.Play(sound); }
    public void StopSound() => _inner.StopSound();
    public void Vibrate(int frequency, int milliseconds) { _inner.Vibrate(frequency, milliseconds); }
}
