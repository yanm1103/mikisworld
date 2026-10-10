using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MikisWorld.Core;
using StbImageSharp;

namespace MikisWorld.Game;

/// <summary>PNG decoding shared by the resource readers (the JAR, the mod folders).</summary>
internal static class ImageDecode
{
    public static (int Width, int Height) Size(byte[] b)
        => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);

    public static Bitmap Load(byte[] bytes)
    {
        var img = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        var px = new int[img.Width * img.Height];
        for (int i = 0; i < px.Length; i++)
            px[i] = (img.Data[i * 4 + 3] << 24) | (img.Data[i * 4] << 16) | (img.Data[i * 4 + 1] << 8) | img.Data[i * 4 + 2];
        return new Bitmap(img.Width, img.Height, px);
    }
}

/// <summary>One mod: a folder with a <c>mod.json</c> and files named like the game's own resources (<c>player.png</c>, <c>coin.ott</c>, <c>level.bin</c>...).
/// Files in the folder itself apply to every game the mod targets; <c>game1/</c> and <c>game2/</c> hold files for one game only.</summary>
internal sealed class ModInfo
{
    public string Id { get; init; }                  // folder name
    public string Dir { get; init; }
    public string Name { get; init; }
    public string Author { get; init; }
    public string Version { get; init; }
    public string Description { get; init; }
    public int Game { get; init; }                   // 0 = both games, 1 or 2
    public List<string> Problems { get; } = new();   // the manifest could not be read, etc.

    public bool Targets(int game) => Game == 0 || Game == game;

    private static readonly HashSet<string> Cosmetic = new(StringComparer.OrdinalIgnoreCase) { ".png", ".ott", ".wav", ".ogg" };

    /// <summary>Files of the mod as (game folder or 0, resource name, full path).</summary>
    public IEnumerable<(int Game, string Name, string Path)> Files()
    {
        foreach (string f in Directory.EnumerateFiles(Dir))
            if (Path.GetFileName(f) != "mod.json") yield return (0, Path.GetFileName(f), f);
        for (int g = 1; g <= 2; g++)
        {
            string sub = System.IO.Path.Combine(Dir, "game" + g);
            if (!Directory.Exists(sub)) continue;
            foreach (string f in Directory.EnumerateFiles(sub)) yield return (g, Path.GetFileName(f), f);
        }
    }

    /// <summary>Only pictures and sounds: the story, the rules and the levels are the original's, so the saves of the original stay valid.</summary>
    public bool IsCosmetic(int game) => Files().Where(f => (f.Game == 0 || f.Game == game) && Targets(game)).All(f => Cosmetic.Contains(Path.GetExtension(f.Name)));
}

/// <summary>Finds the mods, remembers which ones are on (and their order: later ones win), checks them against the game, and builds the
/// resource readers that put them over the original files.</summary>
internal sealed class ModManager
{
    private readonly List<string> _roots;
    private readonly string _statePath;
    public List<ModInfo> Mods { get; private set; } = new();
    private readonly List<string> _order = new();       // ids, lowest priority first
    private readonly HashSet<string> _enabled = new();
    public bool Disabled { get; init; }                 // --no-mods

    public ModManager(IEnumerable<string> roots, string statePath)
    {
        _roots = roots.ToList();
        _statePath = statePath;
        LoadState();
        Refresh();
    }

    public static string[] DefaultRoots(string exeDir) => new[]
    {
        Path.Combine(exeDir, "mods"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", "mods"),
    };

    // ------------------------------------------------------------------ discovery
    public void Refresh()
    {
        var found = new List<ModInfo>();
        foreach (string root in _roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (string dir in Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string id = Path.GetFileName(dir);
                if (found.Any(m => m.Id == id)) continue;                       // the first root wins
                found.Add(Read(id, dir));
            }
        }
        // saved order first, then the new ones alphabetically
        Mods = found.OrderBy(m => { int i = _order.IndexOf(m.Id); return i < 0 ? int.MaxValue : i; }).ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToList();
        _order.Clear();
        _order.AddRange(Mods.Select(m => m.Id));
        _enabled.IntersectWith(_order);
    }

    private sealed record Manifest(string Name = null, string Author = null, string Version = null, string Description = null, int Game = 0);

    private static ModInfo Read(string id, string dir)
    {
        Manifest m = new();
        string problem = null;
        string file = Path.Combine(dir, "mod.json");
        try
        {
            if (File.Exists(file)) m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? m;
            else problem = "no mod.json (using the folder name)";
        }
        catch (Exception e) when (e is JsonException or IOException) { problem = "mod.json could not be read: " + e.Message; }
        if (m.Game is < 0 or > 2) { problem = "game must be 0 (both), 1 or 2"; m = m with { Game = 0 }; }
        var info = new ModInfo { Id = id, Dir = dir, Name = string.IsNullOrWhiteSpace(m.Name) ? id : m.Name, Author = m.Author ?? "", Version = m.Version ?? "", Description = m.Description ?? "", Game = m.Game };
        if (problem != null) info.Problems.Add(problem);
        return info;
    }

    // ------------------------------------------------------------------ what is on
    public bool IsEnabled(ModInfo m) => !Disabled && _enabled.Contains(m.Id);

    public void SetEnabled(ModInfo m, bool on)
    {
        if (on) _enabled.Add(m.Id); else _enabled.Remove(m.Id);
        SaveState();
    }

    /// <summary>Moves a mod in the priority order (a mod further down the list wins over the ones above it).</summary>
    public void Move(ModInfo m, int delta)
    {
        int i = Mods.IndexOf(m), j = Math.Clamp(i + delta, 0, Mods.Count - 1);
        if (i < 0 || i == j) return;
        Mods.RemoveAt(i);
        Mods.Insert(j, m);
        _order.Clear();
        _order.AddRange(Mods.Select(x => x.Id));
        SaveState();
    }

    /// <summary>The mods that apply to a game, lowest priority first.</summary>
    public List<ModInfo> ActiveFor(int game) => Mods.Where(m => IsEnabled(m) && m.Targets(game)).ToList();

    /// <summary>Part of the save file name: empty with no mods (or only pictures and sounds), else a short code of the mods that change the game itself,
    /// so a game with different levels or rules keeps its own high scores and unlocked bonus.</summary>
    public string SaveKey(int game)
    {
        var data = ActiveFor(game).Where(m => !m.IsCosmetic(game)).Select(m => m.Id + "@" + m.Version).ToList();
        if (data.Count == 0) return "";
        byte[] h = SHA1.HashData(Encoding.UTF8.GetBytes(string.Join("|", data)));
        return Convert.ToHexString(h)[..8].ToLowerInvariant();
    }

    // ------------------------------------------------------------------ persistence
    private sealed record State(List<string> Order = null, List<string> Enabled = null);

    private void LoadState()
    {
        try
        {
            if (!File.Exists(_statePath)) return;
            var s = JsonSerializer.Deserialize<State>(File.ReadAllText(_statePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (s?.Order != null) _order.AddRange(s.Order);
            if (s?.Enabled != null) foreach (string e in s.Enabled) _enabled.Add(e);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(new State(_order.ToList(), _enabled.ToList()), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // ------------------------------------------------------------------ checking a mod against the game
    /// <summary>Things that are likely to go wrong, in plain words (empty = nothing suspicious). <paramref name="original"/> reads the game's own files.</summary>
    public static List<string> Check(ModInfo mod, Func<int, IAssets> original)
    {
        var notes = new List<string>(mod.Problems);
        foreach (var (game, name, path) in mod.Files())
        {
            var games = game != 0 ? new[] { game } : mod.Game == 0 ? new[] { 1, 2 } : new[] { mod.Game };
            string ext = Path.GetExtension(name).ToLowerInvariant();
            foreach (int g in games.Where(mod.Targets))
            {
                var orig = original(g);
                string label = (game == 0 ? "" : $"game{game}/") + name;
                bool known = orig.Exists(name);
                switch (ext)
                {
                    case ".png":
                        if (!known) { notes.Add($"{label}: the game has no picture with this name, it is not used"); break; }
                        try
                        {
                            var have = ImageDecode.Size(File.ReadAllBytes(path));
                            var want = orig.ImageSize(name);
                            if (have != want) notes.Add($"{label}: {have.Width}x{have.Height}, the game expects {want.Width}x{want.Height} (game {g})");
                        }
                        catch (Exception e) when (e is IOException or IndexOutOfRangeException) { notes.Add($"{label}: cannot be read as a PNG"); }
                        break;
                    case ".ott":
                        if (!known) { notes.Add($"{label}: the game has no sound with this name, it is not used"); break; }
                        try { RingTone.Decode(File.ReadAllBytes(path)); }
                        catch (Exception e) when (e is IOException or IndexOutOfRangeException or InvalidDataException or ArgumentException) { notes.Add($"{label}: cannot be read as a ring tone"); }
                        break;
                    case ".wav":
                    case ".ogg":
                        if (!orig.Exists(Path.GetFileNameWithoutExtension(name) + ".ott")) notes.Add($"{label}: no sound of the game has this name, it is not used");
                        break;
                    default:
                        if (!known) notes.Add($"{label}: the game has no resource with this name, it is not used");
                        break;
                }
            }
        }
        return notes.Distinct().ToList();
    }
}

/// <summary>The game's resources with the mods put over them: for each name, the last mod that has the file wins, then the original.
/// Files are read from disk on every request (no cache), so a changed file shows up after a reload.</summary>
internal sealed class ModAssets : IAssets, IDisposable
{
    private readonly IAssets _inner;
    private readonly List<ModInfo> _mods;
    private readonly int _game;
    private readonly IDisposable _owned;

    public ModAssets(IAssets inner, IEnumerable<ModInfo> mods, int game, IDisposable owned = null)
    {
        _inner = inner; _mods = mods.ToList(); _game = game; _owned = owned;
    }

    public int Count => _mods.Count;

    private string Find(string name)
    {
        for (int i = _mods.Count - 1; i >= 0; i--)
        {
            string p = Path.Combine(_mods[i].Dir, "game" + _game, name);
            if (File.Exists(p)) return p;
            p = Path.Combine(_mods[i].Dir, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public byte[] ReadResource(string name)
    {
        string p = Find(name);
        return p != null ? File.ReadAllBytes(p) : _inner.ReadResource(name);
    }

    public bool Exists(string name) => Find(name) != null || _inner.Exists(name);
    public (int Width, int Height) ImageSize(string name) => ImageDecode.Size(ReadResource(name));
    public Bitmap LoadBitmap(string name) => ImageDecode.Load(ReadResource(name));
    public void Dispose() => _owned?.Dispose();
}
