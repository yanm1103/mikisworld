namespace MikisWorld.Core;

/// <summary>What the application shell needs from a game (implemented by <see cref="Game"/> and <see cref="Game2"/>).</summary>
public interface IMikisGame
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    GameStrings Strings { get; }
    bool BonusUnlocked { get; set; }
    bool WantsQuit { get; }
    int Score { get; }
    int CurrentState { get; }
    int Frame { get; }

    void EnableRendering();
    void Start();
    /// <param name="dt">elapsed ms</param><param name="totalMs">game time since the run started (some events are scheduled on it)</param>
    void Update(int dt, int totalMs);
    void KeyPressed(int key, int action);
    void KeyReleased(int key, int action);
    void ChooseMode(int index);

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
    public static AppInfo MikisWorld1 { get; } = new()
    {
        About = "Miki's World 1: The Lost Tiara v1.1.2\nCopyright 2002 Sumea\nwww.sumea.com\n\nPC port (unofficial) by TwistZero\ngithub.com/yanm1103",
        HasSplash = false
    };
    public static AppInfo MikisWorld2 { get; } = new()
    {
        About = "Miki's World 2: The Storm Master v1.2.2\nCopyright 2002 Sumea\nwww.sumea.com\n\nPC port (unofficial) by TwistZero\ngithub.com/yanm1103",
        HasSplash = false
    };
}
