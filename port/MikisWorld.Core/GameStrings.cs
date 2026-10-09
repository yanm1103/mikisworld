namespace MikisWorld.Core;

/// <summary>Localized strings (resource "/en" etc.): byte count, then count x UTF strings.
/// The first <c>uiCount</c> entries are menu/UI strings (16 in the first game, 18 in the second); the rest are in-game
/// text bubbles (original GameMIDlet.a(i) = entry uiCount+i).</summary>
public sealed class GameStrings
{
    private readonly string[] _s;
    private readonly int _uiCount;
    private GameStrings(string[] s, int uiCount) { _s = s; _uiCount = uiCount; }
    public int Count => _s.Length;
    public string Ui(int i) => _s[i];
    public string Text(int i) => _s[_uiCount + i];
    public int TextCount => _s.Length - _uiCount;

    public static GameStrings Load(IAssets assets, string locale = "en", int uiCount = 16)
    {
        byte[] data;
        try { data = assets.ReadResource(locale); } catch { data = assets.ReadResource("en"); }
        var r = new DataReader(data);
        int n = r.ReadByte();
        var s = new string[n];
        for (int i = 0; i < n; i++) s[i] = r.ReadUTF();
        return new GameStrings(s, uiCount);
    }

    public static string LoadHelp(IAssets assets, string locale = "en")
    {
        byte[] data;
        try { data = assets.ReadResource(locale + ".hlp"); } catch { data = assets.ReadResource("en.hlp"); }
        return new DataReader(data).ReadUTF();
    }
}
