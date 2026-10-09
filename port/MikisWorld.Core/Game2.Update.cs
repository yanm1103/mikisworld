namespace MikisWorld.Core;

public sealed partial class Game2
{
    partial void SnapshotScene();

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

    private void ReleasePlatform(int index)
    {
        if (index != 0) _platforms[index][3] = (short)(_platforms[index][3] & 0xFFFE);
    }

    private void UpdateGameOver(int n)
    {
        _frame++;
        UpdateMovingPlatforms();
        UpdateRainbowFx();
        UpdateProjectiles();
        if (_gameOverTimer == 0) _host.Play(Sfx.GameOver);
        _gameOverTimer += n;
        if (_gameOverTimer > 3000) { _running = false; _quit = true; }
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

    private void UpdateCamera(int px, int py, int camX, int camY, int viewW, int viewH)
    {
        if (!_scrollLockX) _scrollLockX = px - camX < _outerX || camX + viewW - px < _outerX + _tileW;
        if (!_scrollLockY) _scrollLockY = py - camY < _outerY || camY + viewH - py < _outerY + _tileH;
        if (_scrollLockX)
        {
            if (px - camX >= _innerX && camX + viewW - px >= _innerX) _scrollLockX = false;
            else if (px - camX < _innerX / 3) _camX = px - _innerX / 3;
            else if (camX + viewW - px < _innerX / 3) _camX = px + _innerX / 3 - viewW;
            else if (px - camX < _innerX) _camX -= _scrollStepX;
            else if (camX + viewW - px < _innerX) _camX += _scrollStepX;
        }
        if (_scrollLockY)
        {
            if (py - camY >= _innerY && camY + viewH - py >= _innerY) _scrollLockY = false;
            else if (py - camY < _innerY / 3) _camY = py - _innerY / 3;
            else if (camY + viewH - py < _innerY / 3) _camY = py + _innerY / 3 - viewH;
            else if (py - camY < _innerY) _camY -= _scrollStepY;
            else if (camY + viewH - py < _innerY) _camY += _scrollStepY;
        }
        if (_camX < 0) { _camX = 0; _scrollLockX = false; }
        else if (_camX > _camMaxX) { _camX = _camMaxX; _scrollLockX = false; }
        if (_camY < _camMinY) { _camY = _camMinY; _scrollLockY = false; }
        else if (_camY > _camMaxY) { _camY = _camMaxY; _scrollLockY = false; }
    }

    private static int SweepOverlap(int a0, int a1, int pos, int delta, int p0, int p1, int edge)
    {
        if (a0 >= p0 && a0 <= p1 || a1 >= p0 && a1 <= p1 || a0 <= p0 && a1 >= p1)
        {
            int end = pos + delta;
            int lo = pos;
            if (end < lo) { int t = lo; lo = end; end = t; }
            if (edge >= lo && edge <= end)
            {
                if (delta > 0) return edge - lo;
                return edge - end;
            }
        }
        return delta;
    }

    private void UpdateMovingPlatforms()
    {
        for (int i = _visFirst; i < _visFirst + _visCount; i++)
        {
            short f = _platforms[i][3];
            if ((f & 6) == 2 || (f & 6) == 4)
            {
                int range = ((f & 0x38) >> 3) * _tileW;
                int speed = ((f & 0xC0) >> 6) + 1;
                speed = speed * _scaleX >> 4;
                int pos;
                if (range == 0)
                {
                    range = _map.Width + _platforms[i][2];
                    range *= _tileW;
                    if ((f & 6) == 2) pos = (_frame * speed / 4 + _platforms[i][0] + _platforms[i][2] * _tileW) % range;
                    else pos = (_platforms[i][0] + range - _frame * speed / 4 % range) % range;
                    pos -= _platforms[i][2] * _tileW;
                    _platformX[i] = (short)pos;
                }
                else
                {
                    pos = _frame * speed / 4 % (range * 2);
                    if (pos > range) pos = range - (pos - range);
                    if ((f & 6) == 2) _platformX[i] = (short)(_platforms[i][0] + pos);
                    else if ((f & 6) == 4) _platformX[i] = (short)(_platforms[i][0] - pos);
                }
            }
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

    private void UpdateProjectiles()
    {
        int limit = _camY + Layout2.ScreenH * 3 / 2;
        int bw = _itemW, bh = _itemH;
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] == -1) continue;
            bool blocked = false;
            short px = _projPos[i * 2];
            int right = px + bw;
            short py = _projPos[i * 2 + 1];
            if (_projPos[i * 2 + 1] > limit)
            {
                _projState[i] = -1;
                continue;
            }
            int fall = 1;
            for (int p = _visFirst; p < _visFirst + _visCount; p++)
            {
                if ((_platforms[p][3] & 1) == 0)
                {
                    int p0 = _platformX[p];
                    int p1 = p0 + _tileW * _platforms[p][2];
                    short pyTop = _platforms[p][1];
                    int r = SweepOverlap(px, right, (int)py, 2, p0, p1, (int)pyTop);
                    if (r < 2) { blocked = true; break; }
                }
            }
            if (_collision.Move(px << 4, py - bh << 4, bw << 4, bh << 4, 0, fall << 4))
            {
                blocked = true;
                fall = _collision.OutDy;
            }
            switch (_projState[i])
            {
                case 0:
                    if (!blocked)
                    {
                        _projPos[i * 2 + 1] = (short)(_projPos[i * 2 + 1] + fall);
                        break;
                    }
                    _projState[i] = 1;
                    _projDir[i] = (_rng.NextInt() % 2 == 0) ? (sbyte)1 : (sbyte)-1;
                    break;
                case 1:
                    if (!blocked)
                    {
                        _projState[i] = 0;
                        _projPos[i * 2 + 1] = (short)(_projPos[i * 2 + 1] + fall);
                        break;
                    }
                    int nx = _projPos[i * 2];
                    nx += _projDir[i];
                    if (nx <= 0) { nx = 1; _projDir[i] = (sbyte)-_projDir[i]; }
                    else if (nx + bw >= _map.Width * _tileW) { nx = _map.Width * _tileW - bw - 1; _projDir[i] = (sbyte)-_projDir[i]; }
                    _projPos[i * 2] = (short)nx;
                    break;
            }
        }
    }

    private void SpawnProjectile(int x, int y)
    {
        if (_projState[_nextProj] == -1)
        {
            if (x == -1)
            {
                _projPos[_nextProj * 2] = (short)(_rng.NextInt() % (Layout2.ViewTilesW * _tileW - 8));
                if (_projPos[_nextProj * 2] < 0) _projPos[_nextProj * 2] = (short)-_projPos[_nextProj * 2];
                _projPos[_nextProj * 2] = (short)(_projPos[_nextProj * 2] + _camX);
            }
            else
            {
                _projPos[_nextProj * 2] = (short)x;
            }
            _projPos[_nextProj * 2 + 1] = (short)y;
            _projState[_nextProj] = 0;
            _nextProj++;
            _nextProj %= 3;
        }
    }
}
