package javax.microedition.lcdui;
import java.awt.image.BufferedImage;
import java.io.*;
public class Image {
    final BufferedImage img;
    private Image(BufferedImage b) { img = b; }
    public static Image createImage(String name) throws IOException {
        InputStream in = Image.class.getResourceAsStream(name);
        if (in == null) throw new IOException(name);
        BufferedImage src = javax.imageio.ImageIO.read(in);
        BufferedImage b = new BufferedImage(src.getWidth(), src.getHeight(), BufferedImage.TYPE_INT_ARGB);
        for (int y = 0; y < src.getHeight(); y++) for (int x = 0; x < src.getWidth(); x++) b.setRGB(x, y, src.getRGB(x, y));
        return new Image(b);
    }
    /** MIDP mutable images start out opaque white. */
    public static Image createImage(int w, int h) {
        BufferedImage b = new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) b.setRGB(x, y, 0xFFFFFFFF);
        return new Image(b);
    }
    public static Image createImage(byte[] d, int o, int l) { return createImage(1, 1); }
    public int getWidth() { return img.getWidth(); }
    public int getHeight() { return img.getHeight(); }
    public Graphics getGraphics() { return new Graphics(img); }
    public BufferedImage awt() { return img; }
}
