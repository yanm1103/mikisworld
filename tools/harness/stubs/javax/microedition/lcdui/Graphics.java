package javax.microedition.lcdui;
import java.awt.image.BufferedImage;
/** Minimal but exact MIDP Graphics over a BufferedImage: translate, clip (intersect), fillRect/drawRect, drawImage with anchors.
 *  Text drawing is intentionally a no-op (fonts differ between platforms; text is excluded from frame comparisons). */
public class Graphics {
    public static final int HCENTER = 1, VCENTER = 2, LEFT = 4, RIGHT = 8, TOP = 16, BOTTOM = 32, BASELINE = 64;
    private final BufferedImage t;
    private int tx, ty, cx, cy, cw, ch, color = 0xFF000000;

    public Graphics() { this(new BufferedImage(1, 1, BufferedImage.TYPE_INT_ARGB)); }
    public Graphics(BufferedImage target) { t = target; cw = t.getWidth(); ch = t.getHeight(); }

    public void setColor(int r, int g, int b) { color = 0xFF000000 | (r & 255) << 16 | (g & 255) << 8 | (b & 255); }
    public void setColor(int rgb) { color = 0xFF000000 | rgb; }
    public int getColor() { return color & 0xFFFFFF; }
    public void translate(int x, int y) { tx += x; ty += y; cx -= x; cy -= y; }
    public void setClip(int x, int y, int w, int h) { cx = x; cy = y; cw = w; ch = h; }
    public void clipRect(int x, int y, int w, int h) {
        int x0 = Math.max(cx, x), y0 = Math.max(cy, y), x1 = Math.min(cx + cw, x + w), y1 = Math.min(cy + ch, y + h);
        cx = x0; cy = y0; cw = Math.max(0, x1 - x0); ch = Math.max(0, y1 - y0);
    }
    public int getClipX() { return cx; }
    public int getClipY() { return cy; }
    public int getClipWidth() { return cw; }
    public int getClipHeight() { return ch; }

    private void plot(int x, int y, int argb) {          // x,y in translated space
        if (x < cx || y < cy || x >= cx + cw || y >= cy + ch) return;
        int px = x + tx, py = y + ty;
        if (px < 0 || py < 0 || px >= t.getWidth() || py >= t.getHeight()) return;
        int a = argb >>> 24;
        if (a == 0) return;
        if (a == 255) { t.setRGB(px, py, argb); return; }
        int d = t.getRGB(px, py);
        int r = ((argb >> 16 & 255) * a + (d >> 16 & 255) * (255 - a)) / 255;
        int g = ((argb >> 8 & 255) * a + (d >> 8 & 255) * (255 - a)) / 255;
        int b = ((argb & 255) * a + (d & 255) * (255 - a)) / 255;
        t.setRGB(px, py, 0xFF000000 | r << 16 | g << 8 | b);
    }
    public void fillRect(int x, int y, int w, int h) {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) plot(x + i, y + j, color);
    }
    public void drawRect(int x, int y, int w, int h) {
        for (int i = 0; i <= w; i++) { plot(x + i, y, color); plot(x + i, y + h, color); }
        for (int j = 0; j <= h; j++) { plot(x, y + j, color); plot(x + w, y + j, color); }
    }
    public void drawLine(int a, int b, int c, int d) {}
    public void drawImage(Image i, int x, int y, int anchor) {
        BufferedImage s = i.awt();
        int w = s.getWidth(), h = s.getHeight();
        if ((anchor & HCENTER) != 0) x -= w / 2; else if ((anchor & RIGHT) != 0) x -= w;
        if ((anchor & VCENTER) != 0) y -= h / 2; else if ((anchor & BOTTOM) != 0) y -= h;
        for (int j = 0; j < h; j++) for (int k = 0; k < w; k++) plot(x + k, y + j, s.getRGB(k, j));
    }
    public void drawString(String s, int x, int y, int a) {}
    public void setFont(Font f) {}
    public void setStrokeStyle(int s) {}
}
