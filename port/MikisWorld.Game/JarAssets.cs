using System.IO.Compression;
using MikisWorld.Core;
using StbImageSharp;

namespace MikisWorld.Game;

/// <summary>Reads the game's resources straight out of the original JAR, so the repository does not need to
/// redistribute the artwork/data (the user supplies MikisWorld.jar).</summary>
internal sealed class JarAssets : IAssets, IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, byte[]> _cache = new();

    public JarAssets(string jarPath) { _zip = ZipFile.OpenRead(jarPath); }

    public byte[] ReadResource(string name)
    {
        if (_cache.TryGetValue(name, out var data)) return data;
        var e = _zip.GetEntry(name) ?? throw new FileNotFoundException($"'{name}' not found in the JAR");
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return _cache[name] = ms.ToArray();
    }

    public (int Width, int Height) ImageSize(string name)
    {
        byte[] b = ReadResource(name);
        return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
    }

    public Bitmap LoadBitmap(string name)
    {
        var img = ImageResult.FromMemory(ReadResource(name), ColorComponents.RedGreenBlueAlpha);
        var px = new int[img.Width * img.Height];
        for (int i = 0; i < px.Length; i++)
            px[i] = (img.Data[i * 4 + 3] << 24) | (img.Data[i * 4] << 16) | (img.Data[i * 4 + 1] << 8) | img.Data[i * 4 + 2];
        return new Bitmap(img.Width, img.Height, px);
    }

    public void Dispose() => _zip.Dispose();
}
