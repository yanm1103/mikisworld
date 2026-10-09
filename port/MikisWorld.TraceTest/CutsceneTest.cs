using MikisWorld.Core;

// Regression test: speech bubbles switched on by one cutscene must not survive into the next one.
// (The original allocates the visibility flags in every Load; a bubble of the intro such as "Let me tell you a story.."
//  once stayed on screen during the final cutscene.)
internal static class CutsceneTest
{
    public static int Run(string dir1, string dir2)
    {
        int fail = 0;
        fail += Check("MikisWorld 1", () =>
        {
            var assets = new DirAssets(dir1);
            var p = new CutscenePlayer(assets, new NullHost(), 28, 28, GameStrings.Load(assets).TextCount);
            return (p.Load, p.Update, () => p.Finished, () => p.TextVisible);
        });
        fail += Check("MikisWorld 2", () =>
        {
            var assets = new DirAssets(dir2);
            var p = new CutscenePlayer2(assets, new NullHost(), GameStrings.Load(assets, "en", 18).TextCount, (x, y, t) => { });
            return (p.Load, p.Update, () => p.Finished, () => p.TextVisible);
        });
        Console.WriteLine(fail == 0 ? "ALL OK" : $"{fail} FAILED");
        return fail == 0 ? 0 : 1;
    }

    private static int Check(string name, Func<(Action<string> load, Action update, Func<bool> finished, Func<bool[]> flags)> make)
    {
        var (load, update, finished, flags) = make();
        load("begin.bin");
        // run the intro until a bubble is on, then "skip" (load the outro while it is still visible)
        int guard = 0;
        while (!flags().Any(b => b) && !finished() && guard++ < 20000) update();
        bool wasOn = flags().Any(b => b);
        load("end.bin");
        bool leaked = flags().Any(b => b);
        bool ok = wasOn && !leaked;
        Console.WriteLine($"{(ok ? "  ok   " : "  FAIL ")}{name}: a bubble was on in the intro ({wasOn}); none left after loading end.bin ({!leaked})");
        return ok ? 0 : 1;
    }
}
