namespace MikisWorld.Core;

public sealed partial class Game
{
    // Original d.c(): reset run state and load level.bin.
    private bool _levelLoaded;        // debug only: the original sets a0/aw inside c()

    protected override void LoadLevel()
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
        _stage = 0;
        _motion = 0;
        _birds = 0;
        _finalDoor = 0;
        _finalTimer = 60000;

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
        for (int i = 0; i < _finalBlockCells.Length; i++) _finalBlockCells[i] = r.ReadShort();
        int q = r.ReadByte();
        _finalBirdCells = new short[q];
        for (int i = 0; i < _finalBirdCells.Length; i++) _finalBirdCells[i] = r.ReadShort();
        _totalCoins = r.ReadByte();
        _finalLordX = (short)(r.ReadShort() * _scaleX / 16);
        _finalLordY = (short)(r.ReadShort() * _scaleY / 16);

        _map.LoadLayer(r, 0);
        _collision.Setup(_map.Tiles, _map.Width, _map.Height, _tileW, _tileH, _tileFlags);
        _scrollLockX = false;
        _scrollLockY = false;
        _outerX = Layout.ScreenW / 2 - 2;
        _outerY = Layout.ScreenW / 2 - 2;    // sic: derived from the screen *width* in the original
        _innerX = Layout.ScreenW / 2 - 2;
        _innerY = Layout.ScreenH / 2 - 2;
        _camMaxX = _map.Width * _tileW - Layout.ScreenW;
        if (_camMaxX < 0) _camMaxX = 0;
        _collision.SetShift(4);

        ScanLevel(flags);

        _savedPx = -1;
        _savedPy = -1;
        EnterArea(0);
        _birdFxX = -1;
        _birdFxY = -1;
        UpdateVisibleWindow(_camY, _camY + Layout.ScreenH + _tileH);
        _accum = 40;
    }

    // Original d.if(byte[]): turn marker tiles into platforms / animated tile lists.
    private void ScanLevel(sbyte[] platformFlags)
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
                else if (tiles[idx] == Tile.GateMarker)
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
                else if (tiles[idx] == Tile.CagedBird)
                {
                    _map.Set(x, y - 1, Tile.BirdSign);
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
