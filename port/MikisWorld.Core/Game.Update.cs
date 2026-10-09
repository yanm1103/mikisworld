namespace MikisWorld.Core;

public sealed partial class Game
{
    // partial hook: the renderer paints the current scene into the wipe-transition snapshot
    partial void SnapshotScene();

    // ------------------------------------------------------------------ area changes (original d.for(int))
    /// <summary>Switches area: -2 outro cutscene, -1 intro cutscene, 0 main, 1 final stage, 2+ sub-areas via portals.</summary>
    public void EnterArea(int n)
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

    // Original d.cfr_renamed_0(int,int): find the platforms / animated tiles that are near the screen.
    private void UpdateVisibleWindow(int top, int bottom)
    {
        if (_animTiles[_animFirst + 1] * _tileH < top)
        {
            while (_animFirst < _animTiles.Length - 3)
            {
                if (_animTiles[_animFirst + 1] * _tileH < top) { _animFirst += 3; continue; }
                break;
            }
        }
        else if (_animTiles[_animFirst + 1] * _tileH > top)
        {
            while (_animFirst > 0)
            {
                if (_animTiles[_animFirst + 1] * _tileH >= top) { _animFirst -= 3; continue; }
                break;
            }
        }
        if (top > _platforms[_visFirst][1])
        {
            for (int i = _visFirst; i < _platforms.Length; i++)
                if (_platforms[i][1] >= top) { _visFirst = i; break; }
            int end = -1;
            for (int i = _visFirst; i < _platforms.Length; i++)
                if (_platforms[i][1] >= bottom) { end = i; break; }
            if (end == -1) end = _platforms.Length;
            _visCount = end - _visFirst;
        }
        else if (_visFirst < _platforms.Length)
        {
            for (int i = _visFirst; i >= 0; i--)
                if (_platforms[i][1] < top || i == 0) { _visFirst = i; break; }
            int end = -1;
            for (int i = _visFirst; i < _platforms.Length; i++)
                if (_platforms[i][1] >= bottom) { end = i; break; }
            if (end == -1) end = _platforms.Length;
            _visCount = end - _visFirst;
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

    private void ReleasePlatform(int index)
    {
        if (index != 0) _platforms[index][3] = (short)(_platforms[index][3] & 0xFFFE);
    }

    // Original d.cfr_renamed_11: game-over countdown
    private void UpdateGameOver(int n)
    {
        _frame++;
        UpdateMovingPlatforms();
        UpdateBirdFx();
        UpdateProjectiles();
        if (_gameOverTimer == 0) _host.Play(Sfx.GameOver);
        _gameOverTimer += n;
        if (_gameOverTimer > 3000) { _running = false; _quit = true; }
    }

    // Original d.a(6 ints): camera follows the player with a dead zone and gentle catch-up scrolling
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

    // Original d.a(7 ints): 1-D sweep of the interval [a0,a1] moving by `delta` against a platform top at `edge`
    // (platform spans [p0,p1]); returns the allowed movement.
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

    // Original d.l(): positions of moving platforms (bit 1/2 = right/left, bits 3-5 = range in tiles, bits 6-7 = speed)
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

    // Original d.j(): the freed-bird sprite flies right
    private void UpdateBirdFx()
    {
        if (_birdFxX != -1)
        {
            _birdFxX++;
            if (_birdFxX > _map.Width * _tileW) _birdFxX = -1;
        }
    }

    // Original d.k(): the Lords' lightning bolts fall, then slide sideways along the platforms
    private void UpdateProjectiles()
    {
        int limit = _camY + Layout.ScreenH * 3 / 2;
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

    // Original d.cfr_renamed_7(int,int): launch a bolt at x (or at a random column when x == -1)
    private void SpawnProjectile(int x, int y)
    {
        if (_projState[_nextProj] == -1)
        {
            if (x == -1)
            {
                _projPos[_nextProj * 2] = (short)(_rng.NextInt() % (Layout.ViewTilesW * _tileW - 8));
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
