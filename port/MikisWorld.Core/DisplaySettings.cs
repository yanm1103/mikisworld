namespace MikisWorld.Core;

/// <summary>User options shared by both games: sound level and how the 176x144 / 128x128 picture is put on the window.
/// The frontend reads/saves it; the <see cref="App"/> menu edits it.</summary>
public sealed class DisplaySettings
{
    /// <summary>Master sound level, 0-100 %. It scales the amplitude of the square wave the ring tones are rendered with.</summary>
    public int Volume { get; set; } = 100;

    /// <summary>true: the picture is magnified by a whole number (crisp pixels, black bars). false: it fills the window keeping its aspect ratio.</summary>
    public bool IntegerScale { get; set; } = true;

    /// <summary>Only used when <see cref="IntegerScale"/> is off: smooth the magnification (bilinear) instead of nearest-neighbour.</summary>
    public bool Bilinear { get; set; }

    /// <summary>The original games zero ALL movement whenever ANY key is released (phone keypads). With this on, a direction that is
    /// still held keeps working after you release another button (e.g. hold right, tap jump).</summary>
    public bool KeepDirection { get; set; } = true;

    /// <summary>Borderless full screen. Not persisted: the game always starts in a window.</summary>
    public bool Fullscreen { get; set; }

    /// <summary>Raised by the menu after any change, so the frontend can apply and save it.</summary>
    public event Action Changed;

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>Bilinear is only meaningful (and only offered) without integer scaling.</summary>
    public bool UsesBilinear => !IntegerScale && Bilinear;
}
