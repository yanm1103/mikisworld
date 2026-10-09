package javax.microedition.lcdui;
import javax.microedition.midlet.MIDlet;
public class Display {
    private static final Display I = new Display();
    private Displayable cur;
    public static Display getDisplay(MIDlet m) { return I; }
    public void setCurrent(Displayable d) { cur = d; }
    public Displayable getCurrent() { return cur; }
}
