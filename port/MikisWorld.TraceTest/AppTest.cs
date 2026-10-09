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
        Console.WriteLine("MikisWorld 2: splash and title");
        var app = NewMw2(assets, store);
        Check(app.DebugScreen == "Splash" && app.ScreenWidth == 128 && app.ScreenHeight == 128, "starts on the splash screen, 128x128");
        for (int i = 0; i < 100; i++) app.Update(20);
        Check(app.DebugScreen == "Splash", "splash lasts ~4 s (still showing at 2 s)");
        for (int i = 0; i < 110; i++) app.Update(20);
        Check(app.DebugScreen == "Title", "title after 4 s");
        var skip = NewMw2(assets, new MemoryStorage());
        skip.KeyPressed(53, 8);
        Check(skip.DebugScreen == "Title", "any key skips the splash");

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
        Check(host.Played.Contains(Sfx.Title), "title jingle plays");
        Tap(app, Fire);
        Check(app.DebugMenuOpen, "Fire opens the menu");
        Check(app.DebugMenuLabels().SequenceEqual(new[] { "New game", "Instructions", "High scores", "Sound: on", "About", "Exit" }),
              "menu items match the original commands (no Continue yet): " + string.Join("|", app.DebugMenuLabels()));

        Console.WriteLine("forms");
        Select(app, "Instructions");
        Check(app.DebugScreen == "Help", "Instructions opens the help form");
        app.Back();
        Check(app.DebugScreen == "Title" && app.DebugMenuOpen, "Back returns to the menu");
        Select(app, "About");
        Check(app.DebugScreen == "About", "About opens");
        app.Back();

        Console.WriteLine("sound option persists");
        Select(app, "Sound: on");
        Check(!store.Data.Sound && app.DebugMenuLabels().Contains("Sound: off"), "toggling sound saves it and updates the label");
        Select(app, "Sound: off");
        Check(store.Data.Sound, "toggling again re-enables sound");

        Console.WriteLine("new game, pause, continue");
        Select(app, "New game");
        Check(app.DebugScreen == "Game" && app.Game.CurrentState == State.Cutscene, "New game starts the intro cutscene");
        for (int i = 0; i < 50; i++) app.Update(20);
        int frameBefore = app.Game.Frame;
        app.Back();
        Check(app.DebugMenuOpen && app.DebugMenuLabels()[0] == "Continue game", "Esc in game opens the menu with 'Continue game' first");
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
        public void StopSound() { }
        public void Vibrate(int frequency, int milliseconds) { }
    }
}
