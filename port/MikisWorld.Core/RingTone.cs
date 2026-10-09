namespace MikisWorld.Core;

/// <summary>One note of a ring tone. <c>Note</c> 0 is a rest, 1-12 are C..B; <c>Scale</c> 0-3 doubles the pitch each step
/// (scale 0 is the octave whose A is 440 Hz); <c>Style</c> 0 natural, 1 continuous, 2 staccato.</summary>
public readonly record struct ToneNote(int Note, int Scale, int Style, double Milliseconds);

/// <summary>Nokia Smart Messaging ring tone (.ott): decoder plus a small square-wave synthesiser that stands in
/// for the phone's buzzer. Bit layout: see tools/decode_ott.py (which was used to validate this decoder).</summary>
public sealed class RingTone
{
    private static readonly int[] Bpm =
    {
        25, 28, 31, 35, 40, 45, 50, 56, 63, 70, 80, 90, 100, 112, 125, 140,
        160, 180, 200, 225, 250, 285, 320, 355, 400, 450, 500, 565, 635, 715, 800, 900
    };
    private static readonly double[] DurationFactor = { 1.0, 1.5, 9.0 / 4.0, 2.0 / 3.0 };   // none, dotted, double dotted, triplet

    public string Name { get; private set; }
    public List<ToneNote> Notes { get; } = new();

    private sealed class Bits
    {
        private readonly byte[] _d; private int _p;
        public Bits(byte[] d) { _d = d; }
        public int Read(int n)
        {
            int v = 0;
            for (int i = 0; i < n; i++, _p++) v = (v << 1) | ((_d[_p >> 3] >> (7 - (_p & 7))) & 1);
            return v;
        }
        public void Align() { _p = (_p + 7) & ~7; }
    }

    public static RingTone Decode(byte[] data)
    {
        var b = new Bits(data);
        if (b.Read(8) != 0x02) throw new InvalidDataException("not a ring tone (header)");
        if (b.Read(7) != 0x25) throw new InvalidDataException("not a ringing tone programming command");
        b.Align();
        if (b.Read(7) != 0x1D) throw new InvalidDataException("not a sound command");
        if (b.Read(3) != 1) throw new InvalidDataException("only basic songs are supported");
        var rt = new RingTone();
        int titleLen = b.Read(4);
        var title = new char[titleLen];
        for (int i = 0; i < titleLen; i++) title[i] = (char)b.Read(8);
        rt.Name = new string(title);

        int scale = 1, style = 0, bpm = 63;
        int patterns = b.Read(8);
        for (int p = 0; p < patterns; p++)
        {
            if (b.Read(3) != 0) throw new InvalidDataException("pattern header expected");
            b.Read(2);                                   // pattern id
            int loop = b.Read(4);                        // played loop + 1 times (inferred)
            int count = b.Read(8);
            var notes = new List<ToneNote>();
            for (int i = 0; i < count; i++)
            {
                switch (b.Read(3))
                {
                    case 1:
                        {
                            int note = b.Read(4), dur = b.Read(3), spec = b.Read(2);
                            double ms = 4 * 60000.0 / bpm / (1 << dur) * DurationFactor[spec];
                            notes.Add(new ToneNote(note, scale, style, ms));
                            break;
                        }
                    case 2: scale = b.Read(2); break;
                    case 3: style = b.Read(2); break;
                    case 4: bpm = Bpm[b.Read(5)]; break;
                    case 5: b.Read(4); break;            // volume
                    default: throw new InvalidDataException("unknown instruction");
                }
            }
            for (int r = 0; r <= loop; r++) rt.Notes.AddRange(notes);
        }
        return rt;
    }

    public static double Frequency(ToneNote n)
        => 440.0 * Math.Pow(2, (n.Note - 1 - 9) / 12.0) * (1 << n.Scale);

    public double TotalSeconds => Notes.Sum(n => n.Milliseconds) / 1000.0;

    /// <summary>Mono 16-bit PCM of the whole tune.</summary>
    /// <param name="octaveShift">added to every note's octave (-1 = one octave lower). The scale-to-pitch mapping of the format was
    /// inferred (the spec was not available), and tunes came out shrill on a square-wave speaker, so the game defaults to -1.</param>
    public short[] Render(int sampleRate = 22050, double amplitude = 0.22, int octaveShift = 0)
    {
        double octave = Math.Pow(2, octaveShift);
        var samples = new List<short>((int)(TotalSeconds * sampleRate) + 16);
        double carry = 0;                                 // keeps note boundaries sample-accurate over the whole tune
        double phase = 0;
        foreach (var n in Notes)
        {
            double exact = n.Milliseconds * sampleRate / 1000.0 + carry;
            int total = (int)exact;
            carry = exact - total;
            double gapFraction = n.Style == 1 ? 0 : n.Style == 2 ? 0.5 : 0.08;
            int sounding = n.Note == 0 ? 0 : (int)(total * (1 - gapFraction));
            double freq = n.Note == 0 ? 0 : Frequency(n) * octave;
            int fade = Math.Min(sounding / 4, sampleRate / 500);   // ~2 ms to avoid clicks
            for (int i = 0; i < total; i++)
            {
                double v = 0;
                if (i < sounding)
                {
                    phase += freq / sampleRate;
                    phase -= Math.Floor(phase);
                    v = phase < 0.5 ? 1 : -1;
                    double env = 1;
                    if (i < fade) env = (double)i / fade;
                    else if (i >= sounding - fade) env = (double)(sounding - i) / fade;
                    v *= env * amplitude;
                }
                samples.Add((short)(v * short.MaxValue));
            }
        }
        return samples.ToArray();
    }

    /// <summary>16-bit mono PCM wrapped as a WAV file.</summary>
    public static byte[] ToWav(short[] pcm, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray()); w.Write(36 + pcm.Length * 2); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(pcm.Length * 2);
        foreach (short s in pcm) w.Write(s);
        return ms.ToArray();
    }
}

public static class SfxFiles
{
    public static string FileName(Sfx s) => s switch
    {
        Sfx.OnceUponATime => "onceuponatime.ott",
        Sfx.Bonus => "bonus.ott",
        Sfx.Cage => "cage.ott",
        Sfx.Coin => "coin.ott",
        Sfx.Damage => "damage.ott",
        Sfx.GameEnd => "gameend.ott",
        Sfx.GameOver => "gameover.ott",
        Sfx.GameStart => "gamestart.ott",
        Sfx.Key => "key.ott",
        Sfx.Lord => "lord.ott",
        Sfx.Title => "title.ott",
        Sfx.Transportation => "transportation.ott",
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };
}
