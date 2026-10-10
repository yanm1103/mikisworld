using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Start-up screen: pick the game and set the options that are the same for both (movement, video, volume).
/// Changes are saved straight away through <see cref="DisplaySettings.Changed"/>.</summary>
internal static class Launcher
{
    private enum Nav { None, Up, Down, Left, Right, Select, Back }

    private sealed record Row(string Label, Func<string> Value, Action<int> Change, bool Slider = false, int Game = 0);

    private static readonly Color Bg = new(60, 179, 210, 255), Panel = new(99, 48, 0, 255), Hi = new(160, 90, 20, 255);
    private static readonly Color Sub = new(255, 224, 160, 255);
    private static bool _stickLeft, _stickRight, _stickUp, _stickDown;

    /// <summary>Returns 1 or 2 (the game to start) or 0 to quit.</summary>
    /// <param name="script">developer aid: frame -> names (Up Down Left Right Enter Escape One Two) injected as if typed; <paramref name="shots"/> frame -> screenshot names</param>
    public static int Run(DisplaySettings s, Dictionary<int, List<string>> script = null, Dictionary<int, List<string>> shots = null, int quitAfter = -1)
    {
        int sel = 0, frame = 0;
        string[] hints = { "Arrows: choose / change    Enter: select    1 / 2: play    Esc: quit", "" };
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
                    nav = n switch { "Up" => Nav.Up, "Down" => Nav.Down, "Left" => Nav.Left, "Right" => Nav.Right, "Enter" => Nav.Select, "Escape" => Nav.Back, "One" => Nav.None, _ => nav };
            if (script != null && script.TryGetValue(frame, out var pick) && pick.Contains("One")) return 1;
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
            Program.SyncFullscreen(s);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Bg);
            Draw(rows, sel, hints[0]);
            Raylib.EndDrawing();
            if (shots != null && shots.TryGetValue(frame, out var list)) foreach (string n in list) Raylib.TakeScreenshot($"shot_{n}.png");
            if (quitAfter >= 0 && ++frame > quitAfter) return 0;
            if (quitAfter < 0) frame++;
        }
        return 0;
    }

    private static List<Row> Rows(DisplaySettings s)
    {
        var rows = new List<Row>
        {
            new("Miki's World 1", () => "The Lost Tiara", _ => { }, Game: 1),
            new("Miki's World 2", () => "The Storm Master", _ => { }, Game: 2),
            new("Movement", () => s.RevampedMovement ? "revamped" : "original", _ => { s.RevampedMovement = !s.RevampedMovement; s.NotifyChanged(); }),
            new("Fullscreen", () => s.Fullscreen ? "on" : "off", _ => { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }),
            new("Integer scale", () => s.IntegerScale ? "on" : "off", _ => { s.IntegerScale = !s.IntegerScale; s.NotifyChanged(); }),
        };
        if (!s.IntegerScale)
            rows.Add(new("Filter", () => s.Bilinear ? "bilinear" : "nearest", _ => { s.Bilinear = !s.Bilinear; s.NotifyChanged(); }));
        rows.Add(new("Sound", () => s.Volume + "%", d => { s.Volume = Math.Clamp(s.Volume + d * 5, 0, 100); s.NotifyChanged(); }, Slider: true));
        rows.Add(new("Quit", () => "", _ => { }));
        return rows;
    }

    private static void Draw(List<Row> rows, int sel, string hint)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        float k = Math.Max(0.5f, Math.Min(w / 640f, h / 460f));   // layout is designed for 640x460
        int fs = (int)(22 * k), fsSmall = (int)(15 * k), rowH = (int)(34 * k);
        string title = "Miki's World";
        Raylib.DrawText(title, (w - Raylib.MeasureText(title, (int)(40 * k))) / 2, (int)(10 * k), (int)(40 * k), Color.White);

        int boxW = (int)(520 * k), x = (w - boxW) / 2, y = (int)(66 * k);
        Raylib.DrawRectangle(x - 8, y - 8, boxW + 16, rows.Count * rowH + 16, Panel);
        Raylib.DrawRectangleLines(x - 8, y - 8, boxW + 16, rows.Count * rowH + 16, Color.White);
        for (int i = 0; i < rows.Count; i++)
        {
            int ry = y + i * rowH;
            if (i == sel) Raylib.DrawRectangle(x - 4, ry, boxW + 8, rowH - 2, Hi);
            var r = rows[i];
            // the two games first, then a gap-less list of options
            Raylib.DrawText(r.Game != 0 ? "Play  " + r.Label : r.Label, x + 8, ry + (rowH - fs) / 2, fs, Color.White);
            string v = r.Value();
            if (v.Length == 0) continue;
            string shown = r.Game != 0 ? v : (r.Slider || i == sel ? $"< {v} >" : v);
            Raylib.DrawText(shown, x + boxW - 8 - Raylib.MeasureText(shown, fs), ry + (rowH - fs) / 2, fs, r.Game != 0 ? Sub : Color.White);
        }
        Raylib.DrawText("Settings are shared by both games and saved automatically.", (w - Raylib.MeasureText("Settings are shared by both games and saved automatically.", fsSmall)) / 2, h - (int)(46 * k), fsSmall, Color.White);
        Raylib.DrawText(hint, (w - Raylib.MeasureText(hint, fsSmall)) / 2, h - (int)(24 * k), fsSmall, Sub);
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
