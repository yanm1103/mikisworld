using System.Runtime.InteropServices;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Log output is opt-in (<c>--logs</c>, or the Logs switch of the F3 debug menu). The program is a windowed application
/// (no console window), so asking for logs attaches to the console it was started from, or opens a new one, and routes raylib's
/// own log through it. Switching them off silences raylib again and closes a console this class opened.</summary>
internal static class LogConsole
{
    public static bool Enabled { get; private set; }

    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int which);
    [DllImport("kernel32.dll")] private static extern bool SetStdHandle(int which, IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("ucrtbase.dll")] private static extern IntPtr __acrt_iob_func(uint index);
    [DllImport("ucrtbase.dll", CharSet = CharSet.Ansi)] private static extern IntPtr freopen(string path, string mode, IntPtr stream);

    private static bool _consoleReady;             // a console (or a redirected output) is already wired up
    private static bool _openedConsole;            // ...and it is one this class opened (so it may close it)

    private static bool Missing(IntPtr h) => h == IntPtr.Zero || h == new IntPtr(-1);

    /// <summary>Call first thing in Main, before any raylib call.</summary>
    public static void Configure(bool logs) => SetEnabled(logs);

    public static void SetEnabled(bool on)
    {
        Enabled = on;
        Raylib.SetTraceLogLevel(on ? TraceLogLevel.Info : TraceLogLevel.None);
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (on) EnsureConsole(); else ReleaseConsole();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or IOException) { Enabled = false; }
    }

    private static void EnsureConsole()
    {
        if (_consoleReady) return;
        // output already redirected (a pipe or a file): nothing to do. Otherwise find a console to write to.
        if (Missing(GetStdHandle(-11)))
        {
            if (!AttachConsole(-1)) AllocConsole();
            const uint GenericWrite = 0x40000000, ShareReadWrite = 3, OpenExisting = 3;
            IntPtr con = CreateFileW("CONOUT$", GenericWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (!Missing(con))
            {
                SetStdHandle(-11, con);
                SetStdHandle(-12, con);
                freopen("CONOUT$", "w", __acrt_iob_func(1));         // raylib prints with the C runtime
                freopen("CONOUT$", "w", __acrt_iob_func(2));
                _openedConsole = true;
            }
        }
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        _consoleReady = true;
    }

    private static void ReleaseConsole()
    {
        if (!_consoleReady || !_openedConsole) return;           // a console we were started from stays as it is
        FreeConsole();
        SetStdHandle(-11, IntPtr.Zero);
        SetStdHandle(-12, IntPtr.Zero);
        _consoleReady = false;
        _openedConsole = false;
    }

    /// <summary>A line of the program's own log (only while logs are on).</summary>
    public static void Log(string message)
    {
        if (Enabled) Console.WriteLine("[mikis] " + message);
    }
}
