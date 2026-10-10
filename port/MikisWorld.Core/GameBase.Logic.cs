namespace MikisWorld.Core;

public abstract partial class GameBase
{
    // ---------------------------------------------------------------- input and update helpers shared by both games
    // Original d.k(): the Lords' lightning bolts fall, then slide sideways along the platforms
    protected void UpdateProjectiles()
    {
        int limit = _camY + L.ScreenH * 3 / 2 + ExtraView;
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

    // Original d.cfr_renamed_0(int,int): find the platforms / animated tiles that are near the screen.
    protected void UpdateVisibleWindow(int top, int bottom)
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

    // Original d.l(): positions of moving platforms (bit 1/2 = right/left, bits 3-5 = range in tiles, bits 6-7 = speed)
    protected void UpdateMovingPlatforms()
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

    // Original d.a(6 ints): camera follows the player with a dead zone and gentle catch-up scrolling
    protected void UpdateCamera(int px, int py, int camX, int camY, int viewW, int viewH)
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

    // Original d.cfr_renamed_7(int,int): launch a bolt at x (or at a random column when x == -1)
    protected void SpawnProjectile(int x, int y)
    {
        if (_projState[_nextProj] == -1)
        {
            if (x == -1)
            {
                _projPos[_nextProj * 2] = (short)(_rng.NextInt() % (L.ViewTilesW * _tileW - 8));
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

    // Original d.a(7 ints): 1-D sweep of the interval [a0,a1] moving by `delta` against a platform top at `edge`
    // (platform spans [p0,p1]); returns the allowed movement.
    protected static int SweepOverlap(int a0, int a1, int pos, int delta, int p0, int p1, int edge)
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

    protected void ReleasePlatform(int index)
    {
        if (index != 0) _platforms[index][3] = (short)(_platforms[index][3] & 0xFFFE);
    }

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

        _held.Press(key, action);
        if (key == 51 || key == 49 || key == 50 || action == 1) _jumpBuffer = JumpBufferTicks;
        if (key == 51) { _inputJump = -1; _inputX = 2; }
        else if (key == 54 || action == 5) _inputX = 2;
        else if (key == 49) { _inputJump = -1; _inputX = -2; }
        else if (key == 52 || action == 2) _inputX = -2;
        else if (key == 50 || action == 1) _inputJump = -1;
        else if ((key == 56 || action == 6) && _interact != -1 && !(Tweaks.RevampedMovement && (_state == State.WipeIn || _state == State.WipeOut))) _interactPressed = true;
    }

    /// <summary>Original cfr_renamed_3(int,int): any key release stops all input.</summary>
    public void KeyReleased(int key, int action)
    {
        _held.Release(key);
        _inputX = 0;
        _inputJump = 0;
    }

    /// <summary>Revamped movement: in the air the held direction steers (also against the takeoff side); with no direction held the jump keeps its momentum.</summary>
    protected int AirDrift(int hx)
    {
        if (hx != 0)
        {
            _airDir = hx < 0 ? -1 : 1;
            _sprite = hx < 0 ? 1 : 3;
        }
        return _airDir * _walkSpeed;
    }

    /// <summary>The player pressed "interact": enter / leave a portal or the golden gate (starts the wipe transition).</summary>
    protected void ProcessInteract()
    {
        if (_interactPressed)
        {
            _hintTimer = 0;
            if (_area == Area.SubArea)
            {
                SnapshotScene();
                _frame = 0;
                _returnState = _state;
                _state = State.WipeIn;
                _nextArea = 0;
            }
            else if (_interact == 65535)           // the golden gate
            {
                SnapshotScene();
                _frame = 0;
                _returnState = State.FinalStage;
                _state = State.WipeIn;
                _nextArea = 1;
                _interact = -1;
            }
            else
            {
                for (int i = 0; i < _portalCells.Length; i++)
                {
                    // the original has no destination for portals past the sub-area table (it crashes); revamped movement just ignores them
                    if (_interact == _portalCells[i] && (!Tweaks.RevampedMovement || i < _subAreaX.Length && i < _subAreaRows.Length))
                    {
                        SnapshotScene();
                        _frame = 0;
                        _returnState = _state;
                        _state = State.WipeIn;
                        _nextArea = (sbyte)(2 + i);
                        _interact = -1;
                        break;
                    }
                }
            }
            _interactPressed = false;
        }
    }

    /// <summary>One tick of the player's motion: key input -> motion state, then the movement that state asks for.
    /// <paramref name="restDy"/> is the vertical movement while standing / walking (0 in game 1, 2 in game 2).</summary>
    protected void StepMotion(out int dx, out int dy, int restDy)
    {
        // revamped movement: real held directions, and a jump needs a fresh key press
        int hx = Tweaks.RevampedMovement ? _held.X : _inputX;
        bool wantJump = Tweaks.RevampedMovement ? _jumpBuffer > 0 : _inputJump < 0;
        if (_motion == 0 || _motion == 1 || _motion == 2)
        {
            if (wantJump && hx < 0) { _motion = 3; _airDir = -1; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; _sprite = 1; _airControl = true; }
            else if (wantJump && hx > 0) { _motion = 5; _airDir = 1; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; _sprite = 3; _airControl = true; }
            else if (wantJump) { _motion = 4; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; }
            else if (hx < 0) _motion = 1;
            else if (hx > 0) _motion = 2;
        }
        if (hx == 0 && (_motion == 1 || _motion == 2)) _motion = 0;
        if (_jumpBuffer > 0) _jumpBuffer--;

        dx = 0; dy = restDy;
        int prevCase = _lastCase;
        _lastCase = _motion;
        switch (_motion)
        {
            case 0:
                if (_sprite >= 2) _sprite = 2;
                else _sprite = 0;
                break;
            case 1:
                _sprite = _frame / (Tweaks.SlowWalkAnimation ? 6 : 3) % 2;
                dx -= _walkSpeed;
                break;
            case 2:
                _sprite = 2 + _frame / (Tweaks.SlowWalkAnimation ? 6 : 3) % 2;
                dx += _walkSpeed;
                break;
            case 3:
                dy = -_jumpTable[_jumpStep];
                if (Tweaks.RevampedMovement) dx = AirDrift(hx);
                else
                {
                    if (_inputX < 0 && _inputJump == 0) { dx = -_airSpeed; _airControl = false; _sprite = 1; }
                    else if (_inputX > 0 && _inputJump == 0) { dx = _airSpeed; _airControl = false; _sprite = 3; }
                    else if (_airControl) dx -= _walkSpeed;
                }
                if (!EngineTweaks.JumpAdvance(Tweaks.SlowJump, ref _jumpSub, ref dx, ref dy)) break;
                _jumpStep++;
                if (_jumpStep != _jumpTable.Length) break;
                _jumpStep = 0;
                _motion = 0;
                break;
            case 4:
                if (hx < 0) { _sprite = 1; dx = -_walkSpeed; }
                else if (hx > 0) { _sprite = 3; dx = _walkSpeed; }
                dy = -_jumpTable[_jumpStep];
                if (!EngineTweaks.JumpAdvance(Tweaks.SlowJump, ref _jumpSub, ref dx, ref dy)) break;
                _jumpStep++;
                if (_jumpStep != _jumpTable.Length) break;
                _jumpStep = 0;
                _motion = 0;
                break;
            case 5:
                dy = -_jumpTable[_jumpStep];
                if (Tweaks.RevampedMovement) dx = AirDrift(hx);
                else
                {
                    if (_airControl) dx += _walkSpeed;
                    if (_inputX < 0 && _inputJump == 0) { dx = -_walkSpeed; _airControl = false; _sprite = 1; }
                    else if (_inputX > 0 && _inputJump == 0) { dx = _walkSpeed; _airControl = false; _sprite = 3; }
                }
                if (!EngineTweaks.JumpAdvance(Tweaks.SlowJump, ref _jumpSub, ref dx, ref dy)) break;
                _jumpStep++;
                if (_jumpStep != _jumpTable.Length) break;
                _jumpStep = 0;
                _motion = 0;
                break;
            case 6:
                // walking on flat ground flips between motion 6 and 0 every tick in the original; its air pose would make the walk flicker
                bool pose = !Tweaks.SlowWalkAnimation || prevCase == 6;
                if (hx < 0) { if (pose) _sprite = 1; dx = -_airSpeed; }
                else if (hx > 0) { if (pose) _sprite = 3; dx = _airSpeed; }
                dy += _fallSpeed;
                break;
        }
    }

    // Original d.cfr_renamed_11: game-over countdown
    protected void UpdateGameOver(int n)
    {
        _frame++;
        UpdateMovingPlatforms();
        UpdateFx();
        UpdateProjectiles();
        if (_gameOverTimer == 0) _host.Play(Sfx.GameOver);
        _gameOverTimer += n;
        if (_gameOverTimer > 3000) { _running = false; _quit = true; }
    }

    // Original d.if(byte[]): turn marker tiles into platforms / animated tile lists.
    protected void ScanLevel(sbyte[] platformFlags)
    {
        var animated = new List<int>();     // map indices of coins + portals, row-major
        var portals = new List<int>();
        var markers = new List<int>();      // platform x, y, length triples
        int[] tiles = _map.Tiles;
        int w = _map.Width, h = _map.Height;
        int idx = 0;
        _totalCoins = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++, idx++)
            {
                if (tiles[idx] == Tile.Cloud)
                {
                    markers.Add(x);
                    markers.Add(y);
                    int len = 0;
                    while (x < w && tiles[idx] == Tile.Cloud)
                    {
                        tiles[idx] = 0;
                        len++; idx++; x++;
                    }
                    if (x == w) idx--;
                    markers.Add(len);
                }
                else if (tiles[idx] == GateMarkerTile)
                {
                    _gateX = (short)x;
                    _gateY = (short)y;
                }
                else if (tiles[idx] == Tile.PortalA)
                {
                    portals.Add(idx);
                    animated.Add(idx);
                }
                else if (tiles[idx] == Tile.Coin)
                {
                    _totalCoins++;
                    animated.Add(idx);
                }
                else
                {
                    ScanOtherTile(x, y, tiles[idx]);
                }
            }
        }
        _portalCells = new short[portals.Count];
        for (int i = 0; i < portals.Count; i++) _portalCells[i] = (short)portals[i];
        _animTiles = new short[animated.Count * 3];
        for (int i = 0; i < animated.Count; i++)
        {
            _animTiles[i * 3] = (short)(animated[i] % _map.Width);
            _animTiles[i * 3 + 1] = (short)(animated[i] / _map.Width);
            _animTiles[i * 3 + 2] = -1;
        }
        _animFirst = 0;
        _platforms = new short[platformFlags.Length][];
        _platformX = new short[platformFlags.Length];
        for (int i = 0; i < platformFlags.Length; i++)
        {
            short x = (short)markers[i * 3], y = (short)markers[i * 3 + 1], len = (short)markers[i * 3 + 2];
            x = (short)(x * 8); y = (short)(y * 8);
            short f = (short)(platformFlags[i] & 0xFF);
            x = (short)(x * _scaleX / 16);
            y = (short)(y * _scaleY / 16);
            if (len == 0) len = (short)_map.Width;
            _platforms[i] = new short[] { x, y, len, f };
            _platformX[i] = x;
        }
    }
}
