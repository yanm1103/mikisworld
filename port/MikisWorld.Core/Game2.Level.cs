namespace MikisWorld.Core;

public sealed partial class Game2
{
    private bool _levelLoaded;

    // Original c.e(): reset the run and load level.bin.
    private void LoadLevel()
    {
        _levelLoaded = true;
        _gameOverTimer = 0;
        _nextProj = 0;
        _interact = -1;
        _hintTimer = 0;
        _interactPressed = false;
        _lives = 3;
        _standingOn = -1;
        _keyTaken = false;
        _visFirst = 0;
        _projPos = new short[6];
        _projDir = new sbyte[3];
        _projState = new sbyte[3];
        _spark = -1;
        _events = new int[80];

        var player = _assets.ImageSize("player.png");
        var items = _assets.ImageSize("items.png");
        var damage = _assets.ImageSize("damage.png");
        var lord = _assets.ImageSize("lord.png");
        bool first = _scaleY == 0;
        _scaleX = 16 * _tileW / 8;
        _scaleY = 16 * _tileH / 8;
        if (first)
            for (int i = 0; i < _jumpTable.Length; i++) _jumpTable[i] = _jumpTable[i] * _scaleY >> 4;
        _airSpeed = 1 * _scaleX * 90 / 100;
        _walkSpeed = 2 * _scaleX * 90 / 100;
        _fallSpeed = 2 * _scaleY;
        _riseSpeed = _scaleY * 240 / 100;
        _itemW = items.Width / 5;
        _itemH = items.Height;
        _playerH = player.Height;
        _lordH = lord.Height;
        _hitFxDx = (player.Width / 6 - damage.Width / 2) / 2;
        _hitFxDy = (player.Height / 8 - damage.Height / 2) / 2;
        Cutscene.ScaleX = _scaleX;
        Cutscene.ScaleY = _scaleY;

        _stage = 0;
        _stageStart = 0;
        _motion = 0;
        _rainbows = 0;
        _finalRainbows = 0;
        _finalTimer = 60000;
        _fxX = new int[4];
        _fxY = new int[4];
        for (int i = 0; i < 4; i++) _fxX[i] = -1;
        _eventIdx = 0;
        for (int i = 0; i < _events.Length; i++) _events[i] = -1;

        var r = new DataReader(_assets.ReadResource("level.bin"));
        int count = r.ReadByte();
        var flags = new sbyte[count];
        for (int i = 0; i < count; i++) flags[i] = r.ReadByte();
        _lord1Platform = r.ReadByte();
        int n = r.ReadByte();
        for (int i = 0; i < n; i++) { int part = r.ReadByte(); _lord1Release <<= 10; _lord1Release |= part; }
        _lord1X = (short)(r.ReadShort() * _scaleX / 16);
        _lord1Y = (short)(r.ReadShort() * _scaleY / 16);
        _lord2Platform = r.ReadByte();
        n = r.ReadByte();
        for (int i = 0; i < n; i++) { int part = r.ReadByte(); _lord2Release <<= 10; _lord2Release |= part; }
        _lord2X = (short)(r.ReadShort() * _scaleX / 16);
        _lord2Y = (short)(r.ReadShort() * _scaleY / 16);
        _startMainX = (short)(r.ReadShort() * _scaleX);
        _startMainY = (short)(r.ReadShort() * _scaleY);
        _morkX = _startMainX;
        _morkY = _startMainY + 10 * _tileH * 16;
        _morkFrame = 0;
        _startFinalX = (short)(r.ReadShort() * _scaleX);
        _startFinalY = (short)(r.ReadShort() * _scaleY);
        int k = r.ReadByte();
        _subAreaX = new short[k];
        _subAreaY = new short[k];
        for (int i = 0; i < k; i++)
        {
            _subAreaX[i] = (short)(r.ReadShort() * _scaleX);
            _subAreaY[i] = (short)(r.ReadShort() * _scaleY);
        }
        _mainBottomRow = r.ReadShort();
        _subBottomRow = r.ReadShort();
        _subAreaRows = new short[k];
        for (int i = 0; i < k; i++) _subAreaRows[i] = r.ReadShort();
        int m = r.ReadByte();
        _finalBlockCells = new short[m];
        for (int i = 0; i < m; i++) _finalBlockCells[i] = r.ReadShort();
        _totalCoins = r.ReadByte();
        _finalLordX = (short)(r.ReadShort() * _scaleX / 16);
        _finalLordY = (short)(r.ReadShort() * _scaleY / 16);

        _map.LoadLayerV2(r);
        _collision.Setup(_map.Tiles, _map.Width, _map.Height, _tileW, _tileH, _tileFlags);
        _scrollLockX = false;
        _scrollLockY = false;
        _outerX = Layout2.ScreenW / 2 - 2;
        _outerY = Layout2.ScreenW / 2 - 2;      // sic: derived from the width, as in the first game
        _innerX = Layout2.ScreenW / 2 - 2;
        _innerY = Layout2.ScreenH / 2 - 2;
        _camMaxX = _map.Width * _tileW - Layout2.ScreenW;
        if (_camMaxX < 0) _camMaxX = 0;
        _collision.SetShift(4);

        ScanLevel(flags);

        _savedPx = -1;
        _savedPy = -1;
        EnterArea(0);
        UpdateVisibleWindow(_camY, _camY + Layout2.ScreenH + _tileH);
        _accum = 40;
    }

    // Original c.R4(byte[]): marker tiles -> platforms, animated tile list; rainbows get their right half (tile 21).
    private void ScanLevel(sbyte[] platformFlags)
    {
        var animated = new List<int>();
        var portals = new List<int>();
        var markers = new List<int>();
        int[] tiles = _map.Tiles;
        int w = _map.Width, h = _map.Height;
        int idx = 0;
        _totalCoins = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++, idx++)
            {
                if (tiles[idx] == Tile2.Cloud)
                {
                    markers.Add(x);
                    markers.Add(y);
                    int len = 0;
                    while (x < w && tiles[idx] == Tile2.Cloud)
                    {
                        tiles[idx] = 0;
                        len++; idx++; x++;
                    }
                    if (x == w) idx--;
                    markers.Add(len);
                }
                else if (tiles[idx] == Tile2.GateTL)
                {
                    _gateX = (short)x;
                    _gateY = (short)y;
                }
                else if (tiles[idx] == Tile2.PortalA)
                {
                    portals.Add(idx);
                    animated.Add(idx);
                }
                else if (tiles[idx] == Tile2.Coin)
                {
                    _totalCoins++;
                    animated.Add(idx);
                }
                else if (tiles[idx] == Tile2.RainbowL)
                {
                    _map.Set(x + 1, y, Tile2.RainbowR);
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

    // Original c.R1(int,int)
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

    /// <summary>Original c.R10(int): -2/-1 cutscenes, 0 main, 1 final stage, 2+ sub-areas (portals).</summary>
    public void EnterArea(int n)
    {
        bool placeCamera = true;
        if (n < 0)
        {
            placeCamera = false;
            _camMinY = 0;
            _camMaxY = _map.Height * _tileH - Layout2.ScreenH - 1;
            _area = (sbyte)n;
            _state = State.Cutscene;
            if (n == -1) _host.Play(Sfx.OnceUponATime);
            else if (n == -2) _host.Play(Sfx.GameEnd);
        }
        else if (n == 0)
        {
            _camMinY = 0;
            _camMaxY = _mainBottomRow * _tileH - Layout2.ScreenH - 1;
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
            _camMaxY = _subBottomRow * _tileH - Layout2.ScreenH - 1;
            _px = _startFinalX;
            _py = _startFinalY;
            _finalTimer = 67000;
            _lordX = _finalLordX;
            _lordY = _finalLordY;
            _morkX = _startFinalX;
            _morkY = _startFinalY + 5 * _tileH * 16;
            int[] tiles = _map.Tiles;
            for (int i = 0; i < _rainbows && i < _finalBlockCells.Length; i++)
            {
                int c = _finalBlockCells[i];
                tiles[c] = Tile2.Ledge;
                tiles[c + 1] = Tile2.Ledge;
            }
            _finalRainbows = 0;
            for (int i = 0; i < _projState.Length; i++) _projState[i] = -1;
            _state = State.FinalStage;
            _area = Area.Final;
            _host.Play(Sfx.Lord);
        }
        else
        {
            _camMinY = n == 2 ? _subBottomRow * _tileH : _subAreaRows[n - 3] * _tileH;
            _camMaxY = _subAreaRows[n - 2] * _tileH - Layout2.ScreenH - 1;
            _savedPx = _px;
            _savedPy = _py;
            _px = _subAreaX[n - 2];
            _py = _subAreaY[n - 2];
            _area = Area.SubArea;
        }
        if (placeCamera)
        {
            _camX = (_px >> 4) + _playerW / 2 - _outerX;
            _camY = (_py >> 4) - Layout2.ScreenH / 2;
        }
        if (_camX < 0) _camX = 0;
        if (_camX > _camMaxX) _camX = _camMaxX;
        if (_camY < _camMinY) _camY = _camMinY;
        if (_camY > _camMaxY) _camY = _camMaxY;
        for (int i = 0; i < _projPos.Length; i++) _projPos[i] = -1;
        _spark = -1;
        for (int i = 0; i < _projDir.Length; i++)
        {
            _projDir[i] = 0;
            _projState[i] = -1;
        }
    }
}
