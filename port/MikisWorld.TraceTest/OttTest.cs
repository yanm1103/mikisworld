using System.Text.Json;
using MikisWorld.Core;

// Compares the C# ring-tone decoder with the independent Python reference (tools/decode_ott.py --json).
//   MikisWorld.TraceTest ott <assetsDir> <tracesDir>
internal static class OttTest
{
    public static int Run(string assetsDir, string tracesDir)
    {
        int fail = 0, files = 0;
        foreach (string path in Directory.GetFiles(assetsDir, "*.ott").OrderBy(p => p))
        {
            files++;
            string name = Path.GetFileName(path);
            var tone = RingTone.Decode(File.ReadAllBytes(path));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tracesDir, name + ".json")));
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
        Console.WriteLine(fail == 0 ? $"ALL OK ({files} tunes)" : $"{fail} FAILED");
        return fail == 0 ? 0 : 1;
    }
}
