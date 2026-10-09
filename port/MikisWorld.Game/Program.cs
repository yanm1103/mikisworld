using System.Numerics;
using System.Text.Json;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

internal static class Program
{
    private const int StepMs = 20;                      // the original logic runs in steps of >= 20 ms

    private static int Main(string[] args)
    {
        string jar1 = FindJar(args, "MikisWorld.jar");
        string jar2 = FindJar(args, "MikisWorld2.jar");
        int scale = ArgInt(args, "--scale", 4);
        string savePath = ArgStr(args, "--save", null);
        int game = ArgInt(args, "--game", 0);                       // 0 = ask (when both JARs are present)
        // developer aids: inject keys / take screenshots at given frames, e.g. --inject 30:Enter,60:Down --shoot 90:menu
        var inject = ParseFrames(ArgStr(args, "--inject", ""));
        var shoot = ParseFrames(ArgStr(args, "--shoot", ""));
        int quitAfter = ArgInt(args, "--quit-after", -1);
        string exportDir = ArgStr(args, "--export-sounds", null);
        int octave = ArgInt(args, "--octave", -1);                  // pitch of the ring tones, in octaves relative to the decoded notes
        bool soundTest = Array.IndexOf(args, "--sound-test") >= 0;

        // both games are required: the start-up screen lets you choose, and missing files are reported instead of silently
        // falling back to the one that exists
        var missing = new List<string>();
        if (jar1 == null) missing.Add("MikisWorld.jar");
        if (jar2 == null) missing.Add("MikisWorld2.jar");
        if (missing.Count > 0) return ShowMissingJars(missing, quitAfter >= 0 || exportDir != null || soundTest);
        if (game == 0 && (exportDir != null || soundTest || inject.Count > 0 || quitAfter >= 0)) game = 1;
        var settings = SettingsStore.Load(ArgStr(args, "--settings", null));
        settings.Changed += () => SettingsStore.Save(settings);

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        if (exportDir != null)
        {
            using var a = new JarAssets(game == 2 ? jar2 : jar1);
            AudioHost.Export(a, exportDir, octave);
            return 0;
        }

        Raylib.InitWindow(640, 400, "Miki's World");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);
        if (game == 0) game = ChooseGame();
        if (game == 0) { Raylib.CloseWindow(); return 0; }

        using var assets = new JarAssets(game == 2 ? jar2 : jar1);
        using var audio = new AudioHost(assets, octave, settings.Volume) { TitleLoops = game == 2 ? 3 : 1 };
        settings.Changed += () => audio.SetVolume(settings.Volume);
        if (soundTest)
        {
            audio.Play(Sfx.Coin);
            Raylib.WaitTime(0.03);
            Console.WriteLine($"audio device ready={audio.Ready}, coin playing={audio.IsPlaying}");
            Raylib.CloseWindow();
            return audio.Ready && audio.IsPlaying ? 0 : 1;
        }

        long seed = Environment.TickCount64;
        string saveFile = savePath ?? DefaultSave(game);
        App app = game == 2
            ? new App(h => new Game2(assets, h, seed), assets, audio, new FileStorage(saveFile), AppInfo.MikisWorld2, settings)
            : new App(h => new MikisWorld.Core.Game(assets, h, seed), assets, audio, new FileStorage(saveFile), AppInfo.MikisWorld1, settings);

        int sw0 = app.ScreenWidth, sh0 = app.ScreenHeight;
        Raylib.SetWindowSize(sw0 * scale, sh0 * scale);
        Raylib.SetWindowMinSize(sw0, sh0);
        Raylib.SetWindowTitle(game == 2 ? "Miki's World 2: The Storm Master" : "Miki's World 1: The Lost Tiara");

        var metrics = new RaylibFontMetrics();
        Raylib.SetTextureFilter(Raylib.GetFontDefault().Texture, TextureFilter.Point);
        var frame = new Bitmap(sw0, sh0);
        var rgba = new byte[sw0 * sh0 * 4];
        Texture2D tex;
        unsafe
        {
            fixed (byte* p = rgba)
            {
                var image = new Image { Data = p, Width = sw0, Height = sh0, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
                tex = Raylib.LoadTextureFromImage(image);
            }
        }
        bool smooth = !settings.UsesBilinear;                      // forces the filter to be applied on the first frame
        Raylib.SetTextureFilter(tex, TextureFilter.Point);

        double acc = 0;
        int frameNo = 0;
        double resizedAt = -1;
        string resizeSpec = ArgStr(args, "--resize", null);       // dev aid: "--resize 700x500" at frame 10
        var injectedUp = new List<KeyboardKey>();

        while (!Raylib.WindowShouldClose() && !app.WantsExit)
        {
            acc += Math.Min(Raylib.GetFrameTime(), 0.25);

            // ---- window: always an integer multiple of the native resolution (no blurry/uneven pixels)
            bool borderless = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
            if (resizeSpec != null && frameNo == 10)
            {
                var wh = resizeSpec.Split('x');
                Raylib.SetWindowSize(int.Parse(wh[0]), int.Parse(wh[1]));
            }
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { settings.Fullscreen = !settings.Fullscreen; settings.NotifyChanged(); }
            if (settings.Fullscreen != borderless)                  // the option (menu or F11) is the source of truth
            {
                Raylib.ToggleBorderlessWindowed();
                borderless = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
            }
            if (settings.IntegerScale && !borderless && !Raylib.IsWindowMaximized())
            {
                int step = Raylib.IsKeyPressed(KeyboardKey.Equal) || Raylib.IsKeyPressed(KeyboardKey.KpAdd) ? 1
                         : Raylib.IsKeyPressed(KeyboardKey.Minus) || Raylib.IsKeyPressed(KeyboardKey.KpSubtract) ? -1 : 0;
                if (step != 0) SnapWindow(sw0, sh0, CurrentScale(sw0, sh0) + step);
                if (Raylib.IsWindowResized()) resizedAt = Raylib.GetTime();
                else if (resizedAt >= 0 && Raylib.GetTime() - resizedAt > 0.25)
                {
                    resizedAt = -1;
                    SnapWindow(sw0, sh0, (int)Math.Round(Math.Min((double)Raylib.GetScreenWidth() / sw0, (double)Raylib.GetScreenHeight() / sh0)));
                }
            }

            foreach (var k in injectedUp) HandleKey(app, k, false);
            injectedUp.Clear();
            if (inject.TryGetValue(frameNo, out var names))
                foreach (string n in names)
                {
                    if (n.Equals("F11", StringComparison.OrdinalIgnoreCase)) { settings.Fullscreen = !settings.Fullscreen; settings.NotifyChanged(); continue; }   // window key
                    if (Enum.TryParse<KeyboardKey>(n, true, out var k)) { HandleKey(app, k, true); injectedUp.Add(k); }
                }

            foreach (var key in Input.AllKeys)
            {
                if (Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key) && (Input.Repeats(key) || app.AcceptsRepeat && Input.IsHorizontal(key))) HandleKey(app, key, true);
                if (Raylib.IsKeyReleased(key)) HandleKey(app, key, false);
            }
            int ch;
            while ((ch = Raylib.GetCharPressed()) != 0) app.TextInput((char)ch);

            while (acc >= StepMs / 1000.0)
            {
                app.Update(StepMs);
                acc -= StepMs / 1000.0;
            }
            audio.Tick();

            var canvas = new Canvas(frame) { Metrics = metrics };
            app.Draw(canvas);
            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                int p = frame.Pixels[i];
                rgba[i * 4] = (byte)(p >> 16); rgba[i * 4 + 1] = (byte)(p >> 8); rgba[i * 4 + 2] = (byte)p; rgba[i * 4 + 3] = 255;
            }
            unsafe { fixed (byte* p = rgba) Raylib.UpdateTexture(tex, p); }

            // nearest-neighbour unless the user switched integer scaling off *and* chose bilinear
            if (smooth != settings.UsesBilinear)
            {
                smooth = settings.UsesBilinear;
                var f = smooth ? TextureFilter.Bilinear : TextureFilter.Point;
                Raylib.SetTextureFilter(tex, f);
                Raylib.SetTextureFilter(Raylib.GetFontDefault().Texture, f);
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
            // integer scaling: a whole-number factor (black bars); otherwise fill the window keeping the aspect ratio
            float scalef = settings.IntegerScale ? Math.Max(1, Math.Min(sw / sw0, sh / sh0)) : Math.Min((float)sw / sw0, (float)sh / sh0);
            float dw = sw0 * scalef, dh = sh0 * scalef;
            float ox = MathF.Floor((sw - dw) / 2), oy = MathF.Floor((sh - dh) / 2);
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, sw0, sh0), new Rectangle(ox, oy, dw, dh), Vector2.Zero, 0, Color.White);
            foreach (var t in canvas.Texts) metrics.Draw(t, ox, oy, scalef);
            Raylib.EndDrawing();

            if (shoot.TryGetValue(frameNo, out var shots))
                foreach (string name in shots) Raylib.TakeScreenshot($"shot_{name}.png");
            frameNo++;
            if (quitAfter >= 0 && frameNo > quitAfter) break;
        }
        if (resizeSpec != null) Console.WriteLine($"final window {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()} (native {sw0}x{sh0}) borderless={Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode)} fullscreenSetting={settings.Fullscreen}");
        Raylib.CloseWindow();
        return 0;
    }

    /// <summary>Both JARs are required; explains which one is missing and where they are searched for.</summary>
    private static int ShowMissingJars(List<string> missing, bool headless)
    {
        string text = "Miki's World needs BOTH game files.\n\nMissing: " + string.Join(", ", missing)
            + "\n\nPut MikisWorld.jar and MikisWorld2.jar in the JAR folder next to the project,\nor pass their paths as arguments.\n\n(press any key to close)";
        Console.Error.WriteLine(text.Replace("\n\n(press any key to close)", ""));
        if (headless) return 1;
        Raylib.InitWindow(780, 300, "Miki's World");
        Raylib.SetTargetFPS(30);
        while (!Raylib.WindowShouldClose() && Raylib.GetKeyPressed() == 0)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(99, 48, 0, 255));
            Raylib.DrawText(text, 24, 24, 22, Color.White);
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
        return 1;
    }

    private static int CurrentScale(int w, int h) => Math.Max(1, Math.Min(Raylib.GetScreenWidth() / w, Raylib.GetScreenHeight() / h));

    /// <summary>Resizes the window to <c>scale</c> times the native resolution (limited to what fits on the monitor).</summary>
    private static void SnapWindow(int w, int h, int scale)
    {
        int monitor = Raylib.GetCurrentMonitor();
        int maxScale = Math.Max(1, Math.Min(Raylib.GetMonitorWidth(monitor) / w, (Raylib.GetMonitorHeight(monitor) - 80) / h));
        scale = Math.Clamp(scale, 1, maxScale);
        if (Raylib.GetScreenWidth() != w * scale || Raylib.GetScreenHeight() != h * scale)
            Raylib.SetWindowSize(w * scale, h * scale);
    }

    /// <summary>Small start-up screen shown when both games are available.</summary>
    private static int ChooseGame()
    {
        int sel = 0;
        string[] titles = { "Miki's World 1", "Miki's World 2" };
        string[] subs = { "The Lost Tiara", "The Storm Master" };
        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1)) return 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2)) return 2;
            if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.W)) sel = 0;
            if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.S)) sel = 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) return sel + 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return 0;
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(60, 179, 210, 255));
            Raylib.DrawText("Choose a game", 220, 30, 36, Color.White);
            for (int i = 0; i < 2; i++)
            {
                var box = new Rectangle(60 + i * 280, 120, 240, 160);
                Raylib.DrawRectangleRec(box, i == sel ? new Color(160, 90, 20, 255) : new Color(99, 48, 0, 255));
                Raylib.DrawRectangleLinesEx(box, i == sel ? 4 : 2, Color.White);
                Raylib.DrawText(titles[i], (int)box.X + 20, (int)box.Y + 40, 28, Color.White);
                Raylib.DrawText(subs[i], (int)box.X + 20, (int)box.Y + 80, 22, new Color(255, 224, 160, 255));
                Raylib.DrawText($"[{i + 1}]", (int)box.X + 20, (int)box.Y + 120, 20, Color.White);
            }
            Raylib.DrawText("1 / 2 or arrows + Enter", 195, 330, 22, Color.White);
            Raylib.EndDrawing();
        }
        return 0;
    }

    private static void HandleKey(App app, KeyboardKey key, bool down)
    {
        if (down)
        {
            if (key == KeyboardKey.Escape) { app.Back(); return; }
            if (key == KeyboardKey.Backspace) { app.Backspace(); return; }
            if (key == KeyboardKey.Enter || key == KeyboardKey.KpEnter) app.ConfirmName();
        }
        foreach (var (k, code, action) in Input.Map)
        {
            if (k != key) continue;
            if (down) app.KeyPressed(code, action); else app.KeyReleased(code, action);
        }
    }

    private static string DefaultSave(int game)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", game == 2 ? "save2.json" : "save.json");

    /// <summary>An explicit .jar argument whose name matches, else JAR/&lt;name&gt; found by walking up from the executable.</summary>
    private static string FindJar(string[] args, string name)
    {
        foreach (string a in args)
            if (a.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && File.Exists(a)
                && string.Equals(Path.GetFileName(a), name, StringComparison.OrdinalIgnoreCase)) return a;
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
        {
            string c = Path.Combine(dir, "JAR", name);
            if (File.Exists(c)) return c;
        }
        return null;
    }

    private static int ArgInt(string[] args, string name, int dflt)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name && int.TryParse(args[i + 1], out int v)) return v;
        return dflt;
    }

    private static string ArgStr(string[] args, string name, string dflt)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return dflt;
    }

    private static Dictionary<int, List<string>> ParseFrames(string spec)
    {
        var d = new Dictionary<int, List<string>>();
        foreach (string part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int c = part.IndexOf(':');
            if (c < 0 || !int.TryParse(part[..c], out int f)) continue;
            if (!d.TryGetValue(f, out var l)) d[f] = l = new List<string>();
            l.Add(part[(c + 1)..]);
        }
        return d;
    }
}

/// <summary>Settings, bonus unlock and high scores as one JSON file (the original used three RecordStores).</summary>
internal sealed class FileStorage : IStorage
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public FileStorage(string path) { _path = path; }

    public SaveData Load()
    {
        try { if (File.Exists(_path)) return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(_path)) ?? new SaveData(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new SaveData();
    }

    public void Save(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(data, Opt));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Keyboard to MIDP key codes. Digits are the phone keypad (the game's original controls);
/// arrows/WASD/Enter map to the corresponding game actions.</summary>
internal static class Input
{
    // (key, MIDP key code, game action); game actions: UP=1 LEFT=2 RIGHT=5 DOWN=6 FIRE=8
    public static readonly (KeyboardKey, int, int)[] Map =
    {
        (KeyboardKey.Left, -3, 2), (KeyboardKey.A, -3, 2),
        (KeyboardKey.Right, -4, 5), (KeyboardKey.D, -4, 5),
        (KeyboardKey.Up, -1, 1), (KeyboardKey.W, -1, 1),
        (KeyboardKey.Down, -2, 6), (KeyboardKey.S, -2, 6),
        (KeyboardKey.Enter, -5, 8), (KeyboardKey.Space, -5, 8),
        (KeyboardKey.One, 49, 0), (KeyboardKey.Two, 50, 1), (KeyboardKey.Three, 51, 0),
        (KeyboardKey.Four, 52, 2), (KeyboardKey.Five, 53, 8), (KeyboardKey.Six, 54, 5),
        (KeyboardKey.Seven, 55, 0), (KeyboardKey.Eight, 56, 6), (KeyboardKey.Nine, 57, 0),
        (KeyboardKey.Kp1, 49, 0), (KeyboardKey.Kp2, 50, 1), (KeyboardKey.Kp3, 51, 0),
        (KeyboardKey.Kp4, 52, 2), (KeyboardKey.Kp5, 53, 8), (KeyboardKey.Kp6, 54, 5),
        (KeyboardKey.Kp7, 55, 0), (KeyboardKey.Kp8, 56, 6), (KeyboardKey.Kp9, 57, 0),
    };

    /// <summary>Every key the app reacts to, including Esc/Backspace/KpEnter.</summary>
    public static readonly KeyboardKey[] AllKeys =
        Map.Select(m => m.Item1).Concat(new[] { KeyboardKey.Escape, KeyboardKey.Backspace, KeyboardKey.KpEnter }).Distinct().ToArray();

    /// <summary>Only menu navigation/editing repeats while held; game keys must not (the original had no key repeat).</summary>
    public static bool Repeats(KeyboardKey k) => k is KeyboardKey.Up or KeyboardKey.Down or KeyboardKey.Backspace;

    /// <summary>Left/right repeat only while a slider is on screen (the games themselves never had key repeat).</summary>
    public static bool IsHorizontal(KeyboardKey k) => k is KeyboardKey.Left or KeyboardKey.Right or KeyboardKey.A or KeyboardKey.D;
}

/// <summary>Text for bubbles/menus/ending screen, drawn with Raylib's built-in font on top of the scaled pixel frame.</summary>
internal sealed class RaylibFontMetrics : IFontMetrics
{
    private readonly Font _font = Raylib.GetFontDefault();
    private static float Size(FontKind k) => k == FontKind.Bold ? 12 : 10;

    public int StringWidth(string s, FontKind font) => (int)Raylib.MeasureTextEx(_font, s, Size(font), 1).X;
    public int Height(FontKind font) => (int)Size(font);

    public void Draw(TextCommand t, float ox, float oy, float k)
    {
        float size = Size(t.Font);
        Vector2 sz = Raylib.MeasureTextEx(_font, t.Text, size, 1);
        float x = t.X, y = t.Y;
        if ((t.Anchor & Canvas.HCenter) != 0) x -= sz.X / 2; else if ((t.Anchor & Canvas.Right) != 0) x -= sz.X;
        if ((t.Anchor & Canvas.Bottom) != 0) y -= sz.Y; else if ((t.Anchor & Canvas.VCenter) != 0) y -= sz.Y / 2;
        var color = new Color((byte)(t.Rgb >> 16), (byte)(t.Rgb >> 8), (byte)t.Rgb, (byte)255);
        Raylib.DrawTextEx(_font, t.Text, new Vector2(ox + x * k, oy + y * k), size * k, k, color);
    }
}

/// <summary>Display and sound options, shared by both games (<c>%APPDATA%/MikisWorld/settings.json</c>).
/// Full screen is deliberately not saved: the game always starts in a window.</summary>
internal static class SettingsStore
{
    private sealed record Dto(int Volume = 100, bool IntegerScale = true, bool Bilinear = false);
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    private static string _path;

    public static DisplaySettings Load(string path)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", "settings.json");
        var d = new Dto();
        try { if (File.Exists(_path)) d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(_path)) ?? d; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new DisplaySettings { Volume = Math.Clamp(d.Volume, 0, 100), IntegerScale = d.IntegerScale, Bilinear = d.Bilinear };
    }

    public static void Save(DisplaySettings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new Dto(s.Volume, s.IntegerScale, s.Bilinear), Opt));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
