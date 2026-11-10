namespace MikisWorld.Core;

/// <summary>The per-game screen / tile / HUD constants as an object, so code shared by both games can use them
/// (<see cref="Layout"/> and <see cref="Layout2"/> keep the constants themselves).</summary>
public sealed class GameLayout
{
    public int ScreenW, ScreenH;
    public int TileW, TileH;
    public int ViewTilesW, ViewTilesH;
    public int PlayerW;
    public int LordFrameW, LordPoseW;
    public int DigitW, TimeDigitW;
    public int CoinHudX, CoinHudY, TimeHudX, TimeHudY, LivesX, LivesY;
    public int CellW, CellH, OrbW, OrbH;
    public int BarW, BarStripH, LeftBarX, RightBarX;
    public int GameOverX, GameOverY;
    /// <summary>The picture is drawn with the tiles' sky only (no sky.png / clouds.png): the 128x128 build of game 1 and every build of game 2.</summary>
    public bool TileSky;
    /// <summary>How many strings of the text file come before the in-game texts (the 128x128 build of game 1 has two extra menu strings).</summary>
    public int UiStrings = 16;

    public static readonly GameLayout One = new()
    {
        ScreenW = Layout.ScreenW, ScreenH = Layout.ScreenH, TileW = Layout.TileW, TileH = Layout.TileH,
        ViewTilesW = Layout.ViewTilesW, ViewTilesH = Layout.ViewTilesH, PlayerW = Layout.PlayerW,
        LordFrameW = Layout.LordFrameW, LordPoseW = Layout.LordPoseW, DigitW = Layout.DigitW, TimeDigitW = Layout.TimeDigitW,
        CoinHudX = Layout.CoinHudX, CoinHudY = Layout.CoinHudY, TimeHudX = Layout.TimeHudX, TimeHudY = Layout.TimeHudY,
        LivesX = Layout.LivesX, LivesY = Layout.LivesY,
        CellW = Layout.CellW, CellH = Layout.CellH, OrbW = Layout.OrbW, OrbH = Layout.OrbH,
        BarW = Layout.BarW, BarStripH = Layout.BarStripH, LeftBarX = Layout.LeftBarX, RightBarX = Layout.RightBarX,
        GameOverX = Layout.GameOverX, GameOverY = Layout.GameOverY,
    };

    public static readonly GameLayout Two = new()
    {
        ScreenW = Layout2.ScreenW, ScreenH = Layout2.ScreenH, TileW = Layout2.TileW, TileH = Layout2.TileH,
        ViewTilesW = Layout2.ViewTilesW, ViewTilesH = Layout2.ViewTilesH, PlayerW = Layout2.PlayerW,
        LordFrameW = Layout2.LordFrameW, LordPoseW = Layout2.LordPoseW, DigitW = Layout2.DigitW, TimeDigitW = Layout2.TimeDigitW,
        CoinHudX = Layout2.CoinHudX, CoinHudY = Layout2.CoinHudY, TimeHudX = Layout2.TimeHudX, TimeHudY = Layout2.TimeHudY,
        LivesX = Layout2.LivesX, LivesY = Layout2.LivesY,
        CellW = Layout2.CellW, CellH = Layout2.CellH, OrbW = Layout2.OrbW, OrbH = Layout2.OrbH,
        BarW = Layout2.BarW, BarStripH = Layout2.BarStripH, LeftBarX = Layout2.LeftBarX, RightBarX = Layout2.RightBarX,
        TileSky = true,
    };

    // The other screen-size builds of the same games (read off the original layout classes: g / f / f / g of the four JARs).
    /// <summary>Game 1, 128x128 build (12 px tiles, no sky.png, 25 tile images).</summary>
    public static readonly GameLayout OneSmall = new()
    {
        ScreenW = 128, ScreenH = 128, TileW = 12, TileH = 12, ViewTilesW = 12, ViewTilesH = 12, PlayerW = 15,
        LordFrameW = 36, LordPoseW = 28, DigitW = 8, TimeDigitW = 14,
        CoinHudX = 2, CoinHudY = 115, TimeHudX = 82, TimeHudY = 2, LivesX = 111, LivesY = 114,
        CellW = 20, CellH = 23, OrbW = 9, OrbH = 9, BarW = 5, BarStripH = 8, LeftBarX = 4, RightBarX = 119,
        GameOverX = 35, GameOverY = 49, TileSky = true, UiStrings = 18,
    };

    /// <summary>Game 2, 176x144 build (14 px tiles, sky.png + clouds.png).</summary>
    public static readonly GameLayout TwoLarge = new()
    {
        ScreenW = 176, ScreenH = 144, TileW = 14, TileH = 14, ViewTilesW = 14, ViewTilesH = 12, PlayerW = 16,
        LordFrameW = 40, LordPoseW = 26, DigitW = 8, TimeDigitW = 14,
        CoinHudX = 4, CoinHudY = 131, TimeHudX = 144, TimeHudY = 2, LivesX = 158, LivesY = 130,
        CellW = 25, CellH = 25, OrbW = 11, OrbH = 11, BarW = 5, BarStripH = 8, LeftBarX = 10, RightBarX = 161,
    };

    /// <summary>The build of a game is told apart by its title picture (176 px wide or 128 px wide).</summary>
    public static GameLayout For(int game, int titleWidth) => (game, titleWidth >= 160) switch
    {
        (1, true) => One, (1, false) => OneSmall, (2, true) => TwoLarge, _ => Two,
    };
}
