namespace MikisWorld.Core;

/// <summary>Sound effects (original class <c>c</c>); each maps to one Nokia .ott ring-tone file.</summary>
public enum Sfx
{
    OnceUponATime, // onceuponatime.ott — intro cutscene jingle
    Bonus,         // bonus.ott         — all coins collected
    Cage,          // cage.ott          — bird freed
    Coin,          // coin.ott
    Damage,        // damage.ott
    GameEnd,       // gameend.ott       — outro / minigame won
    GameOver,      // gameover.ott
    GameStart,     // gamestart.ott
    Key,           // key.ott
    Lord,          // lord.ott          — a Lord appears / minigame lost
    Title,         // title.ott
    Transportation // transportation.ott — screen wipe between areas
}

/// <summary>Everything the game logic needs from the platform.</summary>
public interface IGameHost
{
    void Play(Sfx sound);
    void StopSound();
    void Vibrate(int frequency, int milliseconds);
}

public sealed class NullHost : IGameHost
{
    public void Play(Sfx sound) { }
    public void StopSound() { }
    public void Vibrate(int frequency, int milliseconds) { }
}

/// <summary>Read-only access to the game's resources (the files that were inside the JAR).</summary>
public interface IAssets
{
    byte[] ReadResource(string name);               // e.g. "level.bin"
    (int Width, int Height) ImageSize(string name); // e.g. "player.png"
    Bitmap LoadBitmap(string name);                 // decoded PNG; only needed when rendering

    /// <summary>Is there a resource with this name? (mods can add files the original does not have, such as replacement sounds)</summary>
    bool Exists(string name)
    {
        try { ReadResource(name); return true; }
        catch (Exception e) when (e is IOException or KeyNotFoundException or InvalidOperationException) { return false; }
    }
}
