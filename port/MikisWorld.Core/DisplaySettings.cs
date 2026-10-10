namespace MikisWorld.Core;

/// <summary>How the picture is magnified onto the window. The first two are plain texture filters; the rest are shader effects the
/// frontend implements (the Core only keeps the choice).</summary>
/// <summary>How much of the level the picture shows: exactly what the original screen showed, or more (as much as the window and the map allow).</summary>
public enum DisplayView { Original, Extended }

public enum DisplayFilter { Nearest, Bilinear, Sharp, XBRZ, ScaleFX, LCD, CRT }

/// <summary>User options shared by both games: sound level and how the 176x144 / 128x128 picture is put on the window.
/// The frontend reads/saves it; the <see cref="App"/> menu edits it.</summary>
public sealed class DisplaySettings
{
    /// <summary>Master sound level, 0-100 %. It scales the amplitude of the square wave the ring tones are rendered with.</summary>
    public int Volume { get; set; } = 100;

    /// <summary>true: the picture is magnified by a whole number (crisp pixels, black bars). false: it fills the window keeping its aspect ratio.</summary>
    public bool IntegerScale { get; set; } = true;

    /// <summary>"Extended" draws more of the level around the original screen (the game logic does not change).</summary>
    public DisplayView View { get; set; } = DisplayView.Original;

    /// <summary>Extended view only: window pixels per game pixel (0 = automatic: the picture is about 1.5 times as tall as the original screen).
    /// The + and - keys change it.</summary>
    public int ViewZoom { get; set; }

    /// <summary>The magnification filter. It works with whole-number scaling too (the picture is then filtered at that exact size).</summary>
    public DisplayFilter Filter { get; set; } = DisplayFilter.Nearest;

    /// <summary>LCD-like ghosting: a pixel takes a few frames to reach its new colour, leaving a faint trail (the phone screens did this).</summary>
    public bool Ghosting { get; set; }

    public static readonly DisplayFilter[] Filters = (DisplayFilter[])Enum.GetValues(typeof(DisplayFilter));

    public static string FilterName(DisplayFilter f) => f switch
    {
        DisplayFilter.Nearest => "nearest",
        DisplayFilter.Bilinear => "bilinear",
        DisplayFilter.Sharp => "sharp",
        DisplayFilter.XBRZ => "xBRZ",
        DisplayFilter.ScaleFX => "ScaleFX",
        DisplayFilter.LCD => "LCD",
        DisplayFilter.CRT => "CRT",
        _ => f.ToString()
    };

    /// <summary>Next / previous filter in the list (wraps around).</summary>
    public void StepFilter(int dir)
    {
        int n = Filters.Length;
        Filter = Filters[((int)Filter + dir % n + n) % n];
        NotifyChanged();
    }

    /// <summary>true: "revamped" movement (a held jump key does not jump again and again; held directions survive releasing another key,
    /// so you can steer while falling). false: exactly the original behaviour. Same for both games.</summary>
    public bool RevampedMovement { get; set; } = true;

    /// <summary>The original's "Vibrator" option: rumble the controller when Miki is hit (never for the keyboard).</summary>
    public bool Vibration { get; set; } = true;

    /// <summary>Borderless full screen. Not persisted: the game always starts in a window.</summary>
    public bool Fullscreen { get; set; }

    /// <summary>Raised by the menu after any change, so the frontend can apply and save it.</summary>
    public event Action Changed;

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>For the frontend's own text and logo: smooth them only when the filter is a smoothing one and the zoom is not a whole number.</summary>
    public bool UsesBilinear => !IntegerScale && Filter is DisplayFilter.Bilinear or DisplayFilter.Sharp;
}
