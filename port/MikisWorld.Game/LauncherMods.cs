using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>The mods page of the start-up screen: which mods are on, in which order, and what is suspicious about each one.</summary>
internal static partial class Launcher
{
    private static ModManager _mods;
    private static Func<int, IAssets> _original;
    private static bool _openMods;
    private const int ModRows = 7, ModX = 20, ModY = 44, ModW = 280, ModH = 16;

    private static void ModsScreen(DisplaySettings s)
    {
        _mods.Refresh();
        var notes = _mods.Mods.ToDictionary(m => m.Id, m => ModManager.Check(m, _original));
        foreach (var (id, list) in notes) foreach (string n in list) LogConsole.Log($"mod {id}: {n}");
        int sel = 0, top = 0;
        double resizedAt = -1;
        Raylib.PollInputEvents();

        while (!Raylib.WindowShouldClose())
        {
            var list = _mods.Mods;
            if (Raylib.IsKeyPressed(KeyboardKey.F11)) { s.Fullscreen = !s.Fullscreen; s.NotifyChanged(); }
            string[] injected = _script != null && _script.TryGetValue(_frame, out var inj) ? inj.ToArray() : Array.Empty<string>();
            var nav = Poll();
            foreach (string n in injected)
                nav = n switch { "Up" => Nav.Up, "Down" => Nav.Down, "Left" => Nav.Left, "Right" => Nav.Right, "Enter" => Nav.Select, "Escape" => Nav.Back, _ => nav };
            if (list.Count > 0)
            {
                sel = Math.Clamp(sel, 0, list.Count - 1);
                switch (nav)
                {
                    case Nav.Up: sel = (sel + list.Count - 1) % list.Count; break;
                    case Nav.Down: sel = (sel + 1) % list.Count; break;
                    case Nav.Select: _mods.SetEnabled(list[sel], !_mods.IsEnabled(list[sel])); break;
                    case Nav.Left: _mods.Move(list[sel], -1); sel = Math.Max(0, sel - 1); break;      // up the list: loses to the ones below
                    case Nav.Right: _mods.Move(list[sel], +1); sel = Math.Min(list.Count - 1, sel + 1); break;
                }
            }
            if (nav == Nav.Back) { Leave(); return; }
            if (sel < top) top = sel;
            if (sel >= top + ModRows) top = sel - ModRows + 1;

            var mouse = Raylib.GetMousePosition();
            float lx = (mouse.X - _ox) / _k, ly = (mouse.Y - _oy) / _k;
            int hr = lx >= ModX && lx < ModX + ModW && ly >= ModY && ly < ModY + Math.Min(ModRows, list.Count) * ModH ? top + (int)((ly - ModY) / ModH) : -1;
            if (hr >= 0 && hr < list.Count)
            {
                if (mouse != _lastMouse) sel = hr;
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { sel = hr; _mods.SetEnabled(list[hr], !_mods.IsEnabled(list[hr])); }
            }
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0 && list.Count > 0) sel = Math.Clamp(sel + (wheel < 0 ? 1 : -1), 0, list.Count - 1);
            if (Raylib.IsMouseButtonPressed(MouseButton.Right)) { Leave(); return; }
            _lastMouse = mouse;

            bool borderless = Program.SyncFullscreen(s);
            Program.ApplyWindowScale(s, W0, H0, borderless, ref resizedAt);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            DrawMods(s, list, notes, sel, top);
            Raylib.EndDrawing();
            EndFrame();
            if (_quitAfter >= 0 && _frame > _quitAfter) return;
        }
    }

    private static string GameTag(ModInfo m) => m.Game == 0 ? "1+2" : m.Game.ToString();

    private static void DrawMods(DisplaySettings s, List<ModInfo> list, Dictionary<string, List<string>> notes, int sel, int top)
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
        string Fit(string t, float max, int size)
        {
            if (Width(t, size) <= max) return t;
            while (t.Length > 1 && Width(t + "...", size) > max) t = t[..^1];
            return t + "...";
        }

        Rect(0, 0, W0, H0, Bg);
        Centered("Mods", 12, 20, Color.White);
        Rect(ModX - 4, ModY - 4, ModW + 8, ModRows * ModH + 8, Color.White);
        Rect(ModX - 3, ModY - 3, ModW + 6, ModRows * ModH + 6, Panel);
        if (list.Count == 0)
        {
            Text("No mods found. Put a folder per mod in:", ModX + 4, ModY + 4, 10, Color.White);
            int line = 0;
            foreach (string root in ModManager.DefaultRoots(AppContext.BaseDirectory))
                Text(Fit(root, ModW - 8, 8), ModX + 4, ModY + 22 + 12 * line++, 8, Sub);
        }
        for (int n = 0; n < ModRows && top + n < list.Count; n++)
        {
            var m = list[top + n];
            float ry = ModY + n * ModH;
            if (top + n == sel) Rect(ModX - 2, ry, ModW + 4, ModH - 1, Hi);
            bool on = _mods.IsEnabled(m);
            Text(on ? "[x]" : "[ ]", ModX + 4, ry + 4, 10, on ? new Color(160, 255, 160, 255) : Color.White);
            Text(Fit(m.Name, ModW - 90, 10), ModX + 28, ry + 4, 10, Color.White);
            string tag = "G" + GameTag(m) + (notes[m.Id].Count > 0 ? $"  !{notes[m.Id].Count}" : "");
            Text(tag, ModX + ModW - 4 - Width(tag, 10), ry + 4, 10, notes[m.Id].Count > 0 ? new Color(255, 200, 120, 255) : Sub);
        }
        // details of the selected mod
        if (list.Count > 0)
        {
            var m = list[Math.Clamp(sel, 0, list.Count - 1)];
            float y = ModY + ModRows * ModH + 10;
            string by = (m.Author.Length > 0 ? "by " + m.Author : "") + (m.Version.Length > 0 ? "  v" + m.Version : "");
            if (by.Length > 0) Text(Fit(by, ModW, 8), ModX, y, 8, Sub);
            if (m.Description.Length > 0) Text(Fit(m.Description, ModW, 8), ModX, y + 11, 8, Color.White);
            var list2 = notes[m.Id];
            for (int i = 0; i < Math.Min(3, list2.Count); i++) Text(Fit("! " + list2[i], ModW, 8), ModX, y + 24 + 11 * i, 8, new Color(255, 200, 120, 255));
            if (list2.Count > 3) Text($"... and {list2.Count - 3} more (see --logs)", ModX, y + 24 + 33, 8, new Color(255, 200, 120, 255));
        }
        Centered("Enter: on/off     Left/Right: priority     Esc: back", H0 - 14, 8, Sub);
    }
}
