package javax.microedition.lcdui;
public class List extends Screen {
    public static final Command SELECT_COMMAND = new Command("", 1, 0);
    public int selected;
    public List(String t, int type) {}
    public int append(String s, Image i) { items.add(s); return items.size() - 1; }
    public int getSelectedIndex() { return selected; }
    private final java.util.ArrayList<String> items = new java.util.ArrayList<>();
    public int size() { return items.size(); }
    public void delete(int i) { items.remove(i); }
    public void set(int i, String s, Image img) { items.set(i, s); }
}
