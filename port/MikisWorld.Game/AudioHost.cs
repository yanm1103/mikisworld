using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Plays the game's ring tones through Raylib. Like the Nokia Sound API of the original, only one sound plays at a
/// time: starting a new one cuts the previous one off. Vibration has no PC equivalent and is ignored.
/// The volume setting scales the amplitude of the square wave the tunes are rendered with.</summary>
internal sealed unsafe class AudioHost : IGameHost, IDisposable
{
    private const int SampleRate = 22050;
    private readonly Dictionary<Sfx, RingTone> _tones = new();
    private readonly Dictionary<Sfx, Sound> _sounds = new();
    private readonly int _octaveShift;
    private Sound? _current;
    private int _loopsLeft;
    private int _volume;
    private int _pendingVolume = -1;
    private double _pendingSince;
    public bool Ready { get; }

    /// <summary>The second game plays its title jingle three times (<c>Sound.play(3)</c>).</summary>
    public int TitleLoops { get; set; } = 1;

    public AudioHost(IAssets assets, int octaveShift = -1, int volume = 100)
    {
        _octaveShift = octaveShift;
        _volume = Math.Clamp(volume, 0, 100);
        Raylib.InitAudioDevice();
        Ready = Raylib.IsAudioDeviceReady();
        if (!Ready) return;
        foreach (Sfx s in Enum.GetValues<Sfx>())
            _tones[s] = RingTone.Decode(assets.ReadResource(SfxFiles.FileName(s)));
        Build();
    }

    // (re)renders every tune with the current volume; the old samples are released first
    private void Build()
    {
        StopSound();
        foreach (var s in _sounds.Values) Raylib.UnloadSound(s);
        _sounds.Clear();
        double amplitude = RingTone.AmplitudeForVolume(_volume);
        foreach (var (sfx, tone) in _tones)
        {
            short[] pcm = tone.Render(SampleRate, amplitude, _octaveShift);
            fixed (short* p = pcm)
            {
                var wave = new Wave { SampleCount = (uint)pcm.Length, SampleRate = SampleRate, SampleSize = 16, Channels = 1, Data = p };
                _sounds[sfx] = Raylib.LoadSoundFromWave(wave);   // copies the samples
            }
        }
    }

    /// <summary>Asks for a new volume (0-100 %). The tunes are re-rendered shortly after the last change, so dragging the
    /// slider does not rebuild twelve tunes per step.</summary>
    public void SetVolume(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _volume && _pendingVolume < 0) return;
        _pendingVolume = percent;
        _pendingSince = Raylib.GetTime();
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

    /// <summary>Call once per frame: restarts a sound that still has loops left and applies a pending volume change.</summary>
    public void Tick()
    {
        Rumble.Update();
        if (!Ready) return;
        if (_pendingVolume >= 0 && Raylib.GetTime() - _pendingSince > 0.15)
        {
            _volume = _pendingVolume;
            _pendingVolume = -1;
            Build();
            if (_volume > 0) Play(Sfx.Coin);          // audible feedback of the new level
            return;
        }
        if (_loopsLeft > 0 && _current is Sound c && !Raylib.IsSoundPlaying(c))
        {
            _loopsLeft--;
            Raylib.PlaySound(c);
        }
    }

    /// <summary>The original's <c>DeviceControl.startVibra(frequency, ms)</c>: rumbles the controller (the App only calls this while a controller is the active device).</summary>
    public void Vibrate(int frequency, int milliseconds) => Rumble.Pulse(frequency, milliseconds);

    public void Dispose()
    {
        Rumble.Stop();
        if (!Ready) return;
        StopSound();
        foreach (var s in _sounds.Values) Raylib.UnloadSound(s);
        Raylib.CloseAudioDevice();
    }

    /// <summary>Writes every sound effect as a WAV file (no audio device needed).</summary>
    public static void Export(IAssets assets, string dir, int octaveShift = -1, int volume = 100)
    {
        Directory.CreateDirectory(dir);
        foreach (Sfx s in Enum.GetValues<Sfx>())
        {
            var tone = RingTone.Decode(assets.ReadResource(SfxFiles.FileName(s)));
            string path = Path.Combine(dir, Path.ChangeExtension(SfxFiles.FileName(s), ".wav"));
            File.WriteAllBytes(path, RingTone.ToWav(tone.Render(SampleRate, RingTone.AmplitudeForVolume(volume), octaveShift), SampleRate));
            Console.WriteLine($"{path}  ({tone.TotalSeconds:F1}s)");
        }
    }
}
