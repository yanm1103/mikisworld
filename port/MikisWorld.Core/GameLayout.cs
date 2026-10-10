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

    public static readonly GameLayout One = new()
    {
        ScreenW = Layout.ScreenW, ScreenH = Layout.ScreenH, TileW = Layout.TileW, TileH = Layout.TileH,
        ViewTilesW = Layout.ViewTilesW, ViewTilesH = Layout.ViewTilesH, PlayerW = Layout.PlayerW,
        LordFrameW = Layout.LordFrameW, LordPoseW = Layout.LordPoseW, DigitW = Layout.DigitW, TimeDigitW = Layout.TimeDigitW,
        CoinHudX = Layout.CoinHudX, CoinHudY = Layout.CoinHudY, TimeHudX = Layout.TimeHudX, TimeHudY = Layout.TimeHudY,
        LivesX = Layout.LivesX, LivesY = Layout.LivesY,
        CellW = Layout.CellW, CellH = Layout.CellH, OrbW = Layout.OrbW, OrbH = Layout.OrbH,
        BarW = Layout.BarW, BarStripH = Layout.BarStripH, LeftBarX = Layout.LeftBarX, RightBarX = Layout.RightBarX,
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
    };
}
