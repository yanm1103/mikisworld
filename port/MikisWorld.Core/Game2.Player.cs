namespace MikisWorld.Core;

public sealed partial class Game2
{
    // Original c.R12(int,int): one logic step of the player, Mork, rainbows and hits.
    private void UpdatePlayer(int dtMs, int totalMs)
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
            else if (_interact == 65535)           // the open golden gate
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

        int dx = 0, dy = 0;
        if (_state != State.Cutscene && _state != State.Ending)
        {
            if (_motion == 0 || _motion == 1 || _motion == 2)
            {
                if (_inputJump < 0 && _inputX < 0) { _motion = 3; _jumpStep = 0; _sprite = 1; _airControl = true; }
                else if (_inputJump < 0 && _inputX > 0) { _motion = 5; _jumpStep = 0; _sprite = 3; _airControl = true; }
                else if (_inputJump < 0) { _motion = 4; _jumpStep = 0; }
                else if (_inputX < 0) _motion = 1;
                else if (_inputX > 0) _motion = 2;
            }
            if (_inputX == 0 && (_motion == 1 || _motion == 2)) _motion = 0;
            dx = 0;
            dy = 2;
            switch (_motion)
            {
                case 0:
                    if (_sprite >= 2) _sprite = 2;
                    else _sprite = 0;
                    break;
                case 1:
                    _sprite = _frame / 3 % 2;
                    dx -= _walkSpeed;
                    break;
                case 2:
                    _sprite = 2 + _frame / 3 % 2;
                    dx += _walkSpeed;
                    break;
                case 3:
                    dy = -_jumpTable[_jumpStep];
                    if (_inputX < 0 && _inputJump == 0) { dx = -_airSpeed; _airControl = false; _sprite = 1; }
                    else if (_inputX > 0 && _inputJump == 0) { dx = _airSpeed; _airControl = false; _sprite = 3; }
                    else if (_airControl) dx -= _walkSpeed;
                    _jumpStep++;
                    if (_jumpStep != _jumpTable.Length) break;
                    _jumpStep = 0;
                    _motion = 0;
                    break;
                case 4:
                    if (_inputX < 0) { _sprite = 1; dx = -_walkSpeed; }
                    else if (_inputX > 0) { _sprite = 3; dx = _walkSpeed; }
                    dy = -_jumpTable[_jumpStep];
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
                    _jumpStep++;
                    if (_jumpStep != _jumpTable.Length) break;
                    _jumpStep = 0;
                    _motion = 0;
                    break;
                case 6:
                    if (_inputX < 0) { _sprite = 1; dx = -_airSpeed; }
                    else if (_inputX > 0) { _sprite = 3; dx = _airSpeed; }
                    dy += _fallSpeed;
                    break;
            }
        }

        // ---- moving platforms carry the player
        int platX0 = 0;
        if (_standingOn != -1) platX0 = _platformX[_standingOn];
        UpdateMovingPlatforms();
        if (_standingOn != -1)
        {
            if (dy > 0) dy = 0;
            platX0 = _platformX[_standingOn] - platX0;
            dx += platX0 << 4;
        }
        int tx = _px >> 4;
        int ty = _py >> 4;
        int left = tx + _playerW / 2 - _hitHalfW;
        int right = tx + _playerW / 2 + _hitHalfW;
        bool grounded = false;
        int inset = 70 * _playerW / 200;
        // tile collision runs first in this game
        if (_collision.Move(_px + inset, _py - (_playerH << 4), _playerW - inset << 4, _playerH << 4, dx, dy))
        {
            grounded = true;
            dx = _collision.OutDx;
            dy = _collision.OutDy;
        }
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
                    case Tile2.GateOpenTL:
                    case Tile2.GateOpenTR:
                    case Tile2.GateOpenBL:
                    case Tile2.GateOpenBR:
                        _interact = 65535;
                        break;
                    case Tile2.PortalA:
                    case Tile2.PortalB:
                        _interact = cell;
                        break;
                    case Tile2.RainbowL:
                    case Tile2.RainbowR:
                        {
                            int rc = col;
                            if (tiles[cell] == Tile2.RainbowR) rc = col - 1;
                            _map.Set(rc, row, 0);
                            _map.Set(rc + 1, row, 0);
                            for (int s = 0; s < 4; s++)
                            {
                                if (_fxX[s] == -1)
                                {
                                    _fxX[s] = rc * _tileW + (_tileW >> 1);
                                    _fxY[s] = (row + 1) * _tileH - _itemH / 2 + 3 << 4;
                                    break;
                                }
                            }
                            _host.Play(Sfx.Cage);
                            if (_area == Area.Final)
                            {
                                if (_finalRainbows + 1 < 10)
                                {
                                    int slot = _finalRainbows * 2 % _events.Length;
                                    _events[slot] = totalMs + 4000;
                                    _events[slot + 1] = rc + row * _map.Width;
                                    ++_finalRainbows;
                                    break;
                                }
                                _host.Play(Sfx.GameEnd);
                                _skipPressed = false;
                                Cutscene.Load("end.bin");
                                EnterArea(-2);
                                break;
                            }
                            ++_rainbows;
                            break;
                        }
                    case Tile2.Coin:
                    case Tile2.CoinB:
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
                    case Tile2.Key:
                        _map.Set(col, row, 0);
                        _map.Set(_gateX, _gateY, Tile2.GateOpenTL);
                        _map.Set(_gateX + 1, _gateY, Tile2.GateOpenTR);
                        _map.Set(_gateX, _gateY + 1, Tile2.GateOpenBL);
                        _map.Set(_gateX + 1, _gateY + 1, Tile2.GateOpenBR);
                        _keyTaken = true;
                        _host.Play(Sfx.Key);
                        _lordAnim = _frame;
                        _stageStart = _frame;
                        break;
                }
            }
        }
        UpdateRainbowFx();
        UpdateProjectiles();

        // ---- Mork
        if (_state != State.Cutscene && _state != State.Ending && _area != Area.SubArea)
        {
            if (_morkState == 0)
            {
                _morkFrame = _frame / 16 & 1;
                _morkTimer -= dtMs;
                if (_morkTimer <= 0)
                {
                    int ddx = _px - _morkX;
                    int ddy = _py - _morkY;
                    int adx = ddx < 0 ? -ddx : ddx;
                    int ady = ddy < 0 ? -ddy : ddy;
                    if (-ddy > _tileH * Layout2.ViewTilesH * 3 / 2 << 4)
                    {
                        _morkY = _py + (_tileH * Layout2.ViewTilesH * 3 / 2 << 4);
                        _morkX = _px;
                    }
                    else if (adx < ady && adx >= _scaleX << 1 || adx <= _scaleX << 1)
                    {
                        _morkTimer = _tileH * 4 * 16;
                        if (ddy < -(_scaleY << 1)) _morkState = 3;
                        else if (ddy > _scaleY << 1) _morkState = 4;
                    }
                    else
                    {
                        _morkTimer = _tileW * 4 * 16;
                        if (ddx < -(_scaleX << 1)) _morkState = 1;
                        else if (ddx > _scaleX << 1) _morkState = 2;
                    }
                }
            }
            else
            {
                int ox = (_morkX >> 4) + (Layout2.LordPoseW >> 1) - ((_px >> 4) + (_playerW >> 1));
                int oy = (_py >> 4) - (_playerH >> 1) - ((_morkY >> 4) - (_lordH >> 1));
                int stepY = _scaleY;
                int stepX = _scaleX;
                int pause = 1000;
                if (_state == State.FinalStage)
                {
                    stepY = stepY * (200 + 200 * _finalRainbows / 10) / 200;
                    stepX = stepX * (200 + 200 * _finalRainbows / 10) / 200;
                    pause = 1000 * (100 - 100 * _finalRainbows / 10) / 140;
                }
                switch (_morkState)
                {
                    case 1:
                        if (_morkX - stepX >= _px) _morkX -= stepX;
                        _morkTimer -= _scaleX;
                        if (_morkX >= _px) break;
                        _morkState = 2;
                        _morkTimer = 0;
                        break;
                    case 2:
                        if (_morkX + stepX <= _px) _morkX += stepX;
                        _morkTimer -= _scaleX;
                        if (_morkX <= _px) break;
                        _morkState = 1;
                        _morkTimer = 0;
                        break;
                    case 3:
                        if (_morkY - stepY >= _py) _morkY -= stepY;
                        _morkTimer -= _scaleY;
                        if (_morkY >= _py) break;
                        _morkState = 4;
                        _morkTimer = 0;
                        break;
                    case 4:
                        if (_morkY + stepY <= _py) _morkY += stepY;
                        _morkTimer -= _scaleY;
                        if (_morkY <= _py) break;
                        _morkState = 3;
                        _morkTimer = 0;
                        break;
                }
                if (_morkTimer <= 0)
                {
                    _morkState = 0;
                    _morkTimer = pause;
                }
                int halfW = Layout2.LordPoseW >> 1;
                int halfH = _lordH >> 1;
                if (ox >= -halfW && ox <= halfW && oy >= -halfH && oy <= halfH)
                {
                    Damage();
                    _morkState = 0;
                    _morkTimer = 7000;
                }
                _morkFrame = _frame / 10 & 1;
            }
        }
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
                    Damage();
                }
            }
        }
        --_spark;
    }
}
