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

    public bool Exists(string name) => _zip.GetEntry(name) != null;
    public (int Width, int Height) ImageSize(string name) => ImageDecode.Size(ReadResource(name));
    public Bitmap LoadBitmap(string name) => ImageDecode.Load(ReadResource(name));

    public void Dispose() => _zip.Dispose();
}
