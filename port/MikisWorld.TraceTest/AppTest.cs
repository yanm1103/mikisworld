using MikisWorld.Core;

// Scripted checks of the application shell (menus, flow, persistence) without a window.
internal static class AppTest
{
    private static int _fail;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _fail++;
    }

    private const int Up = 50, Down = 56, Fire = 53;     // phone keys '2', '8', '5'

    private static void Tap(App a, int key)
    {
        int act = key == Up ? 1 : key == Down ? 6 : key == Fire ? 8 : 0;
        a.KeyPressed(key, act);
        a.KeyReleased(key, act);
    }

    /// <summary>Opens the menu if needed, moves to the item with this label and activates it.</summary>
    private static void Select(App a, string label)
    {
        if (!a.DebugMenuOpen) Tap(a, Fire);
        int idx = a.DebugMenuLabels().IndexOf(label);
        if (idx < 0) throw new Exception($"menu item '{label}' not in [{string.Join("|", a.DebugMenuLabels())}]");
        a.Back();                                   // close then reopen: the selection resets to the first item
        if (!a.DebugMenuOpen) a.Back();
        for (int i = 0; i < idx; i++) Tap(a, Down);
        Tap(a, Fire);
    }

    public static int Run(string assetsDir, string assets2Dir = null)
    {
        int rc1 = RunMw1(assetsDir);
        int rc2 = assets2Dir != null ? RunMw2(assets2Dir) : 0;
        Console.WriteLine(_fail == 0 ? "ALL OK" : $"{_fail} FAILED");
        return rc1 | rc2;
    }

    private static App NewMw2(DirAssets assets, IStorage store, IGameHost host = null)
        => new App(h => new Game2(assets, h, 7), assets, host ?? new CountingHost(), store, AppInfo.MikisWorld2);

    private static int RunMw2(string dir)
    {
        var assets = new DirAssets(dir);
        var store = new MemoryStorage();
        Console.WriteLine("MikisWorld 2: title (the splash is optional via AppInfo.HasSplash, off in the shipped configuration)");
        var app = NewMw2(assets, store);
        Check(app.DebugScreen == "Title" && app.ScreenWidth == 128 && app.ScreenHeight == 128, "starts directly on the title screen, 128x128");
        var splashInfo = new AppInfo { About = "", HasSplash = true };
        App WithSplash() => new App(h => new Game2(assets, h, 7), assets, new CountingHost(), new MemoryStorage(), splashInfo);
        var sp = WithSplash();
        Check(sp.DebugScreen == "Splash", "with HasSplash the app starts on the splash screen");
        for (int i = 0; i < 100; i++) sp.Update(20);
        Check(sp.DebugScreen == "Splash", "the splash lasts ~4 s (still showing at 2 s)");
        for (int i = 0; i < 110; i++) sp.Update(20);
        Check(sp.DebugScreen == "Title", "title after 4 s");
        var skip = WithSplash();
        skip.KeyPressed(53, 8);
        Check(skip.DebugScreen == "Title", "any key skips the splash");

        var stopHost = new CountingHost();
        var m2s = NewMw2(assets, new MemoryStorage(), stopHost);
        m2s.Update(20); m2s.KeyPressed(55, 0);
        int stopsBefore = stopHost.Stops;
        Select(m2s, "New game");
        Check(stopHost.Stops > stopsBefore, "game 2 stops the title jingle when the game is entered");
        var m2 = NewMw2(assets, new MemoryStorage());
        m2.Update(20);
        m2.KeyPressed(55, 0);
        Check(m2.DebugMenuLabels().SequenceEqual(new[] { "New game", "High scores", "Instructions", "Options", "About", "Exit" }),
              "game 2 menu order follows the original command priorities (High scores before Instructions): " + string.Join("|", m2.DebugMenuLabels()));

        Console.WriteLine("MikisWorld 2: every screen draws without errors");
        var tmp = new App(h => new Game2(assets, h, 3), assets, new CountingHost(), new MemoryStorage(), AppInfo.MikisWorld2);
        var bmp = new Bitmap(128, 128);
        bool ok = true;
        try
        {
            for (int i = 0; i < 400; i++) { tmp.Update(20); tmp.Draw(new Canvas(bmp)); }
            tmp.KeyPressed(53, 8); tmp.Draw(new Canvas(bmp));                  // menu
            Select(tmp, "Instructions"); tmp.Draw(new Canvas(bmp)); tmp.Back();
            Select(tmp, "High scores"); tmp.Draw(new Canvas(bmp)); tmp.Back();
            Select(tmp, "About"); tmp.Draw(new Canvas(bmp)); tmp.Back();
            Select(tmp, "New game");
            for (int i = 0; i < 600; i++) { tmp.Update(20); tmp.Draw(new Canvas(bmp)); }
            tmp.Back(); tmp.Draw(new Canvas(bmp));                              // paused game + menu
        }
        catch (Exception ex) { ok = false; Console.WriteLine("  " + ex); }
        Check(ok, "splash, title, menu, forms, cutscene and paused game all draw");

        Console.WriteLine("MikisWorld 2: new game, cheat code, bonus");
        app = NewMw2(assets, store);
        for (int i = 0; i < 220; i++) app.Update(20);
        Select(app, "New game");
        Check(app.DebugScreen == "Game" && app.Game.CurrentState == State.Cutscene, "New game starts the intro cutscene");
        for (int i = 0; i < 5; i++) app.Update(20);
        app.KeyPressed(53, 8);
        for (int i = 0; i < 20 && app.Game.CurrentState == State.Cutscene; i++) app.Update(20);
        Check(app.Game.CurrentState == State.Playing, "cutscene skipped");
        Check(app.Context == App.InputContext.Gameplay, "playing: the remappable world controls apply (input context Gameplay)");
        int[] wrong = { 55, 55, 55, 55, 51, 51, 50, 50, 50, 55, 55, 55, 51, 51, 56 };           // the first game's code
        foreach (int k in wrong) { app.KeyPressed(k, 0); app.KeyReleased(k, 0); }
        app.Update(20);
        Check(!store.Data.BonusUnlocked, "the first game's code does NOT unlock the second game's bonus");
        int[] cheat = { 56, 51, 51, 55, 55, 55, 50, 50, 50, 51, 51, 55, 55, 55, 55 };
        foreach (int k in cheat) { app.KeyPressed(k, 0); app.KeyReleased(k, 0); }
        app.Update(20);
        Check(store.Data.BonusUnlocked, "typing 8 3 3 7 7 7 2 2 2 3 3 7 7 7 7 unlocks and saves the bonus game");
        var app2 = NewMw2(assets, store);
        for (int i = 0; i < 220; i++) app2.Update(20);
        Select(app2, "New game");
        app2.Update(20);
        Check(app2.Game.CurrentState == State.ModeMenu, "with the bonus unlocked, New game shows the mode list");
        app2.KeyPressed(56, 6); app2.KeyPressed(53, 8);
        for (int i = 0; i < 3; i++) app2.Update(20);
        Check(app2.Game.CurrentState == State.BonusPlayerTurn, "choosing 'Bonus game' starts the minigame");

        Console.WriteLine("MikisWorld 2: hi-score");
        var store3 = new MemoryStorage();
        var app3 = NewMw2(assets, store3);
        for (int i = 0; i < 220; i++) app3.Update(20);
        Select(app3, "New game");
        ((Game2)app3.Game).DebugSetCoins(33);
        ((Game2)app3.Game).DebugEndGame();
        for (int i = 0; i < 400 && app3.DebugScreen == "Game"; i++) app3.Update(20);
        Check(app3.DebugScreen == "NameEntry", "a qualifying score asks for a name");
        foreach (char c in "Amie") app3.TextInput(c);
        app3.ConfirmName();
        Check(store3.Data.HighScores[0].Name == "Amie" && store3.Data.HighScores[0].Score == 33, "score saved");
        return _fail == 0 ? 0 : 1;
    }

    private static int RunMw1(string assetsDir)
    {
        var assets = new DirAssets(assetsDir);
        var store = new MemoryStorage();
        var host = new CountingHost();
        var app = new App(assets, host, store, 1234);

        Console.WriteLine("title and menu");
        app.Update(20);
        Check(app.DebugScreen == "Title" && !app.DebugMenuOpen, "starts on the title screen with the menu closed");
        Check(app.Context == App.InputContext.Menu, "the title screen uses the fixed menu controls");
        Check(host.Played.Contains(Sfx.Title), "title jingle plays");
        Tap(app, Fire);
        Check(app.DebugMenuOpen, "Fire opens the menu");
        Check(app.DebugMenuLabels().SequenceEqual(new[] { "New game", "Instructions", "High scores", "Options", "About", "Exit" }),
              "menu items match the original commands (no Continue yet): " + string.Join("|", app.DebugMenuLabels()));

        // the original title canvas opens the menu on ANY key, or by itself after 5 s
        var anyKey = new App(assets, new CountingHost(), new MemoryStorage(), 1234);
        anyKey.Update(20);
        anyKey.KeyPressed(55, 0);
        Check(anyKey.DebugMenuOpen, "any key (here '7') opens the menu");
        var idle = new App(assets, new CountingHost(), new MemoryStorage(), 1234);
        for (int i = 0; i < 240; i++) idle.Update(20);
        Check(!idle.DebugMenuOpen, "the menu does not open before 5 s");
        for (int i = 0; i < 20; i++) idle.Update(20);
        Check(idle.DebugMenuOpen, "after 5 s without a key the menu opens by itself");
        idle.Back();
        for (int i = 0; i < 200; i++) idle.Update(20);
        Check(!idle.DebugMenuOpen, "closing the menu restarts the 5 s clock");
        for (int i = 0; i < 60; i++) idle.Update(20);
        Check(idle.DebugMenuOpen, "...and it opens again");

        var m1s = new CountingHost();
        var m1a = new App(assets, m1s, new MemoryStorage(), 1);
        m1a.Update(20); m1a.KeyPressed(55, 0);
        Select(m1a, "New game");
        Check(m1s.Stops == 0, "game 1 lets the title jingle run on when the game starts");

        Console.WriteLine("forms");
        Select(app, "Instructions");
        Check(app.DebugScreen == "Help", "Instructions opens the help form");
        app.Back();
        Check(app.DebugScreen == "Title" && app.DebugMenuOpen, "Back returns to the menu");
        Select(app, "About");
        Check(app.DebugScreen == "About", "About opens");
        app.Back();

        Console.WriteLine("options: volume slider, fullscreen, integer scale, filter");
        Select(app, "Options");
        Check(app.DebugScreen == "Options", "Options opens the options screen");
        var set = app.Settings;
        int changes = 0;
        set.Changed += () => changes++;
        void Left() { app.KeyPressed(52, 2); app.KeyReleased(52, 2); }
        void Right() { app.KeyPressed(54, 5); app.KeyReleased(54, 5); }
        Check(set.Volume == 100 && set.IntegerScale && !set.Bilinear && !set.Fullscreen, "defaults: volume 100 %, integer scale on, nearest, windowed");
        for (int i2 = 0; i2 < 4; i2++) Left();
        Check(set.Volume == 80 && changes == 4, "Left lowers the volume in steps of 5 %: " + set.Volume);
        for (int i2 = 0; i2 < 30; i2++) Left();
        Check(set.Volume == 0, "the volume stops at 0 %");
        host.Played.Clear();
        Right();
        Check(set.Volume == 5, "Right raises it again (0 % -> 5 %)");
        for (int i2 = 0; i2 < 30; i2++) Right();
        Check(set.Volume == 100, "the volume stops at 100 %");
        Tap(app, Down);
        Tap(app, Fire);
        Check(set.Fullscreen, "Fullscreen row: Fire turns it on");
        Tap(app, Fire);
        Check(!set.Fullscreen, "...and off again");
        Tap(app, Down);
        Check(app.DebugOptionRows().SequenceEqual(new[] { "Sound", "Fullscreen", "Integer scale", "Movement", "Keyboard keys", "Gamepad" }), "with integer scale on there is no Filter row: " + string.Join("|", app.DebugOptionRows()));
        Tap(app, Fire);
        Check(!set.IntegerScale && app.DebugOptionRows().Contains("Filter"), "Integer scale off -> the Filter row appears");
        Tap(app, Down);
        Tap(app, Fire);
        Check(set.Bilinear && set.UsesBilinear, "Filter row switches nearest -> bilinear");
        Tap(app, Up);
        Tap(app, Fire);
        Check(set.IntegerScale && !set.UsesBilinear && !app.DebugOptionRows().Contains("Filter"), "Integer scale back on -> bilinear is not used and the row disappears");
        app.Back();
        Check(app.DebugScreen == "Title" && app.DebugMenuOpen, "Back returns to the menu");
        app.Back();

        Console.WriteLine("controls: remapping keyboard keys and gamepad buttons");
        {
            var bind = app.Bindings;
            int bchanges = 0;
            bind.Changed += () => bchanges++;
            Check(bind.Get(BindingDevice.Keyboard, GameAction.Jump, 0) == "Up" && bind.Get(BindingDevice.Gamepad, GameAction.Jump, 0) == "A", "defaults: Jump = Up / pad A");
            Select(app, "Options");
            for (int i2 = 0; i2 < 4; i2++) Tap(app, Down);
            Tap(app, Fire);
            Check(app.DebugScreen == "ControlsKeyboard", "Options > Keyboard keys opens the remapping screen");
            Tap(app, Down); Tap(app, Down);
            Check(app.DebugControlsCursor == (2, 0), "cursor on the Jump row, first cell");
            Tap(app, Fire);
            Check(app.CapturingInput && app.CaptureDevice == BindingDevice.Keyboard, "Fire starts waiting for a key");
            app.CaptureInput(BindingDevice.Gamepad, "B");
            Check(app.CapturingInput && bind.Get(BindingDevice.Keyboard, GameAction.Jump, 0) == "Up", "a gamepad button is ignored while waiting for a key");
            app.CaptureInput(BindingDevice.Keyboard, "J");
            Check(!app.CapturingInput && bind.Get(BindingDevice.Keyboard, GameAction.Jump, 0) == "J" && bchanges > 0, "the key is bound and the change is announced");
            app.KeyPressed(54, 5); app.KeyReleased(54, 5);            // Right: second cell
            Tap(app, Fire);
            app.CaptureInput(BindingDevice.Keyboard, "Left");          // was Left's first key
            Check(bind.Get(BindingDevice.Keyboard, GameAction.Left, 0) == "" && bind.Get(BindingDevice.Keyboard, GameAction.Jump, 1) == "Left",
                  "an input can drive only one action: binding 'Left' to Jump takes it away from Left");
            app.Backspace();
            Check(bind.Get(BindingDevice.Keyboard, GameAction.Jump, 1) == "", "Backspace clears the cell");
            Tap(app, Fire);
            app.Back();
            Check(!app.CapturingInput && app.DebugScreen == "ControlsKeyboard", "Esc cancels a pending capture without leaving the screen");
            for (int i2 = 0; i2 < 5; i2++) Tap(app, Down);             // row 7 = Defaults
            Tap(app, Fire);
            Check(bind.Get(BindingDevice.Keyboard, GameAction.Left, 0) == "Left" && bind.Get(BindingDevice.Keyboard, GameAction.Jump, 0) == "Up", "Defaults restores the keyboard bindings");
            app.Back();
            Check(app.DebugScreen == "Options", "Esc returns to the options");
            Tap(app, Down);
            Tap(app, Fire);
            Check(app.DebugScreen == "ControlsPad", "Options > Gamepad opens the controller screen");
            Tap(app, Fire);
            app.CaptureInput(BindingDevice.Keyboard, "K");
            Check(app.CapturingInput, "a key is ignored while waiting for a button");
            app.CaptureInput(BindingDevice.Gamepad, "L1");
            Check(bind.Get(BindingDevice.Gamepad, GameAction.Left, 0) == "L1", "the button is bound: Left = L1");
            app.Back(); app.Back(); app.Back();

            // pure bindings logic
            var b2 = new InputBindings();
            b2.Set(BindingDevice.Gamepad, GameAction.Jump, 0, "R2");
            var copy = new InputBindings();
            copy.Import(BindingDevice.Gamepad, b2.Export(BindingDevice.Gamepad));
            Check(copy.Get(BindingDevice.Gamepad, GameAction.Jump, 0) == "R2" && copy.Get(BindingDevice.Gamepad, GameAction.Left, 1) == "SLeft", "export/import keeps every binding");
            Check(b2.ActionsFor(BindingDevice.Keyboard, "W").SequenceEqual(new[] { GameAction.Jump }) && !b2.ActionsFor(BindingDevice.Keyboard, "Z").Any(), "ActionsFor finds the action of an input");
            Check(InputBindings.Short("LeftShift") == "LShift" && InputBindings.Short("") == "-" && InputBindings.Short("DLeft") == "DLeft", "cell texts are shortened for the small screens");
        }

        Console.WriteLine("new game, pause, continue");
        Select(app, "New game");
        Check(app.DebugScreen == "Game" && app.Game.CurrentState == State.Cutscene, "New game starts the intro cutscene");
        Check(app.Context == App.InputContext.Scene, "during a cutscene the controller buttons mean Confirm (input context Scene)");
        for (int i = 0; i < 50; i++) app.Update(20);
        int frameBefore = app.Game.Frame;
        app.Back();
        Check(app.DebugMenuOpen && app.DebugMenuLabels()[0] == "Continue game", "Esc in game opens the menu with 'Continue game' first");
        Check(app.Context == App.InputContext.Menu, "with the menu open the input context is Menu");
        for (int i = 0; i < 50; i++) app.Update(20);
        Check(app.Game.Frame == frameBefore, "game is paused while the menu is open");
        Select(app, "Continue game");
        for (int i = 0; i < 10; i++) app.Update(20);
        Check(app.Game.Frame > frameBefore && app.DebugScreen == "Game", "Continue resumes the same game");

        Console.WriteLine("game over -> high score -> name entry");
        ((Game)app.Game).DebugSetCoins(17);
        ((Game)app.Game).DebugEndGame();
        for (int i = 0; i < 200 && app.DebugScreen == "Game"; i++) app.Update(20);
        Check(app.DebugScreen == "NameEntry", "a qualifying score asks for a name");
        foreach (char c in "Miki-san-long-name") app.TextInput(c);
        Check(app.DebugName == "Miki-san", "name is limited to 8 characters: " + app.DebugName);
        app.Backspace(); app.Backspace();
        app.TextInput('!');
        app.ConfirmName();
        Check(app.DebugScreen == "HighScores", "Save shows the high-score list");
        Check(store.Data.HighScores[0].Name == "Miki-s!" && store.Data.HighScores[0].Score == 17,
              "score saved to storage: " + store.Data.HighScores[0].Name + " " + store.Data.HighScores[0].Score);
        app.Back();
        Check(app.DebugScreen == "Title" && !app.DebugMenuOpen, "after the list we are back at the title");
        Tap(app, Fire);
        Check(!app.DebugMenuLabels().Contains("Continue game"), "no Continue after a finished game");
        app.Back();

        Console.WriteLine("non-qualifying score skips name entry");
        Select(app, "New game");
        ((Game)app.Game).DebugSetCoins(0);
        ((Game)app.Game).DebugEndGame();
        for (int i = 0; i < 200 && app.DebugScreen == "Game"; i++) app.Update(20);
        Check(app.DebugScreen == "Title", "score 0 does not qualify");

        Console.WriteLine("persistence round trip");
        var app2 = new App(assets, new CountingHost(), store, 1);
        Select(app2, "High scores");
        Check(app2.DebugScreen == "HighScores", "second launch opens the high scores");
        Check(store.Data.HighScores[0].Score == 17, "table survives a restart");

        Console.WriteLine("bonus unlock via cheat code");
        var store2 = new MemoryStorage();
        var app3 = new App(assets, new CountingHost(), store2, 5);
        Select(app3, "New game");
        for (int i = 0; i < 5; i++) app3.Update(20);
        app3.KeyPressed(53, 8);                                           // skip the intro cutscene
        for (int i = 0; i < 20 && app3.Game.CurrentState == State.Cutscene; i++) app3.Update(20);
        int[] cheat = { 55, 55, 55, 55, 51, 51, 50, 50, 50, 55, 55, 55, 51, 51, 56 };
        foreach (int k in cheat) { app3.KeyPressed(k, 0); app3.KeyReleased(k, 0); }
        app3.Update(20);
        Check(store2.Data.BonusUnlocked, "typing the code unlocks and saves the bonus game");
        var app4 = new App(assets, new CountingHost(), store2, 6);
        Select(app4, "New game");
        app4.Update(20);
        Check(app4.Game.CurrentState == State.ModeMenu, "with the bonus unlocked, New game shows the mode list");
        app4.KeyPressed(Down, 6);
        app4.KeyPressed(Fire, 8);
        for (int i = 0; i < 3; i++) app4.Update(20);
        Check(app4.Game.CurrentState == State.BonusPlayerTurn, "choosing 'Bonus game' starts the minigame");

        return _fail == 0 ? 0 : 1;
    }

    private sealed class CountingHost : IGameHost
    {
        public readonly List<Sfx> Played = new();
        public void Play(Sfx sound) { Played.Add(sound); }
        public int Stops;
        public void StopSound() { Stops++; }
        public void Vibrate(int frequency, int milliseconds) { }
    }
}
