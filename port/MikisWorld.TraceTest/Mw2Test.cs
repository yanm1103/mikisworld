using MikisWorld.Core;

// Replays an input script on the C# port of MikisWorld 2 and compares with a trace recorded from the original jar
// (tools/harness/src/Trace.java + names_mw2.properties).
//
//   MikisWorld.TraceTest mw2 <assetsDir> <main|bonus> <seed> <ticks> <script|-> <startArea|-> <expected.trace> [keeplives]
internal static class Mw2Test
{
    private static int GameAction(int k) => k switch
    {
        -1 or 50 => 1, -2 or 56 => 6, -3 or 52 => 2, -4 or 54 => 5, -5 or 53 => 8, _ => 0
    };

    private const string Header = "tick cK c2 n cv aO aM am ak a1 a8 H a4 cO W bU cQ b1 E0 ci proj platHash mapHash grid c0 aX aW bw aE cL b5 I ba cm ck";

    public static int Run(string[] a)
    {
        string dir = a[1], mode = a[2];
        long seed = long.Parse(a[3]);
        int ticks = int.Parse(a[4]);
        var script = new Dictionary<int, List<string[]>>();
        if (a[5] != "-")
            foreach (string raw in File.ReadLines(a[5]))
            {
                string ln = raw.Trim();
                if (ln.Length == 0 || ln[0] == '#') continue;
                string[] p = ln.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                int t = int.Parse(p[0]);
                if (!script.TryGetValue(t, out var list)) script[t] = list = new List<string[]>();
                list.Add(p);
            }
        int startArea = a[6] == "-" ? int.MinValue : int.Parse(a[6]);
        string[] expected = File.ReadAllLines(a[7]);
        int keepLives = a.Length > 8 ? int.Parse(a[8]) : 0;
        // optional: compare rendered frames with frames dumped by the original (Trace.java -Dframes=...)
        string refDir = a.Length > 9 ? a[9] : null;
        var frameTicks = new HashSet<int>();
        if (a.Length > 10) foreach (string f in a[10].Split(',', StringSplitOptions.RemoveEmptyEntries)) frameTicks.Add(int.Parse(f));
        string outDir = Environment.GetEnvironmentVariable("FRAME_OUT");
        int frameMismatches = 0;

        var game = new Game2(new DirAssets(dir), new NullHost(), seed);
        if (mode == "bonus") game.BonusUnlocked = true;
        if (refDir != null) game.EnableRendering();
        game.Start();
        if (startArea != int.MinValue)
        {
            game.EnterArea(startArea);
            if (startArea >= 2) game.DebugSetState(State.Playing);
        }

        var got = new List<string> { Game2.DebugConsts(game), Header, game.DebugLine(-1) };
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
            if (keepLives > 0 && game.DebugLives < 2 && game.CurrentState != State.GameOver) game.DebugLives = keepLives;
            try { game.Update(20, 20 * (t + 1)); }
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
            if (i == 0) continue;          // #consts line: printed with the harness' field-name quirks, informational only
            if (e.StartsWith("#EXCEPTION") && g.StartsWith("#EXCEPTION") && e.Split(' ')[1] == g.Split(' ')[1]) continue;
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
    private static int CompareFrame(Game2 game, string refDir, int tick, string outDir)
    {
        var bmp = new Bitmap(Layout2.ScreenW, Layout2.ScreenH);
        var canvas = new Canvas(bmp);
        game.Draw(canvas);
        byte[] exp = File.ReadAllBytes(Path.Combine(refDir, $"frame_{tick}.rgb"));
        int bad = 0, firstX = -1, firstY = -1, gotRgb = 0, expRgb = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                int p = bmp.Pixels[y * bmp.Width + x] & 0xFFFFFF;
                int e = (exp[(y * bmp.Width + x) * 3] << 16) | (exp[(y * bmp.Width + x) * 3 + 1] << 8) | exp[(y * bmp.Width + x) * 3 + 2];
                if (p != e) { if (bad++ == 0) { firstX = x; firstY = y; gotRgb = p; expRgb = e; } }
            }
        if (outDir != null && bad > 0)
        {
            Directory.CreateDirectory(outDir);
            var rgba = new byte[bmp.Pixels.Length * 4];
            for (int i = 0; i < bmp.Pixels.Length; i++)
            {
                rgba[i * 4] = (byte)(bmp.Pixels[i] >> 16); rgba[i * 4 + 1] = (byte)(bmp.Pixels[i] >> 8); rgba[i * 4 + 2] = (byte)bmp.Pixels[i]; rgba[i * 4 + 3] = 255;
            }
            using var fs = File.Create(Path.Combine(outDir, $"cs2_{tick}.png"));
            new StbImageWriteSharp.ImageWriter().WritePng(rgba, bmp.Width, bmp.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
        }
        if (bad > 0)
            Console.WriteLine($"FRAME tick {tick}: {bad} pixels differ, first at ({firstX},{firstY}) got #{gotRgb:X6} expected #{expRgb:X6}");
        return bad > 0 ? 1 : 0;
    }
}
