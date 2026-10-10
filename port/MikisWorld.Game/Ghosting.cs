namespace MikisWorld.Game;

/// <summary>LCD ghosting: each pixel moves only part of the way to its new colour every frame, so a moving sprite leaves a short trail,
/// as on the phone screens. Works on the RGBA bytes of the game picture, before they go to the GPU.</summary>
internal sealed class Ghosting
{
    private const int Response = 150;                // out of 256: how much of the difference is taken each frame
    private readonly byte[] _shown;
    private bool _primed;

    public Ghosting(int bytes) { _shown = new byte[bytes]; }

    /// <summary>Replaces <paramref name="rgba"/> with what the screen would show (or leaves it alone when ghosting is off).</summary>
    public void Apply(byte[] rgba, bool on)
    {
        if (!on) { _primed = false; return; }
        if (!_primed) { Buffer.BlockCopy(rgba, 0, _shown, 0, rgba.Length); _primed = true; return; }
        for (int i = 0; i < rgba.Length; i++)
        {
            if ((i & 3) == 3) continue;              // alpha stays opaque
            int diff = rgba[i] - _shown[i];
            if (diff == 0) continue;
            int step = diff * Response >> 8;
            if (step == 0) step = diff > 0 ? 1 : -1;
            _shown[i] = (byte)(_shown[i] + step);
            rgba[i] = _shown[i];
        }
    }
}
