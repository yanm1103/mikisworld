using MikisWorld.Core;

// Engine tweaks (slow jump, slow walking animation, revamped movement) against the original behaviour, on both games.
// With every tweak off the golden traces already prove the engine is the original; here we check what the tweaks change.
internal static class TweakTest
{
    private sealed class Rig
    {
        public Func<EngineTweaks> Tweaks;
        public Action<int, int> Press, Release;
        public Action Tick;
        public Func<string> Line;
        public Action<int, int> Teleport;
        public Action ReleaseAll;

        public int[] F() => Line().Split(' ').Take(8).Select(int.Parse).ToArray();   // tick state area motion sprite px py camX
        public int State => F()[1];
        public int Motion => F()[3];
        public int Sprite => F()[4];
        public int X => F()[5];
        public int Y => F()[6];
        public void Ticks(int n) { for (int i = 0; i < n; i++) Tick(); }
    }

    private static Rig Make(int game, string dir)
    {
        var assets = new DirAssets(dir);
        Rig r = new();
        if (game == 1)
        {
            var g = new Game(assets, new NullHost(), 1234);
            g.Start();
            r.Tweaks = () => g.Tweaks; r.Press = g.KeyPressed; r.Release = g.KeyReleased; r.Tick = () => g.Update(20);
            r.Line = () => g.DebugLine(0); r.Teleport = g.DebugTeleport; r.ReleaseAll = g.ReleaseAll;
        }
        else
        {
            var g = new Game2(assets, new NullHost(), 1234);
            g.Start();
            r.Tweaks = () => g.Tweaks; r.Press = g.KeyPressed; r.Release = g.KeyReleased; r.Tick = () => g.Update(20, 0);
            r.Line = () => g.DebugLine(0); r.Teleport = g.DebugTeleport; r.ReleaseAll = g.ReleaseAll;
        }
        for (int i = 0; i < 6000 && r.State != State.Playing; i++)       // skip the intro cutscene
        {
            r.Press(53, 8); r.Release(53, 8); r.Tick();
        }
        r.Ticks(40);                                                       // settle on the ground
        return r;
    }

    private static int fails;
    private static void Check(bool ok, string what)
    {
        if (!ok) fails++;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}");
    }

    public static int Run(string dir1, string dir2)
    {
        foreach (int game in new[] { 1, 2 })
        {
            string dir = game == 1 ? dir1 : dir2;
            Console.WriteLine($"== MikisWorld {game}");

            // ---- jump: same height and distance, twice the time
            (int air, int rise, int dist) Jump(bool slow, int key, int action)
            {
                var r = Make(game, dir);
                r.Tweaks().SlowJump = slow;
                int y0 = r.Y, x0 = r.X, top = 0, air = 0;
                r.Press(key, action);
                int prev = 0;
                for (int i = 0; i < 200; i++)
                {
                    r.Tick();
                    if (i == 0) r.Release(key, action);                    // the original forgets a key that was released before the tick
                    if (prev is >= 3 and <= 5 || r.Motion is >= 3 and <= 5) air++;
                    prev = r.Motion;
                    top = Math.Max(top, y0 - r.Y);
                    if (i > 5 && r.Motion == 0) break;
                }
                return (air, top, r.X - x0);
            }
            var up0 = Jump(false, 50, 1); var up1 = Jump(true, 50, 1);
            Check(Math.Abs(up1.air - up0.air * 2) <= 1, $"straight jump lasts twice as long (+-1 tick: landing is detected per half step) ({up0.air} -> {up1.air} ticks)");
            Check(up1.rise == up0.rise, $"same height ({up0.rise} -> {up1.rise}, fixed point)");
            var rj0 = Jump(false, 51, 0); var rj1 = Jump(true, 51, 0);
            Check(Math.Abs(rj1.air - rj0.air * 2) <= 1 && rj1.rise == rj0.rise && Math.Abs(rj1.dist - rj0.dist) <= 60,
                $"diagonal jump: twice the time, same height and distance (ticks {rj0.air}->{rj1.air}, height {rj0.rise}->{rj1.rise}, dx {rj0.dist}->{rj1.dist})");

            // ---- walking animation: each sprite twice as long
            int Run(bool slow)
            {
                var r = Make(game, dir);
                r.Tweaks().SlowWalkAnimation = slow;
                r.Press(54, 5);
                var runs = new List<int>();
                int run = 0, last = -1;
                for (int i = 0; i < 26; i++)
                {
                    r.Tick();
                    if (r.Sprite == last) run++;
                    else { if (last >= 0) runs.Add(run); run = 1; last = r.Sprite; }
                }
                runs.RemoveAt(0);                                  // the first run starts mid-cycle
                return runs.Distinct().Count() == 1 ? runs[0] : -1;
            }
            int a0 = Run(false), a1 = Run(true);
            // jogo 1: walking on flat ground flickers in the original (irregular runs, reported as -1), the tweak makes it a clean 6-tick cycle
            Check((a0 == 3 || a0 == -1) && a1 == 6, $"walk sprite changes every {(a0 < 0 ? "1-3 (irregular flicker)" : a0.ToString())} ticks -> every {a1}");

            // ---- holding jump: original keeps bouncing, revamped jumps once per press
            int Jumps(bool revamped)
            {
                var r = Make(game, dir);
                r.Tweaks().RevampedMovement = revamped;
                r.Press(50, 1);                                   // pressed once and held
                int starts = 0, prev = r.Motion;
                for (int i = 0; i < 150; i++)
                {
                    r.Tick();
                    if (r.Motion == 4 && prev != 4) starts++;
                    prev = r.Motion;
                }
                return starts;
            }
            int j0 = Jumps(false), j1 = Jumps(true);
            Check(j0 > 1 && j1 == 1, $"held jump key: {j0} jumps in the original, {j1} revamped");
            {
                var r = Make(game, dir);
                r.Tweaks().RevampedMovement = true;
                r.Press(50, 1); r.Tick(); r.Release(50, 1); r.Ticks(120);
                Check(r.Motion == 0, "landed after the first jump");
                r.Press(50, 1);
                r.Tick();
                Check(r.Motion == 4, "a new press jumps again");
            }

            // ---- steering while falling: press left, then release right (the original forgets that left is still held)
            int Drift(bool revamped, bool keys = true)
            {
                var r = Make(game, dir);
                r.Tweaks().RevampedMovement = revamped;
                r.Teleport(r.F()[5], r.F()[6] - (100 << 4));
                if (keys) r.Press(54, 5);
                r.Ticks(2);
                if (keys) r.Press(52, 2);
                r.Ticks(1);
                if (keys) r.Release(54, 5);
                int x0 = r.X;
                r.Ticks(4);
                return r.X - x0;
            }
            int d0 = Drift(false), d1 = Drift(true);
            int baseline = Drift(false, false);
            Check(d0 == baseline, $"original: releasing the other key stops the steering (dx {d0}, nothing pressed {baseline})");
            Check(d1 < 0, $"revamped: keeps going left while falling (dx {d1})");

            // ---- a diagonal jump to the left, then right is pressed in mid-air: revamped turns around, the original keeps its momentum
            int Turn(bool revamped)
            {
                var r = Make(game, dir);
                r.Tweaks().RevampedMovement = revamped;
                r.Press(49, 0);                                   // jump + left
                r.Ticks(4);
                r.Press(54, 5);                                   // right, while still in the air and while left is still held
                int x0 = r.X;
                r.Ticks(6);
                return r.X - x0;
            }
            int t0 = Turn(false), t1 = Turn(true);
            Check(t0 < 0 && t1 > 0, $"mid-air direction change: original keeps going left ({t0}), revamped turns right ({t1})");

            // ---- and after the menu opens the held keys are forgotten
            {
                var r = Make(game, dir);
                r.Tweaks().RevampedMovement = true;
                r.Press(54, 5); r.Tick(); r.ReleaseAll(); r.Ticks(2);
                int x = r.X;
                r.Ticks(3);
                Check(r.X == x, "ReleaseAll forgets held directions (no walking on)");
            }
        }
        // ---- the wipe of game 2: the original snapshot is 12x12 (the screen goes black and comes straight back); the tweak makes it a whole screen
        {
            int SnapshotWidth(bool full)
            {
                var g = new Game2(new DirAssets(dir2), new NullHost(), 1234);
                g.Tweaks.FullWipeSnapshot = full;
                g.EnableRendering();
                g.Start();
                for (int i = 0; i < 6000 && g.CurrentState != State.Playing; i++) { g.KeyPressed(53, 8); g.KeyReleased(53, 8); g.Update(20, 0); }
                typeof(GameBase).GetMethod("SnapshotScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
                var bmp = (Bitmap)typeof(GameBase).GetField("_snapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g);
                return bmp.Width;
            }
            int w0 = SnapshotWidth(false), w1 = SnapshotWidth(true);
            Check(w0 == 12 && w1 == 128, $"game 2 wipe snapshot: {w0} px in the original (a bug), {w1} px with the tweak");
        }

        // ---- ...and what is on screen half way through a portal wipe. The canvas keeps the previous frame, so the original's tiny snapshot
        //      leaves the old area where it was (it only gets covered by black), while the full snapshot makes the old area slide away like in game 1.
        {
            (double slid, double stayed) Wipe(bool full)
            {
                var g = new Game2(new DirAssets(dir2), new NullHost(), 1234);
                g.Tweaks.FullWipeSnapshot = full;
                g.EnableRendering();
                g.Start();
                for (int i = 0; i < 6000 && g.CurrentState != State.Playing; i++) { g.KeyPressed(53, 8); g.KeyReleased(53, 8); g.Update(20, 0); }
                var f = typeof(GameBase).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                T Get<T>(string n) => (T)f.First(x => x.Name == n).GetValue(g);
                var cells = Get<short[]>("_portalCells");
                int w = Get<TileLayer>("_map").Width, tw = Get<int>("_tileW"), th = Get<int>("_tileH");
                g.DebugTeleport((cells[0] % w * tw) << 4, ((cells[0] / w + 1) * th) << 4);
                var bmp = new Bitmap(128, 128);
                int[] before = null;
                for (int i = 0; i < 3; i++) { g.Update(20, 0); g.Draw(new Canvas(bmp)); }
                before = (int[])bmp.Pixels.Clone();
                g.KeyPressed(56, 6); g.KeyReleased(56, 6);
                for (int i = 0; i < 200; i++)
                {
                    g.Update(20, 0);
                    if (g.CurrentState == State.Playing) { g.Draw(new Canvas(bmp)); before = (int[])bmp.Pixels.Clone(); continue; }
                    g.Draw(new Canvas(bmp));
                    if (g.CurrentState == State.WipeIn && Get<int>("_frame") == 17) break;
                }
                int n = 128 * 17 / 35, slide = 0, stay = 0, total = 0;
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128 - n; x++)
                    {
                        if (before[y * 128 + x] == before[y * 128 + x + n]) continue;       // only places where "slid" and "stayed" can be told apart
                        total++;
                        if (bmp.Pixels[y * 128 + x + n] == before[y * 128 + x]) slide++;
                        if (bmp.Pixels[y * 128 + x + n] == before[y * 128 + x + n]) stay++;
                    }
                if (total < 500) throw new InvalidOperationException("the frame before the wipe is too uniform for this test: " + total);
                return ((double)slide / total, (double)stay / total);
            }
            var o = Wipe(false); var t = Wipe(true);
            Check(t.slid > 0.9 && o.stayed > 0.9 && o.slid < 0.5, $"game 2 mid-wipe: the old area stays put in the original ({o.stayed:P0} unchanged) and slides away with the tweak ({t.slid:P0} shifted, like game 1)");
        }

        // ---- Mork (game 2): same moves, 2/3 of the speed
        {
            double Mork(int percent)
            {
                var assets = new DirAssets(dir2);
                var g = new Game2(assets, new NullHost(), 1234);
                g.Tweaks.MorkSpeedPercent = percent;
                g.Start();
                for (int i = 0; i < 6000 && g.CurrentState != State.Playing; i++) { g.KeyPressed(53, 8); g.KeyReleased(53, 8); g.Update(20, 0); }
                for (int i = 0; i < 40; i++) g.Update(20, 0);
                var t = typeof(Game2);
                void Set(string n, object v) => t.GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, v);
                int Get(string n) => (int)t.GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g);
                Set("_morkX", Get("_px") - (4000 << 4)); Set("_morkY", Get("_py")); Set("_morkState", 2); Set("_morkTimer", 1000000);
                int x0 = Get("_morkX");
                for (int i = 0; i < 300; i++) g.Update(20, 0);
                return Get("_morkX") - x0;
            }
            double full = Mork(100), slow = Mork(67);
            double ratio = slow / full;
            Check(full > 0 && Math.Abs(ratio - 2.0 / 3) < 0.02, $"Mork moves at 2/3 of the speed (moved {slow:F0} instead of {full:F0}: {ratio:P0})");
        }
        Console.WriteLine(fails == 0 ? "ALL OK" : $"{fails} FAILED");
        return fails == 0 ? 0 : 1;
    }
}
