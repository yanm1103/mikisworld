using MikisWorld.Game;

/// <summary>The glide between two zoom levels of the extended view.</summary>
internal static class ZoomTest
{
    private static int _fail;
    private static void Check(bool ok, string what) { if (!ok) _fail++; Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}"); }

    public static int Run()
    {
        var z = new ZoomTransition();
        Check(!z.Running(0) && z.PictureZoom(0, 3) == 3, "nothing glides at first, and the picture is made for the wanted zoom");

        // zoom out: from 4 to 3 (the picture of zoom 3 shows more)
        z.Start(10.0, 4f, 3f, 4);
        Check(z.Running(10.0) && z.Scale(10.0) == 4f, "it starts at the scale being shown");
        Check(z.PictureZoom(10.05, 3) == 3, "zooming out: the larger picture (zoom 3) is made at once");
        double prev = 4;
        bool monotonic = true;
        for (double t = 10.0; t < 10.15; t += 0.005) { float s = z.Scale(t); monotonic &= s <= prev + 1e-6; prev = s; }
        Check(monotonic && prev > 3f && prev < 3.1f, $"the scale only goes down while it glides ({prev:0.000} just before the end)");
        Check(!z.Running(10.151) && z.Scale(10.151) == 3f, "after 150 ms it is over, at the new scale");
        float mid = z.Scale(10.0);                                              // (restarting is allowed any time)
        z.Start(20.0, 4f, 3f, 4);
        Check(Math.Abs(z.Scale(20.075) - 3.5f) < 1e-4, "halfway through, the scale is halfway (the ease is symmetric)");
        Check(z.Scale(20.01) - 4f > -0.02f && z.Scale(20.14) - 3f < 0.02f, "slow at both ends");

        // zoom in: from 3 to 5 (the picture of zoom 3 is the larger one, it is kept while gliding)
        z.Start(30.0, 3f, 5f, 3);
        Check(z.PictureZoom(30.05, 5) == 3, "zooming in: the larger picture of the old zoom is kept while it glides");
        Check(z.PictureZoom(30.2, 5) == 5, "and the picture of the new zoom takes over at the end");
        bool up = true; prev = 3;
        for (double t = 30.0; t < 30.15; t += 0.005) { float s = z.Scale(t); up &= s >= prev - 1e-6; prev = s; }
        Check(up, "the scale only goes up while it glides");

        // a new change in the middle of a glide starts from where the picture is
        z.Start(40.0, 4f, 3f, 4);
        float now = z.Scale(40.05);
        z.Start(40.05, now, 2f, 3);
        Check(z.Scale(40.05) == now && z.PictureZoom(40.06, 2) == 2, "a change in the middle starts from the scale being shown");
        Console.WriteLine(_fail == 0 ? "ALL OK" : $"{_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }
}
