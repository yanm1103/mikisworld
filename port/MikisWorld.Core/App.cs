namespace MikisWorld.Core;

/// <summary>
/// The application shell: optional splash, title screen, options menu, instructions, high scores, name entry, game-mode
/// list and the game itself. Replaces the phone's softkey menu of the original (classes <c>GameMIDlet</c>, title class,
/// <c>k</c>) with an on-screen menu, keeping its commands and texts. Works for both games through <see cref="IMikisGame"/>.
/// </summary>
public sealed class App
{
    private enum Screen { Splash, Title, Help, About, HighScores, NameEntry, Options, ControlsKeyboard, ControlsPad, Game }

    private readonly IStorage _storage;
    private readonly SaveData _save;
    private readonly HallOfFame _hof;
    private readonly SettingsHost _host;
    private readonly IMikisGame _game;
    private readonly AppInfo _info;
    private readonly DisplaySettings _set;
    private readonly InputBindings _bind;
    private int _optSel, _optTop;                // selected row of the options and the first row shown (the list scrolls)
    private int _menuTop;
    private bool _debugOpen;                      // the F3 debug menu is on top of everything
    private int _debugSel;
    /// <summary>Debug switches (F3 menu). Shared with the game.</summary>
    public DebugSettings Debug { get; } = new();
    private bool _settingsDirty;                 // an option or a key binding changed since the options were opened
    private int _savedUntil;                      // until when the corner note is shown
    private string _note = "Saved.";              // ...and what it says
    private int _scoreHold = -1;                  // ms the confirm key has been held on the high-score list (-1: not held)
    private bool _scoreCleared;                   // the table was cleared by this hold: the release must not close the list
    private const int ScoreClearMs = 3000;
    private int _resetShownUntil;                   // until when the controls screens say "Defaults restored"
    private int _ctrlRow, _ctrlCol;               // cursor of the controls screens (row 7 = "Defaults")
    private bool _capturing;                      // waiting for the next key / button to bind
    private readonly Bitmap _titleArt, _splash;
    private readonly GameStrings _s;
    private readonly string _helpText;
    private readonly Bitmap _gameBuf;                 // last painted game frame (the game only repaints when it changed)
    private List<TextCommand> _gameTexts = new();
    private readonly List<(int X, int Y, int W, int H, Action<int, int> Click, Action Hover)> _hits = new();   // clickable regions of the last drawn frame, topmost last

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
    private int _scoreHighlight;                  // row of the high-score list shown in yellow
    private int _titleIdle;                       // ms on the title without opening the menu
    private const int TitleMenuDelayMs = 5000;

    public bool WantsExit { get; private set; }

    /// <summary>Which device the player touched last: the on-screen hints ("Enter" or "A") and the Instructions follow it.</summary>
    public BindingDevice LastDevice { get; set; } = BindingDevice.Keyboard;
    private bool Pad => LastDevice == BindingDevice.Gamepad;
    private int W => _game.ScreenWidth;
    private int H => _game.ScreenHeight;

    /// <summary>MikisWorld 1 convenience constructor.</summary>
    public App(IAssets assets, IGameHost host, IStorage storage, long seed)
        : this(h => new Game(assets, h, seed), assets, host, storage, AppInfo.MikisWorld1) { }

    public App(Func<IGameHost, IMikisGame> createGame, IAssets assets, IGameHost host, IStorage storage, AppInfo info,
               DisplaySettings settings = null, InputBindings bindings = null)
    {
        _bind = bindings ?? new InputBindings();
        _storage = storage;
        _info = info;
        _hof = new HallOfFame("aaa", 0, 1, 10, true, info.EmptyNameBecomesSpace);
        _set = settings ?? new DisplaySettings();
        _save = storage.Load() ?? new SaveData();
        _host = new SettingsHost(host) { Enabled = _set.Volume > 0, VibrationAllowed = () => _set.Vibration && LastDevice == BindingDevice.Gamepad };
        _set.Changed += () => _settingsDirty = true;
        _bind.Changed += () => _settingsDirty = true;
        _game = createGame(_host);
        _game.BonusUnlocked = _save.BonusUnlocked;
        _game.Debug = Debug;
        _game.ActionName = PrimaryName;
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
    public InputBindings Bindings => _bind;

    /// <summary>Where the keys go, so the frontend knows how to translate a controller: <c>Menu</c> = fixed navigation (arrows, confirm, back);
    /// <c>Scene</c> = cutscene / ending / bonus minigame (confirm on the main button); <c>Gameplay</c> = the remappable world controls.</summary>
    public enum InputContext { Menu, Scene, Gameplay }

    public InputContext Context
    {
        get
        {
            if (_debugOpen || _screen != Screen.Game || _menuOpen) return InputContext.Menu;
            int st = _game.CurrentState;
            if (st == State.ModeMenu) return InputContext.Menu;
            if (st == State.Cutscene || st == State.Ending || st >= State.BonusPlayerTurn && st <= State.BonusAiMoving) return InputContext.Scene;
            return InputContext.Gameplay;
        }
    }

    /// <summary>True on the high-score name form (a controller has no keyboard, so the frontend edits <see cref="EnteredName"/> itself).</summary>
    public bool InNameEntry => _screen == Screen.NameEntry;
    public string EnteredName => _name;

    /// <summary>True while the controls screen waits for a key/button (the frontend must then send raw inputs to <see cref="CaptureInput"/>).</summary>
    public bool CapturingInput => _capturing;

    /// <summary>Which device the pending capture is for (valid while <see cref="CapturingInput"/>).</summary>
    public BindingDevice CaptureDevice => _screen == Screen.ControlsPad ? BindingDevice.Gamepad : BindingDevice.Keyboard;

    /// <summary>Binds the next input to the selected cell. Inputs of the other device are ignored.</summary>
    public void CaptureInput(BindingDevice device, string name)
    {
        if (!_capturing || device != CaptureDevice || string.IsNullOrEmpty(name)) return;
        _bind.Set(device, InputBindings.Actions[_ctrlRow], _ctrlCol, name);
        _capturing = false;
    }

    public void CancelCapture() => _capturing = false;

    /// <summary>The frontend should deliver key repeats (held key) while this is true: the options sliders use them.</summary>
    public bool AcceptsRepeat => _debugOpen || _screen == Screen.Options || _screen == Screen.ControlsKeyboard || _screen == Screen.ControlsPad;
    public int ScreenWidth => _game.ScreenWidth;
    public int ScreenHeight => _game.ScreenHeight;

    // ---- test hooks ----
    internal string DebugScreen => _screen.ToString();
    internal bool DebugMenuOpen => _menuOpen;
    internal List<string> DebugMenuLabels() => MenuItems().Select(i => i.Label).ToList();
    internal string DebugName => _name;
    internal bool DebugVibrationAllowed => _host.VibrationAllowed();
    internal int DebugScoreHighlight => _scoreHighlight;
    internal int DebugMenuSelected => _menuSel;
    internal List<string> DebugDebugRows() => DebugRows().Select(r => r.Label).ToList();
    internal (int Sel, int Top) DebugOptionsScroll => (_optSel, _optTop);
    internal List<(int X, int Y, int W, int H)> DebugHits() => _hits.Select(h => (h.X, h.Y, h.W, h.H)).ToList();
    internal string DebugHelp() => HelpWithBindings();
    internal bool DebugSavedNote => _ms < _savedUntil;
    internal string DebugNote => _note;
    internal bool DebugDefaultsNotice => _ms < _resetShownUntil;
    internal (int Row, int Col) DebugControlsCursor => (_ctrlRow, _ctrlCol);
    internal List<string> DebugOptionRows() => OptionRows().Select(r => r.Label).ToList();

    // ------------------------------------------------------------------ time
    public void Update(int dt)
    {
        SyncTweaks();
        _ms += dt;
        if (_screen == Screen.Splash && _ms >= 4000) EndSplash();
        if (_screen == Screen.Title && !_titleSoundPlayed)
        {
            _titleSoundPlayed = true;
            _host.Play(Sfx.Title);
        }
        // original title canvas: 5 s without a key and the option menu opens by itself (the clock restarts whenever the title is shown again)
        if (_screen == Screen.Title && !_menuOpen && !_debugOpen)
        {
            _titleIdle += dt;
            if (_titleIdle >= TitleMenuDelayMs) OpenMenu();
        }
        else _titleIdle = 0;
        if (_screen != Screen.HighScores) _scoreHold = -1;
        else if (_scoreHold >= 0 && !_scoreCleared && (_scoreHold += dt) >= ScoreClearMs) ClearScores();
        if (_screen != Screen.Game || _menuOpen || _debugOpen) return;
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
            // the original goes straight back to the title; showing the table (nothing highlighted) tells the player the score was not enough
            _scoreHighlight = -1;
            _formReturn = Screen.Title;
            _formReopensMenu = false;
            _scroll = 0;
            _menuOpen = false;
            _screen = Screen.HighScores;
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
        if (_debugOpen) { DebugKey(key, action); return; }
        switch (_screen)
        {
            case Screen.Splash:
                EndSplash();
                break;
            case Screen.Title:
                if (!_menuOpen) OpenMenu();             // the original title opens the menu on ANY key
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
                if (IsSelect(key, action)) CloseForm();
                break;
            case Screen.HighScores:
                if (IsSelect(key, action)) { _scoreHold = 0; _scoreCleared = false; }      // closes on release; held for 3 s it clears the table
                break;
            case Screen.NameEntry:
                break;
            case Screen.Options:
                OptionsKey(key, action);
                break;
            case Screen.ControlsKeyboard:
            case Screen.ControlsPad:
                ControlsKey(key, action);
                break;
        }
    }

    public void KeyReleased(int key, int action)
    {
        if (_screen == Screen.HighScores && IsSelect(key, action) && _scoreHold >= 0)
        {
            bool cleared = _scoreCleared;
            _scoreHold = -1;
            _scoreCleared = false;
            if (!cleared) CloseForm();
            return;
        }
        if (_screen == Screen.Game && !_menuOpen && !_debugOpen && _game.CurrentState != State.ModeMenu) _game.KeyReleased(key, action);
    }

    /// <summary>Esc / the platform's "back" button.</summary>
    public void Back()
    {
        if (_debugOpen) { _debugOpen = false; return; }
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
            case Screen.ControlsKeyboard:
            case Screen.ControlsPad:
                if (_capturing) _capturing = false;
                else _screen = Screen.Options;
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
        if ((_screen == Screen.ControlsKeyboard || _screen == Screen.ControlsPad) && !_capturing && _ctrlRow < InputBindings.Actions.Length)
            _bind.Clear(CaptureDevice, InputBindings.Actions[_ctrlRow], _ctrlCol);
        if (_screen == Screen.NameEntry && _name.Length > 0) _name = _name[..^1];
    }

    /// <summary>Original "Save" command of the name form.</summary>
    public void ConfirmName()
    {
        if (_screen != Screen.NameEntry) return;
        _scoreHighlight = Math.Max(0, _hof.Insert(0, _name, _pendingScore));
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
        if (_gameInProgress) items.Add((_s.Ui(0), () => { _menuOpen = false; _screen = Screen.Game; EnterGameSound(); }));
        items.Add((_s.Ui(10), StartNewGame));
        var help = (_s.Ui(1), (Action)(() => OpenForm(Screen.Help)));
        var scores = (_s.Ui(2), (Action)(() => OpenForm(Screen.HighScores)));
        if (_info.HighScoresFirst) { items.Add(scores); items.Add(help); }
        else { items.Add(help); items.Add(scores); }
        items.Add((OptionsLabel, () => { OpenForm(Screen.Options); _optSel = 0; }));
        items.Add((_s.Ui(11), () => OpenForm(Screen.About)));
        items.Add((_s.Ui(3), () => WantsExit = true));
        return items;
    }

    /// <summary>F3: opens / closes the debug menu (invincibility, infinite lives, hit boxes).</summary>
    public void ToggleDebugMenu()
    {
        _debugOpen = !_debugOpen;
        _debugSel = 0;
        if (_debugOpen) { _capturing = false; _game.ReleaseAll(); }
    }

    public bool DebugPanelOpen => _debugOpen;

    private sealed record DebugRow(string Label, Func<bool> On, Action Toggle, bool Command = false);   // a command runs once and closes the panel

    private List<DebugRow> DebugRows()
    {
        var rows = new List<DebugRow>
        {
            new("Invincible", () => Debug.Invincible, () => Debug.Invincible = !Debug.Invincible),
            new("Infinite lives", () => Debug.InfiniteLives, () => Debug.InfiniteLives = !Debug.InfiniteLives),
            new("Hitboxes", () => Debug.ShowHitboxes, () => Debug.ShowHitboxes = !Debug.ShowHitboxes),
            new("Unlock bonus", () => _game.BonusUnlocked, () =>
            {
                _game.BonusUnlocked = !_game.BonusUnlocked;               // takes effect at the next "New game"
                _save.BonusUnlocked = _game.BonusUnlocked;
                _storage.Save(_save);
            }),
            new("Skip level", () => false, () => { if (_screen == Screen.Game) _game.DebugSkipLevel(); }, Command: true),
        };
        return rows;
    }

    private void DebugKey(int key, int action)
    {
        var rows = DebugRows();
        _debugSel = Math.Clamp(_debugSel, 0, rows.Count - 1);
        if (IsUp(key, action)) _debugSel = (_debugSel + rows.Count - 1) % rows.Count;
        else if (IsDown(key, action)) _debugSel = (_debugSel + 1) % rows.Count;
        else if (IsSelect(key, action) || IsLeft(key, action) || IsRight(key, action)) RunDebugRow(rows[_debugSel]);
    }

    private void RunDebugRow(DebugRow row)
    {
        row.Toggle();
        if (row.Command) _debugOpen = false;
    }

    private void DrawDebugMenu(Canvas g)
    {
        Dim(g);
        var rows = DebugRows();
        _debugSel = Math.Clamp(_debugSel, 0, rows.Count - 1);
        Hit(0, 0, W, H, (_, _) => _debugOpen = false);              // a click outside closes it
        int lineH = g.Metrics.Height(FontKind.Bold) + 3;
        int w = Math.Min(124, W - 4), h = (rows.Count + 1) * lineH + 10;
        int x = (W - w) / 2, y = (H - h) / 2;
        Panel(g, x, y, w, h);
        DrawShadowed(g, "Debug (F3)", x + 8, y + 5, FontKind.Bold, 0xFFFFFF, 0);
        for (int i = 0; i < rows.Count; i++)
        {
            int ty = y + 5 + (i + 1) * lineH;
            bool sel = i == _debugSel;
            if (sel) { g.SetClip(0, 0, W, H); g.SetColor(160, 90, 20); g.FillRect(x + 3, ty - 1, w - 5, lineH); }
            string value = rows[i].Command ? ">" : rows[i].On() ? "on" : "off";
            DrawShadowed(g, rows[i].Label, x + 8, ty, FontKind.Bold, sel ? 0xFFFFFF : 0xFFE0A0, 0);
            DrawShadowed(g, value, x + w - 8 - g.Metrics.StringWidth(value, FontKind.Bold), ty, FontKind.Bold, rows[i].On() ? 0x80FF80 : (sel ? 0xFFFFFF : 0xFFE0A0), 0);
            int idx = i;
            Hit(x + 3, ty - 1, w - 5, lineH, (_, _) => { _debugSel = idx; RunDebugRow(rows[idx]); }, () => _debugSel = idx);
        }
    }

    private void OpenMenu() { _menuOpen = true; _menuSel = 0; _game.ReleaseAll(); }

    /// <summary>The engine departures the user asked for. The slower jump and walking animation are always on here (they only fix the
    /// pace of the original on a PC); "revamped movement" is the option. The traces/frame tests use a bare game, so they stay original.</summary>
    private void SyncTweaks()
    {
        var t = _game.Tweaks;
        t.SlowJump = true;
        t.FullWipeSnapshot = true;               // game 2: the original wipes a 12x12 snapshot (a bug): wipe like game 1
        t.StackHintBubbles = true;               // "Bonus game opened!" no longer sits on top of "Press ... to enter"
        t.SlowWalkAnimation = true;
        t.MorkSpeedPercent = 67;                 // Mork (game 2) moves at 2/3 of the original speed
        t.RevampedMovement = _set.RevampedMovement;
    }

    private void MenuKey(int key, int action)
    {
        var items = MenuItems();
        if (IsUp(key, action)) _menuSel = (_menuSel + items.Count - 1) % items.Count;
        else if (IsDown(key, action)) _menuSel = (_menuSel + 1) % items.Count;
        else if (IsSelect(key, action)) items[Math.Min(_menuSel, items.Count - 1)].Run();
    }

    private void EnterGameSound() { if (_info.StopTitleSoundOnEnter) _host.StopSound(); }

    private void StartNewGame()
    {
        _menuOpen = false;
        EnterGameSound();
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
        rows.Add(new("Movement", () => _set.RevampedMovement ? "new" : "original", _ => { _set.RevampedMovement = !_set.RevampedMovement; _set.NotifyChanged(); }, false));
        rows.Add(new(_s.Ui(15).TrimEnd(' ', ':'), () => _s.Ui(_set.Vibration ? 12 : 13), _ =>
        {
            _set.Vibration = !_set.Vibration;
            _set.NotifyChanged();
            if (_set.Vibration) _host.VibrateFeedback(100, 100);       // feedback: a short rumble when it is switched back on
        }, false));
        rows.Add(new("Keyboard keys", () => ">", _ => OpenControls(Screen.ControlsKeyboard), false));
        rows.Add(new("Gamepad", () => ">", _ => OpenControls(Screen.ControlsPad), false));
        return rows;
    }

    private void OpenControls(Screen f)
    {
        _screen = f;
        _ctrlRow = 0;
        _ctrlCol = 0;
        _capturing = false;
    }

    /// <summary>"Reset to defaults" row of the controls screens; the hint line confirms it for a moment.</summary>
    private void ResetBindings(BindingDevice device)
    {
        _bind.Reset(device);
        _resetShownUntil = _ms + 1500;
    }

    private void ControlsKey(int key, int action)
    {
        if (_capturing) return;                       // raw inputs go to CaptureInput
        int rows = InputBindings.Actions.Length + 1;
        if (IsUp(key, action)) _ctrlRow = (_ctrlRow + rows - 1) % rows;
        else if (IsDown(key, action)) _ctrlRow = (_ctrlRow + 1) % rows;
        else if (IsLeft(key, action) || IsRight(key, action)) _ctrlCol = 1 - _ctrlCol;
        else if (IsSelect(key, action))
        {
            if (_ctrlRow == InputBindings.Actions.Length) ResetBindings(CaptureDevice);
            else _capturing = true;
        }
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
        if (f == Screen.HighScores) _scoreHighlight = 0;          // from the menu the best score is the one highlighted
        _screen = f;
        _scroll = 0;
        _menuOpen = false;
        if (f == Screen.Options) _settingsDirty = false;
    }

    private void CloseForm()
    {
        if (_screen == Screen.Options && _settingsDirty) { ShowNote("Saved."); _settingsDirty = false; }   // the options are saved as they change: say so
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

    private void Hit(int x, int y, int w, int h, Action<int, int> click, Action hover = null) => _hits.Add((x, y, w, h, click, hover));

    /// <summary>The mouse moved over the picture (coordinates of the game screen): highlights what is under it.</summary>
    public void PointerMoved(int x, int y)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            var h = _hits[i];
            if (x < h.X || y < h.Y || x >= h.X + h.W || y >= h.Y + h.H) continue;
            if (h.Hover != null) { h.Hover(); return; }
        }
        if (_screen == Screen.Game && !_menuOpen && !_debugOpen) _game.BonusPointer(x, y, false);
    }

    /// <summary>Left click on the picture: activates the entry under it (menus, options, controls, forms).</summary>
    public void PointerPressed(int x, int y)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            var h = _hits[i];
            if (x < h.X || y < h.Y || x >= h.X + h.W || y >= h.Y + h.H) continue;
            h.Click(x - h.X, y - h.Y);
            return;
        }
        if (_screen == Screen.Game && !_menuOpen && !_debugOpen) _game.BonusPointer(x, y, true);
        if (_capturing) _capturing = false;
    }

    /// <summary>Right click: the same as Esc / the platform's back button.</summary>
    public void PointerBack() { if (_capturing) _capturing = false; else Back(); }

    /// <summary>Mouse wheel (+1 down, -1 up): scrolls the instructions and moves through the long lists.</summary>
    public void PointerWheel(int dir)
    {
        if (_screen == Screen.Help) _scroll = Math.Max(0, _scroll + dir);
        else if (_debugOpen) _debugSel = Math.Clamp(_debugSel + dir, 0, DebugRows().Count - 1);
        else if (_screen == Screen.Options) _optSel = Math.Clamp(_optSel + dir, 0, OptionRows().Count - 1);
        else if (_menuOpen) _menuSel = Math.Clamp(_menuSel + dir, 0, MenuItems().Count - 1);
    }

    public void Draw(Canvas g)
    {
        _hits.Clear();
        if (_debugOpen && _screen is Screen.Help or Screen.About or Screen.HighScores or Screen.NameEntry or Screen.Options or Screen.ControlsKeyboard or Screen.ControlsPad)
        {
            DrawBackdrop(g);                                                   // a form is hidden while the debug panel is open
            DrawDebugMenu(g);
            return;
        }
        switch (_screen)
        {
            case Screen.Splash:
                DrawSplash(g);
                Hit(0, 0, W, H, (_, _) => EndSplash());
                break;
            case Screen.Title:
                g.SetClip(0, 0, W, H);
                g.SetColor(0, 0, 0);
                g.FillRect(0, 0, W, H);
                g.DrawImage(_titleArt, (W - _titleArt.Width) / 2, (H - _titleArt.Height) / 2, TL);
                if (_debugOpen) { }                                            // the debug panel has the screen to itself
                else if (_menuOpen) { Dim(g); Hit(0, 0, W, H, (_, _) => Back()); DrawMenu(g); }
                else
                {
                    Hit(0, 0, W, H, (_, _) => OpenMenu());                     // any click opens the menu, like any key
                    if (_ms / 600 % 2 == 0) DrawShadowed(g, Pad ? "Press any button" : "Press any key", W / 2, H - 14, FontKind.Small, 0xFFFFFF, Canvas.HCenter);
                }
                break;
            case Screen.Game:
                DrawGame(g);
                if (_debugOpen) { }
                else if (_menuOpen) { Dim(g); Hit(0, 0, W, H, (_, _) => Back()); DrawMenu(g); }
                else if (_game.CurrentState == State.ModeMenu) DrawModeList(g);
                break;
            case Screen.Help: DrawBackdrop(g); DrawHelp(g); Hit(0, 0, W, H, (_, _) => Back()); break;
            case Screen.About: DrawBackdrop(g); DrawAbout(g); Hit(0, 0, W, H, (_, _) => Back()); break;
            case Screen.HighScores: DrawBackdrop(g); DrawScores(g); Hit(0, 0, W, H, (_, _) => Back()); break;
            case Screen.NameEntry: DrawBackdrop(g); DrawNameEntry(g); break;
            case Screen.Options: DrawBackdrop(g); DrawOptions(g); break;
            case Screen.ControlsKeyboard: DrawBackdrop(g); DrawControls(g, BindingDevice.Keyboard); break;
            case Screen.ControlsPad: DrawBackdrop(g); DrawControls(g, BindingDevice.Gamepad); break;
        }
        if (_debugOpen) DrawDebugMenu(g);
        if (_ms < _savedUntil) DrawSavedNote(g);
    }

    private void ShowNote(string text)
    {
        _note = text;
        _savedUntil = _ms + 1800;
    }

    private void DrawSavedNote(Canvas g)
    {
        string text = _note;
        int tw = g.Metrics.StringWidth(text, FontKind.Small), th = g.Metrics.Height(FontKind.Small);
        int w = tw + 8, h = th + 4;
        Panel(g, W - w - 3, H - h - 3, w, h);
        DrawShadowed(g, text, W - w - 3 + 4, H - h - 3 + 2, FontKind.Small, 0xFFFFFF, 0);
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
        _game.DrawHitboxes(g);                                  // debug wireframe (does nothing unless it is switched on)
        if (!_menuOpen && !_debugOpen) g.Texts.AddRange(_gameTexts);          // the game's text ("Hurry up!"...) is drawn on top of the picture: keep it away from the menu
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
        int visible = Math.Max(1, Math.Min(items.Count, (H - 4 - 8) / lineH));
        _menuSel = Math.Clamp(_menuSel, 0, items.Count - 1);
        _menuTop = Scroll(_menuTop, _menuSel, items.Count, visible);
        int w = Math.Min(124, W - 4), h = visible * lineH + 8;
        int x = (W - w) / 2, y = (H - h) / 2;
        Panel(g, x, y, w, h);
        DrawScrollMarks(g, _menuTop > 0, _menuTop + visible < items.Count, x + w - 12, y - 1, y + h - 11);
        for (int n = 0; n < visible; n++)
        {
            int i = _menuTop + n;
            int ty = y + 4 + n * lineH;
            if (i == _menuSel)
            {
                g.SetClip(0, 0, W, H);
                g.SetColor(160, 90, 20);
                g.FillRect(x + 3, ty - 1, w - 5, lineH);
            }
            DrawShadowed(g, items[i].Label, x + 10, ty, FontKind.Bold, i == _menuSel ? 0xFFFFFF : 0xFFE0A0, 0);
            int idx = i;
            Hit(x + 3, ty - 1, w - 5, lineH, (_, _) => { _menuSel = idx; items[idx].Run(); }, () => _menuSel = idx);
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
            int idx = i;
            Hit(x + 3, ty - 1, w - 5, lineH, (_, _) => { _modeSel = idx; _game.ChooseMode(idx); }, () => _modeSel = idx);
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

    /// <summary>Keeps <paramref name="sel"/> inside the window of <paramref name="visible"/> rows that starts at <paramref name="top"/>.</summary>
    private static int Scroll(int top, int sel, int count, int visible)
    {
        if (sel < top) top = sel;
        if (sel >= top + visible) top = sel - visible + 1;
        return Math.Clamp(top, 0, Math.Max(0, count - visible));
    }

    private void DrawScrollMarks(Canvas g, bool up, bool down, int x, int yTop, int yBottom)
    {
        if (up) DrawShadowed(g, "^", x, yTop, FontKind.Small, 0xFFE0A0, 0);
        if (down) DrawShadowed(g, "v", x, yBottom, FontKind.Small, 0xFFE0A0, 0);
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

    private static readonly System.Text.RegularExpressions.Regex HelpKeyLine = new(@"^(4,6|2|1,3|8|5)(\s*[=-]\s*)(.*)$");

    /// <summary>The "Controls" lines of the original help name the phone keys; show the keys / buttons the player really uses instead.</summary>
    private string HelpWithBindings()
    {
        var lines = _helpText.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var m = HelpKeyLine.Match(lines[i].TrimEnd('\r'));
            if (!m.Success) continue;
            string keys = m.Groups[1].Value switch
            {
                "4,6" => BoundNames(GameAction.Left, GameAction.Right),
                "2" => BoundNames(GameAction.Jump),
                // no dedicated diagonal buttons (the controller default): it is just jump + a direction
                "1,3" => BoundNames(GameAction.JumpLeft, GameAction.JumpRight) is "-, -" ? BoundNames(GameAction.Jump) + " + direction" : BoundNames(GameAction.JumpLeft, GameAction.JumpRight),
                "8" => BoundNames(GameAction.Interact),
                _ => BoundNames(GameAction.Confirm),
            };
            lines[i] = keys + m.Groups[2].Value + m.Groups[3].Value;
        }
        return string.Join("\n", lines);
    }

    /// <summary>The first key / button bound to an action for the device in use (null when none): for the hints inside the game.</summary>
    private string PrimaryName(GameAction a)
    {
        for (int slot = 0; slot < InputBindings.Slots; slot++)
        {
            string n = _bind.Get(LastDevice, a, slot);
            if (n != "") return InputBindings.Short(n);
        }
        return null;
    }

    private string BoundNames(params GameAction[] actions)
    {
        var parts = new List<string>();
        foreach (var a in actions)
        {
            var names = Enumerable.Range(0, InputBindings.Slots).Select(s => _bind.Get(LastDevice, a, s)).Where(n => n != "").Select(InputBindings.Short);
            string joined = string.Join("/", names);
            parts.Add(joined.Length == 0 ? "-" : joined);
        }
        return string.Join(", ", parts);
    }

    private void DrawHelp(Canvas g)
    {
        DrawTitled(g, _s.Ui(1), out int x, out int y, out int w, out int h);
        var lines = Wrap(g, HelpWithBindings(), w, FontKind.Small);
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

    /// <summary>Holding confirm for 3 s on the list wipes it (a test aid: the table is otherwise hard to empty).</summary>
    private void ClearScores()
    {
        _scoreCleared = true;
        _hof.Clear(0, "aaa", 0);
        _save.HighScores = _hof.Export(0).Select(e => new ScoreEntry { Name = e.Name, Score = e.Score }).ToList();
        _storage.Save(_save);
        _scoreHighlight = -1;
        ShowNote("Scores cleared");
    }

    private void DrawScores(Canvas g)
    {
        DrawTitled(g, _s.Ui(2), out int x, out int y, out int w, out int h);
        int lh = Math.Min(g.Metrics.Height(FontKind.Small) + 1, h / _hof.Entries);
        for (int i = 0; i < _hof.Entries; i++)
        {
            string line = (i + 1) + ". " + _hof.Name(0, i) + " - " + _hof.Score(0, i);
            DrawShadowed(g, line, x, y + i * lh, FontKind.Small, i == _scoreHighlight ? 0xFFE060 : 0xFFFFFF, 0);
        }
    }

    private void DrawOptions(Canvas g)
    {
        DrawTitled(g, OptionsLabel, out int x, out int y, out int w, out int h);
        var rows = OptionRows();
        int lh = g.Metrics.Height(FontKind.Bold) + 1;
        int visible = Math.Max(1, (h - 1) / lh);
        _optSel = Math.Clamp(_optSel, 0, rows.Count - 1);
        _optTop = Scroll(_optTop, _optSel, rows.Count, visible);
        DrawScrollMarks(g, _optTop > 0, _optTop + visible < rows.Count, W - 22, 10, H - 6 - g.Metrics.Height(FontKind.Small) - 1);
        for (int n = 0; n < visible && _optTop + n < rows.Count; n++)
        {
            int i = _optTop + n;
            int ty = y + 1 + n * lh;
            bool sel = i == _optSel;
            if (sel)
            {
                g.SetClip(0, 0, W, H);
                g.SetColor(160, 90, 20);
                g.FillRect(x - 3, ty - 1, w + 5, lh);
            }
            DrawShadowed(g, rows[i].Label, x, ty, FontKind.Bold, sel ? 0xFFFFFF : 0xFFE0A0, 0);
            int idx = i;
            var row = rows[i];
            Hit(x - 3, ty - 1, w + 5, lh, (px, _) =>
            {
                _optSel = idx;
                if (row.Slider) row.Adjust(px < (w + 5) / 2 ? -1 : +1);          // left half lowers, right half raises
                else row.Adjust(1);
                _optSel = Math.Clamp(_optSel, 0, OptionRows().Count - 1);
            }, () => _optSel = idx);
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
    }

    private void DrawControls(Canvas g, BindingDevice device)
    {
        DrawTitled(g, device == BindingDevice.Keyboard ? "Keyboard keys" : "Gamepad", out int x, out int y, out int w, out int h);
        var acts = InputBindings.Actions;
        int hintH = g.Metrics.Height(FontKind.Small) + 2;
        int lh = Math.Min(g.Metrics.Height(FontKind.Small) + 2, (h - hintH) / (acts.Length + 1));   // all rows + the hint line must fit
        int labelW = acts.Max(a => g.Metrics.StringWidth(InputBindings.Label(a), FontKind.Small)) + 4;
        int cellW = (w - labelW) / 2;
        for (int r = 0; r <= acts.Length; r++)
        {
            int ty = y + r * lh;
            bool last = r == acts.Length;
            if (last)
            {
                bool sel = r == _ctrlRow;
                if (sel) { g.SetClip(0, 0, W, H); g.SetColor(160, 90, 20); g.FillRect(x - 3, ty - 1, w + 5, lh); }
                DrawShadowed(g, "Reset to defaults", x, ty, FontKind.Small, sel ? 0xFFFFFF : 0xFF9090, 0);
                int lastRow = r;
                Hit(x - 3, ty - 1, w + 5, lh, (_, _) => { _capturing = false; _ctrlRow = lastRow; ResetBindings(device); }, () => { if (!_capturing) _ctrlRow = lastRow; });
                continue;
            }
            DrawShadowed(g, InputBindings.Label(acts[r]), x, ty, FontKind.Small, 0xFFE0A0, 0);
            for (int c = 0; c < InputBindings.Slots; c++)
            {
                int cx = x + labelW + c * cellW;
                bool sel = r == _ctrlRow && c == _ctrlCol;
                if (sel)
                {
                    g.SetClip(0, 0, W, H);
                    g.SetColor(_capturing ? 200 : 160, _capturing ? 60 : 90, 20);
                    g.FillRect(cx - 2, ty - 1, cellW, lh);
                }
                int cr = r, cc = c;
                Hit(cx - 2, ty - 1, cellW, lh, (_, _) => { _ctrlRow = cr; _ctrlCol = cc; _capturing = true; }, () => { if (!_capturing) { _ctrlRow = cr; _ctrlCol = cc; } });
                string text = sel && _capturing ? "..." : InputBindings.Short(_bind.Get(device, acts[r], c));
                DrawShadowed(g, text, cx, ty, FontKind.Small, sel ? 0xFFFFFF : 0xFFFFFF, 0);
            }
        }
        string hint;
        if (_ms < _resetShownUntil) hint = "Defaults restored";
        else if (_capturing) hint = device == BindingDevice.Keyboard ? "Press a key (Esc: cancel)" : "Press a button (Start: cancel)";
        else hint = Pad ? "A: change  X: clear" : "Enter: change  Bksp: clear";
        if (g.Metrics.StringWidth(hint, FontKind.Small) > w)           // the 128 px screen of game 2 is narrower than the hint
            hint = _capturing ? (device == BindingDevice.Keyboard ? "Press a key" : "Press a button") : Pad ? "A: set  X: clear" : "Enter: set  Bksp: clear";
        DrawShadowed(g, hint, x, y + h - g.Metrics.Height(FontKind.Small) + 1, FontKind.Small, 0xFFE060, 0);
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
        DrawShadowed(g, (Pad ? "[A] " : "[Enter] ") + _s.Ui(4), x, y + h - lh, FontKind.Small, 0xFFE0A0, 0);
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
    /// <summary>The original's vibrator switch; here also "only when the player is using a controller".</summary>
    public Func<bool> VibrationAllowed { get; set; } = () => true;
    public void Vibrate(int frequency, int milliseconds) { if (VibrationAllowed()) _inner.Vibrate(frequency, milliseconds); }

    /// <summary>A rumble that answers a menu action (switching Vibration on): it does not depend on which device was used last,
    /// a connected controller simply confirms the option.</summary>
    public void VibrateFeedback(int frequency, int milliseconds) => _inner.Vibrate(frequency, milliseconds);
}
