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

    public static int Run(string assetsDir)
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
        app.Game.DebugSetCoins(17);
        app.Game.DebugEndGame();
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
        app.Game.DebugSetCoins(0);
        app.Game.DebugEndGame();
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

        Console.WriteLine(_fail == 0 ? "ALL OK" : $"{_fail} FAILED");
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
