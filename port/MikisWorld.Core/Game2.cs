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
public sealed partial class Game2 : IMikisGame
{
    private readonly IAssets _assets;
    private readonly IGameHost _host;
    private JavaRandom _rng;
    public CutscenePlayer2 Cutscene { get; private set; }
    public GameStrings Strings { get; }

    /// <summary>Optional departures from the original engine (all off by default).</summary>
    public EngineTweaks Tweaks { get; } = new();
    private const int JumpBufferTicks = 5;

    /// <summary>Forget which keys are held (the frontend stops delivering key events, e.g. while a menu is open).</summary>
    public void ReleaseAll() { _held.Clear(); _jumpBuffer = 0; }

    // ---------------------------------------------------------------- scale / constants
    private readonly int _tileW = Layout2.TileW, _tileH = Layout2.TileH;
    private int _scaleX, _scaleY;                    // cy, cx: 16 * tile / 8 (set on every level load)
    private readonly int _playerW = Layout2.PlayerW;
    private int _playerH;                            // player.png height
    private readonly int _hitHalfW;
    private int _walkSpeed, _airSpeed, _fallSpeed, _riseSpeed;   // bi, cE, bW, aK
    private readonly int[] _jumpTable = { 67, 64, 58, 48, 36, 23, 7, -7, -23, -36, -48, -58, -64, -67 };
    private int _itemW, _itemH, _lordH;
    private int _hitFxDx, _hitFxDy;

    private readonly int[] _tileFlags = { 0, 0, 22, 22, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22 };

    // cheat code (stored reversed in this game): typed 8 3 3 7 7 7 2 2 2 3 3 7 7 7 7
    private readonly int[] _cheat = { 56, 51, 51, 55, 55, 55, 50, 50, 50, 51, 51, 55, 55, 55, 55 };
    private readonly int[] _typed;
    private int _typedPos;

    // ---------------------------------------------------------------- level data
    private readonly TileLayer _map = new TileLayer();
    private readonly TileCollision2 _collision = new TileCollision2();
    private short _lord1Platform, _lord2Platform;
    private int _lord1Release, _lord2Release;
    private short _lord1X, _lord1Y, _lord2X, _lord2Y, _finalLordX, _finalLordY;
    private short _startMainX, _startMainY, _startFinalX, _startFinalY;
    private short[] _subAreaX, _subAreaY;
    private short _mainBottomRow, _subBottomRow;
    private short[] _subAreaRows;
    private short[] _finalBlockCells;
    private short _totalCoins;
    private short _gateX, _gateY;
    private short[][] _platforms;
    private short[] _platformX;
    private short[] _portalCells;
    private short[] _animTiles;
    private int _animFirst;
    private int _visFirst, _visCount;

    // ---------------------------------------------------------------- run state
    private readonly byte[] _save = { 0 };
    public bool BonusUnlocked { get => _save[0] == 1; set => _save[0] = (byte)(value ? 1 : 0); }
    private int _state = -1;
    private int _returnState;
    private sbyte _area;
    private sbyte _nextArea;
    private int _accum;
    private int _frame;
    private bool _quit, _running;
    private bool _repaint;                           // b1: the original only repaints after a logic step
    private int _coins, _lives, _rainbows, _finalRainbows;
    private int _stage, _stageStart, _lordAnim;
    private bool _keyTaken;
    private int _gameOverTimer;
    private int _finalTimer;
    private int _hintTimer, _bonusMsgTimer;
    private int _interact;
    private bool _interactPressed, _skipPressed;
    private int _markCooldown;                       // cL: time since the intro ended (drives the "Mork is after you" hints)

    // player
    private int _px, _py;
    private int _savedPx = -1, _savedPy = -1;
    private int _motion, _jumpStep, _sprite;
    private bool _airControl;
    private int _inputX, _inputJump;
    private readonly HeldDirections _held = new();   // revamped movement: directions really held
    private int _jumpBuffer, _jumpSub, _lastCase, _airDir, _morkAcc;               // revamped movement: ticks a fresh jump press stays valid; slow-jump sub-step
    private int _standingOn = -1;

    // camera
    private int _camX, _camY, _camMaxX, _camMinY, _camMaxY;
    private bool _scrollLockX, _scrollLockY;
    private int _outerX, _outerY, _innerX, _innerY;
    private readonly int _scrollStepX = 1, _scrollStepY = 2;

    // lords
    private short _lordX, _lordY;
    private int _lordFrame;

    // Mork
    private int _morkState, _morkX, _morkY, _morkTimer, _morkFrame;
    private int _spark;                              // aE: damage spark counter

    // floating rainbows and final-stage rainbow schedule
    private int[] _fxX, _fxY;
    private int[] _events;
    private int _eventIdx;

    // projectiles
    private short[] _projPos;
    private sbyte[] _projDir, _projState;
    private int _nextProj;

    public Game2(IAssets assets, IGameHost host, long seed)
    {
        _assets = assets;
        _host = host ?? new NullHost();
        _rng = new JavaRandom(seed);
        Strings = GameStrings.Load(assets, "en", Layout2.UiStrings);
        Cutscene = new CutscenePlayer2(assets, _host, Strings.TextCount, SetTile);
        _hitHalfW = _playerW * 3 / 7;
        _typed = new int[_cheat.Length];
    }

    private void SetTile(int x, int y, int tile) => PutTile(x, y, tile);

    public void Reseed(long seed) { _rng = new JavaRandom(seed); }

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
            LoadLevel();
            _skipPressed = false;
            Cutscene.Load("begin.bin");
            EnterArea(-1);
        }
    }

    public bool IsRunning => _running;
    public bool WantsQuit => _quit;
    public int CurrentState => _state;
    public int CurrentArea => _area;
    public int Coins => _coins;
    public int TotalCoins => _totalCoins;
    public int Lives => _lives;
    public int Frame => _frame;
    public int Score => _coins;
    public bool NeedsRepaint => _repaint;
    public int ScreenWidth => Layout2.ScreenW;
    public int ScreenHeight => Layout2.ScreenH;

    // ---------------------------------------------------------------- input
    public void KeyPressed(int key, int action)
    {
        if (_state == State.Cutscene)
        {
            if (key == 53 || action == 8) _skipPressed = true;
            return;
        }
        if (_state == State.Ending && (key == 53 || action == 8))
        {
            if (_gameOverTimer < 7000) _gameOverTimer = 7000;
            else { _running = false; _quit = true; }
            return;
        }
        _typed[_typedPos] = key;
        int p = _typedPos, k = _cheat.Length - 1;
        while (k >= 0 && _typed[p] == _cheat[k])
        {
            p = (p + _typed.Length - 1) % _typed.Length;
            k--;
        }
        _typedPos++;
        _typedPos %= _typed.Length;
        if (k == -1)
        {
            _save[0] = 1;
            _bonusMsgTimer = 2000;
        }
        if (_state >= State.ModeMenu && _state <= State.BonusAiMoving) BonusKey(key, action);

        _held.Press(key, action);
        if (key == 51 || key == 49 || key == 50 || action == 1) _jumpBuffer = JumpBufferTicks;
        if (key == 51) { _inputJump = -1; _inputX = 2; }
        else if (key == 54 || action == 5) _inputX = 2;
        else if (key == 49) { _inputJump = -1; _inputX = -2; }
        else if (key == 52 || action == 2) _inputX = -2;
        else if (key == 50 || action == 1) _inputJump = -1;
        else if ((key == 56 || action == 6) && _interact != -1 && !(Tweaks.RevampedMovement && (_state == State.WipeIn || _state == State.WipeOut))) _interactPressed = true;
    }

    public void KeyReleased(int key, int action)
    {
        _held.Release(key);
        _inputX = 0;
        _inputJump = 0;
    }
}
