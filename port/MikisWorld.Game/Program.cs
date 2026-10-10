using System.Numerics;
using System.Text.Json;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

internal static class Program
{
    private const int StepMs = 20;                      // the original logic runs in steps of >= 20 ms

    private sealed class Run
    {
        public string Jar1, Jar2, SavePath, ResizeSpec;
        public int Scale, Octave, QuitAfter, CrashAt;
        public bool Fixed;                                   // --game given: no launcher, closing the game closes the program
        public Dictionary<int, List<string>> Inject, Shoot, LaunchInject, LaunchShoot;
        public int LaunchQuitAfter;
        public DisplaySettings Settings;
        public InputBindings Bindings;
    }

    private static int Main(string[] args)
    {
        var run = new Run
        {
            Jar1 = FindJar(args, "MikisWorld.jar"),
            Jar2 = FindJar(args, "MikisWorld2.jar"),
            Scale = ArgInt(args, "--scale", 4),
            SavePath = ArgStr(args, "--save", null),
            // developer aids: inject keys / take screenshots at given frames, e.g. --inject 30:Enter,60:Down --shoot 90:menu
            Inject = ParseFrames(ArgStr(args, "--inject", "")),
            Shoot = ParseFrames(ArgStr(args, "--shoot", "")),
            QuitAfter = ArgInt(args, "--quit-after", -1),
            CrashAt = ArgInt(args, "--crash-at", -1),                // dev aid: throw inside the game loop to see the crash screen
            Octave = ArgInt(args, "--octave", -1),                // pitch of the ring tones, in octaves relative to the decoded notes
            ResizeSpec = ArgStr(args, "--resize", null),         // dev aid: "--resize 700x500" at frame 10
            LaunchInject = ParseFrames(ArgStr(args, "--launcher-inject", "")),
            LaunchShoot = ParseFrames(ArgStr(args, "--launcher-shoot", "")),
            LaunchQuitAfter = ArgInt(args, "--launcher-quit-after", -1),
        };
        int game = ArgInt(args, "--game", 0);                       // 0 = ask (when both JARs are present)
        string exportDir = ArgStr(args, "--export-sounds", null);
        bool soundTest = Array.IndexOf(args, "--sound-test") >= 0;

        // both games are required: the start-up screen lets you choose, and missing files are reported instead of silently
        // falling back to the one that exists
        var missing = new List<string>();
        if (run.Jar1 == null) missing.Add("MikisWorld.jar");
        if (run.Jar2 == null) missing.Add("MikisWorld2.jar");
        if (missing.Count > 0) return ShowMissingJars(missing, run.QuitAfter >= 0 || exportDir != null || soundTest);
        if (game == 0 && (exportDir != null || soundTest || run.LaunchInject.Count == 0 && (run.Inject.Count > 0 || run.QuitAfter >= 0))) game = 1;
        run.Fixed = game != 0;
        run.Settings = SettingsStore.Load(ArgStr(args, "--settings", null));
        run.Settings.Changed += () => SettingsStore.Save(run.Settings);
        run.Bindings = BindingsStore.Load(ArgStr(args, "--bindings", null));
        run.Bindings.Changed += () => BindingsStore.Save(run.Bindings);

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        if (exportDir != null)
        {
            using var a = new JarAssets(game == 2 ? run.Jar2 : run.Jar1);
            AudioHost.Export(a, exportDir, run.Octave);
            return 0;
        }

        Raylib.InitWindow(Launcher.W0 * 2, Launcher.H0 * 2, "Miki's World");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);
        Raylib.SetWindowMinSize(Launcher.W0, Launcher.H0);
        int exit = 0;
        bool firstLauncher = true;
        while (true)
        {
            try
            {
                if (game == 0)
                {
                    game = Launcher.Run(run.Settings, firstLauncher ? run.LaunchInject : null, run.LaunchShoot, run.LaunchQuitAfter);
                    firstLauncher = false;
                }
                if (game == 0) break;
                if (soundTest) { exit = SoundTest(run, game); break; }
                var result = RunSession(run, game);
                if (result == SessionResult.CloseProgram || run.Fixed) { exit = result == SessionResult.Crashed ? 1 : 0; break; }
                game = 0;                                     // the game was exited: back to the game selection
                RestoreLauncherWindow(run.Settings);
            }
            catch (Exception e)
            {
                ReportCrash(e, game, null, false);
                if (run.Fixed) { exit = 1; break; }
                game = 0;
                RestoreLauncherWindow(run.Settings);
            }
        }
        Raylib.CloseWindow();
        return exit;
    }

    private enum SessionResult { BackToLauncher, CloseProgram, Crashed }

    private static int SoundTest(Run run, int game)
    {
        using var assets = new JarAssets(game == 2 ? run.Jar2 : run.Jar1);
        using var audio = new AudioHost(assets, run.Octave, run.Settings.Volume);
        audio.Play(Sfx.Coin);
        Raylib.WaitTime(0.03);
        Console.WriteLine($"audio device ready={audio.Ready}, coin playing={audio.IsPlaying}");
        return audio.Ready && audio.IsPlaying ? 0 : 1;
    }

    /// <summary>Back to the start-up screen's window: native size is 320x230 (twice that by default).</summary>
    private static void RestoreLauncherWindow(DisplaySettings settings)
    {
        Raylib.SetWindowTitle("Miki's World");
        Raylib.SetWindowMinSize(Launcher.W0, Launcher.H0);
        if (!settings.Fullscreen) Raylib.SetWindowSize(Launcher.W0 * 2, Launcher.H0 * 2);
    }

    private static SessionResult RunSession(Run run, int game)
    {
        var settings = run.Settings;
        using var assets = new JarAssets(game == 2 ? run.Jar2 : run.Jar1);
        using var audio = new AudioHost(assets, run.Octave, settings.Volume) { TitleLoops = game == 2 ? 3 : 1 };
        Action volumeSync = () => audio.SetVolume(settings.Volume);
        settings.Changed += volumeSync;
        Texture2D tex = default;
        bool haveTexture = false;
        App app = null;
        try
        {
            long seed = Environment.TickCount64;
            string saveFile = run.SavePath ?? DefaultSave(game);
            app = game == 2
                ? new App(h => new Game2(assets, h, seed), assets, audio, new FileStorage(saveFile), AppInfo.MikisWorld2, settings, run.Bindings)
                : new App(h => new MikisWorld.Core.Game(assets, h, seed), assets, audio, new FileStorage(saveFile), AppInfo.MikisWorld1, settings, run.Bindings);

            int sw0 = app.ScreenWidth, sh0 = app.ScreenHeight;
            // a full-screen window keeps the monitor's size; only a normal window is sized to the game
            if (!settings.Fullscreen) Raylib.SetWindowSize(sw0 * run.Scale, sh0 * run.Scale);
            Raylib.SetWindowMinSize(sw0, sh0);
            Raylib.SetWindowTitle(game == 2 ? "Miki's World 2: The Storm Master" : "Miki's World 1: The Lost Tiara");

            var metrics = new RaylibFontMetrics();
            Raylib.SetTextureFilter(Raylib.GetFontDefault().Texture, TextureFilter.Point);
            var frame = new Bitmap(sw0, sh0);
            var rgba = new byte[sw0 * sh0 * 4];
            unsafe
            {
                fixed (byte* p = rgba)
                {
                    var image = new Image { Data = p, Width = sw0, Height = sh0, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
                    tex = Raylib.LoadTextureFromImage(image);
                }
            }
            haveTexture = true;
            bool smooth = !settings.UsesBilinear;                      // forces the filter to be applied on the first frame
            Raylib.SetTextureFilter(tex, TextureFilter.Point);

            double acc = 0;
            int frameNo = 0;
            double resizedAt = -1;
            var router = new InputRouter(new AppTarget(app));
            var poller = new DevicePoller(router);
            var injectedUp = new List<(BindingDevice, string)>();
            Raylib.PollInputEvents();            // the key that picked the game on the start-up screen is still "pressed" in this frame: do not reuse it

            while (!app.WantsExit)
            {
                if (Raylib.WindowShouldClose()) return SessionResult.CloseProgram;
                if (frameNo == run.CrashAt) throw new InvalidOperationException("test crash (--crash-at)");
                acc += Math.Min(Raylib.GetFrameTime(), 0.25);

                // ---- window: always an integer multiple of the native resolution (no blurry/uneven pixels)
                if (run.ResizeSpec != null && frameNo == 10)
                {
                    var wh = run.ResizeSpec.Split('x');
                    Raylib.SetWindowSize(int.Parse(wh[0]), int.Parse(wh[1]));
                }
                if (Raylib.IsKeyPressed(KeyboardKey.F11)) { settings.Fullscreen = !settings.Fullscreen; settings.NotifyChanged(); }
                bool borderless = SyncFullscreen(settings);
                ApplyWindowScale(settings, sw0, sh0, borderless, ref resizedAt);

                double now = Raylib.GetTime();
                foreach (var (dev, n) in injectedUp) router.Release(dev, n);
                injectedUp.Clear();
                if (run.Inject.TryGetValue(frameNo, out var names))
                    foreach (string raw in names)
                    {
                        // "Name" = tap, "+Name" = press and hold, "-Name" = release; "Pad:A" / "Pad:SRight" address the gamepad
                        char mode = raw[0] is '+' or '-' ? raw[0] : ' ';
                        string n = mode == ' ' ? raw : raw[1..];
                        var dev = n.StartsWith("Pad:", StringComparison.OrdinalIgnoreCase) ? BindingDevice.Gamepad : BindingDevice.Keyboard;
                        if (dev == BindingDevice.Gamepad) n = n[4..];
                        else if (n.Equals("F11", StringComparison.OrdinalIgnoreCase)) { settings.Fullscreen = !settings.Fullscreen; settings.NotifyChanged(); continue; }   // window key
                        else if (Enum.TryParse<KeyboardKey>(n, true, out var k)) n = k.ToString();
                        else continue;
                        if (mode != '-') router.Press(dev, n, now);
                        if (mode == ' ') injectedUp.Add((dev, n));
                        if (mode == '-') router.Release(dev, n);
                    }

                poller.Poll(now);
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

                if (run.Shoot.TryGetValue(frameNo, out var shots))
                    foreach (string name in shots) Raylib.TakeScreenshot($"shot_{name}.png");
                frameNo++;
                if (run.QuitAfter >= 0 && frameNo > run.QuitAfter) return SessionResult.CloseProgram;
            }
            if (run.ResizeSpec != null) Console.WriteLine($"final window {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()} (native {sw0}x{sh0}) borderless={Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode)} fullscreenSetting={settings.Fullscreen}");
            return SessionResult.BackToLauncher;
        }
        catch (Exception e)
        {
            ReportCrash(e, game, app, run.QuitAfter >= 0);
            return SessionResult.Crashed;
        }
        finally
        {
            settings.Changed -= volumeSync;
            if (haveTexture) Raylib.UnloadTexture(tex);
        }
    }

    // ------------------------------------------------------------------ crashes
    private static string CrashLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", "crash.log");

    /// <summary>Writes the exception (and what the game was doing) to crash.log, then keeps the window open with the error until a key is pressed.</summary>
    private static void ReportCrash(Exception e, int game, App app, bool dev)
    {
        string where = app == null ? "outside a game" : $"state {app.Game.CurrentState}, frame {app.Game.Frame}";
        string movement = app == null ? "?" : app.Settings.RevampedMovement ? "revamped" : "original";
        string report = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Miki's World {(game == 0 ? "launcher" : game.ToString())} ({where}), movement={movement}\n{e}\n";
        string path = CrashLogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, report + "\n");
        }
        catch (Exception) { path += " (could not be written)"; }
        Console.Error.WriteLine(report);

        string[] lines = (e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace).Split('\n').Select(l => l.TrimEnd('\r')).Select(l => l.Length > 78 ? l[..75] + "..." : l).Take(14).ToArray();
        string text = "The game crashed.\n\n" + string.Join("\n", lines)
            + $"\n\nDetails were saved to:\n{path}\n\nEnter: back     C: copy the error     Esc: quit";
        bool copied = false;
        int shown = 0;
        while (!Raylib.WindowShouldClose())
        {
            if (dev && ++shown == 10) { Raylib.TakeScreenshot("shot_crash.png"); return; }          // unattended runs (tests) do not wait for a key
            if (Raylib.IsKeyPressed(KeyboardKey.C)) { Raylib.SetClipboardText(report); copied = true; }
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) return;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) break;
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(90, 20, 20, 255));
            Raylib.DrawText(text, 16, 16, 14, Color.White);
            if (copied) Raylib.DrawText("copied to the clipboard", 16, Raylib.GetScreenHeight() - 24, 14, new Color(255, 224, 160, 255));
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
        Environment.Exit(1);
    }

    /// <summary>Zoom keys and snapping: with integer scaling the window always ends up an exact multiple of the native size.</summary>
    internal static void ApplyWindowScale(DisplaySettings settings, int w0, int h0, bool borderless, ref double resizedAt)
    {
        if (!settings.IntegerScale || borderless || Raylib.IsWindowMaximized()) return;
        int step = Raylib.IsKeyPressed(KeyboardKey.Equal) || Raylib.IsKeyPressed(KeyboardKey.KpAdd) ? 1
                 : Raylib.IsKeyPressed(KeyboardKey.Minus) || Raylib.IsKeyPressed(KeyboardKey.KpSubtract) ? -1 : 0;
        if (step != 0) SnapWindow(w0, h0, CurrentScale(w0, h0) + step);
        if (Raylib.IsWindowResized()) resizedAt = Raylib.GetTime();
        else if (resizedAt >= 0 && Raylib.GetTime() - resizedAt > 0.25)
        {
            resizedAt = -1;
            SnapWindow(w0, h0, (int)Math.Round(Math.Min((double)Raylib.GetScreenWidth() / w0, (double)Raylib.GetScreenHeight() / h0)));
        }
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

    /// <summary>The Fullscreen option (menu, launcher or F11) is the source of truth: bring the window in line with it.
    /// Returns whether the window is borderless afterwards.</summary>
    internal static bool SyncFullscreen(DisplaySettings settings)
    {
        bool borderless = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
        if (settings.Fullscreen != borderless)
        {
            Raylib.ToggleBorderlessWindowed();
            borderless = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
        }
        return borderless;
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
    private sealed record Dto(int Volume = 100, bool IntegerScale = true, bool Bilinear = false, bool RevampedMovement = true);
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    private static string _path;

    public static DisplaySettings Load(string path)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", "settings.json"));
        var d = new Dto();
        try { if (File.Exists(_path)) d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(_path)) ?? d; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new DisplaySettings { Volume = Math.Clamp(d.Volume, 0, 100), IntegerScale = d.IntegerScale, Bilinear = d.Bilinear, RevampedMovement = d.RevampedMovement };
    }

    public static void Save(DisplaySettings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new Dto(s.Volume, s.IntegerScale, s.Bilinear, s.RevampedMovement), Opt));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Remapped keyboard keys and gamepad buttons (<c>%APPDATA%/MikisWorld/bindings.json</c>), shared by both games.</summary>
internal static class BindingsStore
{
    private sealed class Dto
    {
        public Dictionary<string, string[]> Keyboard { get; set; }
        public Dictionary<string, string[]> Gamepad { get; set; }
    }

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    private static string _path;

    public static InputBindings Load(string path)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikisWorld", "bindings.json"));
        var b = new InputBindings();
        try
        {
            if (File.Exists(_path))
            {
                var d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(_path));
                b.Import(BindingDevice.Keyboard, d?.Keyboard);
                b.Import(BindingDevice.Gamepad, d?.Gamepad);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return b;
    }

    public static void Save(InputBindings b)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new Dto { Keyboard = b.Export(BindingDevice.Keyboard), Gamepad = b.Export(BindingDevice.Gamepad) }, Opt));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
