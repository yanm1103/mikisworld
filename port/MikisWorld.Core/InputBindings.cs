namespace MikisWorld.Core;

/// <summary>What the player can do in the world. Each action has up to <see cref="InputBindings.Slots"/> keyboard keys and
/// gamepad buttons; the frontend turns them into the phone-keypad events the original games understand.</summary>
public enum GameAction { Left, Right, Jump, Interact, Confirm, JumpLeft, JumpRight }

public enum BindingDevice { Keyboard, Gamepad }

/// <summary>Remappable controls. Inputs are plain names so the Core does not depend on any window library:
/// keyboard keys use the frontend's key names ("Left", "A", "Space", "LeftShift"…); gamepad inputs use
/// "A","B","X","Y","L1","R1","L2","R2","Select","Start","L3","R3","DUp","DDown","DLeft","DRight" and the left stick
/// "SUp","SDown","SLeft","SRight". An empty string means "not bound".</summary>
public sealed class InputBindings
{
    public const int Slots = 2;
    public static readonly GameAction[] Actions = (GameAction[])Enum.GetValues(typeof(GameAction));

    private readonly string[][][] _b = new string[2][][];     // [device][action][slot]

    public event Action Changed;

    public InputBindings()
    {
        Reset(BindingDevice.Keyboard);
        Reset(BindingDevice.Gamepad);
    }

    public static string Label(GameAction a) => a switch
    {
        GameAction.Left => "Left",
        GameAction.Right => "Right",
        GameAction.Jump => "Jump",
        GameAction.Interact => "Interact",
        GameAction.Confirm => "Confirm",
        GameAction.JumpLeft => "Jump <",
        GameAction.JumpRight => "Jump >",
        _ => a.ToString()
    };

    public static string[] Defaults(BindingDevice d, GameAction a) => (d, a) switch
    {
        (BindingDevice.Keyboard, GameAction.Left) => new[] { "Left", "A" },
        (BindingDevice.Keyboard, GameAction.Right) => new[] { "Right", "D" },
        (BindingDevice.Keyboard, GameAction.Jump) => new[] { "Up", "W" },
        (BindingDevice.Keyboard, GameAction.Interact) => new[] { "Down", "S" },
        (BindingDevice.Keyboard, GameAction.Confirm) => new[] { "Enter", "Space" },
        (BindingDevice.Keyboard, GameAction.JumpLeft) => new[] { "Q", "" },
        (BindingDevice.Keyboard, GameAction.JumpRight) => new[] { "E", "" },
        (BindingDevice.Gamepad, GameAction.Left) => new[] { "DLeft", "SLeft" },
        (BindingDevice.Gamepad, GameAction.Right) => new[] { "DRight", "SRight" },
        (BindingDevice.Gamepad, GameAction.Jump) => new[] { "A", "DUp" },
        (BindingDevice.Gamepad, GameAction.Interact) => new[] { "X", "DDown" },
        (BindingDevice.Gamepad, GameAction.Confirm) => new[] { "Y", "" },
        _ => new[] { "", "" }
    };

    public void Reset(BindingDevice d)
    {
        _b[(int)d] = new string[Actions.Length][];
        foreach (var a in Actions) _b[(int)d][(int)a] = Defaults(d, a);
        Changed?.Invoke();
    }

    public string Get(BindingDevice d, GameAction a, int slot) => _b[(int)d][(int)a][slot];

    /// <summary>Binds <paramref name="name"/> to a slot. The same input can only drive one action per device, so it is removed from any other cell.</summary>
    public void Set(BindingDevice d, GameAction a, int slot, string name)
    {
        if (!string.IsNullOrEmpty(name))
            foreach (var other in Actions)
                for (int s = 0; s < Slots; s++)
                    if (_b[(int)d][(int)other][s] == name) _b[(int)d][(int)other][s] = "";
        _b[(int)d][(int)a][slot] = name ?? "";
        Changed?.Invoke();
    }

    public void Clear(BindingDevice d, GameAction a, int slot) => Set(d, a, slot, "");

    /// <summary>Actions currently bound to this input.</summary>
    public IEnumerable<GameAction> ActionsFor(BindingDevice d, string name)
    {
        if (string.IsNullOrEmpty(name)) yield break;
        foreach (var a in Actions)
            for (int s = 0; s < Slots; s++)
                if (_b[(int)d][(int)a][s] == name) { yield return a; break; }
    }

    /// <summary>Flat form for saving: "action" -> [slot1, slot2].</summary>
    public Dictionary<string, string[]> Export(BindingDevice d)
        => Actions.ToDictionary(a => a.ToString(), a => (string[])_b[(int)d][(int)a].Clone());

    public void Import(BindingDevice d, IDictionary<string, string[]> data)
    {
        if (data == null) return;
        foreach (var a in Actions)
            if (data.TryGetValue(a.ToString(), out var v) && v != null)
                for (int s = 0; s < Slots; s++)
                    _b[(int)d][(int)a][s] = s < v.Length && v[s] != null ? v[s] : "";
    }

    private static readonly HashSet<string> KeepName = new() { "Left", "Right", "Up", "Down", "DLeft", "DRight", "DUp", "DDown", "SLeft", "SRight", "SUp", "SDown" };

    private static readonly string[] Digits = { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };

    /// <summary>Short text for a menu cell (the screens are only 128-176 px wide).</summary>
    public static string Short(string name)
    {
        if (string.IsNullOrEmpty(name)) return "-";
        if (KeepName.Contains(name)) return name;
        int digit = Array.IndexOf(Digits, name);
        if (digit >= 0) return (digit + 1).ToString();
        string n = name.Replace("Left", "L").Replace("Right", "R").Replace("Control", "Ctrl");
        if (name.StartsWith("Kp")) n = "Num" + name[2..];
        return n.Length > 6 ? n[..6] : n;
    }
}
