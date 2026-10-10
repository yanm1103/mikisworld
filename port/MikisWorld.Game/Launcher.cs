using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Start-up screen: pick the game and set the options that are the same for both (movement, video, volume).
/// Changes are saved straight away through <see cref="DisplaySettings.Changed"/>. The screen has a native size of 320x230 and follows the
/// same scaling options as the games (whole-number zoom with a snapping window, or fill the window).</summary>
internal static partial class Launcher
{
    public const int W0 = 320, H0 = 260;
    private const int RowH = 16, RowX = 30, RowW = 260, RowY = 60;     // the list of rows, in native units

    private enum Nav { None, Up, Down, Left, Right, Select, Back }

    private sealed record Row(string Label, Func<string> Value, Action<int> Change, bool Slider = false, int Game = 0);

    private static readonly Color Bg = new(60, 179, 210, 255), Panel = new(99, 48, 0, 255), Hi = new(160, 90, 20, 255);
    private static readonly Color Sub = new(255, 224, 160, 255);
    private static float _k = 1, _ox, _oy;                    // zoom and offset of the last drawn frame (to map the mouse)
    private static Vector2 _lastMouse;
    private static bool _padLast;                              // the last thing touched was a controller: show its buttons in the hints
    private static bool _stickLeft, _stickRight, _stickUp, _stickDown;

    /// <summary>Returns 1 or 2 (the game to start) or 0 to quit.</summary>
    /// <param name="script">developer aid: frame -> names (Up Down Left Right Enter Escape One) injected as if typed; <paramref name="shots"/> frame -> screenshot names</param>
    public static int Run(DisplaySettings s, InputBindings bind, Dictionary<int, List<string>> script = null, Dictionary<int, List<string>> shots = null, int quitAfter = -1)
    {
        int sel = 0;
        _frame = 0; _script = script; _shots = shots; _quitAfter = quitAfter;
        double resizedAt = -1;
        Raylib.PollInputEvents();                // the Enter that left the game must not also select a row here
        while (!Raylib.WindowShouldClose())
        {
            var rows = Rows(s);
            if (_openControls != null) { ControlsScreen(s, bind, _openControls.Value); _openControls = null; rows = Rows(s); }
            sel = Math.Clamp(sel, 0, rows.Count - 1);
            if (!_options && (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1))) return 1;
            if (!_options && (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2))) return 2;
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }

            var nav = Poll();
            Rumble.Update();
            if (script != null && script.TryGetValue(_frame, out var names))
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
                    if (Navigate(rows[sel].Label, ref sel)) break;
                    if (!rows[sel].Slider) rows[sel].Change(1);
                    break;
                case Nav.Back:
                    if (_options) { _options = false; sel = OptionsRow; break; }
                    return 0;
            }
            // mouse: hover highlights a row, a click activates it (left half of a slider lowers it, right half raises it)
            float wheel = Raylib.GetMouseWheelMove();            // the wheel moves the highlighted row, like in the game menus
            if (wheel != 0) sel = Math.Clamp(sel + (wheel < 0 ? 1 : -1), 0, rows.Count - 1);
            var mouse = Raylib.GetMousePosition();
            float lx = (mouse.X - _ox) / _k, ly = (mouse.Y - _oy) / _k;
            int hover = lx >= RowX - 2 && lx < RowX + RowW + 2 && ly >= RowY && ly < RowY + rows.Count * RowH ? (int)((ly - RowY) / RowH) : -1;
            if (hover >= 0)
            {
                if (mouse != _lastMouse) sel = hover;
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    sel = hover;
                    var r = rows[hover];
                    if (r.Game != 0) return r.Game;
                    if (r.Label == "Quit") return 0;
                    if (Navigate(r.Label, ref sel)) { }
                    else if (r.Slider) r.Change(lx < RowX + RowW / 2 ? -1 : +1); else r.Change(1);
                }
            }
            _lastMouse = mouse;
            bool borderless = Program.SyncFullscreen(s);
            Program.ApplyWindowScale(s, W0, H0, borderless, ref resizedAt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Draw(s, rows, sel);
            Raylib.EndDrawing();
            EndFrame();
            if (quitAfter >= 0 && _frame > quitAfter) return 0;
        }
        return 0;
    }

    private static Texture2D? _logo;
    private static bool _logoTried;

    /// <summary>The collection logo (embedded in the executable); null when it cannot be loaded, then the title is plain text.</summary>
    private static Texture2D? Logo()
    {
        if (_logoTried) return _logo;
        _logoTried = true;
        try
        {
            using var stream = typeof(Launcher).Assembly.GetManifestResourceStream("collectionlogo.png");
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();
            var image = Raylib.LoadImageFromMemory(".png", data);
            _logo = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { _logo = null; }
        return _logo;
    }

    // developer aids shared with the controls screen: frame counter, injected names, screenshots
    private static int _frame, _quitAfter = -1;
    private static Dictionary<int, List<string>> _script, _shots;

    /// <summary>Takes the screenshots asked for this frame and moves on to the next.</summary>
    private static void EndFrame()
    {
        if (_shots != null && _shots.TryGetValue(_frame, out var list)) foreach (string n in list) Raylib.TakeScreenshot($"shot_{n}.png");
        _frame++;
    }

    // the start-up screen has two pages: the games (and Options, Quit), and the Options page with everything else
    private static bool _options;
    private const int OptionsRow = 2;

    /// <summary>Rows that only switch page. Returns true when <paramref name="label"/> was one of them.</summary>
    private static bool Navigate(string label, ref int sel)
    {
        if (label == "Options") { _options = true; sel = 0; return true; }
        if (label == "Back") { _options = false; sel = OptionsRow; return true; }
        return false;
    }

    private static BindingDevice? _openControls;                  // a "Keyboard keys" / "Gamepad" row was activated: open its screen next frame

    private static List<Row> Rows(DisplaySettings s)
    {
        if (!_options)
            return new List<Row>
            {
                new("Miki's World 1", () => "The Lost Tiara", _ => { }, Game: 1),
                new("Miki's World 2", () => "The Storm Master", _ => { }, Game: 2),
                new("Options", () => ">", _ => { }),
                new("Quit", () => "", _ => { }),
            };
        var rows = new List<Row>
        {
            new("Movement", () => s.RevampedMovement ? "new" : "original", _ => { s.RevampedMovement = !s.RevampedMovement; s.NotifyChanged(); }),
            new("Vibration", () => s.Vibration ? "on" : "off", _ =>
            {
                s.Vibration = !s.Vibration;
                s.NotifyChanged();
                if (s.Vibration) Rumble.Pulse(100, 100);          // feedback: a short rumble when it is switched back on (nothing happens without a controller)
            }),
            new("Keyboard keys", () => "", d => { if (d > 0) _openControls = BindingDevice.Keyboard; }),
            new("Gamepad", () => "", d => { if (d > 0) _openControls = BindingDevice.Gamepad; }),
            new("Fullscreen", () => s.Fullscreen ? "on" : "off", _ => { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }),
            new("Integer scale", () => s.IntegerScale ? "on" : "off", _ => { s.IntegerScale = !s.IntegerScale; s.NotifyChanged(); }),
        };
        rows.Add(new("Filter", () => DisplaySettings.FilterName(s.Filter), d => s.StepFilter(d == 0 ? 1 : d), Slider: true));
        rows.Add(new("Ghosting", () => s.Ghosting ? "on" : "off", _ => { s.Ghosting = !s.Ghosting; s.NotifyChanged(); }));
        rows.Add(new("Sound", () => s.Volume + "%", d => { s.Volume = Math.Clamp(s.Volume + d * 5, 0, 100); s.NotifyChanged(); }, Slider: true));
        rows.Add(new("Back", () => "", _ => { }));
        return rows;
    }

    private static void Draw(DisplaySettings s, List<Row> rows, int sel)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        // same rule as the games: a whole-number zoom (black bars) or fill the window keeping the aspect ratio
        float k = s.IntegerScale ? Math.Max(1, Math.Min(w / W0, h / H0)) : Math.Min((float)w / W0, (float)h / H0);
        float ox = MathF.Floor((w - W0 * k) / 2), oy = MathF.Floor((h - H0 * k) / 2);
        _k = k; _ox = ox; _oy = oy;
        var font = Raylib.GetFontDefault();
        Raylib.SetTextureFilter(font.Texture, s.UsesBilinear ? TextureFilter.Bilinear : TextureFilter.Point);

        void Rect(float x, float y, float rw, float rh, Color c) => Raylib.DrawRectangle((int)(ox + x * k), (int)(oy + y * k), (int)MathF.Ceiling(rw * k), (int)MathF.Ceiling(rh * k), c);
        float Width(string t, int size) => Raylib.MeasureTextEx(font, t, size * k, size * k / 10f).X / k;
        void Text(string t, float x, float y, int size, Color c) => Raylib.DrawTextEx(font, t, new Vector2(ox + x * k, oy + y * k), size * k, size * k / 10f, c);
        void Centered(string t, float y, int size, Color c) => Text(t, (W0 - Width(t, size)) / 2, y, size, c);

        Rect(0, 0, W0, H0, Bg);
        var logo = Logo();
        if (logo.HasValue)
        {
            Raylib.SetTextureFilter(logo.Value, s.UsesBilinear ? TextureFilter.Bilinear : TextureFilter.Point);
            Raylib.DrawTexturePro(logo.Value, new Rectangle(0, 0, logo.Value.Width, logo.Value.Height),
                new Rectangle(ox + (W0 - logo.Value.Width) / 2 * k, oy + 4 * k, logo.Value.Width * k, logo.Value.Height * k), Vector2.Zero, 0, Color.White);
        }
        else Centered("Miki's World", 6, 30, Color.White);

        const int rowH = RowH, x0 = RowX, boxW = RowW, y0 = RowY;
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
            string shown = r.Game != 0 || r.Label == "Options" ? v : (r.Slider || i == sel ? $"< {v} >" : v);
            Text(shown, x0 + boxW - 4 - Width(shown, 10), ry + 4, 10, r.Game != 0 ? Sub : Color.White);
        }
        if (_padLast)
        {
            Centered("D-pad: choose                  A: select", H0 - 24, 10, Sub);
            Centered(_options ? "Start: select     B: back" : "Start: select     B: quit", H0 - 13, 10, Sub);
        }
        else
        {
            Centered("Arrows: choose                  Enter: select", H0 - 24, 10, Sub);
            Centered(_options ? "F11: fullscreen     Esc: back" : "1 / 2: play     F11: fullscreen     Esc: quit", H0 - 13, 10, Sub);
        }
    }

    // ---- input: keyboard (with repeat), gamepad buttons and the left stick as one navigation stream
    private static Nav Poll()
    {
        var key = KeyNav();
        if (key != Nav.None) { _padLast = false; return key; }
        var pad = PadNav();
        if (pad != Nav.None) _padLast = true;
        return pad;
    }

    private static Nav KeyNav()
    {
        bool K(KeyboardKey a) => Raylib.IsKeyPressed(a) || Raylib.IsKeyPressedRepeat(a);
        if (K(KeyboardKey.Up) || K(KeyboardKey.W)) return Nav.Up;
        if (K(KeyboardKey.Down) || K(KeyboardKey.S)) return Nav.Down;
        if (K(KeyboardKey.Left) || K(KeyboardKey.A)) return Nav.Left;
        if (K(KeyboardKey.Right) || K(KeyboardKey.D)) return Nav.Right;
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Space)) return Nav.Select;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return Nav.Back;
        return Nav.None;
    }

    private static Nav PadNav()
    {
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
