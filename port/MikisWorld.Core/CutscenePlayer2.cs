namespace MikisWorld.Core;

/// <summary>Cutscene interpreter of MikisWorld 2 (original class <c>d</c>). Same script format as the first game with new
/// opcodes: 6 reads an extra RGB int for the speech bubble, 8 places the closed gate (tiles 16-19) in the map, and only
/// sound id 2 (damage + vibration) is played.</summary>
public sealed class CutscenePlayer2
{
    private readonly IAssets _assets;
    private readonly IGameHost _host;
    private readonly Action<int, int, int> _setTile;
    private DataReader _script;
    private int _counter, _nextEventAt;

    public bool[] ActorVisible;
    public sbyte[] SpriteType;
    public sbyte[] Frame;
    public short[] ActorX, ActorY;
    public bool[] TextVisible;
    public int BubbleR, BubbleG, BubbleB;   // one colour for all bubbles: the last opcode-6 value (as in the original)
    public int CameraX, CameraY;
    public int ScaleX = 24, ScaleY = 24;     // set by the game (cy / cx)

    public CutscenePlayer2(IAssets assets, IGameHost host, int stringCount, Action<int, int, int> setTile)
    {
        _assets = assets; _host = host; _setTile = setTile;
        TextVisible = new bool[stringCount];
    }

    public bool Finished => _nextEventAt == -1;
    public void Close() { _script = null; }

    public void Load(string name)
    {
        _script = new DataReader(_assets.ReadResource(name));
        int n = _script.ReadByte();
        ActorVisible = new bool[n];
        SpriteType = new sbyte[n];
        Frame = new sbyte[n];
        ActorX = new short[n];
        ActorY = new short[n];
        for (int i = 0; i < n; i++) SpriteType[i] = _script.ReadByte();
        _script.ReadByte();            // first event time: read, then discarded (reset() follows in the original)
        _counter = 0;
        _nextEventAt = 0;
    }

    public void Update()
    {
        _counter++;
        while (_nextEventAt != -1 && _counter >= _nextEventAt)
        {
            sbyte op = _script.ReadByte();
            switch (op)
            {
                case 0: { sbyte a = _script.ReadByte(); short x = _script.ReadShort(), y = _script.ReadShort(); ActorX[a] = x; ActorY[a] = y; ActorVisible[a] = true; break; }
                case 1: { sbyte a = _script.ReadByte(); ActorVisible[a] = false; break; }
                case 2: CameraX = _script.ReadShort() * ScaleX >> 4; CameraY = _script.ReadShort() * ScaleY >> 4; break;
                case 3:
                    {
                        sbyte a = _script.ReadByte(); int dx = _script.ReadByte(); short dy = _script.ReadByte(); sbyte f = _script.ReadByte();
                        ActorX[a] = (short)(ActorX[a] + dx);
                        ActorY[a] = (short)(ActorY[a] + dy);
                        Frame[a] = f;
                        break;
                    }
                case 4:
                    {
                        sbyte id = _script.ReadByte();
                        if (id == 2) { _host.Play(Sfx.Damage); _host.Vibrate(100, 200); }
                        break;
                    }
                case 5: _script.ReadByte(); break;
                case 6: { sbyte s = _script.ReadByte(); int rgb = _script.ReadInt(); BubbleR = rgb >> 16 & 0xFF; BubbleG = rgb >> 8 & 0xFF; BubbleB = rgb & 0xFF; TextVisible[s] = true; break; }
                case 7: { sbyte s = _script.ReadByte(); TextVisible[s] = false; break; }
                case 8:
                    {
                        int x = _script.ReadShort(), y = _script.ReadShort();
                        _setTile(x, y, Tile2.GateTL);
                        _setTile(x + 1, y, Tile2.GateTR);
                        _setTile(x, y + 1, Tile2.GateBL);
                        _setTile(x + 1, y + 1, Tile2.GateBR);
                        break;
                    }
            }
            _counter = _nextEventAt;
            sbyte delay = _script.ReadByte();
            if (delay >= 0) _nextEventAt += delay; else _nextEventAt = -1;
        }
    }
}
