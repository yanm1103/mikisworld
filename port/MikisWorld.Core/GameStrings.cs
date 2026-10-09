namespace MikisWorld.Core;

/// <summary>Localized strings (resource "/en" etc.): byte count, then count x UTF strings.
/// Entries 0-15 are menu/UI strings, 16+ are in-game text bubbles (original GameMIDlet.a(i) = entry 16+i).</summary>
public sealed class GameStrings
{
    private readonly string[] _s;
    private GameStrings(string[] s) { _s = s; }
    public int Count => _s.Length;
    public string Ui(int i) => _s[i];
    public string Text(int i) => _s[16 + i];
    public int TextCount => _s.Length - 16;

    public static GameStrings Load(IAssets assets, string locale = "en")
    {
        byte[] data;
        try { data = assets.ReadResource(locale); } catch { data = assets.ReadResource("en"); }
        var r = new DataReader(data);
        int n = r.ReadByte();
        var s = new string[n];
        for (int i = 0; i < n; i++) s[i] = r.ReadUTF();
        return new GameStrings(s);
    }

    public static string LoadHelp(IAssets assets, string locale = "en")
    {
        byte[] data;
        try { data = assets.ReadResource(locale + ".hlp"); } catch { data = assets.ReadResource("en.hlp"); }
        return new DataReader(data).ReadUTF();
    }
}
