using System.Reflection;
using MikisWorld.Core;

// Reported bugs: hammering the "enter portal" key (a) made the screen transition twice and (b) crashed the game.
// Cause (both are in the original too): a key press during the wipe stays pending, and after the wipe the stale "portal under the player"
// of the AREA JUST LEFT is used in the new area (for the exit portal of a sub-area that is a portal index with no destination -> array overflow).
// Revamped movement ignores presses while a transition runs and ignores portals without destination.
internal static class PortalSpamTest
{
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    private static void Set(object o, string name, object v) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

    private sealed class Rig
    {
        public object G;
        public Func<int> State;
        public Action<int, int> Press, Release, Teleport;
        public Action Tick;
        public Action<bool> SetRevamped;
    }

    private static Rig Make(int game, string dir, bool revamped)
    {
        var assets = new DirAssets(dir);
        var r = new Rig();
        if (game == 1)
        {
            var x = new Game(assets, new NullHost(), 5); x.Tweaks.RevampedMovement = revamped; x.Start();
            r.G = x; r.State = () => x.CurrentState; r.Press = x.KeyPressed; r.Release = x.KeyReleased; r.Tick = () => x.Update(20); r.Teleport = x.DebugTeleport;
        }
        else
        {
            var x = new Game2(assets, new NullHost(), 5); x.Tweaks.RevampedMovement = revamped; x.Start();
            r.G = x; r.State = () => x.CurrentState; r.Press = x.KeyPressed; r.Release = x.KeyReleased; r.Tick = () => x.Update(20, 0); r.Teleport = x.DebugTeleport;
        }
        for (int i = 0; i < 6000 && r.State() != State.Playing; i++) { r.Press(53, 8); r.Release(53, 8); r.Tick(); }
        return r;
    }

    private static int fails;
    private static void Check(bool ok, string what) { if (!ok) fails++; Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}"); }

    public static int Run(string dir1, string dir2)
    {
        foreach (int game in new[] { 1, 2 })
        {
            string dir = game == 1 ? dir1 : dir2;
            Console.WriteLine($"== MikisWorld {game}");

            // (a) enter a sub-area, then press "leave" twice, the second time while the transition runs
            (int transitions, string crash, int interactOnArrival) Leave(bool revamped)
            {
                var r = Make(game, dir, revamped);
                var cells = Field<short[]>(r.G, "_portalCells");
                int w = Field<TileLayer>(r.G, "_map").Width, tw = Field<int>(r.G, "_tileW"), th = Field<int>(r.G, "_tileH");
                r.Teleport((cells[0] % w * tw) << 4, ((cells[0] / w + 1) * th) << 4);
                r.Ticks(3);
                r.Press(56, 6); r.Release(56, 6);
                r.Ticks(150);                                           // wipe in + wipe out: we are in the sub-area now
                int interact = -1;
                for (int c = cells.Length / 2; c < cells.Length && interact == -1; c++)     // the exit portals sit after the entry portals in the list
                {
                    r.Teleport((cells[c] % w * tw) << 4, ((cells[c] / w + 1) * th) << 4);
                    r.Ticks(2);
                    interact = Field<int>(r.G, "_interact");
                }
                int count = 0, last = r.State();
                string crash = null;
                try
                {
                    for (int t = 0; t < 400; t++)
                    {
                        if (t == 0 || t == 10) { r.Press(56, 6); r.Release(56, 6); }
                        r.Tick();
                        if (r.State() == State.WipeIn && last != State.WipeIn) count++;
                        last = r.State();
                    }
                }
                catch (IndexOutOfRangeException) { crash = "IndexOutOfRange"; }
                return (count, crash, interact);
            }
            var o = Leave(false); var v = Leave(true);
            Console.WriteLine($"     standing on the exit portal on arrival: {(o.interactOnArrival != -1)}");
            Check(o.transitions >= 2 || o.crash != null, $"original: the second press is replayed in the main area (transitions {o.transitions}, crash {o.crash ?? "none"})");
            Check(v.transitions == 1 && v.crash == null, $"revamped: one transition, no crash (transitions {v.transitions}, crash {v.crash ?? "none"})");

            // (b) a portal with no destination, standing in the main area
            foreach (bool revamped in new[] { false, true })
            {
                var r = Make(game, dir, revamped);
                var cells = Field<short[]>(r.G, "_portalCells");
                int destinations = Field<short[]>(r.G, "_subAreaX").Length;
                if (cells.Length <= destinations) { Console.WriteLine("     (every portal has a destination)"); continue; }
                Set(r.G, "_interact", (int)cells[destinations]);
                r.Press(56, 6);
                bool crashed = false;
                try { r.Ticks(300); } catch (IndexOutOfRangeException) { crashed = true; }
                Check(crashed == !revamped, revamped ? "revamped: the portal without destination is ignored" : "original: the portal without destination crashes (documented bug, kept)");
            }
        }
        Console.WriteLine(fails == 0 ? "ALL OK" : $"{fails} FAILED");
        return fails == 0 ? 0 : 1;
    }

    private static void Ticks(this Rig r, int n) { for (int i = 0; i < n; i++) r.Tick(); }
}
