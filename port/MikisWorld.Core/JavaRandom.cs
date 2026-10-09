namespace MikisWorld.Core;

/// <summary>Bit-exact java.util.Random (48-bit LCG). The original game seeds it from the clock;
/// a fixed seed makes runs reproducible and lets us diff against traces of the original bytecode.</summary>
public sealed class JavaRandom
{
    private long _seed;

    public JavaRandom(long seed) { _seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1); }

    private int Next(int bits)
    {
        _seed = (_seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1);
        return (int)(_seed >> (48 - bits));
    }

    public int NextInt() => Next(32);
}
