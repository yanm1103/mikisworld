namespace MikisWorld.Core;

public sealed partial class Game2
{

    // ------------------------------------------------------------------ main logic step (original c.do(int,int))
    /// <summary>Advances the game. <paramref name="dt"/> = elapsed ms; <paramref name="totalMs"/> = accumulated game time
    /// (the final stage schedules rainbow respawns against it).</summary>
    public void Update(int dt, int totalMs)
    {
        _accum += dt;
        if (_accum < 20) return;
        _repaint = true;
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
                Update(n, totalMs);
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

        UpdateVisibleWindow(_camY - _tileH, _camY + Layout2.ScreenH + _tileH);
        int[] tiles = _map.Tiles;
        int bottom = _camY + Layout2.ScreenH;
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
                    case Tile2.PortalA:
                    case Tile2.PortalB:
                        PutTile(x, y, phase == 0 ? Tile2.PortalA : Tile2.PortalB);
                        break;
                    case Tile2.Coin:
                    case Tile2.CoinB:
                        PutTile(x, y, phase == 0 ? Tile2.Coin : Tile2.CoinB);
                        break;
                }
            }
        }

        switch (_state)
        {
            case State.Ending:
                _gameOverTimer += n;
                if (_gameOverTimer > 7000) { _running = false; _quit = true; break; }
                UpdatePlayer(n, totalMs);
                break;
            case State.Playing:
                _markCooldown += n;
                UpdatePlayer(n, totalMs);
                break;
            case State.Lord1:
                {
                    int t = _lordAnim - _stageStart;
                    _lordAnim++;
                    if (t >= 0 && t < 10 || t > 20 && t < 30)
                    {
                        _lordY = t / 4 % 2 == 0 ? _lord1Y : (short)0;
                    }
                    else if (t == 10) { _lordY = _lord1Y; _lordFrame = 1; }
                    else if (t == 20)
                    {
                        _lordFrame = 0;
                        int off = _lordH - _itemH - 2;
                        SpawnProjectile(_lord1X, _lord1Y - off);
                    }
                    else if (t == 30) { _lordY = 0; ReleasePlatform(_lord1Release & 0x3FF); }
                    else if (t == 40) ReleasePlatform(_lord1Release >> 10 & 0x3FF);
                    else if (t == 50)
                    {
                        ReleasePlatform(_lord1Release >> 20 & 0x3FF);
                        _stage++;
                        _state = State.Playing;
                    }
                    UpdatePlayer(n, totalMs);
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
                    else if (t > 0 && t < 10)
                    {
                        _lordFrame = 0;
                        _lordY = t / 4 % 2 == 0 ? _lord2Y : (short)0;
                    }
                    else if (t == 10) { _lordY = 0; ReleasePlatform(_lord2Release & 0x3FF); }
                    else if (t == 30) ReleasePlatform(_lord2Release >> 10 & 0x3FF);
                    else if (t == 40)
                    {
                        ReleasePlatform(_lord2Release >> 20 & 0x3FF);
                        _stage++;
                        _state = State.Playing;
                    }
                    UpdatePlayer(n, totalMs);
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
                while (_events[_eventIdx] != -1 && _events[_eventIdx] < totalMs)
                {
                    int cell = _events[_eventIdx + 1];
                    int cx = cell % _map.Width, cy = cell / _map.Width;
                    PutTile(cx, cy, Tile2.RainbowL);
                    PutTile(cx + 1, cy, Tile2.RainbowR);
                    _events[_eventIdx] = -1;
                    _eventIdx += 2;
                    _eventIdx %= _events.Length;
                }
                if (_finalTimer >= 60000)
                {
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
                        SpawnProjectile(_finalLordX, _finalLordY - off);
                    }
                }
                UpdatePlayer(n, totalMs);
                break;
            case State.Cutscene:
                UpdatePlayer(n, totalMs);
                Cutscene.Update();
                if (!Cutscene.Finished && !_skipPressed) break;
                _skipPressed = false;
                _markCooldown = 0;
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
                UpdatePlayer(n, totalMs);
                break;
        }
        if (followPlayer)
        {
            int px = (_px >> 4) + _playerW / 2;
            int py = (_py >> 4) - _playerH / 2;
            UpdateCamera(px, py, _camX, _camY, Layout2.ScreenW, Layout2.ScreenH);
        }
        if (_state == State.Cutscene || _state == State.Ending)
        {
            _camX = Cutscene.CameraX;
            _camY = Cutscene.CameraY;
        }
    }

    // Original c.R2(int,int): one hit (projectile or Mork)
    private void Damage()
    {
        _morkTimer = 4000;
        --_lives;
        _host.Play(Sfx.Damage);
        _host.Vibrate(100, 200);
        _spark = 21;
        if (_lives <= 0)
        {
            _lives = 0;
            _gameOverTimer = 0;
            _state = State.GameOver;
        }
    }

    // Original c.i(): the four floating rainbow sprites rise until they leave the screen
    private void UpdateRainbowFx()
    {
        for (int i = 0; i < 4; i++)
        {
            if (_fxX[i] != -1)
            {
                _fxY[i] = _fxY[i] - _riseSpeed;
                if (_fxY[i] < _camY << 4) _fxX[i] = -1;
            }
        }
    }

}
