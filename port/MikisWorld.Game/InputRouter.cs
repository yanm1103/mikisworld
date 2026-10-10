using MikisWorld.Core;

namespace MikisWorld.Game;

/// <summary>A phone-keypad event as the original games receive it: MIDP key code + game action
/// (UP=1 LEFT=2 RIGHT=5 DOWN=6 FIRE=8).</summary>
internal readonly record struct Logical(int Code, int Action)
{
    public static readonly Logical Up = new(-1, 1), Down = new(-2, 6), Left = new(-3, 2), Right = new(-4, 5), Fire = new(-5, 8);
    public bool IsDirection => Code == -3 || Code == -4;

    public static Logical Of(GameAction a) => a switch
    {
        GameAction.Left => Left,
        GameAction.Right => Right,
        GameAction.Jump => Up,
        GameAction.Interact => Down,
        GameAction.Confirm => Fire,
        GameAction.JumpLeft => new Logical(49, 0),
        _ => new Logical(51, 0)
    };
}

/// <summary>What the router needs from the application (an interface so it can be tested without a window).</summary>
internal interface IInputTarget
{
    App.InputContext Context { get; }
    InputBindings Bindings { get; }
    bool CapturingInput { get; }
    bool AcceptsRepeat { get; }
    bool InNameEntry { get; }
    string EnteredName { get; }
    void KeyPressed(int code, int action);
    void KeyReleased(int code, int action);
    void Back();
    void Backspace();
    void ConfirmName();
    void TextInput(char c);
    void CaptureInput(BindingDevice device, string name);
    void CancelCapture();
}

internal sealed class AppTarget : IInputTarget
{
    private readonly App _a;
    public AppTarget(App a) { _a = a; }
    public App.InputContext Context => _a.Context;
    public InputBindings Bindings => _a.Bindings;
    public bool CapturingInput => _a.CapturingInput;
    public bool AcceptsRepeat => _a.AcceptsRepeat;
    public bool InNameEntry => _a.InNameEntry;
    public string EnteredName => _a.EnteredName;
    public void KeyPressed(int code, int action) => _a.KeyPressed(code, action);
    public void KeyReleased(int code, int action) => _a.KeyReleased(code, action);
    public void Back() => _a.Back();
    public void Backspace() => _a.Backspace();
    public void ConfirmName() => _a.ConfirmName();
    public void TextInput(char c) => _a.TextInput(c);
    public void CaptureInput(BindingDevice device, string name) => _a.CaptureInput(device, name);
    public void CancelCapture() => _a.CancelCapture();
}

/// <summary>Turns raw inputs (keyboard keys, gamepad buttons, left-stick directions) into the key events the games understand,
/// using the remappable <see cref="InputBindings"/> in the world and fixed navigation in menus. Also handles control capture,
/// key repeat in menus. Which directions are still held is the engine's business (see EngineTweaks.RevampedMovement).</summary>
internal sealed class InputRouter
{
    private const double RepeatDelay = 0.4, RepeatEvery = 0.07;
    private const string NameAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private sealed class Held
    {
        public List<Logical> Logicals = new();
        public bool Menu, Backspace;
        public double Since, Last;
    }

    private readonly IInputTarget _app;
    private readonly Dictionary<(BindingDevice, string), Held> _held = new();
    private readonly Dictionary<Logical, int> _count = new();

    public InputRouter(IInputTarget app) { _app = app; }

    // ------------------------------------------------------------------ raw events
    public void Press(BindingDevice dev, string name, double now)
    {
        var key = (dev, name);
        if (_held.ContainsKey(key)) return;
        if (_app.CapturingInput) { Capture(dev, name); return; }

        if (dev == BindingDevice.Keyboard)
        {
            if (name == "Escape") { _app.Back(); return; }
            if (name == "Backspace")
            {
                _app.Backspace();
                _held[key] = new Held { Backspace = true, Since = now, Last = now };
                return;
            }
            if (name is "Enter" or "KpEnter") _app.ConfirmName();
        }
        else if (PadSpecial(name)) return;

        var ctx = _app.Context;
        var h = new Held { Menu = ctx == App.InputContext.Menu, Since = now, Last = now, Logicals = Translate(dev, name, ctx) };
        _held[key] = h;
        foreach (var l in h.Logicals)
        {
            _count[l] = _count.GetValueOrDefault(l) + 1;
            if (_count[l] > 1) continue;                      // another input already holds the same key
            _app.KeyPressed(l.Code, l.Action);
        }
    }

    public void Release(BindingDevice dev, string name)
    {
        if (!_held.Remove((dev, name), out var h)) return;
        foreach (var l in h.Logicals)
        {
            if (--_count[l] > 0) continue;
            _count.Remove(l);
            _app.KeyReleased(l.Code, l.Action);
        }
    }

    public void ReleaseAll(BindingDevice dev)
    {
        foreach (var k in _held.Keys.Where(k => k.Item1 == dev).ToList()) Release(dev, k.Item2);
    }

    /// <summary>Key repeat for menus (up/down always, left/right on sliders and the controls screens, backspace).</summary>
    public void Tick(double now)
    {
        var menu = _app.Context == App.InputContext.Menu;
        foreach (var h in _held.Values.ToList())
        {
            if (now - h.Since < RepeatDelay || now - h.Last < RepeatEvery) continue;
            if (h.Backspace) { _app.Backspace(); h.Last = now; continue; }
            if (!h.Menu || !menu) continue;
            foreach (var l in h.Logicals)
                if (l.Action is 1 or 6 || _app.AcceptsRepeat && l.Action is 2 or 5)
                {
                    _app.KeyPressed(l.Code, l.Action);
                    h.Last = now;
                }
        }
    }

    // ------------------------------------------------------------------ capture
    private void Capture(BindingDevice dev, string name)
    {
        if (dev == BindingDevice.Keyboard)
        {
            if (name == "Escape") _app.Back();                                 // cancels
            else if (name is "F11" or "Backspace") { }                         // reserved
            else _app.CaptureInput(dev, name);
        }
        else if (name == "Start") _app.CancelCapture();
        else _app.CaptureInput(dev, name);
    }

    // ------------------------------------------------------------------ translation
    /// <summary>Buttons with a fixed meaning that never reach the game as keys. Returns true when handled.</summary>
    private bool PadSpecial(string name)
    {
        if (_app.InNameEntry) { NameEntryPad(name); return true; }
        if (name == "Start" || name == "B" && _app.Context == App.InputContext.Menu) { _app.Back(); return true; }
        return false;
    }

    /// <summary>Arcade-style name entry for a controller: up/down change the letter, right adds one, left/B erase, A/Start save.</summary>
    private void NameEntryPad(string name)
    {
        string n = _app.EnteredName;
        switch (name)
        {
            case "DUp" or "SUp" or "DDown" or "SDown":
                int step = name is "DUp" or "SUp" ? 1 : -1;
                int i = n.Length == 0 ? -1 : NameAlphabet.IndexOf(char.ToUpperInvariant(n[^1]));
                if (n.Length > 0) _app.Backspace();
                _app.TextInput(NameAlphabet[((i + step) % NameAlphabet.Length + NameAlphabet.Length) % NameAlphabet.Length]);
                break;
            case "DRight" or "SRight":
                _app.TextInput('A');
                break;
            case "DLeft" or "SLeft" or "B":
                _app.Backspace();
                break;
            case "A" or "Start":
                if (n.Length == 0) foreach (char c in "MIKI") _app.TextInput(c);
                _app.ConfirmName();
                break;
        }
    }

    private List<Logical> Translate(BindingDevice dev, string name, App.InputContext ctx)
    {
        var r = new List<Logical>();
        void Add(Logical l) { if (!r.Contains(l)) r.Add(l); }
        var acts = _app.Bindings.ActionsFor(dev, name).ToList();

        if (dev == BindingDevice.Keyboard)
        {
            if (acts.Count == 0 && DigitCode(name) is int d) Add(new Logical(d, d switch { 50 => 1, 52 => 2, 54 => 5, 56 => 6, 53 => 8, _ => 0 }));
            if (ctx == App.InputContext.Menu)
                switch (name)
                {
                    case "Left" or "A": Add(Logical.Left); break;
                    case "Right" or "D": Add(Logical.Right); break;
                    case "Up" or "W": Add(Logical.Up); break;
                    case "Down" or "S": Add(Logical.Down); break;
                    case "Enter" or "KpEnter" or "Space": Add(Logical.Fire); break;
                }
            foreach (var a in acts) Add(Logical.Of(a));
        }
        else if (ctx == App.InputContext.Menu)
        {
            // menus: fixed navigation (A would otherwise be "jump" = up)
            switch (name)
            {
                case "DLeft" or "SLeft": Add(Logical.Left); break;
                case "DRight" or "SRight": Add(Logical.Right); break;
                case "DUp" or "SUp": Add(Logical.Up); break;
                case "DDown" or "SDown": Add(Logical.Down); break;
                case "A": Add(Logical.Fire); break;
            }
            if (acts.Contains(GameAction.Confirm)) Add(Logical.Fire);
        }
        else if (ctx == App.InputContext.Scene && name == "A") Add(Logical.Fire);   // cutscenes / minigame: A confirms
        else foreach (var a in acts) Add(Logical.Of(a));
        return r;
    }

    private static int? DigitCode(string name)
    {
        if (name.Length == 3 && name.StartsWith("Kp") && char.IsDigit(name[2]) && name[2] != '0') return 48 + (name[2] - '0');
        int i = Array.IndexOf(new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" }, name);
        return i >= 0 ? 49 + i : null;
    }
}
