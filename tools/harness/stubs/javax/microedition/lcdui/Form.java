package javax.microedition.lcdui;
import java.util.*;
public class Form extends Screen {
    private java.util.List<Object> items = new ArrayList<>();
    public Form(String t) {}
    public int append(String s) { items.add(s); return items.size() - 1; }
    public int append(Item i) { items.add(i); return items.size() - 1; }
    public int size() { return items.size(); }
    public void delete(int i) { items.remove(i); }
}
