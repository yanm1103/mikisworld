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
        DrawScene(new Canvas(_snapshot));
    }

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
}
