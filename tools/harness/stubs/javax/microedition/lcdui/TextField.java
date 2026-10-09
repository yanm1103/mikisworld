package javax.microedition.lcdui;
public class TextField extends Item {
    private String s;
    public TextField(String label, String text, int max, int c) { s = text; }
    public int setMaxSize(int m) { return m; }
    public String getString() { return s; }
    public void setString(String t) { s = t; }
}
