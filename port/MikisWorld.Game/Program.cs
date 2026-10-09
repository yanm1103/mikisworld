using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

internal static class Program
{
    private const int StepMs = 20;                      // the original logic runs in steps of >= 20 ms

    private static int Main(string[] args)
    {
        string jar = FindJar(args);
        if (jar == null)
        {
            Console.Error.WriteLine("MikisWorld.jar not found. Pass its path as the first argument or put it in ./JAR/.");
            return 1;
        }
        int shotAfter = ArgInt(args, "--screenshot-after", -1);   // dev aid: save a screenshot and quit
        int scale = ArgInt(args, "--scale", 4);

        using var assets = new JarAssets(jar);
        var host = new SilentHost();
        var game = new Core.Game(assets, host, Environment.TickCount64);

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(Layout.ScreenW * scale, Layout.ScreenH * scale, "Miki's World");
        Raylib.SetWindowMinSize(Layout.ScreenW, Layout.ScreenH);
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);

        var metrics = new RaylibFontMetrics();
        game.EnableRendering();
        var frame = new Bitmap(Layout.ScreenW, Layout.ScreenH);
        var rgba = new byte[Layout.ScreenW * Layout.ScreenH * 4];
        Texture2D tex;
        unsafe
        {
            fixed (byte* p = rgba)
            {
                var image = new Image { Data = p, Width = Layout.ScreenW, Height = Layout.ScreenH, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
                tex = Raylib.LoadTextureFromImage(image);
            }
        }
        Raylib.SetTextureFilter(tex, TextureFilter.Point);

        game.Start();
        double acc = 0;
        int frames = 0;

        while (!Raylib.WindowShouldClose())
        {
            double dt = Raylib.GetFrameTime();
            acc += Math.Min(dt, 0.25);

            foreach (var (key, code, action) in Input.Map)
            {
                if (Raylib.IsKeyPressed(key)) game.KeyPressed(code, action);
                if (Raylib.IsKeyReleased(key)) game.KeyReleased(code, action);
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) break;

            while (acc >= StepMs / 1000.0)
            {
                game.Update(StepMs);
                acc -= StepMs / 1000.0;
            }
            if (game.WantsQuit) break;

            var canvas = new Canvas(frame) { Metrics = metrics };
            game.Draw(canvas);
            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                int p = frame.Pixels[i];
                rgba[i * 4] = (byte)(p >> 16); rgba[i * 4 + 1] = (byte)(p >> 8); rgba[i * 4 + 2] = (byte)p; rgba[i * 4 + 3] = 255;
            }
            unsafe { fixed (byte* p = rgba) Raylib.UpdateTexture(tex, p); }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
            int k = Math.Max(1, Math.Min(sw / Layout.ScreenW, sh / Layout.ScreenH));
            int ox = (sw - Layout.ScreenW * k) / 2, oy = (sh - Layout.ScreenH * k) / 2;
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, Layout.ScreenW, Layout.ScreenH),
                new Rectangle(ox, oy, Layout.ScreenW * k, Layout.ScreenH * k), Vector2.Zero, 0, Color.White);
            foreach (var t in canvas.Texts) metrics.Draw(t, ox, oy, k);
            Raylib.EndDrawing();

            if (shotAfter >= 0 && ++frames > shotAfter)
            {
                Raylib.TakeScreenshot("screenshot.png");
                break;
            }
        }
        Raylib.CloseWindow();
        return 0;
    }

    private static string FindJar(string[] args)
    {
        foreach (string a in args) if (a.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && File.Exists(a)) return a;
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
        {
            string c = Path.Combine(dir, "JAR", "MikisWorld.jar");
            if (File.Exists(c)) return c;
        }
        return null;
    }

    private static int ArgInt(string[] args, string name, int dflt)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name && int.TryParse(args[i + 1], out int v)) return v;
        return dflt;
    }
}

/// <summary>Sound is wired up separately; until then the host is silent.</summary>
internal sealed class SilentHost : IGameHost
{
    public void Play(Sfx sound) { }
    public void StopSound() { }
    public void Vibrate(int frequency, int milliseconds) { }
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
}

/// <summary>Text for bubbles/ending screen, drawn with Raylib's built-in font on top of the scaled pixel frame.</summary>
internal sealed class RaylibFontMetrics : IFontMetrics
{
    private readonly Font _font = Raylib.GetFontDefault();
    private static float Size(FontKind k) => k == FontKind.Bold ? 12 : 10;

    public int StringWidth(string s, FontKind font) => (int)Raylib.MeasureTextEx(_font, s, Size(font), 1).X;
    public int Height(FontKind font) => (int)Size(font);

    public void Draw(TextCommand t, int ox, int oy, int k)
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
