namespace MikisWorld.Core;

/// <summary>
/// Bonus minigame: a 5x4 "chain reaction" board. Cell byte: bits 0-2 = orb count, bit 3 = owner (0 player, 1 CPU).
/// A cell explodes when its count equals its neighbour count (corner 2, edge 3, inside 4), pushing one orb
/// into each neighbour (taking ownership); whoever loses all orbs loses.
/// </summary>
public sealed partial class Game2
{
    public const int BoardW = 5, BoardH = 4;

    private bool _bonusInitialised;                 // Board is null in the original until the minigame starts
    public byte[] Board = new byte[20];             // cB
    private byte[] _simBoard = new byte[20];        // bC (AI scratch board; contents persist between turns, as in the original)
    private byte[] _queue = new byte[40];           // R: circular queue of cells to test for explosion
    private int _queueHead, _queueTail;             // ad, u
    public int Turn;                                // t: 0 player, 1 CPU
    public int CursorX, CursorY;                    // cJ, cH
    public bool CursorValid = true;                 // cq
    private int _pickX = -1, _pickY = -1;           // aU, aT: cell chosen by the player this tick
    public int AnimProgress;                        // aV: 0..10 explosion animation
    private int _animStart;                         // p
    public int ExplodingCell;                       // bw
    public int PlayerOrbs, CpuOrbs;                 // O, N
    public int Winner;                              // aC: 0 none, 1 player, 2 CPU
    private int _cpuX, _cpuY;                       // cf, ce
    public int BarLeft, BarLeftTarget, BarRight, BarRightTarget;   // c6, bg, cp, al
    private int _pendingMode;                       // ci: 1 = main game, 2 = bonus, chosen in the mode menu
    private int[] _preferred;                       // bB: fallback cells for the AI (corners, then edges)

    /// <summary>Called by the UI when the player picks an entry of the "Select game" list (0 = main game, 1 = bonus game).</summary>
    public void ChooseMode(int index) { _pendingMode = index == 0 ? 1 : 2; }

    // Original d.i(): set up a fresh bonus game
    private void InitBonus()
    {
        _pendingMode = 0;
        _bonusInitialised = true;
        Turn = 0;
        CursorX = 0; CursorY = 0;
        CursorValid = true;
        _pickX = -1; _pickY = -1;
        _lordY = 0;
        _py = 0;
        _gameOverTimer = 0;
        _sprite = 3;
        Board = new byte[20];
        _simBoard = new byte[20];
        _queue = new byte[40];
        _queueHead = 0; _queueTail = 0;
        BarLeft = BarLeftTarget = BarRight = BarRightTarget = Layout2.ScreenH / 2;
        Winner = 0;
        _preferred = new int[28];
        _preferred[0] = 0; _preferred[1] = 0;
        _preferred[2] = 4; _preferred[3] = 0;
        _preferred[4] = 0; _preferred[5] = 3;
        _preferred[6] = 4; _preferred[7] = 3;
        int n3 = 8;
        for (int n4 = 1; n4 < 3; n4++)
        {
            _preferred[n3 + n4 * 4] = n4;
            _preferred[n3 + n4 * 4 + 1] = 0;
            _preferred[n3 + n4 * 4 + 2] = n4;
            _preferred[n3 + n4 * 4 + 3] = 3;
            n3 += 4;
        }
        for (int n5 = 1; n5 < 2; n5++)
        {
            _preferred[n3 + n5 * 4] = 0;
            _preferred[n3 + n5 * 4 + 1] = n5;
            _preferred[n3 + n5 * 4 + 2] = 4;
            _preferred[n3 + n5 * 4 + 3] = n5;
            n3 += 4;
        }
    }

    // Original d.int(int,int): cursor movement / placing an orb
    private void BonusKey(int key, int action)
    {
        if (_state != State.BonusPlayerTurn) return;
        if (Winner != 0) return;
        if ((key == 50 || action == 1 || key == 49 || key == 51) && CursorY > 0) --CursorY;
        if ((key == 56 || action == 6 || key == 55 || key == 57) && CursorY < 3) ++CursorY;
        if ((key == 52 || action == 2 || key == 49 || key == 55) && CursorX > 0) --CursorX;
        if ((key == 54 || action == 5 || key == 51 || key == 57) && CursorX < 4) ++CursorX;
        if (key != 53 && action != 8) return;
        UpdateCursorValid();
        if (!CursorValid) return;
        _pickX = CursorX;
        _pickY = CursorY;
    }

    // Original d.m(): can the player put an orb on the cursor cell?
    private void UpdateCursorValid()
    {
        int n = CursorX + CursorY * 5;
        CursorValid = Board[n] == 0 || Turn == 0 && (Board[n] & 8) == 0 || Turn == 1 && (Board[n] & 8) != 0;
    }

    // Original d.char(int,int): logic step of states 21-25
    private void BonusTick(int dtMs)
    {
        if (_pendingMode == 1)
        {
            LoadLevel();
            _skipPressed = false;
            Cutscene.Load("begin.bin");
            EnterArea(-1);
            _pendingMode = 0;
        }
        else if (_pendingMode == 2)
        {
            InitBonus();
            _state = State.BonusPlayerTurn;
            _pendingMode = 0;
        }
        else if (_state == State.ModeMenu)
        {
            // the UI shows the "Main game / Bonus game" list; nothing to simulate
        }
        else
        {
            _frame++;
            if (_frame % 2 == 0) AnimateBars();
            if (Winner != 0)
            {
                _gameOverTimer += dtMs;
                if (_gameOverTimer > 5000) { _running = false; _quit = true; }
            }
            switch (_state)
            {
                case State.BonusPlayerTurn:
                    UpdateCursorValid();
                    if (_pickX == -1) break;
                    AddOrb(Board, _pickX, _pickY, Turn);
                    if (TryExplode(Board, _pickX, _pickY, Turn))
                    {
                        ExplodingCell = _pickX + _pickY * 5;
                        AnimProgress = 0;
                        _animStart = _frame;
                        _state = State.BonusAnimating;
                    }
                    else EndTurn();
                    _pickX = -1;
                    break;
                case State.BonusAnimating:
                    {
                        int step = _frame - _animStart;
                        if (step <= 0 || step >= 10) step = 10;
                        AnimProgress = step;
                        if (step != 10) break;
                        int cx = ExplodingCell % 5, cy = ExplodingCell / 5;
                        SpreadFrom(Board, cx, cy);
                        int next = ProcessQueue(Board);
                        UpdateBars();
                        if (Winner == 0)
                        {
                            CountOrbs(Board);
                            if (PlayerOrbs == 0 || CpuOrbs == 0)
                            {
                                if (PlayerOrbs == 0)
                                {
                                    BarLeftTarget += 20;
                                    Winner = 2;
                                    _host.Play(Sfx.Lord);
                                }
                                else
                                {
                                    BarRightTarget += 20;
                                    Winner = 1;
                                    _host.Play(Sfx.GameEnd);
                                }
                            }
                        }
                        if (next == -1) { EndTurn(); break; }
                        ExplodingCell = next;
                        AnimProgress = 0;
                        _animStart = _frame;
                        break;
                    }
                case State.BonusAiThinking:
                    if (Winner != 0) break;
                    AiThink();
                    _animStart = 0;
                    _state = State.BonusAiMoving;
                    break;
                case State.BonusAiMoving:
                    _lordFrame = 1;
                    ++_animStart;
                    if (_animStart != 20) break;
                    _lordFrame = 0;
                    AddOrb(Board, _cpuX, _cpuY, Turn);
                    if (TryExplode(Board, _cpuX, _cpuY, Turn))
                    {
                        ExplodingCell = _cpuX + _cpuY * 5;
                        AnimProgress = 0;
                        _animStart = _frame;
                        _state = State.BonusAnimating;
                        break;
                    }
                    EndTurn();
                    break;
            }
        }
    }

    // Original d.a(byte[],int,int): the four neighbours of an exploding cell receive an orb
    private void SpreadFrom(byte[] board, int x, int y)
    {
        if (x > 0) AddOrb(board, x - 1, y, Turn);
        if (x < 4) AddOrb(board, x + 1, y, Turn);
        if (y > 0) AddOrb(board, x, y - 1, Turn);
        if (y < 3) AddOrb(board, x, y + 1, Turn);
    }

    // Original d.do(byte[]): pop queued cells until one explodes; returns it (or -1)
    private int ProcessQueue(byte[] board)
    {
        int cell = 0;
        bool exploded = false;
        while (!exploded && _queueHead != _queueTail)
        {
            cell = _queue[_queueTail];
            int x = cell % 5, y = cell / 5;
            exploded = TryExplode(board, x, y, Turn);
            ++_queueTail;
            _queueTail %= _queue.Length;
        }
        return exploded ? cell : -1;
    }

    // Original d.h(): bars glide toward their targets (2 px per call, 1 px when close)
    private void AnimateBars()
    {
        if (BarLeft < BarLeftTarget)
        {
            ++BarLeft;
            if (BarLeftTarget - BarLeft > 10) ++BarLeft;
        }
        else if (BarLeft > BarLeftTarget)
        {
            --BarLeft;
            if (BarLeft - BarLeftTarget > 10) --BarLeft;
        }
        if (BarRight < BarRightTarget)
        {
            ++BarRight;
            if (BarRightTarget - BarRight > 10) ++BarRight;
        }
        else if (BarRight > BarRightTarget)
        {
            --BarRight;
            if (BarRight - BarRightTarget > 10) --BarRight;
        }
    }

    // Original d.f(): bar targets reflect the orb ratio
    private void UpdateBars()
    {
        CountOrbs(Board);
        int total = PlayerOrbs + CpuOrbs;
        if (total != 0)
        {
            BarLeftTarget = Layout2.ScreenH - Layout2.ScreenH * 70 * PlayerOrbs / total / 100;
            BarRightTarget = Layout2.ScreenH - Layout2.ScreenH * 70 * CpuOrbs / total / 100;
            if (Winner == 2) BarLeftTarget += 30;
            else if (Winner == 1) BarRightTarget += 30;
        }
    }

    // Original d.g()
    private void EndTurn()
    {
        ++Turn;
        Turn %= 2;
        _state = Turn == 1 ? State.BonusAiThinking : State.BonusPlayerTurn;
    }

    // Original d.if(byte[],int,int,int): explode the cell if it reached critical mass; queue its neighbours
    private bool TryExplode(byte[] board, int x, int y, int turn)
    {
        int critical = 4;
        if (x == 0 || x + 1 == 5) --critical;
        if (y == 0 || y + 1 == 4) --critical;
        int cell = x + y * 5;
        if ((board[cell] & 7) == critical)
        {
            board[cell] = 0;
            if (x > 0) Enqueue(x - 1 + y * 5);
            if (x < 4) Enqueue(x + 1 + y * 5);
            if (y > 0) Enqueue(x + (y - 1) * 5);
            if (y < 3) Enqueue(x + (y + 1) * 5);
            return true;
        }
        return false;
    }

    private void Enqueue(int cell)
    {
        _queue[_queueHead] = (byte)cell;
        ++_queueHead;
        _queueHead %= _queue.Length;
    }

    // Original d.a(byte[],int,int,int): add an orb and set the owner
    private void AddOrb(byte[] board, int x, int y, int turn)
    {
        int cell = x + y * 5;
        int critical = 4;
        if (x == 0 || x == 4) --critical;
        if (y == 0 || y == 3) --critical;
        if ((board[cell] & 7) < critical) board[cell] = (byte)(board[cell] + 1);
        if (turn == 0) board[cell] = (byte)(board[cell] & 7);
        else if (turn == 1) board[cell] = (byte)(board[cell] | 8);
    }

    // Original d.a(byte[]): count orbs per owner into PlayerOrbs / CpuOrbs
    private void CountOrbs(byte[] board)
    {
        PlayerOrbs = 0;
        CpuOrbs = 0;
        int i = 0;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 5; x++, i++)
            {
                if ((board[i] & 8) == 0) PlayerOrbs += board[i];
                else CpuOrbs += board[i] & 7;
            }
    }

    // Original d.null(): the CPU simulates every legal move, ranks them by resulting CPU orb count and picks
    // the best one 60% of the time (otherwise one of the top three at random).
    private void AiThink()
    {
        int roll = _rng.NextInt();            // drawn and ignored in the original (keeps the RNG sequence identical)
        _ = roll;
        CountOrbs(_simBoard);                 // sic: counts the scratch board left from the previous turn
        int before = CpuOrbs;
        int[] bestScore = { 0, 0, 0 };
        int[] bestX = { 0, 0, 0 };
        int[] bestY = { 0, 0, 0 };
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                Array.Copy(Board, 0, _simBoard, 0, Board.Length);
                int cell = x + y * 5;
                bool legal = _simBoard[cell] == 0 || (_simBoard[cell] & 8) != 0;
                if (!legal) continue;
                _simBoard[cell] = (byte)(_simBoard[cell] + 1);
                _simBoard[cell] = (byte)(_simBoard[cell] | 8);
                TryExplode(_simBoard, x, y, Turn);
                int steps = 0;
                while (_queueHead != _queueTail)
                {
                    int cx = cell % 5, cy = cell / 5;
                    SpreadFrom(_simBoard, cx, cy);
                    cell = ProcessQueue(_simBoard);
                    if (++steps <= 5) continue;
                    steps = 0;
                    CountOrbs(_simBoard);
                    if (PlayerOrbs == 0 || CpuOrbs == 0) break;
                }
                CountOrbs(_simBoard);
                if (bestScore[0] < CpuOrbs)
                {
                    int k = 0;
                    while (k < bestScore.Length && CpuOrbs > bestScore[k]) ++k;
                    int slot = --k;
                    while (k > 0)
                    {
                        bestScore[k - 1] = bestScore[k];
                        bestX[k - 1] = bestX[k];
                        bestY[k - 1] = bestY[k];
                        --k;
                    }
                    bestScore[slot] = CpuOrbs;
                    bestX[slot] = x;
                    bestY[slot] = y;
                }
            }
        }
        int candidates = bestScore.Length;
        if (bestScore[bestScore.Length - 1] == before + 1)
        {
            // no move gains anything: fall back to the first empty preferred cells
            int found = 0;
            for (int i = 0; i < _preferred.Length; i += 2)
            {
                int cellIdx = _preferred[i] + _preferred[i + 1] * 5;
                bool empty = Board[cellIdx] == 0;
                if (empty)
                {
                    bestX[found] = _preferred[i];
                    bestY[found] = _preferred[i + 1];
                    if (++found == bestScore.Length) break;
                }
            }
            if (found != 0) candidates = found;
        }
        int pick = bestX.Length - 1;
        int r = _rng.NextInt() % 100;
        if (r < 40 && r > -40 && (pick = _rng.NextInt() % candidates) < 0) pick = -pick;
        _cpuX = bestX[pick];
        _cpuY = bestY[pick];
    }
}
