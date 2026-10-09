package javax.microedition.lcdui;
public class List extends Screen {
    public static final Command SELECT_COMMAND = new Command("", 1, 0);
    public int selected;
    public List(String t, int type) {}
    public int append(String s, Image i) { return 0; }
    public int getSelectedIndex() { return selected; }
}
