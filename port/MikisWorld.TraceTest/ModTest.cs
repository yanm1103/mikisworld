using System.Text.Json;
using MikisWorld.Core;
using MikisWorld.Game;

/// <summary>Mods: discovery, priority, per-game files, the save key, the checks, reading changed files again, and what a sprite mod does to the picture.</summary>
internal static class ModTest
{
    private static int _fail;
    private static void Check(bool ok, string what) { if (!ok) _fail++; Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}"); }

    private static void Write(string path, byte[] data) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, data); }
    private static void Json(string dir, object manifest) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "mod.json"), JsonSerializer.Serialize(manifest)); }

    /// <summary>The same PNG with every colour mapped (the tests only need pixels that differ from the original's).</summary>
    private static byte[] Recolour(string pngPath, Func<int, int> map)
    {
        var img = StbImageSharp.ImageResult.FromMemory(File.ReadAllBytes(pngPath), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        for (int i = 0; i < img.Data.Length; i += 4)
        {
            int c = map(img.Data[i] << 16 | img.Data[i + 1] << 8 | img.Data[i + 2]);
            img.Data[i] = (byte)(c >> 16); img.Data[i + 1] = (byte)(c >> 8); img.Data[i + 2] = (byte)c;
        }
        using var ms = new MemoryStream();
        new StbImageWriteSharp.ImageWriter().WritePng(img.Data, img.Width, img.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, ms);
        return ms.ToArray();
    }

    public static int Run(string dir1, string dir2)
    {
        string root = Path.Combine(Path.GetTempPath(), "mw_modtest_" + Environment.ProcessId);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        string mods = Path.Combine(root, "mods"), state = Path.Combine(root, "state.json");
        IAssets orig1 = new DirAssets(dir1);
        IAssets orig2 = new DirAssets(dir2);
        IAssets Orig(int g) => g == 2 ? orig2 : orig1;

        // three mods: art for game 1; a sound + art for game 2 + a picture of game 1's size; a level mod for game 1; and a broken manifest
        string art = Path.Combine(mods, "art"), both = Path.Combine(mods, "both"), lvl = Path.Combine(mods, "levels");
        Write(Path.Combine(art, "player.png"), Recolour(Path.Combine(dir1, "player.png"), c => c ^ 0xFFFFFF));
        Json(art, new { name = "Inverted Miki", author = "t", version = "1", game = 1 });
        Write(Path.Combine(both, "coin.wav"), new byte[] { 1, 2, 3 });
        Write(Path.Combine(both, "game2", "player.png"), Recolour(Path.Combine(dir2, "player.png"), c => c ^ 0x00FF00));
        Write(Path.Combine(both, "tiles.png"), Recolour(Path.Combine(dir1, "tiles.png"), c => c));       // the size of game 1's sheet: wrong for game 2
        Json(both, new { name = "Both", game = 0 });
        Write(Path.Combine(lvl, "level.bin"), File.ReadAllBytes(Path.Combine(dir1, "level.bin")));
        Json(lvl, new { name = "Levels", game = 1, version = "2" });
        Directory.CreateDirectory(Path.Combine(mods, "broken"));
        File.WriteAllText(Path.Combine(mods, "broken", "mod.json"), "{ not json");

        Console.WriteLine("discovery");
        var m = new ModManager(new[] { mods }, state);
        Check(m.Mods.Select(x => x.Id).SequenceEqual(new[] { "art", "both", "broken", "levels" }), "four folders found, in alphabetical order: " + string.Join(",", m.Mods.Select(x => x.Id)));
        Check(m.Mods.First(x => x.Id == "art").Name == "Inverted Miki" && m.Mods.First(x => x.Id == "broken").Problems.Count == 1, "names come from mod.json; a broken manifest is reported, not fatal");
        Check(m.ActiveFor(1).Count == 0, "nothing is on at first");

        Console.WriteLine("what is on");
        foreach (var x in m.Mods.Where(x => x.Id is "art" or "both" or "levels")) m.SetEnabled(x, true);
        Check(m.ActiveFor(1).Select(x => x.Id).SequenceEqual(new[] { "art", "both", "levels" }) && m.ActiveFor(2).Select(x => x.Id).SequenceEqual(new[] { "both" }), "a mod applies to the game(s) it names");
        Check(new ModManager(new[] { mods }, state).ActiveFor(1).Count == 3, "the choice is remembered");
        var other = new ModManager(new[] { mods }, state);
        other.Move(other.Mods.First(x => x.Id == "levels"), -10);
        Check(other.Mods[0].Id == "levels" && new ModManager(new[] { mods }, state).Mods[0].Id == "levels", "the order can be changed and is remembered");
        Check(new ModManager(new[] { mods }, state) { Disabled = true }.ActiveFor(1).Count == 0, "--no-mods switches them all off");
        m = new ModManager(new[] { mods }, state);                       // the order of this run's manager: levels first

        Console.WriteLine("files over the original");
        var a1 = new ModAssets(orig1, m.ActiveFor(1), 1);
        Check(!a1.ReadResource("player.png").SequenceEqual(orig1.ReadResource("player.png")), "game 1 gets the mod's player.png");
        Check(a1.ReadResource("level.bin").SequenceEqual(orig1.ReadResource("level.bin")) && a1.ReadResource("sky.png").SequenceEqual(orig1.ReadResource("sky.png")), "other files fall through to the original");
        var a2 = new ModAssets(orig2, m.ActiveFor(2), 2);
        Check(a2.ReadResource("player.png").SequenceEqual(File.ReadAllBytes(Path.Combine(mods, "both", "game2", "player.png"))), "game2/ files apply to game 2 only");
        Check(a2.Exists("coin.wav") && a1.Exists("coin.wav") && !orig1.Exists("coin.wav"), "files the original does not have (a replacement sound) can be found");
        Check(a1.ImageSize("player.png") == orig1.ImageSize("player.png"), "sizes are read from the mod's file");
        File.Copy(Path.Combine(dir1, "tiles.png"), Path.Combine(art, "player.png"), true);
        Check(a1.ReadResource("player.png").SequenceEqual(File.ReadAllBytes(Path.Combine(dir1, "tiles.png"))), "a changed file is seen at once (nothing is cached): this is what F5 needs");
        Write(Path.Combine(art, "player.png"), Recolour(Path.Combine(dir1, "player.png"), c => c ^ 0xFFFFFF));

        Console.WriteLine("priority: the later mod wins");
        Write(Path.Combine(lvl, "player.png"), Recolour(Path.Combine(dir1, "player.png"), c => c ^ 0x0000FF));
        var top = new ModAssets(orig1, m.ActiveFor(1), 1);               // order: levels, art, both -> 'both' is last but has no game-1 player.png; 'art' wins over 'levels'
        Check(top.ReadResource("player.png").SequenceEqual(File.ReadAllBytes(Path.Combine(art, "player.png"))), "of two mods with the same file, the one lower in the list is used");
        File.Delete(Path.Combine(lvl, "player.png"));

        Console.WriteLine("the save follows the mods that change the game");
        string key1 = m.SaveKey(1), key2 = m.SaveKey(2);
        Check(key1.Length == 8 && key2.Length == 0, $"game 1 (a level mod is on) has its own save code '{key1}'; game 2 (only art/sound) keeps the original save");
        m.SetEnabled(m.Mods.First(x => x.Id == "levels"), false);
        Check(m.SaveKey(1).Length == 0, "with only pictures and sounds on, the original save is used");
        m.SetEnabled(m.Mods.First(x => x.Id == "levels"), true);
        Check(m.SaveKey(1) == key1, "the code is stable");

        Console.WriteLine("checks");
        var notes = m.Mods.ToDictionary(x => x.Id, x => ModManager.Check(x, Orig));
        Check(notes["art"].Count == 0, "a correct mod has no remarks: " + string.Join("; ", notes["art"]));
        Check(notes["both"].Any(n => n.Contains("tiles.png") && n.Contains("expects") && n.Contains("game 2")) && !notes["both"].Any(n => n.Contains("(game 1)")), "a picture of the wrong size for one game is reported for that game only: " + string.Join("; ", notes["both"]));
        Check(notes["broken"].Count == 1, "the broken manifest shows up as a remark");
        Write(Path.Combine(mods, "broken", "key.ott"), new byte[] { 0, 1 });
        Write(Path.Combine(mods, "broken", "notes.txt"), new byte[] { 65 });
        Write(Path.Combine(mods, "broken", "wrong.png"), Recolour(Path.Combine(dir1, "sky.png"), c => c));
        var nb = ModManager.Check(m.Mods.First(x => x.Id == "broken"), Orig);
        Check(nb.Any(n => n.Contains("key.ott")) && nb.Any(n => n.Contains("notes.txt")) && nb.Any(n => n.Contains("wrong.png")), "a broken sound, an unknown file and an unknown picture are reported: " + string.Join("; ", nb));

        Console.WriteLine("the picture changes with the mod and with a reload");
        var plain = DrawGame(new Game(orig1, new NullHost(), 5));
        var withMod = new Game(a1, new NullHost(), 5);
        var modded = DrawGame(withMod);
        Check(!plain.SequenceEqual(modded), "a sprite mod changes what is drawn");
        Write(Path.Combine(art, "player.png"), File.ReadAllBytes(Path.Combine(dir1, "player.png")));           // the artist puts the original back
        withMod.ReloadAssets();
        var reloaded = new Bitmap(withMod.ScreenWidth, withMod.ScreenHeight);
        withMod.Draw(new Canvas(reloaded));
        Check(reloaded.Pixels.SequenceEqual(plain), "ReloadAssets (F5) picks the edited file up: the picture is the original's again");

        Directory.Delete(root, true);
        Console.WriteLine(_fail == 0 ? "ALL OK" : $"{_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>Starts a fresh game, gets past the intro and returns one drawn frame.</summary>
    private static int[] DrawGame(Game g)
    {
        g.EnableRendering();
        g.Start();
        for (int i = 0; i < 3000 && g.CurrentState != State.Playing; i++) { g.KeyPressed(53, 8); g.KeyReleased(53, 8); g.Update(20); }
        for (int i = 0; i < 60; i++) g.Update(20);
        var b = new Bitmap(g.ScreenWidth, g.ScreenHeight);
        g.Draw(new Canvas(b));
        return b.Pixels;
    }
}
