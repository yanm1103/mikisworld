using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Start-up screen: pick the game and set the options that are the same for both (movement, video, volume).
/// Changes are saved straight away through <see cref="DisplaySettings.Changed"/>. The screen has a native size of 320x230 and follows the
/// same scaling options as the games (whole-number zoom with a snapping window, or fill the window).</summary>
internal static class Launcher
{
    public const int W0 = 320, H0 = 230;

    private enum Nav { None, Up, Down, Left, Right, Select, Back }

    private sealed record Row(string Label, Func<string> Value, Action<int> Change, bool Slider = false, int Game = 0);

    private static readonly Color Bg = new(60, 179, 210, 255), Panel = new(99, 48, 0, 255), Hi = new(160, 90, 20, 255);
    private static readonly Color Sub = new(255, 224, 160, 255);
    private static bool _stickLeft, _stickRight, _stickUp, _stickDown;

    /// <summary>Returns 1 or 2 (the game to start) or 0 to quit.</summary>
    /// <param name="script">developer aid: frame -> names (Up Down Left Right Enter Escape One) injected as if typed; <paramref name="shots"/> frame -> screenshot names</param>
    public static int Run(DisplaySettings s, Dictionary<int, List<string>> script = null, Dictionary<int, List<string>> shots = null, int quitAfter = -1)
    {
        int sel = 0, frame = 0;
        double resizedAt = -1;
        Raylib.PollInputEvents();                // the Enter that left the game must not also select a row here
        while (!Raylib.WindowShouldClose())
        {
            var rows = Rows(s);
            sel = Math.Clamp(sel, 0, rows.Count - 1);
            if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1)) return 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2)) return 2;
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }

            var nav = Poll();
            if (script != null && script.TryGetValue(frame, out var names))
                foreach (string n in names)
                {
                    if (n == "One") return 1;
                    if (n == "F11") { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }
                    nav = n switch { "Up" => Nav.Up, "Down" => Nav.Down, "Left" => Nav.Left, "Right" => Nav.Right, "Enter" => Nav.Select, "Escape" => Nav.Back, _ => nav };
                }
            switch (nav)
            {
                case Nav.Up: sel = (sel + rows.Count - 1) % rows.Count; break;
                case Nav.Down: sel = (sel + 1) % rows.Count; break;
                case Nav.Left: rows[sel].Change(-1); break;
                case Nav.Right: if (rows[sel].Slider || rows[sel].Game == 0) rows[sel].Change(1); break;
                case Nav.Select:
                    if (rows[sel].Game != 0) return rows[sel].Game;
                    if (rows[sel].Label == "Quit") return 0;
                    if (!rows[sel].Slider) rows[sel].Change(1);
                    break;
                case Nav.Back: return 0;
            }
            bool borderless = Program.SyncFullscreen(s);
            Program.ApplyWindowScale(s, W0, H0, borderless, ref resizedAt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Draw(s, rows, sel);
            Raylib.EndDrawing();
            if (shots != null && shots.TryGetValue(frame, out var list)) foreach (string n in list) Raylib.TakeScreenshot($"shot_{n}.png");
            frame++;
            if (quitAfter >= 0 && frame > quitAfter) return 0;
        }
        return 0;
    }

    private static List<Row> Rows(DisplaySettings s)
    {
        var rows = new List<Row>
        {
            new("Miki's World 1", () => "The Lost Tiara", _ => { }, Game: 1),
            new("Miki's World 2", () => "The Storm Master", _ => { }, Game: 2),
            new("Movement", () => s.RevampedMovement ? "new" : "original", _ => { s.RevampedMovement = !s.RevampedMovement; s.NotifyChanged(); }),
            new("Fullscreen", () => s.Fullscreen ? "on" : "off", _ => { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }),
            new("Integer scale", () => s.IntegerScale ? "on" : "off", _ => { s.IntegerScale = !s.IntegerScale; s.NotifyChanged(); }),
        };
        if (!s.IntegerScale)
            rows.Add(new("Filter", () => s.Bilinear ? "bilinear" : "nearest", _ => { s.Bilinear = !s.Bilinear; s.NotifyChanged(); }));
        rows.Add(new("Sound", () => s.Volume + "%", d => { s.Volume = Math.Clamp(s.Volume + d * 5, 0, 100); s.NotifyChanged(); }, Slider: true));
        rows.Add(new("Quit", () => "", _ => { }));
        return rows;
    }

    private static void Draw(DisplaySettings s, List<Row> rows, int sel)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        // same rule as the games: a whole-number zoom (black bars) or fill the window keeping the aspect ratio
        float k = s.IntegerScale ? Math.Max(1, Math.Min(w / W0, h / H0)) : Math.Min((float)w / W0, (float)h / H0);
        float ox = MathF.Floor((w - W0 * k) / 2), oy = MathF.Floor((h - H0 * k) / 2);
        var font = Raylib.GetFontDefault();
        Raylib.SetTextureFilter(font.Texture, s.UsesBilinear ? TextureFilter.Bilinear : TextureFilter.Point);

        void Rect(float x, float y, float rw, float rh, Color c) => Raylib.DrawRectangle((int)(ox + x * k), (int)(oy + y * k), (int)MathF.Ceiling(rw * k), (int)MathF.Ceiling(rh * k), c);
        float Width(string t, int size) => Raylib.MeasureTextEx(font, t, size * k, size * k / 10f).X / k;
        void Text(string t, float x, float y, int size, Color c) => Raylib.DrawTextEx(font, t, new Vector2(ox + x * k, oy + y * k), size * k, size * k / 10f, c);
        void Centered(string t, float y, int size, Color c) => Text(t, (W0 - Width(t, size)) / 2, y, size, c);

        Rect(0, 0, W0, H0, Bg);
        Centered("Miki's World", 6, 30, Color.White);

        const int rowH = 17, x0 = 30, boxW = 260, y0 = 40;
        Rect(x0 - 4, y0 - 4, boxW + 8, rows.Count * rowH + 8, Color.White);
        Rect(x0 - 3, y0 - 3, boxW + 6, rows.Count * rowH + 6, Panel);
        for (int i = 0; i < rows.Count; i++)
        {
            float ry = y0 + i * rowH;
            if (i == sel) Rect(x0 - 2, ry, boxW + 4, rowH - 1, Hi);
            var r = rows[i];
            Text(r.Game != 0 ? "Play  " + r.Label : r.Label, x0 + 4, ry + 4, 10, Color.White);
            string v = r.Value();
            if (v.Length == 0) continue;
            string shown = r.Game != 0 ? v : (r.Slider || i == sel ? $"< {v} >" : v);
            Text(shown, x0 + boxW - 4 - Width(shown, 10), ry + 4, 10, r.Game != 0 ? Sub : Color.White);
        }
        Centered("Settings are shared and saved automatically.", H0 - 36, 10, Color.White);
        Centered("Arrows: choose / change     Enter: select", H0 - 24, 10, Sub);
        Centered("1 / 2: play     F11: fullscreen     Esc: quit", H0 - 13, 10, Sub);
    }

    // ---- input: keyboard (with repeat), gamepad buttons and the left stick as one navigation stream
    private static Nav Poll()
    {
        bool K(KeyboardKey a) => Raylib.IsKeyPressed(a) || Raylib.IsKeyPressedRepeat(a);
        if (K(KeyboardKey.Up) || K(KeyboardKey.W)) return Nav.Up;
        if (K(KeyboardKey.Down) || K(KeyboardKey.S)) return Nav.Down;
        if (K(KeyboardKey.Left) || K(KeyboardKey.A)) return Nav.Left;
        if (K(KeyboardKey.Right) || K(KeyboardKey.D)) return Nav.Right;
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Space)) return Nav.Select;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return Nav.Back;

        for (int pad = 0; pad < 4; pad++)
        {
            if (!Raylib.IsGamepadAvailable(pad)) continue;
            float x = Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftX), y = Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftY);
            bool l = x < -0.6f, r = x > 0.6f, u = y < -0.6f, d = y > 0.6f;
            Nav stick = l && !_stickLeft ? Nav.Left : r && !_stickRight ? Nav.Right : u && !_stickUp ? Nav.Up : d && !_stickDown ? Nav.Down : Nav.None;
            _stickLeft = l; _stickRight = r; _stickUp = u; _stickDown = d;
            if (stick != Nav.None) return stick;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.LeftFaceUp)) return Nav.Up;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.LeftFaceDown)) return Nav.Down;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.LeftFaceLeft)) return Nav.Left;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.LeftFaceRight)) return Nav.Right;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.RightFaceDown) || Raylib.IsGamepadButtonPressed(pad, GamepadButton.MiddleRight)) return Nav.Select;
            if (Raylib.IsGamepadButtonPressed(pad, GamepadButton.RightFaceRight)) return Nav.Back;
        }
        return Nav.None;
    }
}
