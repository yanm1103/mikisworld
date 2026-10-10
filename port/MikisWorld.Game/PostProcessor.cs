using System.Numerics;
using MikisWorld.Core;
using Raylib_cs;

namespace MikisWorld.Game;

/// <summary>Puts the game's texture on the window with the chosen <see cref="DisplayFilter"/>.
/// <list type="bullet">
/// <item>nearest / bilinear: plain texture filtering;</item>
/// <item>sharp, xBRZ: one shader pass straight onto the window;</item>
/// <item>ScaleFX: five passes into render textures at the game's size (3x for the last), then drawn like a bilinear picture;</item>
/// <item>LCD, CRT: the window picture (game + text) is first drawn into a render texture, then a screen shader works on all of it.</item>
/// </list>
/// A shader that fails to compile (or a missing feature, such as float render targets) falls back to nearest and says so in the log.</summary>
internal sealed class PostProcessor : IDisposable
{
    private readonly Dictionary<string, Shader?> _shaders = new();
    private readonly HashSet<DisplayFilter> _broken = new();

    // ScaleFX
    private RenderTexture2D[] _sfx;
    private int _sfxW, _sfxH;
    private bool _sfxFloat;

    // screen effects
    private RenderTexture2D _scene;
    private int _sceneW, _sceneH;
    private bool _haveScene;

    // ---------------------------------------------------------------- shader sources
    private static string Resource(string name)
    {
        using var stream = typeof(PostProcessor).Assembly.GetManifestResourceStream("shader-" + name + ".glsl");
        if (stream == null) throw new FileNotFoundException("shader resource " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Vertex(bool scaleFix) => Resource("vertex").Replace("TEXSCALE", scaleFix ? "1.0001" : "1.0");

    /// <summary>libretro shaders keep vertex and fragment in one file, picked with #if defined(FRAGMENT).</summary>
    private static string Libretro(string name)
    {
        string text = Resource(name);
        const string head = "#version 330\n#define FRAGMENT\n";
        return text.Contains("#version 130") ? text.Replace("#version 130", head) : head + text;      // xbrz-freescale has no #version line
    }

    private Shader? Get(string key, Func<(string vs, string fs)> source)
    {
        if (_shaders.TryGetValue(key, out var cached)) return cached;
        Shader? result = null;
        try
        {
            var (vs, fs) = source();
            var sh = Raylib.LoadShaderFromMemory(vs, fs);
            if (Raylib.IsShaderReady(sh)) result = sh;
            else LogConsole.Log($"shader '{key}' did not compile");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { LogConsole.Log($"shader '{key}': {e.Message}"); }
        _shaders[key] = result;
        return result;
    }

    private Shader? Sharp() => Get("sharp", () => (Vertex(false), Resource("sharp")));
    private Shader? Xbrz() => Get("xbrz", () => (Vertex(true), Libretro("xbrz-freescale")));
    private Shader? Lcd() => Get("lcd", () => (Vertex(false), Resource("lcd")));
    private Shader? Crt() => Get("crt", () => (Vertex(false), Resource("crt")));
    private Shader? SfxPass(int i) => Get("scalefx" + i, () => (Vertex(true), Libretro("scalefx-pass" + i)));

    /// <summary>The filter that will really be used: the chosen one, or nearest when its shaders are unavailable.</summary>
    public DisplayFilter Effective(DisplayFilter wanted)
    {
        if (_broken.Contains(wanted)) return DisplayFilter.Nearest;
        bool ok = wanted switch
        {
            DisplayFilter.Sharp => Sharp() != null,
            DisplayFilter.XBRZ => Xbrz() != null,
            DisplayFilter.LCD => Lcd() != null,
            DisplayFilter.CRT => Crt() != null,
            DisplayFilter.ScaleFX => Enumerable.Range(0, 5).All(i => SfxPass(i) != null),
            _ => true
        };
        if (!ok) { _broken.Add(wanted); LogConsole.Log($"filter {wanted} unavailable: using nearest"); }
        return ok ? wanted : DisplayFilter.Nearest;
    }

    // ---------------------------------------------------------------- uniforms
    private static void Set(Shader sh, string name, Vector2 v)
    {
        int loc = Raylib.GetShaderLocation(sh, name);
        if (loc >= 0) Raylib.SetShaderValue(sh, loc, v, ShaderUniformDataType.Vec2);
    }

    private static void Set(Shader sh, string name, Vector4 v)
    {
        int loc = Raylib.GetShaderLocation(sh, name);
        if (loc >= 0) Raylib.SetShaderValue(sh, loc, v, ShaderUniformDataType.Vec4);
    }

    private static void Set(Shader sh, string name, int v)
    {
        int loc = Raylib.GetShaderLocation(sh, name);
        if (loc >= 0) Raylib.SetShaderValue(sh, loc, v, ShaderUniformDataType.Int);
    }

    private static void LibretroUniforms(Shader sh, Vector2 source, Vector2 output)
    {
        Set(sh, "TextureSize", source);
        Set(sh, "InputSize", source);
        Set(sh, "OutputSize", output);
        Set(sh, "FrameCount", 0);
        Set(sh, "FrameDirection", 1);
    }

    // ---------------------------------------------------------------- ScaleFX (before BeginDrawing)
    /// <summary>A render texture that keeps full floats: ScaleFX stores its edge data there, and 8 bits would not hold it.</summary>
    private static RenderTexture2D FloatTarget(int w, int h, out bool isFloat)
    {
        unsafe
        {
            uint fbo = Rlgl.LoadFramebuffer(w, h);
            uint tex = Rlgl.LoadTexture(null, w, h, PixelFormat.UncompressedR32G32B32A32, 1);
            Rlgl.FramebufferAttach(fbo, tex, FramebufferAttachType.ColorChannel0, FramebufferAttachTextureType.Texture2D, 0);
            if (fbo != 0 && tex != 0 && Rlgl.FramebufferComplete(fbo))
            {
                isFloat = true;
                var rt = new RenderTexture2D { Id = fbo };
                rt.Texture = new Texture2D { Id = tex, Width = w, Height = h, Mipmaps = 1, Format = PixelFormat.UncompressedR32G32B32A32 };
                return rt;
            }
            if (tex != 0) Rlgl.UnloadTexture(tex);
            if (fbo != 0) Rlgl.UnloadFramebuffer(fbo);
        }
        isFloat = false;
        return Raylib.LoadRenderTexture(w, h);
    }

    private void UnloadScaleFx()
    {
        if (_sfx == null) return;
        for (int i = 0; i < _sfx.Length; i++)
        {
            if (_sfxFloat && i < 4) { Rlgl.UnloadTexture(_sfx[i].Texture.Id); Rlgl.UnloadFramebuffer(_sfx[i].Id); }
            else Raylib.UnloadRenderTexture(_sfx[i]);
        }
        _sfx = null;
    }

    private void EnsureScaleFx(int gw, int gh)
    {
        if (_sfx != null && _sfxW == gw && _sfxH == gh) return;
        UnloadScaleFx();
        _sfx = new RenderTexture2D[5];
        _sfxFloat = true;
        for (int i = 0; i < 4; i++)
        {
            _sfx[i] = FloatTarget(gw, gh, out bool isFloat);
            _sfxFloat &= isFloat;
            Raylib.SetTextureFilter(_sfx[i].Texture, TextureFilter.Point);
            Raylib.SetTextureWrap(_sfx[i].Texture, TextureWrap.Clamp);
        }
        if (!_sfxFloat) LogConsole.Log("float render targets unavailable: ScaleFX runs with 8-bit intermediates");
        _sfx[4] = Raylib.LoadRenderTexture(gw * 3, gh * 3);
        Raylib.SetTextureFilter(_sfx[4].Texture, TextureFilter.Bilinear);
        _sfxW = gw; _sfxH = gh;
    }

    private void RunPass(int i, Texture2D source, Texture2D? extra, string extraName)
    {
        var sh = SfxPass(i).Value;
        var target = _sfx[i];
        int ow = target.Texture.Width, oh = target.Texture.Height;
        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(Color.Black);
        Rlgl.DisableColorBlend();                      // the passes store data, not colours: alpha must not blend with the old content
        Raylib.BeginShaderMode(sh);
        LibretroUniforms(sh, new Vector2(source.Width, source.Height), new Vector2(ow, oh));
        if (extra.HasValue)
        {
            int loc = Raylib.GetShaderLocation(sh, extraName);
            if (loc >= 0) Raylib.SetShaderValueTexture(sh, loc, extra.Value);
        }
        // a negative height keeps the stored picture the same way up pass after pass (render textures are upside down otherwise)
        Raylib.DrawTexturePro(source, new Rectangle(0, 0, source.Width, -source.Height), new Rectangle(0, 0, ow, oh), Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
        Rlgl.EnableColorBlend();
        Raylib.EndTextureMode();
    }

    /// <summary>Work that has to happen outside BeginDrawing: the ScaleFX passes.</summary>
    public void Prepare(Texture2D game, DisplayFilter filter)
    {
        if (Effective(filter) != DisplayFilter.ScaleFX) return;
        EnsureScaleFx(game.Width, game.Height);
        Raylib.SetTextureFilter(game, TextureFilter.Point);
        RunPass(0, game, null, null);
        RunPass(1, _sfx[0].Texture, null, null);
        RunPass(2, _sfx[1].Texture, _sfx[0].Texture, "PassPrev2Texture");
        RunPass(3, _sfx[2].Texture, null, null);
        RunPass(4, _sfx[3].Texture, game, "PassPrev5Texture");
    }

    // ---------------------------------------------------------------- drawing
    /// <summary>Draws the game (and then <paramref name="overlay"/>, the text) on the window.</summary>
    public void Present(Texture2D game, DisplayFilter wanted, Rectangle dest, Action overlay)
    {
        var filter = Effective(wanted);
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        if (filter is DisplayFilter.LCD or DisplayFilter.CRT)
        {
            EnsureScene(sw, sh);
            Raylib.BeginTextureMode(_scene);
            Raylib.ClearBackground(Color.Black);
            DrawPicture(game, DisplayFilter.Nearest, dest);
            overlay();
            Raylib.EndTextureMode();

            Raylib.ClearBackground(Color.Black);
            var effect = (filter == DisplayFilter.LCD ? Lcd() : Crt()).Value;
            Raylib.BeginShaderMode(effect);
            Set(effect, "Resolution", new Vector2(sw, sh));
            Set(effect, "GameRect", new Vector4(dest.X, dest.Y, dest.Width, dest.Height));
            Set(effect, "GameSize", new Vector2(game.Width, game.Height));
            Raylib.DrawTexturePro(_scene.Texture, new Rectangle(0, 0, sw, -sh), new Rectangle(0, 0, sw, sh), Vector2.Zero, 0, Color.White);
            Raylib.EndShaderMode();
            return;
        }
        Raylib.ClearBackground(Color.Black);
        DrawPicture(game, filter, dest);
        overlay();
    }

    private void DrawPicture(Texture2D game, DisplayFilter filter, Rectangle dest)
    {
        var src = new Rectangle(0, 0, game.Width, game.Height);
        switch (filter)
        {
            case DisplayFilter.Bilinear:
                Raylib.SetTextureFilter(game, TextureFilter.Bilinear);
                Raylib.DrawTexturePro(game, src, dest, Vector2.Zero, 0, Color.White);
                break;
            case DisplayFilter.Sharp:
                {
                    Raylib.SetTextureFilter(game, TextureFilter.Bilinear);
                    var sh = Sharp().Value;
                    Raylib.BeginShaderMode(sh);
                    Set(sh, "TextureSize", new Vector2(game.Width, game.Height));
                    Set(sh, "OutputSize", new Vector2(dest.Width, dest.Height));
                    Raylib.DrawTexturePro(game, src, dest, Vector2.Zero, 0, Color.White);
                    Raylib.EndShaderMode();
                    break;
                }
            case DisplayFilter.XBRZ:
                {
                    Raylib.SetTextureFilter(game, TextureFilter.Point);
                    var sh = Xbrz().Value;
                    Raylib.BeginShaderMode(sh);
                    LibretroUniforms(sh, new Vector2(game.Width, game.Height), new Vector2(dest.Width, dest.Height));
                    Raylib.DrawTexturePro(game, src, dest, Vector2.Zero, 0, Color.White);
                    Raylib.EndShaderMode();
                    break;
                }
            case DisplayFilter.ScaleFX:
                {
                    var big = _sfx[4].Texture;                       // stored the same way up as the game texture (see RunPass)
                    Raylib.DrawTexturePro(big, new Rectangle(0, 0, big.Width, big.Height), dest, Vector2.Zero, 0, Color.White);
                    break;
                }
            default:
                Raylib.SetTextureFilter(game, TextureFilter.Point);
                Raylib.DrawTexturePro(game, src, dest, Vector2.Zero, 0, Color.White);
                break;
        }
    }

    private void EnsureScene(int w, int h)
    {
        if (_haveScene && _sceneW == w && _sceneH == h) return;
        if (_haveScene) Raylib.UnloadRenderTexture(_scene);
        _scene = Raylib.LoadRenderTexture(w, h);
        Raylib.SetTextureFilter(_scene.Texture, TextureFilter.Point);
        _sceneW = w; _sceneH = h; _haveScene = true;
    }

    public void Dispose()
    {
        UnloadScaleFx();
        if (_haveScene) Raylib.UnloadRenderTexture(_scene);
        foreach (var sh in _shaders.Values) if (sh.HasValue) Raylib.UnloadShader(sh.Value);
        _shaders.Clear();
    }
}
