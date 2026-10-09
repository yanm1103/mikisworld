using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Plays the game's ring tones through Raylib. Like the Nokia Sound API of the original, only one sound plays at a
/// time: starting a new one cuts the previous one off. Vibration has no PC equivalent and is ignored.</summary>
internal sealed unsafe class AudioHost : IGameHost, IDisposable
{
    private const int SampleRate = 22050;
    private readonly Dictionary<Sfx, Sound> _sounds = new();
    private Sound? _current;
    private int _loopsLeft;
    public bool Ready { get; }

    /// <summary>The second game plays its title jingle three times (<c>Sound.play(3)</c>).</summary>
    public int TitleLoops { get; set; } = 1;

    public AudioHost(IAssets assets)
    {
        Raylib.InitAudioDevice();
        Ready = Raylib.IsAudioDeviceReady();
        if (!Ready) return;
        foreach (Sfx s in Enum.GetValues<Sfx>())
        {
            short[] pcm = RingTone.Decode(assets.ReadResource(SfxFiles.FileName(s))).Render(SampleRate);
            fixed (short* p = pcm)
            {
                var wave = new Wave { SampleCount = (uint)pcm.Length, SampleRate = SampleRate, SampleSize = 16, Channels = 1, Data = p };
                _sounds[s] = Raylib.LoadSoundFromWave(wave);   // copies the samples
            }
        }
    }

    public void Play(Sfx sound)
    {
        if (!Ready) return;
        StopSound();
        var s = _sounds[sound];
        Raylib.PlaySound(s);
        _current = s;
        _loopsLeft = sound == Sfx.Title ? TitleLoops - 1 : 0;
    }

    public void StopSound()
    {
        if (_current is Sound c) Raylib.StopSound(c);
        _current = null;
        _loopsLeft = 0;
    }

    public bool IsPlaying => _current is Sound c && Raylib.IsSoundPlaying(c);

    /// <summary>Call once per frame: restarts a sound that still has loops left.</summary>
    public void Tick()
    {
        if (_loopsLeft > 0 && _current is Sound c && !Raylib.IsSoundPlaying(c))
        {
            _loopsLeft--;
            Raylib.PlaySound(c);
        }
    }

    public void Vibrate(int frequency, int milliseconds) { }

    public void Dispose()
    {
        if (!Ready) return;
        StopSound();
        foreach (var s in _sounds.Values) Raylib.UnloadSound(s);
        Raylib.CloseAudioDevice();
    }

    /// <summary>Writes every sound effect as a WAV file (no audio device needed).</summary>
    public static void Export(IAssets assets, string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (Sfx s in Enum.GetValues<Sfx>())
        {
            var tone = RingTone.Decode(assets.ReadResource(SfxFiles.FileName(s)));
            string path = Path.Combine(dir, Path.ChangeExtension(SfxFiles.FileName(s), ".wav"));
            File.WriteAllBytes(path, RingTone.ToWav(tone.Render(SampleRate), SampleRate));
            Console.WriteLine($"{path}  ({tone.TotalSeconds:F1}s)");
        }
    }
}
