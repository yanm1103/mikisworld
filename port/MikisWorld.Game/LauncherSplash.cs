using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>The splash picture shown once when the program starts (embedded: <c>splash.png</c>, drawn for the 320x290 of the start screen).
/// It stays for 2.5 seconds; any key, mouse button or gamepad button skips it.</summary>
internal static partial class Launcher
{
    public const double SplashSeconds = 2.5;

    /// <summary>Returns when the time is up or the player skipped it. Returns at once if the picture cannot be loaded.</summary>
    private static void Splash(DisplaySettings s)
    {
        Texture2D tex;
        try
        {
            using var stream = typeof(Launcher).Assembly.GetManifestResourceStream("splash.png");
            if (stream == null) return;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var image = Raylib.LoadImageFromMemory(".png", ms.ToArray());
            tex = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { return; }

        double start = Raylib.GetTime(), resizedAt = -1;
        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }
            bool skip = Raylib.GetKeyPressed() != 0 || Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonPressed(MouseButton.Right)
                        || Raylib.GetGamepadButtonPressed() != 0;
            if (_script != null && _script.TryGetValue(_frame, out var names) && names.Count > 0) skip = true;      // developer aid: any injected name
            if (skip || Raylib.GetTime() - start >= SplashSeconds) break;

            bool borderless = Program.SyncFullscreen(s);
            Program.ApplyWindowScale(s, W0, H0, borderless, ref resizedAt);
            int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
            // same rule as the start screen: a whole-number zoom, or as large as fits; the picture keeps its own proportions
            float k = s.IntegerScale ? Math.Max(1, Math.Min(w / (float)tex.Width, h / (float)tex.Height)) : Math.Min(w / (float)tex.Width, h / (float)tex.Height);
            if (s.IntegerScale) k = MathF.Max(1, MathF.Floor(k));
            float dw = tex.Width * k, dh = tex.Height * k;
            Raylib.SetTextureFilter(tex, s.UsesBilinear ? TextureFilter.Bilinear : TextureFilter.Point);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), new Rectangle(MathF.Floor((w - dw) / 2), MathF.Floor((h - dh) / 2), dw, dh), System.Numerics.Vector2.Zero, 0, Color.White);
            Raylib.EndDrawing();
            EndFrame();
        }
        Raylib.UnloadTexture(tex);
    }
}
