namespace MikisWorld.Core;

/// <summary>Game states (original field <c>d.cK</c>).</summary>
public static class State
{
    public const int Playing = 0;
    public const int GameOver = 3;
    public const int Lord1 = 4;          // first Lord encounter
    public const int Lord2 = 5;          // second Lord encounter
    public const int FinalStage = 6;
    public const int Unknown7 = 7;
    public const int WipeOut = 10;       // screen wipe, revealing the new area
    public const int WipeIn = 11;        // screen wipe, covering the old area
    public const int Ending = 12;
    public const int Cutscene = 20;
    public const int ModeMenu = 21;      // "Main game / Bonus game" list
    public const int BonusPlayerTurn = 22;
    public const int BonusAnimating = 23;
    public const int BonusAiThinking = 24;
    public const int BonusAiMoving = 25;
}

/// <summary>Area the player is in (original field <c>d.c2</c>).</summary>
public static class Area
{
    public const int OutroCutscene = -2, IntroCutscene = -1, Main = 0, SubArea = 1, Final = 2;
}

/// <summary>Tile ids used in the map / tile sheet (tiles.png is a 9x3 sheet of 14x14 tiles).</summary>
public static class Tile
{
    public const int Coin = 6, Key = 7, GateMarker = 8, CagedBird = 12, Cloud = 13, PortalA = 15;
    public const int GateTL = 16, GateTR = 17, GateBL = 18, GateBR = 19, CoinB = 20, BirdSign = 21, PortalB = 22, Ledge = 23, Heart = 24;
}

/// <summary>
/// The platformer + bonus minigame logic (original class <c>d</c>, minus rendering/UI).
/// Deliberately a literal translation: same integer arithmetic, same evaluation order, same use of the RNG,
/// so it can be diffed tick-by-tick against traces taken from the original bytecode.
/// </summary>
public sealed partial class Game : IMikisGame
{
    // ---------------------------------------------------------------- environment
    private readonly IAssets _assets;
    private readonly IGameHost _host;
    private JavaRandom _rng;
    public CutscenePlayer Cutscene { get; private set; }

    // ---------------------------------------------------------------- scale / constants
    private readonly int _tileW = Layout.TileW, _tileH = Layout.TileH;
    private readonly int _scaleX, _scaleY;          // cl, ck: file coordinates are multiplied by scale/16
    private readonly int _playerW = Layout.PlayerW;
    private readonly int _playerH;                  // player.png height
    private readonly int _hitHalfW;                 // half width of the player's hit box
    private readonly int _walkSpeed, _airSpeed, _fallSpeed;   // be/cr/bO
    private readonly int[] _jumpTable = { 67, 64, 58, 48, 36, 23, 7, -7, -23, -36, -48, -58, -64, -67 };
    private readonly int _itemW, _itemH;            // one frame of items.png
    private readonly int _lordH;                    // lord.png height
    private readonly int _hitFxDx, _hitFxDy;

    // collision flags per tile id (see TileCollision): 22 = one-way platform, 1 = solid, 0 = free
    private readonly int[] _tileFlags = { 0, 0, 22, 22, 22, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22 };

    // cheat code: 7 7 7 7 3 3 2 2 2 7 7 7 3 3 8
    private readonly int[] _cheat = { 55, 55, 55, 55, 51, 51, 50, 50, 50, 55, 55, 55, 51, 51, 56 };
    private readonly int[] _typed;
    private int _typedPos;

    // ---------------------------------------------------------------- level data (level.bin)
    private TileLayer _map = new TileLayer();
    private readonly TileCollision _collision = new TileCollision();
    private short _lord1Platform, _lord2Platform;
    private int _lord1Release, _lord2Release;       // 3 platform indices, 10 bits each
    private short _lord1X, _lord1Y, _lord2X, _lord2Y, _finalLordX, _finalLordY;
    private short _startMainX, _startMainY, _startFinalX, _startFinalY;     // fixed point (<<4)
    private short[] _subAreaX, _subAreaY;                                   // fixed point (<<4)
    private short _mainBottomRow, _subBottomRow;
    private short[] _subAreaRows;
    private short[] _finalBlockCells;               // cells that get ledges in the final stage (one pair per freed bird)
    private short[] _finalBirdCells;                // cages of the final stage
    private short _totalCoins = 55;
    private short _gateX, _gateY;
    private short[][] _platforms;                   // {x, y, widthInTiles, flags}
    private short[] _platformX;                     // current x (moving platforms)
    private short[] _portalCells;                   // map indices of portal tiles
    private short[] _animTiles;                     // x, y, phase triplets of animated tiles (coins/portals)
    private int _animFirst;

    // ---------------------------------------------------------------- run state
    private readonly byte[] _save = { 0 };          // [0] = bonus game unlocked
    public bool BonusUnlocked { get => _save[0] == 1; set => _save[0] = (byte)(value ? 1 : 0); }
    private int _state;                              // State.*
    private int _returnState;                        // state to go back to after a wipe
    private sbyte _area;                             // Area.*
    private sbyte _nextArea;
    private int _accum;                              // ms accumulator for the 20 ms logic step
    private int _frame;                              // frame counter b1
    private bool _quit;                              // wants to leave to the title screen
    private bool _running;
    private int _coins, _lives, _birds, _finalDoor;
    private int _stage;                              // lord stage cO (0 none, 1 first done, 2 second active, 3 done)
    private int _stageStart;                         // frame when the lord event started
    private bool _keyTaken;
    private int _gameOverTimer;                      // bU
    private int _finalTimer;                 // cQ (ms), final-stage countdown
    private int _hintTimer, _bonusMsgTimer;          // bf, bn
    private int _interact;                      // W: interactable under the player (-1 none, 65535 gate, else portal cell)
    private bool _interactPressed;                   // s
    private bool _skipPressed;                       // cS
    private int _lordAnim;                           // bQ

    // player
    private int _px, _py;                            // fixed point (<<4): left x, feet y
    private int _savedPx = -1, _savedPy = -1;
    private int _motion;                             // n: 0 idle, 1/2 walk L/R, 3/5 jump diag L/R, 4 jump up, 6 falling
    private int _jumpStep, _sprite;                  // bp, cv
    private bool _airControl;                        // cn
    private int _inputX, _inputJump;                 // bS (-2/0/2), bR (-1/0)
    private int _standingOn = -1;                    // aQ platform index

    // camera
    private int _camX, _camY, _camMaxX, _camMinY, _camMaxY;
    private bool _scrollLockX, _scrollLockY;
    private int _outerX, _outerY, _innerX, _innerY;   // scroll triggers: bV, bN (outer), cd, bY (inner)
    private readonly int _scrollStepX = 1, _scrollStepY = 2;

    // lord actors
    private short _lordX, _lordY;                    // ao, an
    private int _lordFrame;                          // T

    // projectiles (3) and hit effects
    private short[] _projPos = new short[6];
    private sbyte[] _projDir = new sbyte[3];
    private sbyte[] _projState = new sbyte[3];       // -1 none, 0 falling, 1 bouncing sideways
    private short[] _hitFxPos = new short[6];
    private int[] _hitFxStart = new int[3];
    private int _nextProj;

    // bird released animation
    private int _birdFxX = -1, _birdFxY = -1;

    // visible platform window
    private int _visFirst, _visCount;

    public Game(IAssets assets, IGameHost host, long seed)
    {
        _assets = assets;
        _host = host ?? new NullHost();
        _rng = new JavaRandom(seed);

        var player = assets.ImageSize("player.png");
        var items = assets.ImageSize("items.png");
        var damage = assets.ImageSize("damage.png");
        var lord = assets.ImageSize("lord.png");
        _playerH = player.Height;
        _itemW = items.Width / 5; _itemH = items.Height;
        _lordH = lord.Height;
        _hitFxDx = (player.Width / 8 - damage.Width / 2) / 2;
        _hitFxDy = (player.Height / 8 - damage.Height / 2) / 2;
        _hitHalfW = _playerW * 3 / 7;

        _scaleX = 16 * _tileW / 8;
        _scaleY = 16 * _tileH / 8;
        for (int i = 0; i < _jumpTable.Length; i++) _jumpTable[i] = _jumpTable[i] * _scaleY >> 4;
        _airSpeed = 1 * _scaleX * 90 / 100;
        _walkSpeed = 2 * _scaleX * 90 / 100;
        _fallSpeed = 2 * _scaleY;

        Strings = GameStrings.Load(assets);
        Cutscene = new CutscenePlayer(assets, _host, _scaleX, _scaleY, Strings.Count - 16);

        _typed = new int[_cheat.Length];
        _typedPos = 0;
    }

    public GameStrings Strings { get; }

    // ---- IMikisGame
    public int ScreenWidth => Layout.ScreenW;
    public int ScreenHeight => Layout.ScreenH;
    public void Update(int dt, int totalMs) => Update(dt);
    public void ChooseModeIndex(int i) => ChooseMode(i);

    public void Reseed(long seed) { _rng = new JavaRandom(seed); }

    // ---------------------------------------------------------------- lifecycle
    /// <summary>Original goto(): start (or restart) the game.</summary>
    public void Start()
    {
        _quit = false;
        _running = true;
        _coins = 0;
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

    // ---------------------------------------------------------------- input
    /// <summary>Original a(int key, int action). <paramref name="key"/> is a MIDP key code (49='1'…, 53='5'), <paramref name="action"/> a game action (UP=1, LEFT=2, RIGHT=5, DOWN=6, FIRE=8).</summary>
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

        if (key == 51) { _inputJump = -1; _inputX = 2; }
        else if (key == 54 || action == 5) _inputX = 2;
        else if (key == 49) { _inputJump = -1; _inputX = -2; }
        else if (key == 52 || action == 2) _inputX = -2;
        else if (key == 50 || action == 1) _inputJump = -1;
        else if ((key == 56 || action == 6) && _interact != -1) _interactPressed = true;
    }

    /// <summary>Original cfr_renamed_3(int,int): any key release stops all input.</summary>
    public void KeyReleased(int key, int action)
    {
        _inputX = 0;
        _inputJump = 0;
    }

    // ---------------------------------------------------------------- read-only state (used by renderer and tests)
    public int CurrentState => _state;
    public int CurrentArea => _area;
    public int Coins => _coins;
    public int TotalCoins => _totalCoins;
    public int Lives => _lives;
    public int Frame => _frame;
    public bool IsBonusMode => _state >= State.ModeMenu && _state <= State.BonusAiMoving;

    /// <summary>Score value for the hall of fame (original: coins collected).</summary>
    public int Score => _coins;
    public bool GameWon => _coins == _totalCoins || BonusUnlocked;
}
