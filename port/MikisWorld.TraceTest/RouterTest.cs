using MikisWorld.Core;
using MikisWorld.Game;


/// <summary>Input router: keyboard/gamepad names to the phone-keypad events the games receive, per context.</summary>
internal static class RouterTest
{
    private sealed class Fake : IInputTarget
    {
        public App.InputContext Context { get; set; } = App.InputContext.Gameplay;
        public InputBindings Bindings { get; } = new();
        public bool CapturingInput { get; set; }
        public BindingDevice LastDevice { get; set; }
        public bool AcceptsRepeat { get; set; }
        public bool InNameEntry { get; set; }
        public string EnteredName { get; set; } = "";
        public List<string> Log = new();
        public void KeyPressed(int code, int action) => Log.Add($"P{code}/{action}");
        public void KeyReleased(int code, int action) => Log.Add($"R{code}/{action}");
        public void Back() => Log.Add("Back");
        public void Backspace() { Log.Add("Bksp"); if (EnteredName.Length > 0) EnteredName = EnteredName[..^1]; }
        public void ConfirmName() => Log.Add("Confirm");
        public void TextInput(char c) { Log.Add("T" + c); EnteredName += c; }
        public void CaptureInput(BindingDevice device, string name) => Log.Add($"Cap:{device}:{name}");
        public void CancelCapture() => Log.Add("CancelCap");
        public string Take() { var s = string.Join(" ", Log); Log.Clear(); return s; }
    }

    public static int Run()
    {
        int fails = 0;
        void Check(string what, string got, string want)
        {
            bool ok = got == want;
            if (!ok) fails++;
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}{(ok ? "" : $": got [{got}] want [{want}]")}");
        }
        const BindingDevice K = BindingDevice.Keyboard, P = BindingDevice.Gamepad;

        // ---- world: two inputs for one action
        {
            var t = new Fake(); var r = new InputRouter(t);
            r.Press(K, "Right", 0); Check("Right = (-4,5)", t.Take(), "P-4/5");
            r.Press(K, "D", 0); Check("second input of the same action: no new event", t.Take(), "");
            r.Release(K, "Right"); Check("first release while the other is held: nothing", t.Take(), "");
            r.Release(K, "D"); Check("last release: key released", t.Take(), "R-4/5");
        }
        // ---- every release is delivered for its own key (the engine decides what stays held)
        {
            var t = new Fake(); var r = new InputRouter(t);
            r.Press(K, "Right", 0); r.Press(K, "Up", 0); t.Take();
            r.Release(K, "Up"); Check("only the released key is reported", t.Take(), "R-1/1");
            r.Release(K, "Right"); Check("right released", t.Take(), "R-4/5");
        }
        // ---- digits stay on the phone keypad (cheat codes), arrows etc. come from the bindings
        {
            var t = new Fake(); var r = new InputRouter(t);
            r.Press(K, "Seven", 0); r.Press(K, "Kp3", 0); r.Press(K, "Two", 0);
            Check("digits are fixed", t.Take(), "P55/0 P51/0 P50/1");
            r.Press(K, "Q", 0); r.Press(K, "Space", 0);
            Check("Q = jump left (49), Space = confirm", t.Take(), "P49/0 P-5/8");
        }
        // ---- remapping
        {
            var t = new Fake(); var r = new InputRouter(t);
            t.Bindings.Set(K, GameAction.Jump, 0, "Z");
            r.Press(K, "Z", 0); Check("Z = jump after remap", t.Take(), "P-1/1");
            r.Press(K, "Up", 0); Check("old key no longer bound", t.Take(), "");
            r.Press(K, "W", 0); Check("second slot still works", t.Take(), "");   // W is slot 1 of Jump: held count 2 -> no event while Z is held
            t.Bindings.Set(K, GameAction.Left, 0, "W");
            r.Press(K, "A", 0); Check("A still left (slot 1)", t.Take(), "P-3/2");
        }
        // ---- gamepad in the world / menu / scene
        {
            var t = new Fake(); var r = new InputRouter(t);
            r.Press(P, "A", 0); Check("pad A = jump in the world", t.Take(), "P-1/1");
            r.Press(P, "X", 0); r.Press(P, "Y", 0); r.Press(P, "DRight", 0); r.Press(P, "SLeft", 0);
            Check("pad X interact, Y confirm, dpad right, stick left", t.Take(), "P-2/6 P-5/8 P-4/5 P-3/2");
            r.Press(P, "Start", 0); Check("Start opens the menu", t.Take(), "Back");
            r.Press(P, "B", 0); Check("B does nothing in the world", t.Take(), "");

        }
        {
            var t = new Fake(); var r = new InputRouter(t);
            t.Context = App.InputContext.Menu;
            r.Press(P, "DDown", 0); r.Press(P, "A", 0); r.Press(P, "B", 0);
            Check("menu: dpad down, A select, B back", t.Take(), "P-2/6 P-5/8 Back");

            t = new Fake(); r = new InputRouter(t); t.Context = App.InputContext.Scene;
            r.Press(P, "A", 0); r.Press(P, "DLeft", 0);
            Check("scene: A confirms, other buttons keep their world meaning", t.Take(), "P-5/8 P-3/2");
        }
        // ---- no re-press of directions after a release inside a menu
        {
            var t = new Fake(); var r = new InputRouter(t);
            r.Press(K, "Right", 0); t.Context = App.InputContext.Menu; r.Press(K, "Down", 0); t.Take();
            r.Release(K, "Down"); Check("menu release never re-presses directions", t.Take(), "R-2/6");
        }
        // ---- capture
        {
            var t = new Fake(); var r = new InputRouter(t); t.CapturingInput = true;
            r.Press(K, "F11", 0); r.Press(K, "Backspace", 0); Check("reserved keys are not captured", t.Take(), "");
            r.Press(K, "Z", 0); Check("key captured", t.Take(), "Cap:Keyboard:Z");
            r.Press(K, "Escape", 0); Check("Esc cancels (Back)", t.Take(), "Back");
            r.Press(P, "Start", 0); Check("pad Start cancels", t.Take(), "CancelCap");
            r.Press(P, "R2", 0); Check("pad button captured", t.Take(), "Cap:Gamepad:R2");
            t.CapturingInput = false;
            r.Release(K, "Z"); Check("captured key leaves no stuck state", t.Take(), "");
        }
        // ---- menu repeat
        {
            var t = new Fake(); var r = new InputRouter(t); t.Context = App.InputContext.Menu;
            r.Press(K, "Down", 10); r.Press(K, "Left", 10); r.Press(K, "Backspace", 10); t.Take();
            r.Tick(10.2); Check("no repeat before the delay", t.Take(), "");
            r.Tick(10.5); Check("repeats down + backspace (left only on sliders)", t.Take(), "P-2/6 Bksp");
            t.AcceptsRepeat = true; r.Tick(10.6); Check("left repeats on sliders", t.Take(), "P-2/6 P-3/2 Bksp");
            t.Context = App.InputContext.Gameplay; r.Tick(10.7); Check("nothing repeats in the world", t.Take(), "Bksp");
        }
        // ---- name entry with a controller
        {
            var t = new Fake { InNameEntry = true }; var r = new InputRouter(t);
            r.Press(P, "DUp", 0); r.Press(P, "DUp", 0);                      // empty + up = "A", up again = "B" (the last letter changes)
            r.Press(P, "DRight", 0);                                         // new letter "A"
            r.Press(P, "DDown", 0);                                          // down from "A" wraps to "9"
            Check("name entry typed", t.EnteredName, "B9");
            r.Press(P, "A", 0); Check("A saves the name", t.Take().EndsWith("Confirm") ? "saved" : "no", "saved");
            var u = new Fake { InNameEntry = true }; var r2 = new InputRouter(u);
            r2.Press(P, "Start", 0); Check("saving an empty name gives MIKI", u.EnteredName, "MIKI");
        }
        return fails;
    }
}
