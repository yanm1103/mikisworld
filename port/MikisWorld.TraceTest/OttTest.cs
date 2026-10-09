using System.Text.Json;
using MikisWorld.Core;

// Compares the C# ring-tone decoder with the independent Python reference (tools/decode_ott.py --json).
//   MikisWorld.TraceTest ott <assetsDir> <tracesDir>
internal static class OttTest
{
    public static int Run(string assetsDir, string tracesDir, string prefix = "")
    {
        int fail = 0, files = 0;
        foreach (string path in Directory.GetFiles(assetsDir, "*.ott").OrderBy(p => p))
        {
            files++;
            string name = Path.GetFileName(path);
            var tone = RingTone.Decode(File.ReadAllBytes(path));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tracesDir, prefix + name + ".json")));
            string refName = doc.RootElement.GetProperty("name").GetString();
            var events = doc.RootElement.GetProperty("events").EnumerateArray().ToList();
            string err = null;
            if (tone.Name != refName) err = $"name '{tone.Name}' != '{refName}'";
            else if (tone.Notes.Count != events.Count) err = $"{tone.Notes.Count} notes != {events.Count}";
            else
                for (int i = 0; i < events.Count && err == null; i++)
                {
                    var e = events[i];
                    var n = tone.Notes[i];
                    if (e[0].GetInt32() != n.Note || e[1].GetInt32() != n.Scale || e[2].GetInt32() != n.Style || Math.Abs(e[3].GetDouble() - n.Milliseconds) > 1e-6)
                        err = $"note {i}: got ({n.Note},{n.Scale},{n.Style},{n.Milliseconds}) expected {e}";
                }
            short[] pcm = tone.Render();
            double seconds = pcm.Length / 22050.0;
            bool lenOk = Math.Abs(seconds - tone.TotalSeconds) < 0.01;
            bool audible = pcm.Any(s => Math.Abs(s) > 1000);
            if (err == null && !lenOk) err = $"rendered {seconds:F3}s but tune is {tone.TotalSeconds:F3}s";
            if (err == null && !audible) err = "rendered silence";
            Console.WriteLine($"{(err == null ? "OK  " : "FAIL")} {name,-22} {tone.Notes.Count,4} notes {tone.TotalSeconds,6:F2}s {(err == null ? "" : err)}");
            if (err != null) fail++;
        }
        // octave shift: a square wave one octave lower must cross zero about half as often
        {
            var coin = RingTone.Decode(File.ReadAllBytes(Path.Combine(assetsDir, "coin.ott")));
            int Crossings(short[] x) { int c = 0; for (int i = 1; i < x.Length; i++) if ((x[i - 1] < 0) != (x[i] < 0)) c++; return c; }
            double ratio = (double)Crossings(coin.Render(octaveShift: -1)) / Crossings(coin.Render(octaveShift: 0));
            bool ok = ratio > 0.45 && ratio < 0.55;
            Console.WriteLine($"{(ok ? "OK  " : "FAIL")} octave shift -1 halves the frequency (zero-crossing ratio {ratio:F2})");
            if (!ok) fail++;
        }
        // volume: the amplitude of the generated square wave is proportional to the volume setting
        {
            var coin = RingTone.Decode(File.ReadAllBytes(Path.Combine(assetsDir, "coin.ott")));
            int Peak(short[] x) => x.Max(v => Math.Abs((int)v));
            int full = Peak(coin.Render(amplitude: RingTone.AmplitudeForVolume(100)));
            int half = Peak(coin.Render(amplitude: RingTone.AmplitudeForVolume(50)));
            int none = Peak(coin.Render(amplitude: RingTone.AmplitudeForVolume(0)));
            bool ok = Math.Abs(half / (double)full - 0.5) < 0.02 && none == 0 && full > 5000;
            Console.WriteLine($"{(ok ? "OK  " : "FAIL")} volume scales the wave amplitude (100 %: {full}, 50 %: {half}, 0 %: {none})");
            if (!ok) fail++;
        }
        Console.WriteLine(fail == 0 ? $"ALL OK ({files} tunes)" : $"{fail} FAILED");
        return fail == 0 ? 0 : 1;
    }
}
