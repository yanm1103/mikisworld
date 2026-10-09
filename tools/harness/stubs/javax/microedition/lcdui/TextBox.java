package javax.microedition.lcdui;
public class TextBox extends Screen {
    private String s;
    public TextBox(String title, String text, int max, int constraints) { s = text; }
    public int setMaxSize(int m) { return m; }
    public String getString() { return s; }
    public void setString(String t) { s = t; }
}
