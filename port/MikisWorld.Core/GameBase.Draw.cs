namespace MikisWorld.Core;

public abstract partial class GameBase
{
    protected const int TL = Canvas.Left | Canvas.Top;
    protected const int BL = Canvas.Left | Canvas.Bottom;

    protected bool _render;
    protected Bitmap _imgNumbers, _imgTime, _imgDamage, _imgGameOver, _imgLord, _imgPlayer, _imgItems, _imgEnd,
                     _imgGrid, _imgBonus, _imgCursor, _snapshot, _barLeft, _barRight;

    protected abstract void DrawScene(Canvas g);

    /// <summary>The scene is painted into the wipe-transition snapshot.</summary>
    protected void SnapshotScene()
    {
        if (!_render) return;
        if (Tweaks.FullWipeSnapshot && (_snapshot.Width != L.ScreenW || _snapshot.Height != L.ScreenH)) _snapshot = new Bitmap(L.ScreenW, L.ScreenH);
        DrawScene(new Canvas(_snapshot));
    }

    /// <summary>Name of the key / button the player uses for an action (null = unknown: the original text stays). Set by the application.</summary>
    public Func<GameAction, string> ActionName { get; set; }

    private static readonly System.Text.RegularExpressions.Regex PressDigit = new(@"(?<=Press )\d");

    /// <summary>The texts of the game say "Press 8 to enter" / "(Press 5 to skip)" (phone keys); show the key / button really bound.</summary>
    protected string KeyHints(string text)
    {
        if (ActionName == null) return text;
        return PressDigit.Replace(text, m =>
        {
            string name = m.Value == "8" ? ActionName(GameAction.Interact) : m.Value == "5" ? ActionName(GameAction.Confirm) : null;
            return string.IsNullOrEmpty(name) ? m.Value : name;
        });
    }

    /// <summary>How far up a bubble has to move so it does not sit on top of <paramref name="shownBelow"/> other bubbles.</summary>
    protected int BubbleLift(Canvas g, int shownBelow) => Tweaks.StackHintBubbles ? shownBelow * (g.Metrics.Height(FontKind.Small) + 7) : 0;

    // original d.a(Graphics,int,int,Image,int,int,int,boolean): right-aligned number made of sprite digits
    protected void DrawCounter(Canvas g, int x, int y, Bitmap digits, int digitW, int count, int value, bool lastIsIcon)
    {
        x += (count - 1) * digitW;
        for (int i = 0; i < count; i++)
        {
            int d = value % 10;
            value /= 10;
            if (i == count - 1 && lastIsIcon) d = 10;
            g.SetClip(0, 0, L.ScreenW, L.ScreenH);
            g.ClipRect(x, y, digitW, digits.Height);
            g.DrawImage(digits, x - d * digitW, y, TL);
            x -= digitW;
            if (!lastIsIcon && value == 0) break;
        }
    }

    // original d.a(Graphics,int,int,int): orbs of a resting cell (bit 3 selects the CPU colour)
    protected void DrawCellOrbs(Canvas g, int x, int y, int value)
    {
        x += 2; y += 2;
        int w = L.OrbW, h = L.OrbH;
        int colour = 0;
        if ((value & 8) != 0) { colour = w; value &= 7; }
        if (value > 0) { g.SetClip(x, y, w, h); g.DrawImage(_imgBonus, x - colour, y, TL); }
        if (value > 1) { g.SetClip(x + w, y, w, h); g.DrawImage(_imgBonus, x + w - colour, y, TL); }
        if (value > 2) { g.SetClip(x, y + h, w, h); g.DrawImage(_imgBonus, x - colour, y + h, TL); }
        if (value > 3) { g.SetClip(x + w, y + h, w, h); g.DrawImage(_imgBonus, x + w - colour, y + h, TL); }
    }

    // original d.a(Graphics,int,int,int,int,int,int): four orbs flying out of an exploding cell
    protected void DrawExplosion(Canvas g, int gx, int gy, int cellX, int cellY, int owner, int progress)
    {
        g.SetClip(0, 0, L.ScreenW, L.ScreenH);
        int x = gx + cellX * L.CellW + 2;
        int y = gy + cellY * L.CellH + 2;
        int w = L.OrbW, h = L.OrbH;
        int colour = owner == 1 ? w : 0;
        int dx = L.CellW * progress / 10;
        int dy = L.CellH * progress / 10;
        if (cellX > 0) { g.SetClip(x - dx, y, w, h); g.DrawImage(_imgBonus, x - colour - dx, y, TL); }
        if (cellX < 4) { g.SetClip(x + dx + w, y, w, h); g.DrawImage(_imgBonus, x - colour + dx + w, y, TL); }
        if (cellY > 0) { g.SetClip(x + w, y - dy, w, h); g.DrawImage(_imgBonus, x - colour + w, y - dy, TL); }
        if (cellY < 3) { g.SetClip(x, y + dy + h, w, h); g.DrawImage(_imgBonus, x - colour, y + dy + h, TL); }
    }
}
