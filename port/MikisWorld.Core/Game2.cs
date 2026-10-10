namespace MikisWorld.Core;

/// <summary>Layout of MikisWorld 2 (original class <c>f</c>): 128x128 screen, 12 px tiles.</summary>
public static class Layout2
{
    public const int ScreenW = 128, ScreenH = 128;
    public const int TileW = 12, TileH = 12;
    public const int ViewTilesW = 12, ViewTilesH = 12;
    public const int PlayerW = 15;
    public const int LordFrameW = 36, LordPoseW = 24;
    public const int DigitW = 8, TimeDigitW = 14;
    public const int CoinHudX = 2, CoinHudY = 115;
    public const int TimeHudX = 98, TimeHudY = 2;
    public const int LivesX = 111, LivesY = 114;
    public const int CellW = 20, CellH = 23, OrbW = 9, OrbH = 9;
    public const int BarW = 5, BarStripH = 8, LeftBarX = 4, RightBarX = 119;
    public const int UiStrings = 18;
}

/// <summary>Tile ids of MikisWorld 2's tiles.png (9x3 sheet of 12x12 tiles).</summary>
public static class Tile2
{
    public const int Sky = 0, Brick = 5, Coin = 6, Key = 7;
    public const int GateOpenTL = 8, GateOpenTR = 9, GateOpenBL = 10, GateOpenBR = 11;   // after the key
    public const int RainbowL = 12, Cloud = 13, PortalA = 15;
    public const int GateTL = 16, GateTR = 17, GateBL = 18, GateBR = 19;                  // closed gate
    public const int CoinB = 20, RainbowR = 21, PortalB = 22, Ledge = 23, Heart = 24;
}

/// <summary>
/// MikisWorld 2 ("The Storm Master"): second literal translation of the game class (original <c>c</c>), validated tick by tick
/// against traces of the original bytecode. Shares everything else with the first game (RNG, level reader, hall of fame, ...).
/// </summary>
public sealed partial class Game2 : GameBase, IMikisGame
{
    public CutscenePlayer2 Cutscene { get; private set; }
    public GameStrings Strings { get; }

    private int _riseSpeed;                          // aK
    private readonly int[] _tileFlags = { 0, 0, 22, 22, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22 };

    private bool _repaint;                           // the original only repaints after a logic step
    private int _rainbows, _finalRainbows;
    private int _markCooldown;                       // time since the intro ended (drives the "Mork is after you" hints)
    private int _morkAcc;                            // slowed Mork: movement ticks accumulator

    // Mork
    private int _morkState, _morkX, _morkY, _morkTimer, _morkFrame;
    private int _spark;                              // damage spark counter

    // floating rainbows and final-stage rainbow schedule
    private int[] _fxX, _fxY;
    private int[] _events;
    private int _eventIdx;

    public Game2(IAssets assets, IGameHost host, long seed) : base(assets, host, seed, GameLayout.Two)
    {
        _state = -1;
        _collision = new TileCollision2();
        _projPos = null; _projDir = null; _projState = null;   // the original creates them in the level loader (a bonus-first run sees them unset)
        Strings = GameStrings.Load(assets, "en", Layout2.UiStrings);
        Cutscene = new CutscenePlayer2(assets, _host, Strings.TextCount, SetTile);
        _hitHalfW = _playerW * 3 / 7;
        // cheat code (stored reversed in this game): typed 8 3 3 7 7 7 2 2 2 3 3 7 7 7 7
        _cheat = new[] { 56, 51, 51, 55, 55, 55, 50, 50, 50, 51, 51, 55, 55, 55, 55 };
        _typed = new int[_cheat.Length];
    }

    protected override void LoadCutscene(string name) => Cutscene.Load(name);
    protected override int[] TileFlags => _tileFlags;
    protected override int ColliderShrink => 1;
    protected override int GateMarkerTile => Tile2.GateTL;

    /// <summary>Mork hurts when the player's centre point is inside his box (centred on the lord sprite, half the pose width / half the height).</summary>
    protected override void DrawGameSpecificHitboxes(Canvas g)
    {
        if (_area == Area.SubArea || _state == State.Cutscene || _state == State.Ending) return;
        int halfW = Layout2.LordPoseW >> 1, halfH = _lordH >> 1;
        int mcx = (_morkX >> 4) + halfW, mcy = (_morkY >> 4) - halfH;
        Box(g, ColMork, mcx - halfW, mcy - halfH, 2 * halfW + 1, 2 * halfH + 1);
        Cross(g, ColLanding, (_px >> 4) + (_playerW >> 1), (_py >> 4) - (_playerH >> 1));
    }
    protected override void UpdateFx() => UpdateRainbowFx();

    protected override void ScanOtherTile(int x, int y, int tile)
    {
        if (tile == Tile2.RainbowL) PutTile(x + 1, y, Tile2.RainbowR);
    }

    private void SetTile(int x, int y, int tile) => PutTile(x, y, tile);

    protected override void OnInitBonus()
    {
        var items = _assets.ImageSize("items.png");   // original l(): b9 / da are (re)computed here
        _itemW = items.Width / 5;
        _itemH = items.Height;
    }

    /// <summary>Original start (c.long()).</summary>
    public void Start()
    {
        _state = -1;
        _quit = false;
        _running = true;
        _coins = 0;
        _markCooldown = 10000;
        if (BonusUnlocked)
        {
            _state = State.ModeMenu;
        }
        else
        {
            BeginIntro();
        }
    }

    public bool NeedsRepaint => _repaint;
}
