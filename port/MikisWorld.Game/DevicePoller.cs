using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Polls Raylib's keyboard and first connected gamepad and feeds the <see cref="InputRouter"/>.</summary>
internal sealed class DevicePoller
{
    private static readonly KeyboardKey[] Keys = Enum.GetValues<KeyboardKey>().Where(k => k != KeyboardKey.Null).DistinctBy(k => (int)k).ToArray();

    private static readonly (string Name, GamepadButton Button)[] PadButtons =
    {
        ("A", GamepadButton.RightFaceDown), ("B", GamepadButton.RightFaceRight), ("X", GamepadButton.RightFaceLeft), ("Y", GamepadButton.RightFaceUp),
        ("L1", GamepadButton.LeftTrigger1), ("R1", GamepadButton.RightTrigger1), ("L2", GamepadButton.LeftTrigger2), ("R2", GamepadButton.RightTrigger2),
        ("Select", GamepadButton.MiddleLeft), ("Start", GamepadButton.MiddleRight), ("L3", GamepadButton.LeftThumb), ("R3", GamepadButton.RightThumb),
        ("DUp", GamepadButton.LeftFaceUp), ("DDown", GamepadButton.LeftFaceDown), ("DLeft", GamepadButton.LeftFaceLeft), ("DRight", GamepadButton.LeftFaceRight),
    };

    private const float StickOn = 0.5f, StickOff = 0.35f;
    private readonly InputRouter _router;
    private readonly HashSet<string> _padDown = new();

    public DevicePoller(InputRouter router) { _router = router; }

    public static string KeyName(KeyboardKey k) => Keys.First(x => (int)x == (int)k).ToString();

    public void Poll(double now)
    {
        foreach (var k in Keys)
        {
            if (Raylib.IsKeyPressed(k)) _router.Press(BindingDevice.Keyboard, k.ToString(), now);
            if (Raylib.IsKeyReleased(k)) _router.Release(BindingDevice.Keyboard, k.ToString());
        }
        PollPad(now);
        _router.Tick(now);
    }

    private void PollPad(double now)
    {
        int pad = -1;
        for (int i = 0; i < 4 && pad < 0; i++) if (Raylib.IsGamepadAvailable(i)) pad = i;
        if (pad < 0)
        {
            if (_padDown.Count > 0) { _padDown.Clear(); _router.ReleaseAll(BindingDevice.Gamepad); }
            return;
        }
        foreach (var (name, button) in PadButtons) Set(name, Raylib.IsGamepadButtonDown(pad, button), now);

        float x = Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftX), y = Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftY);
        Stick("SLeft", x < 0 ? -x : 0, now);
        Stick("SRight", x > 0 ? x : 0, now);
        Stick("SUp", y < 0 ? -y : 0, now);
        Stick("SDown", y > 0 ? y : 0, now);
    }

    private void Stick(string name, float value, double now)
        => Set(name, value > (_padDown.Contains(name) ? StickOff : StickOn), now);

    private void Set(string name, bool down, double now)
    {
        if (down && _padDown.Add(name)) _router.Press(BindingDevice.Gamepad, name, now);
        else if (!down && _padDown.Remove(name)) _router.Release(BindingDevice.Gamepad, name);
    }
}
