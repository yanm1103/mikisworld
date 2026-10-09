namespace MikisWorld.Core;

/// <summary>Screen/HUD constants (original class <c>g</c>) for the 176x144 layout.
/// The original supported several resolutions through this class; the PC port uses one logical resolution.</summary>
public static class Layout
{
    public const int ScreenW = 176, ScreenH = 144;
    public const int TileW = 14, TileH = 14;
    public const int ViewTilesW = 14, ViewTilesH = 12;
    public const int PlayerW = 16;
    public const int LordFrameW = 40;      // lord.png: first 2 frames are 40 px wide…
    public const int LordPoseW = 31;       // …then 31 px wide poses
    public const int GameOverX = 59, GameOverY = 57;
    public const int LivesX = 158, LivesY = 130;           // hearts, drawn right-to-left
    public const int CoinHudX = 4, CoinHudY = 131, DigitW = 8;
    public const int TimeHudX = 144, TimeHudY = 2, TimeDigitW = 14;
    // bonus (chain reaction) minigame
    public const int CellW = 25, CellH = 25, OrbW = 11, OrbH = 11;
    public const int BarW = 5, BarStripH = 8, LeftBarX = 10, RightBarX = 161;
    // title screen (class l)
    public const int TitleW = 176, TitleH = 144, NameMaxLen = 8;
}
