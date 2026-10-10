using System.Runtime.InteropServices;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Controller rumble. The raylib version in use has no vibration call, so on Windows this talks to XInput directly
/// (Xbox-compatible controllers, and others through Steam Input / DS4Windows). Anything else, or a missing XInput DLL, is silently ignored.</summary>
internal static class Rumble
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration { public ushort Left, Right; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int SetState14(int user, ref Vibration v);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int SetState91(int user, ref Vibration v);

    /// <summary>Which XInput DLL answered (for --rumble-test): none / xinput1_4 / xinput9_1_0.</summary>
    public static string Api => _api switch { 1 => "xinput1_4", 2 => "xinput9_1_0", 0 => "none (rumble unavailable)", _ => "not used yet" };

    private static int _api = -1;            // -1 unknown, 0 none, 1 xinput1_4, 2 xinput9_1_0
    private static double _stopAt;
    private static bool _active;

    private static int Set(int user, ushort left, ushort right)
    {
        var v = new Vibration { Left = left, Right = right };
        if (_api == -1)
        {
            _api = 0;
            if (OperatingSystem.IsWindows())
            {
                try { SetState14(user, ref v); _api = 1; }
                catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
                {
                    try { SetState91(user, ref v); _api = 2; }
                    catch (Exception e2) when (e2 is DllNotFoundException or EntryPointNotFoundException) { }
                }
            }
        }
        return _api switch { 1 => SetState14(user, ref v), 2 => SetState91(user, ref v), _ => -1 };
    }

    private static void All(ushort left, ushort right)
    {
        for (int user = 0; user < 4; user++) Set(user, left, right);     // controllers that are not connected just report an error code
    }

    /// <summary>Rumble for <paramref name="milliseconds"/> at <paramref name="percent"/> (0-100) of the full strength.</summary>
    public static void Pulse(int percent, int milliseconds)
    {
        ushort speed = (ushort)(Math.Clamp(percent, 0, 100) * 65535 / 100);
        All(speed, speed);
        _active = true;
        _stopAt = Raylib.GetTime() + milliseconds / 1000.0;
    }

    /// <summary>Call every frame: ends a pulse when its time is up.</summary>
    public static void Update()
    {
        if (_active && Raylib.GetTime() >= _stopAt) Stop();
    }

    public static void Stop()
    {
        if (!_active) return;
        _active = false;
        All(0, 0);
    }
}
