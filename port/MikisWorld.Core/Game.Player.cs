namespace MikisWorld.Core;

public sealed partial class Game
{
    // Original d.cfr_renamed_8(int,int): one logic step of the player (input, jump arc, platforms, tiles, items, hits).
    private void UpdatePlayer(int dtMs)
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
                    if (_interact == _portalCells[i])
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
        _frame++;
        if (_stage == 2 && _area == Area.Main && _frame % 35 == 0) SpawnProjectile(-1, _camY);

        // ---- input -> motion state
        // revamped movement: real held directions, and a jump needs a fresh key press
        int hx = Tweaks.RevampedMovement ? _held.X : _inputX;
        bool wantJump = Tweaks.RevampedMovement ? _jumpBuffer > 0 : _inputJump < 0;
        if (_motion == 0 || _motion == 1 || _motion == 2)
        {
            if (wantJump && hx < 0) { _motion = 3; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; _sprite = 1; _airControl = true; }
            else if (wantJump && hx > 0) { _motion = 5; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; _sprite = 3; _airControl = true; }
            else if (wantJump) { _motion = 4; _jumpStep = 0; _jumpSub = 0; _jumpBuffer = 0; }
            else if (hx < 0) _motion = 1;
            else if (hx > 0) _motion = 2;
        }
        if (hx == 0 && (_motion == 1 || _motion == 2)) _motion = 0;
        if (_jumpBuffer > 0) _jumpBuffer--;

        int dx = 0, dy = 0;
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
                if (_inputX < 0 && _inputJump == 0) { dx = -_airSpeed; _airControl = false; _sprite = 1; }
                else if (_inputX > 0 && _inputJump == 0) { dx = _airSpeed; _airControl = false; _sprite = 3; }
                else if (_airControl) dx -= _walkSpeed;
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
                if (_airControl) dx += _walkSpeed;
                if (_inputX < 0 && _inputJump == 0) { dx = -_walkSpeed; _airControl = false; _sprite = 1; }
                else if (_inputX > 0 && _inputJump == 0) { dx = _walkSpeed; _airControl = false; _sprite = 3; }
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

        // ---- moving platforms carry the player
        int platX0 = 0;
        if (_standingOn != -1) platX0 = _platformX[_standingOn];
        UpdateMovingPlatforms();
        if (_standingOn != -1)
        {
            platX0 = _platformX[_standingOn] - platX0;
            dx += platX0 << 4;
        }
        int tx = _px >> 4;
        int ty = _py >> 4;
        int left = tx + _playerW / 2 - _hitHalfW;
        int right = tx + _playerW / 2 + _hitHalfW;
        bool grounded = false;
        if (_standingOn == -1 && dy > 0)
        {
            int below = ty + 1;
            for (int p = _visFirst; p < _visFirst + _visCount && !grounded; p++)
            {
                if ((_platforms[p][3] & 1) != 0) continue;
                int p0 = _platformX[p];
                int p1 = p0 + _tileW * _platforms[p][2];
                int top = _platforms[p][1];
                int allowed = SweepOverlap(left << 4, right << 4, below << 4, dy, p0 << 4, p1 << 4, top << 4);
                if (allowed < dy)
                {
                    grounded = true;
                    _standingOn = p;
                    dy = allowed;
                    if (_stage == 0 && _standingOn == _lord1Platform)
                    {
                        _stage++;
                        _stageStart = _frame;
                        _lordFrame = 0;
                        _lordAnim = _frame;
                        _lordX = _lord1X;
                        _lordY = _lord1Y;
                        _host.Play(Sfx.Lord);
                        _state = State.Lord1;
                    }
                    else if (_stage == 2 && _standingOn == _lord2Platform)
                    {
                        _stage++;
                        _stageStart = _frame;
                        _lordFrame = 0;
                        _lordAnim = _frame;
                        _lordX = _lord2X;
                        _lordY = _lord2Y;
                        _host.Play(Sfx.Lord);
                        _state = State.Lord2;
                    }
                }
            }
        }
        else if (_standingOn != -1)
        {
            int p0 = _platformX[_standingOn];
            int top = _platforms[_standingOn][1];
            int p1 = p0 + _tileW * _platforms[_standingOn][2];
            if (!((left >= p0 && left <= p1 || right >= p0 && right <= p1 || left <= p0 && right >= p1) && top - ty == 1 && (_platforms[_standingOn][3] & 1) == 0))
                _standingOn = -1;
        }
        int inset = 70 * _playerW / 200;
        if (_collision.Move(_px + inset, _py - (_playerH << 4), _playerW - inset << 4, _playerH << 4, dx, dy))
        {
            grounded = true;
            dx = _collision.OutDx;
            dy = _collision.OutDy;
        }
        if (!grounded && _standingOn == -1 && _motion != 6 && dy >= 0 && _motion != 3 && _motion != 4 && _motion != 5)
            _motion = 6;
        if (grounded && (_motion == 6 || _motion == 3 || _motion == 4 || _motion == 5))
            _motion = 0;
        if (tx <= 2 && dx < 0)
        {
            _px = 32;
            dx = 0;
        }
        else if (tx + _playerW + 2 >= _map.Width * _tileW && dx > 0)
        {
            _px = _map.Width * _tileW - _playerW - 2 << 4;
            dx = 0;
        }
        _px += dx;
        _py += dy;
        tx = _px >> 4;
        ty = _py >> 4;

        // ---- tiles under the player
        int rowTop = (ty - _playerH / 2) / _tileH;
        int rowBottom = ty / _tileH;
        int colLeft = (tx + _playerW / 3) / _tileW;
        int colRight = (tx + _playerW - _playerW / 3) / _tileW;
        int[] tiles = _map.Tiles;
        if (_interact == -1) _hintTimer = 2000;
        _interact = -1;
        for (int row = rowTop; row <= rowBottom; row++)
        {
            int cell = row * _map.Width + colLeft;
            for (int col = colLeft; col <= colRight; col++, cell++)
            {
                switch (tiles[cell])
                {
                    case Tile.GateTL:
                    case Tile.GateTR:
                    case Tile.GateBL:
                    case Tile.GateBR:
                        _interact = 65535;
                        break;
                    case Tile.PortalA:
                    case Tile.PortalB:
                        _interact = cell;
                        break;
                    case Tile.CagedBird:
                        _map.Set(col, row, 0);
                        _map.Set(col, row - 1, 0);
                        _birdFxX = col * _tileW;
                        _birdFxY = (row + 1) * _tileH - _itemH / 2 + 3;
                        _host.Play(Sfx.Cage);
                        if (_area == Area.Final)
                        {
                            _finalDoor++;
                            if (_finalDoor < _finalBirdCells.Length)
                            {
                                int c = _finalBirdCells[_finalDoor] % _map.Width;
                                int r = _finalBirdCells[_finalDoor] / _map.Width;
                                _map.Set(c, r, Tile.CagedBird);
                                _map.Set(c, r - 1, Tile.BirdSign);
                                break;
                            }
                            _host.Play(Sfx.GameEnd);
                            _skipPressed = false;
                            Cutscene.Load("end.bin");
                            EnterArea(-2);
                            break;
                        }
                        _birds++;
                        break;
                    case Tile.Coin:
                    case Tile.CoinB:
                        _map.Set(col, row, 0);
                        _coins++;
                        if (_coins == _totalCoins)
                        {
                            _save[0] = 1;
                            _bonusMsgTimer = 2000;
                            _host.Play(Sfx.Bonus);
                        }
                        else
                        {
                            _host.Play(Sfx.Coin);
                        }
                        if (_coins % 15 != 0 || _lives >= 6) break;
                        _lives++;
                        break;
                    case Tile.Key:
                        _map.Set(col, row, 0);
                        _map.Set(_gateX, _gateY, Tile.GateTL);
                        _map.Set(_gateX + 1, _gateY, Tile.GateTR);
                        _map.Set(_gateX, _gateY + 1, Tile.GateBL);
                        _map.Set(_gateX + 1, _gateY + 1, Tile.GateBR);
                        _keyTaken = true;
                        _host.Play(Sfx.Key);
                        _lordAnim = _frame;
                        _stageStart = _frame;
                        break;
                }
            }
        }
        UpdateBirdFx();
        UpdateProjectiles();
        int centerX = tx + _playerW / 2;
        for (int i = 0; i < 3; i++)
        {
            if (_projState[i] != -1)
            {
                int ddx = centerX - _projPos[i * 2];
                int ddy = ty - _projPos[i * 2 + 1];
                if (ddx < 0) ddx = -ddx;
                if (ddx < 4 && ddy < 5 && ddy > -2)
                {
                    _projState[i] = -1;
                    _lives--;
                    _host.Play(Sfx.Damage);
                    _host.Vibrate(100, 200);
                    _hitFxPos[i * 2] = (short)(tx + _hitFxDx);
                    _hitFxPos[i * 2 + 1] = (short)(ty + _hitFxDy);
                    _hitFxStart[i] = _frame;
                    if (_lives <= 0)
                    {
                        _lives = 0;
                        _gameOverTimer = 0;
                        _state = State.GameOver;
                    }
                }
            }
            if (_hitFxPos[i * 2] != -1 && _frame - _hitFxStart[i] > 21) _hitFxPos[i * 2] = -1;
        }
    }
}
