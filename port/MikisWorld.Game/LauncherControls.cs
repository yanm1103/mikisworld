using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>The key / button remapping screens of the start-up screen (the same bindings the games' own Options use).</summary>
internal static partial class Launcher
{
    private const int CtlRowH = 16, CtlX = 20, CtlW = 280, CtlY = 44, CtlLabelW = 76;

    /// <summary>Opens the remapping screen of one device and returns when the player leaves it.</summary>
    private static void ControlsScreen(DisplaySettings s, InputBindings bind, BindingDevice device)
    {
        var acts = InputBindings.Actions;
        int rows = acts.Length + 1;                    // the last row resets the device to its defaults
        int row = 0, col = 0;
        bool capturing = false;
        long noticeUntil = 0;
        double resizedAt = -1;
        Raylib.PollInputEvents();                      // the key that opened the screen must not also act inside it

        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }

            string[] injected = _script != null && _script.TryGetValue(_frame, out var inj) ? inj.ToArray() : Array.Empty<string>();
            if (capturing)
            {
                string name = device == BindingDevice.Keyboard ? CaptureKey() : DevicePoller.PressedPadInput();
                if (name == null && injected.Length > 0) name = injected[0];                  // developer aid: a name stands for the key pressed
                if (name != null)
                {
                    capturing = false;
                    if (name != (device == BindingDevice.Keyboard ? "Escape" : "Start")) bind.Set(device, acts[row], col, name);   // Esc / Start cancel
                }
            }
            else
            {
                var nav = Poll();
                foreach (string n in injected)
                    nav = n switch { "Up" => Nav.Up, "Down" => Nav.Down, "Left" => Nav.Left, "Right" => Nav.Right, "Enter" => Nav.Select, "Escape" => Nav.Back, _ => nav };
                bool clear = Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressed(KeyboardKey.Delete);
                for (int p = 0; p < 4; p++) if (Raylib.IsGamepadAvailable(p) && Raylib.IsGamepadButtonPressed(p, GamepadButton.RightFaceLeft)) clear = true;
                switch (nav)
                {
                    case Nav.Up: row = (row + rows - 1) % rows; break;
                    case Nav.Down: row = (row + 1) % rows; break;
                    case Nav.Left: case Nav.Right: col = 1 - col; break;
                    case Nav.Select:
                        if (row == acts.Length) { bind.Reset(device); noticeUntil = _frame + 90; }
                        else { capturing = true; BeginCapture(); }
                        break;
                    case Nav.Back: Leave(); return;
                }
                if (clear && row < acts.Length) bind.Clear(device, acts[row], col);
            }

            // mouse: hover selects, a click on a cell captures, on the last row resets, right click leaves
            var mouse = Raylib.GetMousePosition();
            float lx = (mouse.X - _ox) / _k, ly = (mouse.Y - _oy) / _k;
            int hr = lx >= CtlX && lx < CtlX + CtlW && ly >= CtlY && ly < CtlY + rows * CtlRowH ? (int)((ly - CtlY) / CtlRowH) : -1;
            if (hr >= 0 && !capturing)
            {
                int hc = lx >= CtlX + CtlLabelW + (CtlW - CtlLabelW) / 2 ? 1 : 0;
                if (mouse != _lastMouse) { row = hr; col = hc; }
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    row = hr; col = hc;
                    if (row == acts.Length) { bind.Reset(device); noticeUntil = _frame + 90; }
                    else { capturing = true; BeginCapture(); }
                }
            }
            else if (capturing && Raylib.IsMouseButtonPressed(MouseButton.Left)) capturing = false;
            if (Raylib.IsMouseButtonPressed(MouseButton.Right)) { if (capturing) capturing = false; else { Leave(); return; } }
            _lastMouse = mouse;

            bool borderless = Program.SyncFullscreen(s);
            Program.ApplyWindowScale(s, W0, H0, borderless, ref resizedAt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            DrawControlsScreen(s, bind, device, row, col, capturing, _frame < noticeUntil);
            Raylib.EndDrawing();
            EndFrame();
            if (_quitAfter >= 0 && _frame > _quitAfter) return;
        }
    }

    /// <summary>Back to the list: the key that closed this screen must not also quit the start-up screen.</summary>
    private static void Leave()
    {
        Raylib.PollInputEvents();
        EndFrame();
    }

    private static void BeginCapture()
    {
        while (Raylib.GetKeyPressed() != 0) { }          // drop what was typed before
        DevicePoller.PrimePadCapture();
    }

    private static string CaptureKey()
    {
        int k = (int)Raylib.GetKeyPressed();
        return k == 0 ? null : DevicePoller.KeyName((KeyboardKey)k);
    }

    private static void DrawControlsScreen(DisplaySettings s, InputBindings bind, BindingDevice device, int row, int col, bool capturing, bool restored)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
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
        Centered(device == BindingDevice.Keyboard ? "Keyboard keys" : "Gamepad", 12, 20, Color.White);

        var acts = InputBindings.Actions;
        Rect(CtlX - 4, CtlY - 4, CtlW + 8, (acts.Length + 1) * CtlRowH + 8, Color.White);
        Rect(CtlX - 3, CtlY - 3, CtlW + 6, (acts.Length + 1) * CtlRowH + 6, Panel);
        float cellW = (CtlW - CtlLabelW) / 2f;
        for (int r = 0; r <= acts.Length; r++)
        {
            float ry = CtlY + r * CtlRowH;
            if (r == acts.Length)
            {
                if (row == r) Rect(CtlX - 2, ry, CtlW + 4, CtlRowH - 1, Hi);
                Text("Reset to defaults", CtlX + 4, ry + 4, 10, row == r ? Color.White : new Color(255, 144, 144, 255));
                continue;
            }
            Text(InputBindings.Label(acts[r]), CtlX + 4, ry + 4, 10, Sub);
            for (int c = 0; c < InputBindings.Slots; c++)
            {
                float cx = CtlX + CtlLabelW + c * cellW;
                bool sel = r == row && c == col;
                if (sel) Rect(cx - 2, ry, cellW, CtlRowH - 1, capturing ? new Color(200, 60, 20, 255) : Hi);
                Text(sel && capturing ? "..." : InputBindings.Short(bind.Get(device, acts[r], c)), cx + 2, ry + 4, 10, Color.White);
            }
        }
        string hint1 = restored ? "Defaults restored"
            : capturing ? (device == BindingDevice.Keyboard ? "Press a key  (Esc: cancel)" : "Press a button  (Start: cancel)")
            : device == BindingDevice.Gamepad && _padLast ? "A: change     X: clear     B: back" : "Enter: change     Backspace: clear     Esc: back";
        Centered(hint1, H0 - 14, 10, Sub);
    }
}
