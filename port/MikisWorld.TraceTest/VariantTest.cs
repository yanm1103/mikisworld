using MikisWorld.Core;

// Smoke test for the other screen-size builds of the games (128x128 game 1, 176x144 game 2): loads the game from an extracted JAR,
// plays through the intro, walks around in the world and in the bonus minigame, and writes PNGs of what it draws.
//   MikisWorld.TraceTest variant <assetsDir> <1|2> <outDir>
internal static class VariantTest
{
    public static int Run(string dir, int game, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var assets = new DirAssets(dir);
        IMikisGame g = game == 1 ? new Game(assets, new NullHost(), 7) : new Game2(assets, new NullHost(), 7);
        g.BonusUnlocked = true;
        g.EnableRendering();
        g.Start();
        int W = g.ScreenWidth, H = g.ScreenHeight;
        Console.WriteLine($"game {game}: screen {W}x{H}");
        var bmp = new Bitmap(W, H);
        var cv = new Canvas(bmp);
        int shot = 0;
        void Save(string tag)
        {
            g.Draw(cv);
            var rgba = new byte[bmp.Pixels.Length * 4];
            for (int i = 0; i < bmp.Pixels.Length; i++)
            {
                rgba[i * 4] = (byte)(bmp.Pixels[i] >> 16); rgba[i * 4 + 1] = (byte)(bmp.Pixels[i] >> 8); rgba[i * 4 + 2] = (byte)bmp.Pixels[i]; rgba[i * 4 + 3] = 255;
            }
            using var fs = File.Create(Path.Combine(outDir, $"{shot++:00}_{tag}.png"));
            new StbImageWriteSharp.ImageWriter().WritePng(rgba, W, H, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
        }
        g.ChooseMode(0);
        for (int t = 0; t < 4000; t++)
        {
            if (t % 15 == 0) g.KeyPressed(53, 8);           // skip the cutscene / confirm
            if (t % 15 == 3) g.KeyReleased(53, 8);
            g.Update(20, t * 20);
            if (t == 100 || t == 150 || t == 200 || t == 250 || t == 300 || t == 400 || t == 600 || t == 1500 || t == 3999) Save($"t{t}_state{g.CurrentState}");
        }
        for (int t = 0; t < 400; t++)
        {
            if (t == 5) g.KeyPressed(54, 5);
            g.Update(20, 100000 + t * 20);
            if (t == 100 || t == 399) Save($"walk{t}");
        }
        g.ReleaseAll();
        // extended view: the picture is larger than the original screen
        {
            int vw = W * 2 + 40, vh = H * 2;
            g.SetView(vw, vh);
            var big = new Bitmap(vw, vh); var bc = new Canvas(big);
            g.Update(20, 200000);
            g.Draw(bc);
            var rgba = new byte[big.Pixels.Length * 4];
            for (int i = 0; i < big.Pixels.Length; i++) { rgba[i*4]=(byte)(big.Pixels[i]>>16); rgba[i*4+1]=(byte)(big.Pixels[i]>>8); rgba[i*4+2]=(byte)big.Pixels[i]; rgba[i*4+3]=255; }
            using var fs = File.Create(Path.Combine(outDir, $"{shot++:00}_extended.png")); new StbImageWriteSharp.ImageWriter().WritePng(rgba, vw, vh, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
            g.SetView(W, H);
        }
        // bonus minigame
        var g2 = game == 1 ? (IMikisGame)new Game(assets, new NullHost(), 7) : new Game2(assets, new NullHost(), 7);
        g2.BonusUnlocked = true; g2.EnableRendering(); g2.Start(); g2.ChooseMode(1);
        g = g2;
        for (int t = 0; t < 60; t++) g.Update(20, t * 20);
        Save("bonus");
        g.DrawBackdrop(cv, 100);
        { var rgba = new byte[bmp.Pixels.Length * 4]; for (int i = 0; i < bmp.Pixels.Length; i++) { rgba[i*4]=(byte)(bmp.Pixels[i]>>16); rgba[i*4+1]=(byte)(bmp.Pixels[i]>>8); rgba[i*4+2]=(byte)bmp.Pixels[i]; rgba[i*4+3]=255; }
          using var fs = File.Create(Path.Combine(outDir, $"{shot++:00}_backdrop.png")); new StbImageWriteSharp.ImageWriter().WritePng(rgba, W, H, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs); }
        Console.WriteLine("ALL OK (no crash); look at the PNGs in " + outDir);
        return 0;
    }
}
