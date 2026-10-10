namespace MikisWorld.Core;

/// <summary>What the application shell needs from a game (implemented by <see cref="Game"/> and <see cref="Game2"/>).</summary>
public interface IMikisGame
{
    /// <summary>The original screen (what the menus and the game logic work with).</summary>
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    /// <summary>The picture actually drawn: the original screen, or larger in the "Extended" view (see <see cref="SetView"/>).</summary>
    int ViewWidth { get; }
    int ViewHeight { get; }
    void SetView(int w, int h);
    GameStrings Strings { get; }
    bool BonusUnlocked { get; set; }
    bool WantsQuit { get; }
    int Score { get; }
    int CurrentState { get; }
    int CurrentArea { get; }
    int Frame { get; }

    void EnableRendering();
    /// <summary>Loads the pictures again (a mod changed a file while the game runs). Game state is kept.</summary>
    void ReloadAssets();
    void Start();
    /// <param name="dt">elapsed ms</param><param name="totalMs">game time since the run started (some events are scheduled on it)</param>
    void Update(int dt, int totalMs);
    void KeyPressed(int key, int action);
    void KeyReleased(int key, int action);
    void ChooseMode(int index);

    /// <summary>Debug switches (the application shares its own instance); <see cref="DrawHitboxes"/> paints the wireframe on top of the frame, every frame.</summary>
    DebugSettings Debug { get; set; }
    /// <summary>Key / button names for the hints inside the game ("Press 8 to enter").</summary>
    Func<GameAction, string> ActionName { get; set; }
    void DrawHitboxes(Canvas g);

    /// <summary>Optional departures from the original engine (off by default; the application sets them from the user's options).</summary>
    EngineTweaks Tweaks { get; }
    /// <summary>The frontend stops delivering key events (e.g. a menu opens): forget which keys are held.</summary>
    void ReleaseAll();
    /// <summary>Debug: from the world, go to the final stage; from the final stage, go to the ending.</summary>
    void DebugSkipLevel();
    /// <summary>Bonus minigame with the mouse: hover moves the cursor, a click places the orb.</summary>
    void BonusPointer(int x, int y, bool click);

    /// <summary>Paints the frame; returns false when the game decided nothing changed since the last paint.</summary>
    bool Draw(Canvas g);
    /// <summary>Animated background for screens outside the game world (menus).</summary>
    void DrawBackdrop(Canvas g, int frame);
}

/// <summary>Per-game texts and options of the application shell.</summary>
public sealed class AppInfo
{
    public string About { get; init; }
    public bool HasSplash { get; init; }
    /// <summary>Order of the option menu: the original commands have a priority, and in game 2 "High scores" (3) comes before "Instructions" (4).</summary>
    public bool HighScoresFirst { get; init; }
    /// <summary>Quirk of the original hi-score class: see <see cref="HallOfFame"/>.</summary>
    public bool EmptyNameBecomesSpace { get; init; }
    /// <summary>Game 2's title screen stops the title jingle when the game is entered (class <c>a</c>.stop → Sound.stop); game 1 lets it run on.</summary>
    public bool StopTitleSoundOnEnter { get; init; }
    public static AppInfo MikisWorld1 { get; } = new()
    {
        About = "Miki's World 1: The Lost Tiara v1.1.2\nCopyright 2002 Sumea\n\nPC port (unofficial) by TwistZero\ngithub.com/yanm1103",
        HasSplash = false
    };
    public static AppInfo MikisWorld2 { get; } = new()
    {
        About = "Miki's World 2: The Storm Master v1.2.2\nCopyright 2002 Sumea\n\nPC port (unofficial) by TwistZero\ngithub.com/yanm1103",
        HasSplash = false,
        HighScoresFirst = true,
        EmptyNameBecomesSpace = true,
        StopTitleSoundOnEnter = true
    };
}
