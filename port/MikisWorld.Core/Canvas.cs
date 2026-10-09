namespace MikisWorld.Core;

/// <summary>ARGB pixels, row-major. Mutable images start opaque white (MIDP semantics).</summary>
public sealed class Bitmap
{
    public readonly int Width, Height;
    public readonly int[] Pixels;

    public Bitmap(int width, int height, int[] pixels = null)
    {
        Width = width; Height = height;
        Pixels = pixels ?? new int[width * height];
        if (pixels == null) Array.Fill(Pixels, unchecked((int)0xFFFFFFFF));
    }
}

public enum FontKind { Small, Bold }

/// <summary>Text is not rasterised by the core (the original used device fonts). It is recorded as commands so the
/// frontend can draw it with whatever font it likes, on top of the finished pixel frame.</summary>
public readonly record struct TextCommand(string Text, int X, int Y, int Anchor, int Rgb, FontKind Font);

public interface IFontMetrics
{
    int StringWidth(string s, FontKind font);
    int Height(FontKind font);
}

/// <summary>Fixed-pitch metrics, also used by the reference harness (6 px/char, 12 px high).</summary>
public sealed class SimpleFontMetrics : IFontMetrics
{
    public int StringWidth(string s, FontKind font) => s.Length * 6;
    public int Height(FontKind font) => 12;
}

/// <summary>Software implementation of the subset of javax.microedition.lcdui.Graphics the game uses.
/// Mirrors the semantics exactly: translate, clip rectangle with intersection, anchors, binary/alpha blending.</summary>
public sealed class Canvas
{
    public const int HCenter = 1, VCenter = 2, Left = 4, Right = 8, Top = 16, Bottom = 32, Baseline = 64;

    private readonly Bitmap _t;
    private int _tx, _ty, _cx, _cy, _cw, _ch;
    private int _color = unchecked((int)0xFF000000);
    public readonly List<TextCommand> Texts = new();
    public IFontMetrics Metrics { get; set; } = new SimpleFontMetrics();
    private FontKind _font;

    public Canvas(Bitmap target)
    {
        _t = target; _cw = target.Width; _ch = target.Height;
    }

    public Bitmap Target => _t;

    public void SetColor(int r, int g, int b) { _color = unchecked((int)0xFF000000) | (r & 255) << 16 | (g & 255) << 8 | (b & 255); }
    public void SetFont(FontKind f) { _font = f; }
    public void Translate(int x, int y) { _tx += x; _ty += y; _cx -= x; _cy -= y; }
    public void SetClip(int x, int y, int w, int h) { _cx = x; _cy = y; _cw = w; _ch = h; }

    public void ClipRect(int x, int y, int w, int h)
    {
        int x0 = Math.Max(_cx, x), y0 = Math.Max(_cy, y), x1 = Math.Min(_cx + _cw, x + w), y1 = Math.Min(_cy + _ch, y + h);
        _cx = x0; _cy = y0; _cw = Math.Max(0, x1 - x0); _ch = Math.Max(0, y1 - y0);
    }

    public int ClipX => _cx;
    public int ClipY => _cy;
    public int ClipWidth => _cw;
    public int ClipHeight => _ch;

    private void Plot(int x, int y, int argb)
    {
        if (x < _cx || y < _cy || x >= _cx + _cw || y >= _cy + _ch) return;
        int px = x + _tx, py = y + _ty;
        if (px < 0 || py < 0 || px >= _t.Width || py >= _t.Height) return;
        int a = (int)((uint)argb >> 24);
        if (a == 0) return;
        int i = py * _t.Width + px;
        if (a == 255) { _t.Pixels[i] = argb; return; }
        int d = _t.Pixels[i];
        int r = ((argb >> 16 & 255) * a + (d >> 16 & 255) * (255 - a)) / 255;
        int g = ((argb >> 8 & 255) * a + (d >> 8 & 255) * (255 - a)) / 255;
        int b = ((argb & 255) * a + (d & 255) * (255 - a)) / 255;
        _t.Pixels[i] = unchecked((int)0xFF000000) | r << 16 | g << 8 | b;
    }

    public void FillRect(int x, int y, int w, int h)
    {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Plot(x + i, y + j, _color);
    }

    /// <summary>Outline of a (w+1) x (h+1) pixel rectangle, as in MIDP.</summary>
    public void DrawRect(int x, int y, int w, int h)
    {
        for (int i = 0; i <= w; i++) { Plot(x + i, y, _color); Plot(x + i, y + h, _color); }
        for (int j = 0; j <= h; j++) { Plot(x, y + j, _color); Plot(x + w, y + j, _color); }
    }

    public void DrawImage(Bitmap img, int x, int y, int anchor)
    {
        int w = img.Width, h = img.Height;
        if ((anchor & HCenter) != 0) x -= w / 2; else if ((anchor & Right) != 0) x -= w;
        if ((anchor & VCenter) != 0) y -= h / 2; else if ((anchor & Bottom) != 0) y -= h;
        for (int j = 0; j < h; j++)
        {
            int row = j * w;
            for (int k = 0; k < w; k++) Plot(x + k, y + j, img.Pixels[row + k]);
        }
    }

    public void DrawString(string s, int x, int y, int anchor)
    {
        Texts.Add(new TextCommand(s, x + _tx, y + _ty, anchor, _color & 0xFFFFFF, _font));
    }
}
