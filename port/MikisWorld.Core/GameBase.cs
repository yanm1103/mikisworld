namespace MikisWorld.Core;

/// <summary>
/// What the two games have in common (they were written from the same code base): the state both keep, input, the bonus minigame,
/// and the helpers of the update / draw code. <see cref="Game"/> (Miki's World 1) and <see cref="Game2"/> (Miki's World 2) add what
/// differs. The logic stays a literal translation of the original: this class only holds the parts whose translation is identical.
/// </summary>
public abstract partial class GameBase
{
    // ---------------------------------------------------------------- environment
    protected readonly IAssets _assets;
    protected readonly IGameHost _host;
    protected JavaRandom _rng;
    protected readonly GameLayout L;

    protected GameBase(IAssets assets, IGameHost host, long seed, GameLayout layout)
    {
        _assets = assets;
        _host = host ?? new NullHost();
        _rng = new JavaRandom(seed);
        L = layout;
        _tileW = L.TileW;
        _tileH = L.TileH;
        _playerW = L.PlayerW;
    }

    /// <summary>Optional departures from the original engine (all off by default).</summary>
    public EngineTweaks Tweaks { get; } = new();
    protected const int JumpBufferTicks = 5;

    // ---------------------------------------------------------------- what each game provides
    protected abstract void LoadLevel();
    protected abstract void LoadCutscene(string name);
    public abstract void EnterArea(int n);

    /// <summary>Start of the main game: load the level and play the intro cutscene.</summary>
    protected void BeginIntro()
    {
        LoadLevel();
        _skipPressed = false;
        LoadCutscene("begin.bin");
        EnterArea(-1);
    }

    /// <summary>Forget which keys are held (the frontend stops delivering key events, e.g. while a menu is open).</summary>
    public void ReleaseAll() { _held.Clear(); _jumpBuffer = 0; }

    // ---------------------------------------------------------------- scale / constants
    protected readonly int _tileW, _tileH;
    protected readonly int _playerW;
    protected int _scaleX, _scaleY;                 // file coordinates are multiplied by scale/16
    protected int _playerH;                         // player.png height
    protected int _hitHalfW;                        // half width of the player's hit box
    protected int _walkSpeed, _airSpeed, _fallSpeed;
    protected readonly int[] _jumpTable = { 67, 64, 58, 48, 36, 23, 7, -7, -23, -36, -48, -58, -64, -67 };
    protected int _itemW, _itemH;                   // one frame of items.png
    protected int _lordH;                           // lord.png height
    protected int _hitFxDx, _hitFxDy;

    // cheat code, typed on the phone keypad (stored as each game's class keeps it)
    protected int[] _cheat;
    protected int[] _typed;
    protected int _typedPos;

    // ---------------------------------------------------------------- level data (level.bin)
    protected TileLayer _map = new TileLayer();
    protected short _lord1Platform, _lord2Platform;
    protected int _lord1Release, _lord2Release;     // 3 platform indices, 10 bits each
    protected short _lord1X, _lord1Y, _lord2X, _lord2Y, _finalLordX, _finalLordY;
    protected short _startMainX, _startMainY, _startFinalX, _startFinalY;     // fixed point (<<4)
    protected short[] _subAreaX, _subAreaY;                                   // fixed point (<<4)
    protected short _mainBottomRow, _subBottomRow;
    protected short[] _subAreaRows;
    protected short[] _finalBlockCells;             // cells that get ledges in the final stage (one pair per freed bird / rainbow)
    protected short _totalCoins;
    protected ITileCollision _collision;            // each game has its own collision class
    protected short _gateX, _gateY;
    protected short[][] _platforms;                 // {x, y, widthInTiles, flags}
    protected short[] _platformX;                   // current x (moving platforms)
    protected short[] _portalCells;                 // map indices of portal tiles
    protected short[] _animTiles;                   // x, y, phase triplets of animated tiles (coins/portals)
    protected int _animFirst;

    // ---------------------------------------------------------------- run state
    protected readonly byte[] _save = { 0 };        // [0] = bonus game unlocked
    public bool BonusUnlocked { get => _save[0] == 1; set => _save[0] = (byte)(value ? 1 : 0); }
    protected int _state;                           // State.*
    protected int _returnState;                     // state to go back to after a wipe
    protected sbyte _area;                          // Area.*
    protected sbyte _nextArea;
    protected int _accum;                           // ms accumulator for the 20 ms logic step
    protected int _frame;                           // frame counter
    protected bool _quit;                           // wants to leave to the title screen
    protected bool _running;
    protected int _coins, _lives;
    protected int _stage;                           // lord stage (0 none, 1 first done, 2 second active, 3 done)
    protected int _stageStart;                      // frame when the lord event started
    protected bool _keyTaken;
    protected int _gameOverTimer;
    protected int _finalTimer;                      // ms, final-stage countdown
    protected int _hintTimer, _bonusMsgTimer;
    protected int _interact;                        // interactable under the player (-1 none, 65535 gate, else portal cell)
    protected bool _interactPressed;
    protected bool _skipPressed;
    protected int _lordAnim;

    // player
    protected int _px, _py;                         // fixed point (<<4): left x, feet y
    protected int _savedPx = -1, _savedPy = -1;
    protected int _motion;                          // 0 idle, 1/2 walk L/R, 3/5 jump diag L/R, 4 jump up, 6 falling
    protected int _jumpStep, _sprite;
    protected bool _airControl;
    protected int _inputX, _inputJump;              // (-2/0/2), (-1/0)
    protected readonly HeldDirections _held = new();   // revamped movement: directions really held
    protected int _jumpBuffer, _jumpSub, _lastCase, _airDir;     // revamped movement: ticks a fresh jump press stays valid; slow-jump sub-step
    protected int _standingOn = -1;                 // platform index

    // camera
    protected int _camX, _camY, _camMaxX, _camMinY, _camMaxY;
    protected bool _scrollLockX, _scrollLockY;
    protected int _outerX, _outerY, _innerX, _innerY;   // scroll triggers (outer / inner)
    protected readonly int _scrollStepX = 1, _scrollStepY = 2;

    // lord actors
    protected short _lordX, _lordY;
    protected int _lordFrame;

    // projectiles (3)
    protected short[] _projPos = new short[6];
    protected sbyte[] _projDir = new sbyte[3];
    protected sbyte[] _projState = new sbyte[3];    // -1 none, 0 falling, 1 bouncing sideways
    protected int _nextProj;

    // visible platform window
    protected int _visFirst, _visCount;

    // ---------------------------------------------------------------- read-only state (used by the frontend and the tests)
    public void Reseed(long seed) { _rng = new JavaRandom(seed); }
    public bool IsRunning => _running;
    public bool WantsQuit => _quit;
    public int CurrentState => _state;
    public int CurrentArea => _area;
    public int Coins => _coins;
    public int TotalCoins => _totalCoins;
    public int Lives => _lives;
    public int Frame => _frame;
    public int ScreenWidth => L.ScreenW;
    public int ScreenHeight => L.ScreenH;
    public bool IsBonusMode => _state >= State.ModeMenu && _state <= State.BonusAiMoving;

    /// <summary>Score value for the hall of fame (original: coins collected).</summary>
    public int Score => _coins;
}
