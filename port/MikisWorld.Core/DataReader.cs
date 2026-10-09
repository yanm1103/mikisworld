using System.Text;

namespace MikisWorld.Core;

/// <summary>Big-endian reader with java.io.DataInputStream semantics (signed bytes/shorts, modified-UTF strings).</summary>
public sealed class DataReader
{
    private readonly byte[] _b;
    public int Position;

    public DataReader(byte[] data) { _b = data; }
    public int Length => _b.Length;
    public int Remaining => _b.Length - Position;

    public sbyte ReadByte() => unchecked((sbyte)_b[Position++]);
    public bool ReadBoolean() => _b[Position++] != 0;
    public short ReadShort() { short v = (short)((_b[Position] << 8) | _b[Position + 1]); Position += 2; return v; }
    public int ReadInt()
    {
        int v = (_b[Position] << 24) | (_b[Position + 1] << 16) | (_b[Position + 2] << 8) | _b[Position + 3];
        Position += 4;
        return v;
    }
    public void Skip(int n) { Position += n; }

    public string ReadUTF()
    {
        int len = (_b[Position] << 8) | _b[Position + 1];
        Position += 2;
        // Strings in this game are plain ASCII/Latin-1; modified-UTF-8 only differs for NUL and supplementary chars.
        string s = Encoding.UTF8.GetString(_b, Position, len);
        Position += len;
        return s;
    }
}
