namespace MikisWorld.Core;

/// <summary>Scripted cutscene interpreter (original class <c>e</c>) for begin.bin / end.bin.
/// File: byte actorCount, actorCount x byte spriteType, byte firstEventTime, then events
/// { byte op; args...; byte delay } where delay &lt; 0 ends the script.</summary>
public sealed class CutscenePlayer
{
    private readonly IAssets _assets;
    private readonly IGameHost _host;
    private readonly int _scaleX, _scaleY;      // cl / ck

    private DataReader _script;
    private int _counter, _nextEventAt;

    public bool[] ActorVisible;
    public sbyte[] SpriteType;        // 0-1 player, 2-3 lord, 4-5 items, 6 damage, 7-10 symbols
    public sbyte[] Frame;
    public short[] ActorX, ActorY;
    public bool[] TextVisible;
    private readonly int _stringCount;
    public int CameraX, CameraY;

    public CutscenePlayer(IAssets assets, IGameHost host, int scaleX, int scaleY, int stringCount)
    {
        _assets = assets; _host = host; _scaleX = scaleX; _scaleY = scaleY;
        _stringCount = stringCount;
        TextVisible = new bool[stringCount];
    }

    public bool Finished => _nextEventAt == -1;

    public void Close() { _script = null; }

    public void Load(string name)
    {
        _script = new DataReader(_assets.ReadResource(name));
        TextVisible = new bool[_stringCount];     // original: fresh flags on every load, so no bubble survives into the next cutscene
        int n = _script.ReadByte();
        ActorVisible = new bool[n];
        SpriteType = new sbyte[n];
        Frame = new sbyte[n];
        ActorX = new short[n];
        ActorY = new short[n];
        for (int i = 0; i < n; i++) SpriteType[i] = _script.ReadByte();
        _script.ReadByte();            // time of the first event: read, then discarded (original calls reset() right after)
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
                case 2: CameraX = _script.ReadShort() * _scaleX >> 4; CameraY = _script.ReadShort() * _scaleY >> 4; break;
                case 3:
                {
                    sbyte a = _script.ReadByte(); short dx = _script.ReadByte(); short dy = _script.ReadByte(); sbyte f = _script.ReadByte();
                    ActorX[a] = (short)(ActorX[a] + dx);
                    ActorY[a] = (short)(ActorY[a] + dy);
                    Frame[a] = f;
                    break;
                }
                case 4: PlaySound(_script.ReadByte()); break;
                case 5: _script.ReadByte(); break;
                case 6: { sbyte s = _script.ReadByte(); TextVisible[s] = true; break; }
                case 7: { sbyte s = _script.ReadByte(); TextVisible[s] = false; break; }
            }
            _counter = _nextEventAt;
            sbyte delay = _script.ReadByte();
            if (delay >= 0) _nextEventAt += delay; else _nextEventAt = -1;
        }
    }

    private void PlaySound(int id)
    {
        switch (id)
        {
            case 0: _host.Play(Sfx.Cage); break;
            case 1: _host.Play(Sfx.Lord); break;
            case 2: _host.Play(Sfx.Damage); break;
        }
    }
}
