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
public sealed partial class Game : GameBase, IMikisGame
{
    public CutscenePlayer Cutscene { get; private set; }
    public GameStrings Strings { get; }

    // collision flags per tile id (see TileCollision): 22 = one-way platform, 1 = solid, 0 = free
    private readonly int[] _tileFlags = { 0, 0, 22, 22, 22, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22 };

    private short[] _finalBirdCells;                // cages of the final stage
    private int _birds, _finalDoor;
    private short[] _hitFxPos = new short[6];
    private int[] _hitFxStart = new int[3];
    private int _birdFxX = -1, _birdFxY = -1;       // bird released animation

    public Game(IAssets assets, IGameHost host, long seed) : base(assets, host, seed, GameLayout.One)
    {
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
        _totalCoins = 55;
        _collision = new TileCollision();

        Strings = GameStrings.Load(assets);
        Cutscene = new CutscenePlayer(assets, _host, _scaleX, _scaleY, Strings.Count - 16);

        // cheat code: 7 7 7 7 3 3 2 2 2 7 7 7 3 3 8
        _cheat = new[] { 55, 55, 55, 55, 51, 51, 50, 50, 50, 55, 55, 55, 51, 51, 56 };
        _typed = new int[_cheat.Length];
        _typedPos = 0;
    }

    protected override void LoadCutscene(string name) => Cutscene.Load(name);
    protected override int[] TileFlags => _tileFlags;
    protected override bool IsItemTile(int tile) => tile is Tile.Coin or Tile.CoinB or Tile.Key or Tile.CagedBird;
    protected override int GateMarkerTile => Tile.GateMarker;
    protected override void UpdateFx() => UpdateBirdFx();

    protected override void ScanOtherTile(int x, int y, int tile)
    {
        if (tile == Tile.CagedBird) _map.Set(x, y - 1, Tile.BirdSign);
    }
    public void Update(int dt, int totalMs) => Update(dt);
    public void ChooseModeIndex(int i) => ChooseMode(i);

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
            BeginIntro();
        }
    }

    public bool GameWon => _coins == _totalCoins || BonusUnlocked;
}
