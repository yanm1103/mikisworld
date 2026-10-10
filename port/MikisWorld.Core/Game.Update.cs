namespace MikisWorld.Core;

public sealed partial class Game
{
    // ------------------------------------------------------------------ area changes (original d.for(int))
    /// <summary>Switches area: -2 outro cutscene, -1 intro cutscene, 0 main, 1 final stage, 2+ sub-areas via portals.</summary>
    public override void EnterArea(int n)
    {
        bool placeCamera = true;
        if (n < 0)
        {
            placeCamera = false;
            _camMinY = 0;
            _camMaxY = _map.Height * _tileH - Layout.ScreenH - 1;
            _area = (sbyte)n;
            _state = State.Cutscene;
            if (n == -1) _host.Play(Sfx.OnceUponATime);
            else if (n == -2) _host.Play(Sfx.GameEnd);
        }
        else if (n == 0)
        {
            _camMinY = 0;
            _camMaxY = _mainBottomRow * _tileH - Layout.ScreenH - 1;
            _px = _startMainX;
            _py = _startMainY;
            if (_savedPx != -1)
            {
                _px = _savedPx;
                _py = _savedPy;
                _savedPx = -1;
                _savedPy = -1;
            }
            _area = 0;
        }
        else if (n == 1)
        {
            _camMinY = _mainBottomRow * _tileH;
            _camMaxY = _subBottomRow * _tileH - Layout.ScreenH - 1;
            _px = _startFinalX;
            _py = _startFinalY;
            _finalTimer = 67000;
            _lordX = _finalLordX;
            _lordY = _finalLordY;
            int[] tiles = _map.Tiles;
            int w = _map.Width;
            for (int i = 0; i < _birds && i < _finalBlockCells.Length; i++)
            {
                int c = _finalBlockCells[i];
                tiles[c] = Tile.Ledge;
                tiles[c + 1] = Tile.Ledge;
            }
            for (int i = 1; i < _finalBirdCells.Length; i++)
            {
                int c = _finalBirdCells[i];
                tiles[c] = 0;
                tiles[c - w] = 0;
            }
            _finalDoor = 0;
            for (int i = 0; i < _projState.Length; i++) _projState[i] = -1;
            _state = State.FinalStage;
            _area = Area.Final;
            _host.Play(Sfx.Lord);
        }
        else
        {
            _camMinY = n == 2 ? _subBottomRow * _tileH : _subAreaRows[n - 3] * _tileH;
            _camMaxY = _subAreaRows[n - 2] * _tileH - Layout.ScreenH - 1;
            _savedPx = _px;
            _savedPy = _py;
            _px = _subAreaX[n - 2];
            _py = _subAreaY[n - 2];
            _area = Area.SubArea;
        }
        if (placeCamera)
        {
            _camX = (_px >> 4) + _playerW / 2 - _outerX;
            _camY = (_py >> 4) - Layout.ScreenH / 2;
        }
        if (_camX < 0) _camX = 0;
        if (_camX > _camMaxX) _camX = _camMaxX;
        if (_camY < _camMinY) _camY = _camMinY;
        if (_camY > _camMaxY) _camY = _camMaxY;
        for (int i = 0; i < _projPos.Length; i++)
        {
            _hitFxPos[i] = -1;
            _projPos[i] = -1;
        }
        for (int i = 0; i < _projDir.Length; i++)
        {
            _projDir[i] = 0;
            _projState[i] = -1;
        }
    }

    // ------------------------------------------------------------------ main logic step (original d.do(int,int))
    /// <summary>Advances the game. The original runs its logic in steps of at least 20 ms; pass the elapsed milliseconds.</summary>
    public void Update(int dt)
    {
        _accum += dt;
        if (_accum < 20) return;
        int n = _accum;
        _accum = 0;
        _hintTimer -= n;
        _bonusMsgTimer -= n;
        if (_state >= State.ModeMenu && _state <= State.BonusAiMoving)
        {
            BonusTick(n);
            return;
        }
        bool followPlayer = true;
        if (_state == State.WipeIn)
        {
            if (_frame == 1) _host.Play(Sfx.Transportation);
            _frame++;
            if (_frame == 35)
            {
                EnterArea(_nextArea);
                _accum = 30;
                _frame = 0;
                Update(n);
                _hintTimer = 0;
                SnapshotScene();
                _state = State.WipeOut;
            }
            return;
        }
        if (_state == State.WipeOut)
        {
            _frame++;
            if (_frame == 35)
            {
                _frame = 0;
                _state = _returnState;
            }
            return;
        }

        UpdateVisibleWindow(_camY - _tileH, _camY + Layout.ScreenH + _tileH);
        int[] tiles = _map.Tiles;
        int bottom = _camY + Layout.ScreenH;
        for (int i = _animFirst; i < _animTiles.Length && _animTiles[i + 1] * _tileH < bottom; i += 3)
        {
            short x = _animTiles[i], y = _animTiles[i + 1];
            int cell = x + _map.Width * y;
            int phase = (_frame / 10 + x + y) / 1 % 3;
            if (phase > 1) phase = 1;
            if (_animTiles[i + 2] != phase)
            {
                _animTiles[i + 2] = (short)phase;
                switch (tiles[cell])
                {
                    case Tile.PortalA:
                    case Tile.PortalB:
                        _map.Set(x, y, phase == 0 ? Tile.PortalA : Tile.PortalB);
                        break;
                    case Tile.Coin:
                    case Tile.CoinB:
                        _map.Set(x, y, phase == 0 ? Tile.Coin : Tile.CoinB);
                        break;
                }
            }
        }

        switch (_state)
        {
            case State.Ending:
                _gameOverTimer += n;
                if (_gameOverTimer > 14000) { _running = false; _quit = true; }
                else UpdatePlayer(n);
                break;
            case State.Playing:
                UpdatePlayer(n);
                break;
            case State.Lord1:
                {
                    int t = _lordAnim - _stageStart;
                    _lordAnim++;
                    if (t >= 0 && t < 30 || t > 60 && t < 90)
                    {
                        _lordY = t / 4 % 2 == 0 ? _lord1Y : (short)0;
                    }
                    else if (t == 30) { _lordY = _lord1Y; _lordFrame = 1; }
                    else if (t == 60)
                    {
                        _lordFrame = 0;
                        int off = _lordH - _itemH - 2;
                        SpawnProjectile(_lord1X, _lord1Y - off);
                    }
                    else if (t == 90) { _lordY = 0; ReleasePlatform(_lord1Release & 0x3FF); }
                    else if (t == 110) ReleasePlatform(_lord1Release >> 10 & 0x3FF);
                    else if (t == 130)
                    {
                        ReleasePlatform(_lord1Release >> 20 & 0x3FF);
                        _stage++;
                        _state = State.Playing;
                    }
                    UpdatePlayer(n);
                    break;
                }
            case State.Lord2:
                {
                    int t = _lordAnim - _stageStart;
                    _lordAnim++;
                    if (!_keyTaken)
                    {
                        if (_projState[_nextProj] == -1)
                        {
                            int m = t % 35;
                            if (m < 23) _lordFrame = 1;
                            else if (m == 23)
                            {
                                _lordFrame = 0;
                                int off = _lordH - _itemH - 2;
                                SpawnProjectile(_lord2X, _lord2Y - off);
                            }
                        }
                    }
                    else if (t > 0 && t < 30)
                    {
                        _lordFrame = 0;
                        _lordY = t / 4 % 2 == 0 ? _lord2Y : (short)0;
                    }
                    else if (t == 30) { _lordY = 0; ReleasePlatform(_lord2Release & 0x3FF); }
                    else if (t == 50) ReleasePlatform(_lord2Release >> 10 & 0x3FF);
                    else if (t == 70)
                    {
                        ReleasePlatform(_lord2Release >> 20 & 0x3FF);
                        _stage++;
                        _state = State.Playing;
                    }
                    UpdatePlayer(n);
                    break;
                }
            case State.FinalStage:
                if (_area == Area.Final)
                {
                    _finalTimer -= n;
                    if (_finalTimer < 0)
                    {
                        if (_finalTimer < 0) _finalTimer = 0;
                        _gameOverTimer = 0;
                        _state = State.GameOver;
                    }
                }
                if (_finalTimer >= 60000)
                {
                    // intro: the camera scrolls down to the player over the first seven seconds
                    _camX = (_px >> 4) + _playerW / 2 - _outerX;
                    _camY = _finalTimer >= 65000 ? _camMinY : _camMinY + (65000 - _finalTimer) * 115 * (_camMaxY - _camMinY) / 500000;
                    if (_camY >= _camMaxY) _camY = _camMaxY;
                    followPlayer = false;
                    break;
                }
                if (_projState[_nextProj] == -1)
                {
                    int m = _frame % 35;
                    if (m < 23) _lordFrame = 1;
                    else if (m == 23)
                    {
                        _lordFrame = 0;
                        int off = _lordH - _itemH - 2;
                        SpawnProjectile(_lord2X2(), _lord2Y2() - off);
                    }
                }
                UpdatePlayer(n);
                break;
            case State.Cutscene:
                UpdatePlayer(n);
                Cutscene.Update();
                if (!Cutscene.Finished && !_skipPressed) break;
                _skipPressed = false;
                _state = State.Playing;
                if (_area == Area.IntroCutscene)
                {
                    Cutscene.Close();
                    EnterArea(0);
                    _host.Play(Sfx.GameStart);
                    break;
                }
                _host.Play(Sfx.OnceUponATime);
                _gameOverTimer = 0;
                _state = State.Ending;
                break;
            case State.GameOver:
                UpdateGameOver(n);
                break;
            case State.Unknown7:
                UpdatePlayer(n);
                break;
        }
        if (followPlayer)
        {
            int px = (_px >> 4) + _playerW / 2;
            int py = (_py >> 4) - _playerH / 2;
            UpdateCamera(px, py, _camX, _camY, Layout.ScreenW, Layout.ScreenH);
        }
        if (_state == State.Cutscene || _state == State.Ending)
        {
            _camX = Cutscene.CameraX;
            _camY = Cutscene.CameraY;
        }
    }

    // the final-stage Lord shoots from the position stored in aZ/aX (== the position EnterArea(1) put in _lordX/_lordY)
    private short _lord2X2() => _finalLordX;
    private short _lord2Y2() => _finalLordY;

    // Original d.j(): the freed-bird sprite flies right
    private void UpdateBirdFx()
    {
        if (_birdFxX != -1)
        {
            _birdFxX++;
            if (_birdFxX > _map.Width * _tileW) _birdFxX = -1;
        }
    }

}
