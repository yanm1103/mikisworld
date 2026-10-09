package javax.microedition.lcdui;
public class Font {
    public static Font getFont(int face, int style, int size) { return new Font(); }
    public int stringWidth(String s) { return s.length() * 6; }
    public int getHeight() { return 12; }
}
