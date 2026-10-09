using MikisWorld.Core;

// Replays an input script on the C# port and compares it with a trace recorded from the original JAR
// (tools/harness/src/Trace.java).
//
//   MikisWorld.TraceTest <assetsDir> <mode main|bonus> <seed> <ticks> <script|-> <startArea|-> <expected.trace>
//
// Exit code 0 = every line identical (incl. a matching "#EXCEPTION" at the same tick), 1 = first difference printed.

internal sealed class DirAssets : IAssets
{
    private readonly string _dir;
    public DirAssets(string dir) { _dir = dir; }
    public byte[] ReadResource(string name) => File.ReadAllBytes(Path.Combine(_dir, name));
    public (int Width, int Height) ImageSize(string name)
    {
        // PNG: 8-byte signature, then IHDR with big-endian width/height at offset 16/20
        byte[] b = File.ReadAllBytes(Path.Combine(_dir, name));
        int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return (w, h);
    }

    public Bitmap LoadBitmap(string name)
    {
        using var s = File.OpenRead(Path.Combine(_dir, name));
        var img = StbImageSharp.ImageResult.FromStream(s, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        var px = new int[img.Width * img.Height];
        for (int i = 0; i < px.Length; i++)
            px[i] = (img.Data[i * 4 + 3] << 24) | (img.Data[i * 4] << 16) | (img.Data[i * 4 + 1] << 8) | img.Data[i * 4 + 2];
        return new Bitmap(img.Width, img.Height, px);
    }
}

internal static class Program
{
    private static int GameAction(int k) => k switch
    {
        -1 or 50 => 1, -2 or 56 => 6, -3 or 52 => 2, -4 or 54 => 5, -5 or 53 => 8, _ => 0
    };

    // Differential test of the hall-of-fame logic against the original class k (tools/harness/src/HallTest.java).
    //   MikisWorld.TraceTest hall <seed> <steps> <expected.txt>
    private static int HallTest(string[] a)
    {
        var rnd = new JavaRandom(long.Parse(a[1]));
        int steps = int.Parse(a[2]);
        string[] expected = File.ReadAllLines(a[3]);
        string[] names = { "bob", "AMY", " ", null, "", "zed", "a b", "mike" };
        var hof = new HallOfFame("aaa", 0, 1, 10, true);
        for (int i = 0; i < steps; i++)
        {
            string n = names[(rnd.NextInt() & 0x7fffffff) % names.Length];
            int s = (rnd.NextInt() & 0x7fffffff) % 25;
            bool high = hof.IsHighScore(0, s);
            if (high || (rnd.NextInt() & 1) == 0) hof.Insert(0, n, s);
            var sb = new System.Text.StringBuilder().Append(i).Append(' ').Append(s).Append(' ').Append(high ? "true" : "false").Append(" |");
            for (int j = 0; j < 10; j++) sb.Append(' ').Append(hof.Name(0, j)).Append('=').Append(hof.Score(0, j));
            if (sb.ToString() != expected[i]) { Console.WriteLine($"MISMATCH step {i}"); Console.WriteLine("  expected: " + expected[i]); Console.WriteLine("  got     : " + sb); return 1; }
        }
        Console.WriteLine($"OK  hall of fame, {steps} steps identical");
        return 0;
    }

    private static int Main(string[] a)
    {
        if (a[0] == "hall") return HallTest(a);
        string dir = a[0], mode = a[1];
        long seed = long.Parse(a[2]);
        int ticks = int.Parse(a[3]);
        var script = new Dictionary<int, List<string[]>>();
        if (a[4] != "-")
            foreach (string raw in File.ReadLines(a[4]))
            {
                string ln = raw.Trim();
                if (ln.Length == 0 || ln[0] == '#') continue;
                string[] p = ln.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                int t = int.Parse(p[0]);
                if (!script.TryGetValue(t, out var list)) script[t] = list = new List<string[]>();
                list.Add(p);
            }
        int startArea = a[5] == "-" ? int.MinValue : int.Parse(a[5]);
        string[] expected = File.ReadAllLines(a[6]);

        // optional: compare rendered frames with reference frames dumped by the original (Trace.java -Dframes=...)
        string refDir = a.Length > 7 ? a[7] : null;
        var frameTicks = new HashSet<int>();
        if (a.Length > 8) foreach (string f in a[8].Split(',', StringSplitOptions.RemoveEmptyEntries)) frameTicks.Add(int.Parse(f));
        string outDir = Environment.GetEnvironmentVariable("FRAME_OUT");
        int frameMismatches = 0;

        var game = new Game(new DirAssets(dir), new NullHost(), seed);
        if (refDir != null) game.EnableRendering();
        if (mode == "bonus") game.BonusUnlocked = true;
        game.Start();
        if (startArea != int.MinValue)
        {
            game.EnterArea(startArea);
            if (startArea >= 2) game.DebugSetState(State.Playing);
        }

        var got = new List<string> { Game.DebugConsts(game), TraceHeader, game.DebugLine(-1) };
        string exception = null;
        for (int t = 0; t < ticks && exception == null; t++)
        {
            if (script.TryGetValue(t, out var evs))
                foreach (var e in evs)
                {
                    switch (e[1])
                    {
                        case "T": game.DebugTeleport(int.Parse(e[2]), int.Parse(e[3])); break;
                        case "S": game.ChooseMode(int.Parse(e[2])); break;
                        case "P": { int k = int.Parse(e[2]); game.KeyPressed(k, GameAction(k)); break; }
                        case "R": { int k = int.Parse(e[2]); game.KeyReleased(k, GameAction(k)); break; }
                    }
                }
            try { game.Update(20); }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
            {
                exception = $"#EXCEPTION tick={t}";
                break;
            }
            got.Add(game.DebugLine(t));
            if (refDir != null && frameTicks.Contains(t)) frameMismatches += CompareFrame(game, refDir, t, outDir);
        }
        if (exception != null) got.Add(exception);

        int n = Math.Max(got.Count, expected.Length);
        for (int i = 0; i < n; i++)
        {
            string g = i < got.Count ? got[i] : "<missing>";
            string e = i < expected.Length ? expected[i] : "<missing>";
            if (e.StartsWith("#EXCEPTION") && g.StartsWith("#EXCEPTION"))
            {
                // only the tick must match; the message text differs between JVM and CLR
                if (e.Split(' ')[1] == g.Split(' ')[1]) continue;
            }
            if (g != e)
            {
                Console.WriteLine($"MISMATCH at line {i + 1}");
                Console.WriteLine("  expected: " + e);
                Console.WriteLine("  got     : " + g);
                if (i > 0 && i - 1 < got.Count) Console.WriteLine("  previous: " + got[i - 1]);
                return 1;
            }
        }
        Console.WriteLine($"OK  {got.Count} lines identical" + (refDir != null ? $", {frameTicks.Count} frames compared, {frameMismatches} mismatching" : ""));
        return frameMismatches == 0 ? 0 : 1;
    }

    // Renders the current frame and compares it pixel by pixel with the original's frame_<tick>.rgb.
    private static int CompareFrame(Game game, string refDir, int tick, string outDir)
    {
        var bmp = new Bitmap(Layout.ScreenW, Layout.ScreenH);
        var canvas = new Canvas(bmp);
        game.Draw(canvas);
        byte[] exp = File.ReadAllBytes(Path.Combine(refDir, $"frame_{tick}.rgb"));
        int bad = 0, firstX = -1, firstY = -1; int gotRgb = 0, expRgb = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                int p = bmp.Pixels[y * bmp.Width + x] & 0xFFFFFF;
                int e = (exp[(y * bmp.Width + x) * 3] << 16) | (exp[(y * bmp.Width + x) * 3 + 1] << 8) | exp[(y * bmp.Width + x) * 3 + 2];
                if (p != e) { if (bad++ == 0) { firstX = x; firstY = y; gotRgb = p; expRgb = e; } }
            }
        if (outDir != null)
        {
            Directory.CreateDirectory(outDir);
            var rgba = new byte[bmp.Pixels.Length * 4];
            for (int i = 0; i < bmp.Pixels.Length; i++)
            {
                rgba[i * 4] = (byte)(bmp.Pixels[i] >> 16); rgba[i * 4 + 1] = (byte)(bmp.Pixels[i] >> 8); rgba[i * 4 + 2] = (byte)bmp.Pixels[i]; rgba[i * 4 + 3] = 255;
            }
            using var fs = File.Create(Path.Combine(outDir, $"cs_{tick}.png"));
            new StbImageWriteSharp.ImageWriter().WritePng(rgba, bmp.Width, bmp.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
        }
        if (bad > 0)
            Console.WriteLine($"FRAME tick {tick}: {bad} pixels differ, first at ({firstX},{firstY}) got #{gotRgb:X6} expected #{expRgb:X6}");
        return bad > 0 ? 1 : 0;
    }

    private const string TraceHeader = "tick cK c2 n cv aO aM am ak a1 a8 H a4 cO W bU cQ b1 E0 ci proj platHash mapHash grid";
}
