namespace MikisWorld.Core;

/// <summary>Tile map (original class <c>a</c>): level data only; drawing lives in the renderer.</summary>
public sealed class TileLayer
{
    public int[] Tiles;     // row-major tile ids
    public int Width, Height;

    public int this[int x, int y]
    {
        get => Tiles[x + y * Width];
        set => Tiles[x + y * Width] = value;
    }

    public void Set(int x, int y, int tile) => Tiles[x + y * Width] = tile;

    /// <summary>Reads the layer container used by level.bin: byte layerCount, int offsets[], then layer 0 at offsets[0].
    /// (Original a.a(DataInputStream, int) with layer index 0.)</summary>
    public void LoadLayer(DataReader r, int layer)
    {
        int start = r.Position;
        r.ReadByte();                      // layer count
        r.Skip(layer * 4);
        int off = r.ReadInt();
        r.Skip(off - layer * 4 - 1 - 4);
        Load(r);
        _ = start;
    }

    /// <summary>MikisWorld 2's container: byte layerCount, then (layerCount-1)*2 bytes, then layer 0 directly.</summary>
    public void LoadLayerV2(DataReader r)
    {
        int layers = r.ReadByte();
        r.Skip((layers - 1) * 2);
        Load(r);
    }

    /// <summary>short width, short height, byte bitsPerTile (4 or 8), then packed ints, MSB first.</summary>
    public void Load(DataReader r)
    {
        Width = r.ReadShort();
        Height = r.ReadShort();
        int bpp = r.ReadByte();
        if (Tiles == null || Tiles.Length != Width * Height) Tiles = new int[Width * Height];
        int perInt = 8, bits = 4, mask = 15;
        if (bpp == 8) { perInt = 4; bits = 8; mask = 255; }
        int i = 0;
        while (i < Width * Height)
        {
            int v = r.ReadInt();
            for (int j = 0; j < perInt && i < Tiles.Length; j++, i++)
                Tiles[i] = (sbyte)(v >> (32 - (j + 1) * bits) & mask);
        }
    }
}
